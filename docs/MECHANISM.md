# Mechanism & RE notes

## The engine debug-overlay system

Every `CBaseEntity` in CS2 carries a set of **debug-overlay** flags. The flags are an enum of bits
(`DebugOverlayBits_t`), e.g.:

| bit | name | meaning |
|---|---|---|
| `0x2` | `OVERLAY_NAME_BIT` | draw the entity's name |
| `0x4` | `OVERLAY_BBOX_BIT` | draw the entity's bounding box |
| `0x2000` | `OVERLAY_TRIGGER_BOUNDS_BIT` | draw trigger bounds (what the engine's own `showtriggers` uses) |

When an entity has overlay bits set **and** `sv_debug_overlays_broadcast 1` is set on the server,
the server networks those overlays to clients. A client renders received broadcast overlays only
when it has `cl_debug_overlays_broadcast 1` set locally — this is a client cvar and **cannot be
forced from the server**, which is why the toggle instructs the player to set it. (This is the
"not out of the box" caveat from the original CS2Surf discussion.)

## What this plugin does

1. **Track triggers.** An `IEntityListener` collects every entity whose classname starts with
   `trigger_` as it spawns (and drops it on delete). The list is cleared per map.
2. **Enable broadcast.** On `OnServerActivate` it runs `sv_debug_overlays_broadcast 1`.
3. **Toggle.** `!showtriggers` flips a per-player flag. While ≥1 player wants triggers shown, the
   plugin calls `AddDebugOverlayBits(trigger, 0x4 | 0x2)` on every tracked trigger; when the last
   player turns it off it calls `RemoveDebugOverlayBits`.

   `0x4` is `OVERLAY_BBOX_BIT` (the trigger's AABB box) and `0x2` is `OVERLAY_NAME_BIT` (its name).
   The engine's own `showtriggers` uses `OVERLAY_TRIGGER_BOUNDS_BIT` (0x2000) instead — but that bit
   crashes the server when broadcast to a client (see Crash safety below), so this plugin uses BBOX.
   Note CS:GO (Source 1) did this differently again — its `showtriggers_toggle` toggled `EF_NODRAW`
   to render the brush directly; Source 2 reworked it onto the debug-overlay path used here.

## Crash safety — use BBOX, not TriggerBounds

Setting `OVERLAY_TRIGGER_BOUNDS_BIT` (0x2000) crashes the server (`SIGSEGV`) once the overlays are
broadcast to a connected client. RE of a crashdump: the trigger's `DrawDebugGeometryOverlays`
(vtable[0x20], `FUN_01583a80`) routes through the engine overlay manager's `+0x430` method, which
serializes the trigger's physics-**mesh geometry** object into the per-client overlay net-buffer.
When that geometry object is malformed, the buffer count comes back negative, so the copy runs with a
NULL source and a negative size (dump: `rsi=0, rdx=0xfffffffffffffb80` = `-24 * 0x30`) → crash, every
frame, while broadcasting. (Empty servers don't reproduce it — the crash is in the client-broadcast
serialization.)

Gating on `SolidType.VPhysics` (only flag triggers with a vcollide) was tried first but is only a
**heuristic** — having a VPhysics solid does not guarantee the geometry object is well-formed, so it
can still crash. The robust fix is to **not use `TriggerBounds` at all**: the plugin sets
`OVERLAY_BBOX_BIT` (0x4) instead. BBOX dispatches to a *different* engine overlay handler that draws
the entity's `GetCollisionBounds` (a clean AABB) and never enters the mesh-serializer in
`FUN_01583a80` — so it is crash-proof for **every** trigger, no gate needed.

Tradeoff: BBOX is the trigger's axis-aligned bounding **box**, not the exact brush outline. For
box-shaped triggers (nearly all of them) the two are identical; angled or complex brushes render as
their bounding box. Drawing the exact outline crash-free would require emitting the engine's explicit
box-overlay *primitives* directly (no entity-mesh dependency) — a larger RE job, not yet done.

The two functions take `(CBaseEntity* entity, uint64 bits)` and edit the entity's overlay set —
which in the current build is a `CUtlHashMap` at `entity + 0x160`, **not** a plain integer field
(so we call the engine functions rather than poking a schema/netvar).

## Reverse engineering (current build)

`AddDebugOverlayBits` / `RemoveDebugOverlayBits` have no unique standalone signature (their public
entries are shared trampolines that match several functions). The engine's `showtriggers` console
command, `CMD_ShowTriggers`, **is** unique and calls both — so we sign that and resolve the two as
its `call` (E8 rel32) targets:

```
CMD_ShowTriggers   (server)  RVA 0x1938260
  sig: 55 48 89 E5 41 57 41 56 41 BE ? ? ? ? 41 55 41 54 53 48 83 EC 28 83 BE 38
  +120  →  E8 call → AddDebugOverlayBits     trampoline RVA 0xc81d40
  +196  →  E8 call → RemoveDebugOverlayBits  trampoline RVA 0xc81d80
```

Resolution at load (`ShowTriggerModule.ResolveCallTarget`): read the byte at `CMD_ShowTriggers +
offset`, assert it is `0xE8`, then `target = addr + 5 + rel32`. The opcode assert means an offset
drift on a future game update **disables the feature with an error log** instead of calling into a
random address.

Only `CMD_ShowTriggers` lives in [`showtrigger.jsonc`](../.assets/gamedata/showtrigger.jsonc); the
two call offsets are constants in the module.

### Re-signing (after a game update breaks it)

Using `ghidra-cli` against the updated `libserver.so`:

1. Find `CMD_ShowTriggers`: search the sig above; if it no longer matches, re-derive it (it's the
   handler for the `showtriggers` command — decompile, confirm it iterates triggers and calls two
   small `(entity, bits)` functions).
2. Disassemble it and find the two `call` (E8) instructions to the Add/Remove trampolines. Note
   their byte offsets from the function start.
3. Update the sig in `showtrigger.jsonc` and the `AddCallOffset` / `RemoveCallOffset` constants in
   `ShowTriggerModule.cs`.

Reference: original (stubbed) source at
[CS2Surf-CN/Timer `src/surf/misc/showtrigger.cpp`](https://github.com/CS2Surf-CN/Timer/blob/main/src/surf/misc/showtrigger.cpp).
