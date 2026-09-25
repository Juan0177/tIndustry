# tIndustry — Godot 4 .NET (**path ufficiale**)

Client di gioco primario. Sim in `src/TIndustry.Shared/`. Il client Raylib (`TIndustry.Logistics`) è **legacy** (manutenzione opzionale).

## Run

```bash
godot4 --path godot
TINDUSTRY_FRESH=1 godot4 --path godot          # ignore continua
TINDUSTRY_CAPTURE=1 TINDUSTRY_CAPTURE_MODE=home godot4 --path godot
```

Richiede Godot **4.4+** .NET e restore C# (`dotnet build godot/TIndustry.Godot.csproj`).

## Controlli build

**Cursore** (default, categoria St) · 1–9 tools · **Esc** / riesci tool → cursore · **R** ruota · **C** cicla filtro · click piazza · **destro** elimina · WASD pan

Palette Mindustry (angolo basso-destra): griglia sprite senza testo sotto; rail **St / Lo / Pr / Po**; hover/selezione apre pannello info (nome IT, I/O, costi con icona barrata se stock insufficiente); `?` apre dettaglio.

| Hotkey | Azione |
| --- | --- |
| Esc / \` | Cursore (nessun tool) |
| 1–9 | Nastro … Ponte (2 = Minatore) |
| M | Mercato (vendi stock) |
| G | Campagna (seleziona livello) |
| T | Ricerca (grafo icon-node) |
| R | Ruota direzione piazzamento |
| C | Cicla filtro selezionatore |
| RMB | Elimina (non è uno slot palette) |

Ponte: estremi full 1×1, centro ~78% thickness, span 2–4, mid tiles free (belt can cross under).

## Save / load

| Azione | UI | Hotkey | Slot file |
| --- | --- | --- | --- |
| Salva continua | chip **F5** | F5 | `continua.json` |
| Carica continua | chip **F9** | F9 | `continua.json` |
| Salva slot | chip **F6** | F6 | `slot-1.json` |
| Carica slot | chip **F7** | F7 | `slot-1.json` |

Path: `%LocalAppData%/tIndustry/godot-saves/` (Linux: `~/.local/share/tIndustry/godot-saves/`).  
Persists belts (+items, bridge partner, sorter filter), buildings, wallet, nextItemId, core.

## Projects

| Path | Role |
| --- | --- |
| `godot/` | **Official** playable client |
| `src/TIndustry.Shared/` | Factory sim + content + save (shared) |
| `TIndustry.Logistics` | **Legacy** Raylib client (optional maintenance) |

## Ricerca

**T** apre il grafo Ricerca: **nodi icona** + archi ortogonali, pan (Shift/RMB), Ctrl+rotella zoom, H reset, percorso evidenziato. Sblocca strutture spendendo denaro/materiali; place tools 3–9 restano gated. Salvataggio v3 include `unlockedStructures` + vendite Mercato.

## Mercato

**M** apre il Mercato: vendi stock del Core (1 / tutti) a prezzo dinamico (scende con stock alto). Il pannello Core mostra `$`. Necessario per guadagnare denaro e sbloccare in Ricerca.

## Campagna

**G** apre la selezione livelli (IT). Obiettivi in alto a sinistra (`OBIETTIVO`). Completando un livello si sblocca il successivo. Continua salva `activeCampaignLevelId` (save v4).
