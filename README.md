# ShowTrigger

A ModSharp (CS2 / Source 2) plugin that draws **trigger zones** in the world so you can see where
`trigger_*` brush entities are. Per-player toggle, no map edits, no client mod required (beyond one
console cvar the toggle tells the player to set).

Port of the (stubbed) CS2Surf-CN `showtrigger` feature, built properly and re-sig'd against the
current game build.

## Usage

In chat: **`!showtriggers`** (aliases `!showtrigger`, `!st`) — toggles trigger zones for you.

When you turn it on it reminds you to run **`cl_debug_overlays_broadcast 1`** in your own console —
that client cvar is what actually renders the broadcast overlays, and it can't be forced from the
server (see Mechanism).

The overlay drawn is `OVERLAY_TRIGGER_BOUNDS_BIT` (`0x2000`) — the exact bit the engine's own
`showtriggers` uses (verified in the disassembly). OR in `OVERLAY_NAME_BIT` (`0x2`) via
`TriggerOverlayBits` in [`ShowTriggerModule.cs`](ShowTrigger/Modules/ShowTriggerModule.cs) if you
also want trigger names drawn.

## How it works (short)

Every entity has a debug-overlay bitfield. Setting the trigger-overlay bits on a `trigger_*` entity
and running `sv_debug_overlays_broadcast 1` makes the server network that overlay to clients. The
plugin tracks all triggers (entity listener), and on toggle flips those bits on every trigger via
two engine functions resolved from gamedata.

Full detail incl. the RE: [docs/MECHANISM.md](docs/MECHANISM.md).

## Build

```bash
cd ShowTrigger
dotnet build -c Release          # if `version` env is exported as N/A: `env -u version dotnet build -c Release`
```

Outputs `.build/modules/ShowTrigger/ShowTrigger.dll` and `.build/gamedata/showtrigger.jsonc`.

## Deploy

```bash
modsharp-deploy . <server-profile> --no-restart
```

Ships the module to `/game/sharp/modules/ShowTrigger/` and the gamedata to
`/game/sharp/gamedata/showtrigger.jsonc`. No config file. Restart (or mapchange) to load.

## If it breaks after a game update

The feature self-disables (logged) if the signature or call-offsets drift. Re-RE per
[docs/MECHANISM.md](docs/MECHANISM.md) → "Re-signing".
