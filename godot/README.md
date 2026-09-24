# tIndustry — Godot 4 .NET (Phase B)

Playable factory slice with **placeable Mindustry belts**. Raylib on `main` remains the dual play/fix path.

## Requirements

- **Godot 4.4+ .NET** (mono) + .NET SDK 8+
- Repo checkout with `data/content.json`

## Open / run

```bash
godot4 --path godot
# or
Godot_v4.4.1-stable_mono_linux.x86_64 --path godot
```

Main scene: `godot/scenes/Spike.tscn`

## Controls (IT)

| Input | Azione |
| --- | --- |
| Click sinistro / trascina | Piazza nastro (direzione corrente) |
| **R** | Ruota direzione piazzamento |
| Click destro / trascina | Rimuovi nastro |
| WASD / middle-drag | Pan camera |
| Rotella | Zoom |

## Cosa vedi

- Seed L-belt miner → core (loop già attivo)
- Piazzamento libero: dritti con chevron scroll + **angoli piattaforma** dove il flusso gira 90°
- Nastri regolari = **tile piena** (edge-to-edge); il look sottile ~78% è solo per i **ponti** (non ancora in scena)
- Minatore T1 statico, core magazzino, HUD italiano
- **Nessun combat**

## Projects

| Path | Role |
| --- | --- |
| `godot/` | Godot playable slice |
| `src/TIndustry.Shared/` | `BeltGrid`, content, miner, core sink |
| `TIndustry.Logistics.csproj` | Raylib dual path |

## CI

Godot optional. Gate = Raylib `dotnet build` / `--self-test`. Shared excluded from Raylib glob.
