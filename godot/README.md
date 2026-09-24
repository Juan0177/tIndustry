# tIndustry — Godot 4 .NET (Phase E + Factory UI)

Playable factory loop: **miner → forno → junction → assemblatore → splitter → core** (filo di rame). Raylib resta dual path.

## Requirements

- Godot 4.4+ .NET + .NET SDK 8+
- `data/content.json`

## Run

```bash
godot4 --path godot
# optional UI screenshot capture:
TINDUSTRY_CAPTURE=1 godot4 --path godot
```

## UI (FactoryHud)

- **Toolbar** in basso: Nastro · Minatore · Forno · Assemblatore · Giunzione · Splitter · **Ruota** (mostra Dir)
- Click **Ruota** (o tasto **R**) per ciclare direzione
- **Core** in alto a destra: stock IT (ferro / lastre / rame / filo) + consegnati / nastro
- Toast breve sulla selezione tool
- Hotkeys **1–6** / **R** restano attivi; click UI non piazza sul mondo (`_UnhandledInput`)

## Controlli

| Input | Azione |
| --- | --- |
| Toolbar click / **1–6** | Seleziona tool |
| **Ruota** / **R** | Ruota direzione |
| Click / trascina | Piazza (nastri/logistics: drag) |
| Destro | Rimuovi nastro/edificio |
| WASD / middle-drag | Pan |
| Rotella | Zoom |

## Cosa vedi

- Seed Phase E + building pads + opaque items
- HUD Control reale (non più wall di debug text)
- Nessun combat · Phase F power/save ancora next

## Projects

| Path | Role |
| --- | --- |
| `godot/` | Playable slice + `FactoryHud` |
| `src/TIndustry.Shared/` | BeltGrid / FactorySlice |
| `TIndustry.Logistics` | Raylib dual path |
