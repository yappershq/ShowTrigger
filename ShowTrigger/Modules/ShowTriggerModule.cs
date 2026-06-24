using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Sharp.Modules.CommandCenter.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace ShowTrigger.Modules;

/// <summary>
/// Drives the engine debug-overlay system to draw trigger zones.
///
/// MECHANISM (see docs/MECHANISM.md): every CBaseEntity carries a debug-overlay bitfield. The
/// engine's <c>CMD_ShowTriggers</c> command flips OVERLAY_NAME | OVERLAY_BBOX on triggers via two
/// tiny functions — <c>AddDebugOverlayBits</c> / <c>RemoveDebugOverlayBits</c> — and, when
/// <c>sv_debug_overlays_broadcast 1</c> is set, the server networks those overlays to clients.
/// We resolve those two functions from gamedata and call them on every <c>trigger_*</c> entity.
///
/// Per-player is cooperative: the overlay bits live on the (shared) entities, so a client only
/// renders them if they also set <c>cl_debug_overlays_broadcast 1</c> locally — which the toggle
/// instructs them to do. We reference-count enabled players: bits go ON while ≥1 player wants them.
/// </summary>
internal sealed unsafe class ShowTriggerModule : IModule, IEntityListener, IClientListener, IGameListener
{
    private const string ModuleId = "ShowTrigger";

    // The EXACT value the engine's own CMD_ShowTriggers passes (verified in libserver disasm:
    // `mov $0x2000,%esi` right before the Add/Remove call). OR in DebugOverlayBits.Name if you
    // also want the trigger's name drawn.
    private const ulong TriggerOverlayBits = (ulong) DebugOverlayBits.TriggerBounds;

    // The two engine functions are resolved as the `call` (E8) targets inside CMD_ShowTriggers.
    // Offsets verified against the current build (two independent RE passes + objdump).
    //
    // Add vs Remove direction is NOT guessed: CMD_ShowTriggers branches on
    // `V_atoi(argv[1]) != 0` (default 1) — i.e. `showtriggers 1` (show) takes the +120 call and
    // `showtriggers 0` (hide) takes +196. So +120 = Add (set bits), +196 = Remove (clear bits).
    // The opcode is asserted at load, so a future offset drift fails loudly, not silently.
    private const int AddCallOffset    = 120;
    private const int RemoveCallOffset = 196;

    private readonly InterfaceBridge            _bridge;
    private readonly ILogger<ShowTriggerModule> _logger;

    // Live trigger entities, maintained by the entity listener.
    private readonly HashSet<IBaseEntity> _triggers = new();
    // Per-slot "this player asked to see triggers".
    private readonly bool[] _enabled = new bool[64];

    private delegate* unmanaged<nint, ulong, void> _addBits;
    private delegate* unmanaged<nint, ulong, void> _removeBits;
    private bool _nativeReady;

    int IEntityListener.ListenerVersion  => IEntityListener.ApiVersion;
    int IEntityListener.ListenerPriority => 0;
    int IClientListener.ListenerVersion  => IClientListener.ApiVersion;
    int IClientListener.ListenerPriority => 0;
    int IGameListener.ListenerVersion    => IGameListener.ApiVersion;
    int IGameListener.ListenerPriority   => 0;

    public ShowTriggerModule(InterfaceBridge bridge, ILogger<ShowTriggerModule> logger)
    {
        _bridge = bridge;
        _logger = logger;
    }

    // ===== IModule =====

    public bool Init()
    {
        _bridge.EntityManager.InstallEntityListener(this);
        _bridge.ClientManager.InstallClientListener(this);
        _bridge.ModSharp.InstallGameListener(this);
        return true;
    }

    public void OnAllSharpModulesLoaded()
    {
        try
        {
            var gd = _bridge.ModSharp.GetGameData();
            gd.Register("showtrigger"); // loads <sharp>/gamedata/showtrigger.jsonc

            // CMD_ShowTriggers has a unique sig; Add/RemoveDebugOverlayBits do not (the public
            // entries are non-unique trampolines), so we resolve them as the engine does: the
            // E8 call targets at fixed offsets inside CMD_ShowTriggers.
            var cmd = gd.GetAddress("CMD_ShowTriggers");
            if (cmd == nint.Zero)
            {
                _logger.LogError("[ShowTrigger] CMD_ShowTriggers sig unresolved — feature disabled. Re-sig needed?");
                return;
            }

            var add = ResolveCallTarget(cmd + AddCallOffset,    "AddDebugOverlayBits");
            var rem = ResolveCallTarget(cmd + RemoveCallOffset, "RemoveDebugOverlayBits");
            if (add == nint.Zero || rem == nint.Zero)
                return;

            _addBits     = (delegate* unmanaged<nint, ulong, void>) add;
            _removeBits  = (delegate* unmanaged<nint, ulong, void>) rem;
            _nativeReady = true;
            _logger.LogInformation(
                "[ShowTrigger] CMD_ShowTriggers=0x{Cmd:X} → Add=0x{Add:X}, Remove=0x{Rem:X}", cmd, add, rem);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[ShowTrigger] gamedata load failed — feature disabled");
        }

        RegisterCommands();
    }

    private void RegisterCommands()
    {
        // CommandCenter, no permission gate: !showtriggers / .showtriggers / /showtriggers in chat,
        // and ms_showtriggers in client console (the ms_ prefix is added automatically).
        var commandCenter = _bridge.SharpModuleManager
            .GetOptionalSharpModuleInterface<ICommandCenter>(ICommandCenter.Identity)?.Instance;

        if (commandCenter is null)
        {
            _logger.LogWarning("[ShowTrigger] CommandCenter not present — !showtriggers unavailable");
            return;
        }

        var registry = commandCenter.GetRegistry(ModuleId);
        registry.RegisterClientCommand("showtriggers", OnShowTriggerCommand);
        registry.RegisterClientCommand("showtrigger",  OnShowTriggerCommand);
        registry.RegisterClientCommand("st",           OnShowTriggerCommand);
    }

    public void Shutdown()
    {
        // Best-effort: clear bits before unloading so a hot-reload doesn't leave overlays stuck on.
        if (_nativeReady)
            ApplyBits(false);

        _bridge.EntityManager.RemoveEntityListener(this);
        _bridge.ClientManager.RemoveClientListener(this);
        _bridge.ModSharp.RemoveGameListener(this);
    }

    /// <summary>
    /// Resolve a near-<c>call</c> (E8 rel32) target at <paramref name="instr"/>:
    /// target = instr + 5 + rel32. Asserts the 0xE8 opcode so an offset drift on a future game
    /// update fails loudly here instead of jumping into the middle of some other instruction.
    /// </summary>
    private nint ResolveCallTarget(nint instr, string name)
    {
        var op = *(byte*) instr;
        if (op != 0xE8)
        {
            _logger.LogError(
                "[ShowTrigger] {Name}: expected E8 call at 0x{Addr:X} but found 0x{Op:X2} — offset drift, feature disabled",
                name, instr, op);
            return nint.Zero;
        }

        var rel = *(int*) (instr + 1);
        return instr + 5 + rel;
    }

    // ===== IGameListener: per-map reset =====

    void IGameListener.OnServerActivate()
    {
        // Old map's triggers are gone; the entity listener repopulates as the new map spawns.
        _triggers.Clear();
        Array.Clear(_enabled);

        // Required for the overlay bits to actually reach clients. Idempotent; cheap to re-assert.
        _bridge.ModSharp.ServerCommand("sv_debug_overlays_broadcast 1");
    }

    // ===== IEntityListener: track trigger entities =====

    void IEntityListener.OnEntitySpawned(IBaseEntity entity)
    {
        if (!IsTrigger(entity))
            return;

        _triggers.Add(entity);

        // If triggers are currently shown, light up the freshly-spawned one immediately.
        if (_nativeReady && AnyEnabled() && entity.IsValid())
            _addBits(entity.GetAbsPtr(), TriggerOverlayBits);
    }

    void IEntityListener.OnEntityDeleted(IBaseEntity entity)
    {
        if (IsTrigger(entity))
            _triggers.Remove(entity);
    }

    private static bool IsTrigger(IBaseEntity e)
        => e.Classname is { Length: > 0 } c && c.StartsWith("trigger_", StringComparison.Ordinal);

    // ===== the toggle command (CommandCenter, no perms) =====

    private void OnShowTriggerCommand(IGameClient client, StringCommand command)
    {
        if (!_nativeReady)
        {
            client.Print(HudPrintChannel.Chat, " \x02[ShowTrigger]\x01 Unavailable — gamedata not resolved.");
            return;
        }

        var slot = client.Slot.AsPrimitive();
        var on   = !_enabled[slot];
        _enabled[slot] = on;

        ApplyBits(AnyEnabled());

        client.Print(HudPrintChannel.Chat,
            on ? " \x04[ShowTrigger]\x01 Trigger zones \x04ON\x01."
               : " \x02[ShowTrigger]\x01 Trigger zones \x02OFF\x01.");

        if (on)
            client.Print(HudPrintChannel.Chat,
                " \x10[ShowTrigger]\x01 Type \x10cl_debug_overlays_broadcast 1\x01 in console to see them.");
    }

    // ===== IClientListener: per-player cleanup =====

    void IClientListener.OnClientDisconnected(IGameClient client, NetworkDisconnectionReason reason)
    {
        var slot = client.Slot.AsPrimitive();
        if (!_enabled[slot])
            return;

        _enabled[slot] = false;
        if (_nativeReady)
            ApplyBits(AnyEnabled());
    }

    // ===== apply =====

    private void ApplyBits(bool on)
    {
        if (!_nativeReady)
            return;

        foreach (var trigger in _triggers)
        {
            if (!trigger.IsValid())
                continue;

            var ptr = trigger.GetAbsPtr();
            if (ptr == nint.Zero)
                continue;

            if (on)
                _addBits(ptr, TriggerOverlayBits);
            else
                _removeBits(ptr, TriggerOverlayBits);
        }
    }

    private bool AnyEnabled()
    {
        foreach (var b in _enabled)
            if (b)
                return true;

        return false;
    }
}
