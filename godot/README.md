# tIndustry — Godot 4 .NET (Phase A)

Playable factory slice beyond the spike. Raylib on `main` remains the dual play/fix path until the port replaces it.

## Requirements

- **Godot 4.4+ .NET** (mono) build — standard (non-.NET) editor cannot compile C#
- .NET SDK 8+ (Godot C# projects target `net8.0`)
- Repo checkout with `data/content.json`

## Open / run

```bash
# From repo root
godot4 --path godot
# or
Godot_v4.4.1-stable_mono_linux.x86_64 --path godot
```

Main scene: `godot/scenes/Spike.tscn`

Controls: **WASD** / middle-drag pan, mouse wheel zoom.

## What you should see (Phase A loop)

- Checkerboard grid + deposit tint under miner
- **Minatore T1 statico** that actually produces `iron-ore` (Shared `MinerProducer`)
- **L-belt** Mindustry visuals (scrolling chevrons + platform corner)
- Items riding the belt into **Core magazzino** (blue tiles) → stock HUD
- Italian HUD: stock «Ferro grezzo», progresso minatore, item sul nastro
- **No combat**

## Projects

| Path | Role |
| --- | --- |
| `godot/` | Godot 4 C# playable slice |
| `src/TIndustry.Shared/` | Belt, content load, wallet, miner, core sink, smelter stub |
| `TIndustry.Logistics.csproj` | Raylib game — dual path (build + `--self-test`) |

## CI

Godot is **optional / docs-only** in CI. Existing `dotnet` Raylib build/publish/self-test jobs are the gate. Shared is excluded from the Raylib csproj glob (`Compile Remove`).
