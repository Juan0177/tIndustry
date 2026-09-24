# tIndustry — Godot 4 .NET (Phase E)

Playable factory loop: **miner → forno → junction → assemblatore → splitter → core** (filo di rame). Raylib resta dual path.

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
| **2** / M | Tool minatore (2×2) |
| **3** / F | Tool forno (2×2) |
| **4** / A | Tool assemblatore (2×2) |
| **5** / J | Tool giunzione (cross-axis) |
| **6** / T | Tool splitter (T-fork) |
| Click / trascina | Piazza (nastri/logistics: drag) |
| **R** | Ruota direzione |
| Destro | Rimuovi nastro/edificio |
| WASD / middle-drag | Pan |
| Rotella | Zoom |

## Cosa vedi

- Seed Phase E: minatore ferro → forno → **giunzione** → assemblatore + minatore rame → **splitter** → core
- Nastri Mindustry **full tile** + gallery corners (Blu1–4)
- HUD IT: ore / lastre / rame / filo + conteggio J/S
- Nessun combat · nessun power stub ancora

## Projects

| Path | Role |
| --- | --- |
| `godot/` | Playable slice |
| `src/TIndustry.Shared/` | BeltGrid (belt/junction/splitter), FactorySlice |
| `TIndustry.Logistics` | Raylib dual path |
