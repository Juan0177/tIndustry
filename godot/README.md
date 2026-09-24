# tIndustry — Godot 4 .NET (sorter + bridge)

Playable factory loop with **FactoryHud**, **JSON save/load**, power stubs, **sorter** + **bridge** (decision 14 thin span). Raylib resta dual path.

## Run

```bash
godot4 --path godot
TINDUSTRY_FRESH=1 godot4 --path godot          # ignore continua
TINDUSTRY_CAPTURE=1 godot4 --path godot        # screenshots
```

## Controlli build

1–9 tools · R ruota · **C** cicla filtro selezionatore · click piazza · destro rimuovi · WASD pan

| Hotkey | Tool |
| --- | --- |
| 1 | Nastro |
| 2 | Minatore |
| 3 | Forno |
| 4 | Assemblatore |
| 5 | Giunzione |
| 6 | Splitter |
| 7 | Generatore |
| 8 | Selezionatore |
| 9 | Ponte |

Ponte: estremi full 1×1, centro ~78% thickness, span 2–4, mid tiles free (belt can cross under).

## Save / load

| Azione | UI | Hotkey | Slot file |
| --- | --- | --- | --- |
| Salva continua | **Salva · F5** | F5 | `continua.json` |
| Carica continua | **Carica · F9** | F9 | `continua.json` |
| Salva slot | **Slot↑ · F6** | F6 | `slot-1.json` |
| Carica slot | **Slot↓ · F7** | F7 | `slot-1.json` |

Path: `%LocalAppData%/tIndustry/godot-saves/` (Linux: `~/.local/share/tIndustry/godot-saves/`).  
Persists belts (+items, bridge partner, sorter filter), buildings, wallet, nextItemId, core.

## Projects

| Path | Role |
| --- | --- |
| `godot/` | Playable slice + FactoryHud |
| `src/TIndustry.Shared/` | FactorySlice + BeltGrid sorter/bridge + save |
| `TIndustry.Logistics` | Raylib (GameSave v8 separate) |
