# tIndustry — Godot 4 .NET (**path ufficiale**)

Client di gioco primario. Sim in `src/TIndustry.Shared/`.

## Download

Release zip ufficiali = questo client:

- Windows: `tIndustry-win-x64.zip` → `tIndustry.exe`
- Linux: `tIndustry-linux-x64.zip` → `tIndustry.x86_64`

## Run (da sorgente)

```bash
godot4 --path godot
TINDUSTRY_FRESH=1 godot4 --path godot          # ignore continua
TINDUSTRY_CAPTURE=1 TINDUSTRY_CAPTURE_MODE=home godot4 --path godot
```

Richiede Godot **4.4+** .NET e restore C# (`dotnet build godot/TIndustry.Godot.csproj`).

## Export locale (CI)

```bash
./tools/ci/setup-godot.sh   # editor + templates (una tantum)
./tools/ci/export-godot.sh  # → dist/tIndustry-*-x64.zip
```

## Controlli build

**Cursore** (default, categoria Strumenti) · 1–9 tools · **Esc** / riesci tool → cursore · **R** ruota · **C** cicla filtro · click piazza · **destro** elimina · pan: **WASD**, rotella premuta, o trascina col sinistro in modalità cursore · rotella zoom · **H** torna al CORE

Palette Mindustry (angolo basso-destra): griglia sprite senza testo né numeri; rail categorie a icone (Strumenti / Logistica / Produzione / Potenza); hover/selezione apre il pannello info (nome, tasto rapido `[2]`, costi con icona barrata se mancano); `?` apre dettaglio.

HUD in alto a sinistra (stile Mindustry): pulsanti **Menu · Ricerca · Mercato · Campagna**, poi pannello CORE con **Fondi**, energia e solo le risorse che hai (icona + numero, nome al passaggio del mouse), **Potenzia CORE** con i costi, e gli obiettivi di campagna.

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

## Prove automatiche (sviluppo)

```bash
# Profilo isolato: non tocca impostazioni/salvataggi reali
TINDUSTRY_DATA_DIR=/tmp/tind-profile TINDUSTRY_CAPTURE_DIR=/tmp/tind-shots   TINDUSTRY_PLAYTEST=tutorial godot4 --path godot --resolution 1280x720
```

`TINDUSTRY_PLAYTEST=tutorial` gioca il tutorial con input simulati e salva un PNG per passo (exit 0 = OK). `TINDUSTRY_CAPTURE=1 TINDUSTRY_CAPTURE_MODE=<home|research|campaign|…>` salva screenshot delle schermate in `TINDUSTRY_CAPTURE_DIR` (default `godot/artifacts/`).

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

## Ricerca

**T** apre il grafo Ricerca: **nodi icona** + archi ortogonali, trascina per muoverti, rotella zoom, H vista intera, percorso evidenziato. Sblocca strutture spendendo denaro/materiali; place tools 3–9 restano gated. Salvataggio v3 include `unlockedStructures` + vendite Mercato.

## Mercato

**M** apre il Mercato: vendi stock del Core (1 / tutti) a prezzo dinamico (scende con stock alto). Il pannello Core mostra `$`. Necessario per guadagnare denaro e sbloccare in Ricerca.

## Campagna

**G** apre la selezione livelli (IT). Obiettivi in alto a sinistra (`OBIETTIVO`). Completando un livello si sblocca il successivo. Continua salva `activeCampaignLevelId` (save v4).
