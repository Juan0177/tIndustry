# tIndustry — Godot 4 .NET spike

Vertical slice (**not** a full port). Raylib game on `main` play path is unchanged.

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

## What you should see

- Checkerboard grid map
- An **L-shaped** belt: scrolling arrows on straight legs + a **full-cell blue platform pad** at the corner (rivets, recessed L channel, **no arrows**)
- Iron-ore icons **riding on top** of both legs and through the corner (same `BeltLane` Advance math as Logistics, per-cell direction)
- **Static** miner sprite (`assets/miner.png`) — same as Raylib; no drill / tip spin

### Corners / junctions

This spike shows one **L corner** as a static platform pad (straight legs keep scrolling arrows). Cross/splitter UV and multi-way junctions remain follow-ups for the core port.

## Projects

| Path | Role |
| --- | --- |
| `godot/` | Godot 4 C# spike app |
| `src/TIndustry.Shared/` | Thin shared belt/grid/content DTOs (additive; Raylib does not reference it yet) |
| `TIndustry.Logistics.csproj` | Existing Raylib game — **unchanged play path** |

## CI

Godot is **optional / docs-only** in CI: runners do not install a headless Godot .NET editor. Existing `dotnet` Raylib build/publish jobs are untouched.
