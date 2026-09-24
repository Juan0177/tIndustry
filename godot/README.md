# tIndustry — Godot 4 .NET (Phase F + save/load)

Playable factory loop with **FactoryHud** and **JSON save/load**. Raylib resta dual path.

## Run

```bash
godot4 --path godot
TINDUSTRY_FRESH=1 godot4 --path godot          # ignore continua
TINDUSTRY_CAPTURE=1 godot4 --path godot        # screenshots
```

## Save / load

| Azione | UI | Hotkey | Slot file |
| --- | --- | --- | --- |
| Salva continua | **Salva · F5** | F5 | `continua.json` |
| Carica continua | **Carica · F9** | F9 | `continua.json` |
| Salva slot | **Slot↑ · F6** | F6 | `slot-1.json` |
| Carica slot | **Slot↓ · F7** | F7 | `slot-1.json` |

Path: `%LocalAppData%/tIndustry/godot-saves/` (Linux: `~/.local/share/tIndustry/godot-saves/`).  
On boot, **continua** auto-loads if present (skip with `TINDUSTRY_FRESH=1`).

Persists: belts (+items), miners/forni/assemblatori/generatori, wallet, nextItemId, core.

## Controlli build

1–7 tools · R ruota · click piazza · destro rimuovi · WASD pan

## Projects

| Path | Role |
| --- | --- |
| `godot/` | Playable slice + FactoryHud |
| `src/TIndustry.Shared/` | FactorySlice + `FactorySliceSaveStore` |
| `TIndustry.Logistics` | Raylib (GameSave v8 separate) |
