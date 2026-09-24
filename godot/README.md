# tIndustry — Godot 4 .NET (Phase C)

Playable factory loop: **miner → nastro → forno → nastro → core** (lastre). Raylib resta dual path.

## Requirements

- Godot 4.4+ .NET + .NET SDK 8+
- `data/content.json`

## Run

```bash
godot4 --path godot
```

## Controlli

| Input | Azione |
| --- | --- |
| **1** / N | Tool nastro |
| **2** / M | Tool minatore (2×2 statico) |
| **3** / F | Tool forno (2×2 stub) |
| Click / trascina | Piazza (nastri: drag) |
| **R** | Ruota direzione |
| Destro | Rimuovi nastro/edificio |
| WASD / middle-drag | Pan |
| Rotella | Zoom |

## Cosa vedi

- Seed Phase C: minatore → feed → **Forno** (`smelt-iron`) → output L → core
- Nastri Mindustry **full tile** + platform corners
- HUD IT: ore + lastre in magazzino
- Nessun combat

## Projects

| Path | Role |
| --- | --- |
| `godot/` | Playable slice |
| `src/TIndustry.Shared/` | BeltGrid, miner, forno stub, FactorySlice |
| `TIndustry.Logistics` | Raylib dual path |
