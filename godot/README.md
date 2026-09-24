# tIndustry — Godot 4 .NET (Phase D)

Playable factory loop: **miner → nastro → forno → assemblatore → nastro → core** (filo di rame). Raylib resta dual path.

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
| **4** / A | Tool assemblatore (2×2 stub) |
| Click / trascina | Piazza (nastri: drag) |
| **R** | Ruota direzione |
| Destro | Rimuovi nastro/edificio |
| WASD / middle-drag | Pan |
| Rotella | Zoom |

## Cosa vedi

- Seed Phase D: minatore ferro → forno (`smelt-iron`) + minatore rame → **Assemblatore** (`craft-copper-wire`) → core
- Nastri Mindustry **full tile** + gallery corners (Blu1–4)
- HUD IT: ore / lastre / rame / filo in magazzino
- Nessun combat · nessun power stub ancora

## Projects

| Path | Role |
| --- | --- |
| `godot/` | Playable slice |
| `src/TIndustry.Shared/` | BeltGrid, miner, forno/assemblatore stub, FactorySlice |
| `TIndustry.Logistics` | Raylib dual path |
