# tIndustry — Godot 4 .NET (Phase F + Factory UI)

Playable factory loop: **miner → forno (+ generatore) → junction → assemblatore → splitter → core**. Raylib resta dual path.

## Requirements

- Godot 4.4+ .NET + .NET SDK 8+
- `data/content.json`

## Run

```bash
godot4 --path godot
TINDUSTRY_CAPTURE=1 godot4 --path godot   # Phase F screenshots
```

## UI (FactoryHud)

- **Toolbar**: Nastro · Minatore · Forno · Assemblatore · Giunzione · Splitter · **Generatore** · Ruota
- **Core** panel: stock IT + `potenza ON/off`
- Hotkeys **1–7** / **R**; click UI non piazza sul mondo

## Controlli

| Input | Azione |
| --- | --- |
| Toolbar / **1–7** | Tool (7/`G` = generatore) |
| **Ruota** / **R** | Ruota direzione |
| Click / trascina | Piazza |
| Destro | Rimuovi |
| WASD / middle-drag | Pan |

## Phase F

- Generatore 2×2 brucia **carbone** dai nastri
- Adiacenza 4-connected → craft **+20%** velocità
- Seed: coal miner → gen nord del forno + Phase E loop
- Building pads + opaque items + junctions retained

## Projects

| Path | Role |
| --- | --- |
| `godot/` | Playable slice + FactoryHud |
| `src/TIndustry.Shared/` | FactorySlice / GeneratorStub / BeltGrid |
| `TIndustry.Logistics` | Raylib dual path |
