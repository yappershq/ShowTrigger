<div align="center">
  <h1><strong>ShowTrigger</strong></h1>
  <p>In-world trigger-zone visualizer for ModSharp / CS2 — see exactly where every <code>trigger_*</code> brush is, in-game, no map edits.</p>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/game-CS2-orange" alt="CS2">
  <img src="https://img.shields.io/badge/framework-ModSharp-blue" alt="ModSharp">
  <img src="https://img.shields.io/github/stars/yappershq/ShowTrigger?style=flat&logo=github" alt="Stars">
</p>

---

ShowTrigger toggles the engine's debug-overlay bits on every trigger entity and broadcasts them to
clients, so players can see trigger bounds (teleports, pushes, hurt zones, …) drawn live. Per-player
toggle, no config. A proper, re-signed build of the (stubbed) CS2Surf-CN `showtrigger` feature.

## 🚀 Install

Copy the build output into your ModSharp install (`<sharp>` = your `sharp` directory):

| From | To |
|------|----|
| `.build/modules/ShowTrigger/` | `<sharp>/modules/ShowTrigger/` |
| `.build/gamedata/showtrigger.jsonc` | `<sharp>/gamedata/showtrigger.jsonc` |
| `.build/locales/showtrigger.json` | `<sharp>/locales/showtrigger.json` |

Restart the server (or change map) to load. Requires CommandCenter + LocalizerManager (both ship
with ModSharp).

## ⌨️ Commands

No permissions required — anyone can toggle it for themselves.

| Command | Aliases | Description |
|---------|---------|-------------|
| `!showtriggers` | `!showtrigger`, `!st` | Toggle trigger zones on/off for yourself. Also `ms_showtriggers` in the client console. |

When you turn it on, it reminds you to run `cl_debug_overlays_broadcast 1` in your own console —
that client cvar is what actually renders the broadcast overlays, and it can't be forced server-side.

## 🔧 How it works

Every entity carries a debug-overlay bitfield. Setting `OVERLAY_TRIGGER_BOUNDS_BIT` (`0x2000`) on a
`trigger_*` entity and enabling `sv_debug_overlays_broadcast 1` makes the server network that overlay
to clients. The plugin tracks triggers via an entity listener and flips the bit on every trigger
through two engine functions resolved from gamedata (`AddDebugOverlayBits` / `RemoveDebugOverlayBits`),
located as the `call` targets inside the unique `CMD_ShowTriggers` signature.

Full reverse-engineering writeup — sigs, offsets, the schema-verified overlay-bit enum, and how to
re-sign after a game update — in **[docs/MECHANISM.md](docs/MECHANISM.md)**.

## 📦 Build

```bash
dotnet build -c Release
```

Outputs `.build/modules/ShowTrigger/ShowTrigger.dll`, `.build/gamedata/showtrigger.jsonc`, and
`.build/locales/showtrigger.json`.

## 🙏 Credits

Based on the `showtrigger` concept from
[CS2Surf-CN/Timer](https://github.com/CS2Surf-CN/Timer) (their implementation ships stubbed); this is
an independent, re-signed ModSharp build with the overlay-bit enum verified against the live game.

---

<div align="center">
  <p>Made with ❤️ by <a href="https://github.com/yappershq">yappershq</a></p>
  <p>⭐ Star this repo if you find it useful!</p>
</div>
