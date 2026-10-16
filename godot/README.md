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

## Modalità dev

Solo per sviluppo: si attiva con `TINDUSTRY_DEV=1` (o `tools/dev/dev.sh --dev`) e funziona solo nelle build di debug (`godot4 --path godot`, editor); il gioco esportato per i giocatori la ignora. Spenta durante i playtest.

```powershell
$env:TINDUSTRY_DEV = "1"
$env:TINDUSTRY_DEV_AVVIO = "sblocca tutto; soldi 10000; velocita 4"   # facoltativo
godot4 --path godot
```

In alto al centro compare la riga di stato: velocità, tick, tempo di gioco e cella sotto il mouse.

| Tasto | Azione |
| --- | --- |
| F1 | Apri / chiudi la console (Esc chiude) |
| F2 | Pausa / riprendi |
| F3 / F4 | Più lento / più veloce (0.25× … 32×) |
| . | In pausa: avanza di un tick |

Nella console: Invio esegue, ↑/↓ cronologia, Tab completa il comando.

| Comando | Effetto |
| --- | --- |
| `velocita <x>` | velocità della simulazione 0.1–32 (`0` = pausa) |
| `pausa` · `passo [n]` | pausa / riprendi · in pausa avanza di n tick (1 tick = 1/60 s) |
| `avanti <secondi>` | simula subito fino a 3600 s di gioco |
| `soldi <n>` | aggiunge (o toglie, se negativo) fondi |
| `dai <oggetto\|tutto> [n]` | aggiunge n oggetti al CORE (default 100) |
| `sblocca <blocco\|tutto>` | sblocca in Ricerca senza pagare |
| `energia` | riempie il buffer di energia |
| `vai <x> <y>` · `vai core` | sposta la visuale |
| `vinci` | completa il livello di campagna attivo |
| `info` · `oggetti` · `blocchi` | stato della partita · id validi |
| `pulisci` · `aiuto` | svuota la console · elenco comandi |

Ogni comando ha anche un alias inglese (`speed`, `give`, `unlock`, `money`, `ff`, `tp`…). `TINDUSTRY_DEV_AVVIO` esegue i comandi (separati da `;`) all'inizio di ogni partita: nuova, livello di campagna o caricamento.

I trucchi finiscono nei salvataggi come una partita normale: per le prove usa un profilo temporaneo (`--profilo-temp` o `TINDUSTRY_DATA_DIR`).

## Prove automatiche

```bash
tools/dev/playtest.sh ui /tmp/shots-ui          # oppure: tutorial
python tools/dev/compare_shots.py <baseline> <nuovi>
```

`tools/dev/playtest.sh` lancia il gioco con `TINDUSTRY_PLAYTEST=ui|tutorial`, input simulati e un profilo dati temporaneo (`TINDUSTRY_DATA_DIR`): non tocca impostazioni e salvataggi reali. Salva un PNG per passo e `log.txt` nella cartella indicata; esce con 0 solo se il log contiene `PLAYTEST OK` e nessun `ERROR`. `compare_shots.py` confronta due cartelle di screenshot ignorando la zona FPS.

Lanciando Godot a mano, `TINDUSTRY_PLAYTEST` richiede `TINDUSTRY_DATA_DIR`; `TINDUSTRY_CAPTURE_DIR` cambia dove vanno gli screenshot (default `godot/artifacts/`).

## Save / load

| Azione | UI | Hotkey | Slot file |
| --- | --- | --- | --- |
| Salva continua | chip **F5** | F5 | `continua.json` |
| Carica continua | chip **F9** | F9 | `continua.json` |
| Salva slot | chip **F6** | F6 | `slot-1.json` |
| Carica slot | chip **F7** | F7 | `slot-1.json` |

Path: `%LocalAppData%/tIndustry/godot-saves/` (Linux: `~/.local/share/tIndustry/godot-saves/`), oppure `godot-saves/` dentro `TINDUSTRY_DATA_DIR` se impostata. Formato alla versione 9: i salvataggi più vecchi non sono compatibili.  
Persists belts (+items, bridge partner, sorter filter), buildings, wallet, nextItemId, core.

## Projects

| Path | Role |
| --- | --- |
| `godot/` | **Official** playable client |
| `src/TIndustry.Shared/` | Factory sim + content + save (shared) |
| `data/` | Contenuti (`content.json`, `campaign.json`), unica fonte |

Mappa completa: [`docs/ARCHITETTURA.md`](../docs/ARCHITETTURA.md).

## Ricerca

**T** apre la Ricerca, un albero stile Mindustry: **CORE in cima**, ogni blocco sotto il suo prerequisito (linee a gomito; prerequisiti aggiuntivi tratteggiati). Bordo dorato = puoi sbloccarlo ora, lucchetto = prima sblocca il precedente; passa col mouse per costo e mancanze, click per il pannello di sblocco. Trascina per muoverti, rotella zoom, H vista intera. Sblocca strutture spendendo denaro/materiali; place tools 3–9 restano gated. Salvataggio v3 include `unlockedStructures` + vendite Mercato.

## Mercato

**M** apre il Mercato: vendi stock del Core (1 / tutti) a prezzo dinamico (scende con stock alto). Il pannello Core mostra `$`. Necessario per guadagnare denaro e sbloccare in Ricerca.

## Campagna

**G** apre la selezione livelli (IT). Obiettivi in alto a sinistra (`OBIETTIVO`). Completando un livello si sblocca il successivo. Continua salva `activeCampaignLevelId` (save v4).
