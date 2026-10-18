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

**Cursore** (default, categoria Strumenti) · 1–9 tools · **Esc** / riesci tool → cursore · **R** ruota · **C** cicla filtro · click pianifica · **destro** cancella o smonta · pan: **WASD**, rotella premuta, o trascina col sinistro in modalità cursore · rotella zoom · **H** torna al CORE

### Costruzione (come Mindustry)

Piazzare non crea il blocco subito: diventa un **piano**, una sagoma semitrasparente col contorno giallo. Trascinando i nastri compare una sagoma per cella. Il **drone** parte dal CORE, vola verso il primo piano della coda e lo costruisce: lo sprite si disegna dal basso mentre il raggio parte dal drone. Le risorse escono dal CORE man mano e non bloccano il piazzamento: se mancano, il cantiere si ferma e, passandoci sopra col mouse, dice cosa manca ("Mancano: 3 Lastra di ferro", oppure "Mancano: $20"). Riparte da solo quando le risorse arrivano. Se un cantiere resta fermo per un secondo e in coda ci sono altri piani, il drone lo lascia dov'è (con quello che ha già speso) e passa ai successivi, come in Mindustry: il piano fermo va in fondo alla coda e il cantiere riprende quando ritocca a lui. Un nastro accanto al CORE è pianificato già rivolto verso il CORE. Con la coda vuota il drone torna accanto al CORE.

Tasto **destro**:

- su un **piano** non ancora iniziato lo cancella; su un blocco già segnato per lo smontaggio toglie il segno, da qualunque cella del blocco (anche dall'altro capo di un ponte);
- su un **cantiere** in costruzione lo annulla: il drone lo smonta e restituisce man mano tutto quello già speso (un cantiere lasciato fermo viene smontato quando ritocca a lui);
- su un **edificio** o un **nastro** lo segna in rosso (riempimento rosso traslucido): il drone lo smonta e rimborsa man mano (100% nella partita normale e nei livelli 1–3 della campagna, 50% dal livello 4); gli oggetti dentro vanno persi;
- su un cantiere già in smontaggio o su una cella vuota non fa nulla.

Tenendo premuto il destro e trascinando si **segnano** più blocchi, una volta per blocco: il trascinamento segna, cancella piani e annulla cantieri, ma non toglie mai un segno di smontaggio, anche ripassando su blocchi già segnati o con due trascinamenti sovrapposti. Solo un clic destro singolo, senza trascinare, toglie un segno. Un trascinamento veloce non lascia buchi: le celle saltate fra due movimenti del mouse vengono riempite (anche piazzando nastri).

**Coda nell'HUD**: nella colonna in alto a sinistra, sotto le risorse del CORE, un riquadro mostra il blocco in lavorazione con la sua barra (rossa se si smonta, ambra con "Manca: <materiale>" se il cantiere è fermo per le risorse) e "+N" piani in attesa. Un clic sposta la visuale sul cantiere. Il riquadro sparisce quando la coda è vuota. Se un piano non si può più costruire quando il drone arriva (cella occupata, blocco non sbloccato) un avviso dice il motivo.

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
| RMB | Cancella un piano, annulla un cantiere o segna un blocco da smontare; clic singolo toglie il segno, trascinare segna soltanto (non è uno slot palette) |

Ponte: estremi full 1×1, centro ~78% thickness, span 2–4, mid tiles free (belt can cross under).

## Prove automatiche

```bash
tools/dev/playtest.sh ui /tmp/shots-ui          # oppure: tutorial
python tools/dev/compare_shots.py <baseline> <nuovi>
```

`tools/dev/playtest.sh` lancia il gioco con `TINDUSTRY_PLAYTEST=ui|tutorial`, input simulati e un profilo dati temporaneo (`TINDUSTRY_DATA_DIR`): non tocca impostazioni e salvataggi reali. Salva un PNG per passo e `log.txt` nella cartella indicata; esce con 0 solo se il log contiene `PLAYTEST OK` e nessun `ERROR`. Con `TINDUSTRY_PLAYTEST_PERF=1` il playtest `ui` posa anche 200 nastri e stampa FPS medio e minimo (`PLAYTEST perf: …`), senza esito. Con `TINDUSTRY_PLAYTEST_BLOCCHI=1` costruisce ogni blocco di produzione e potenza spento e acceso e salva `ui-blocchi-spenti.png`, `ui-blocchi-accesi.png` e `ui-blocchi-zoom04.png`, senza esito. `compare_shots.py` confronta due cartelle di screenshot ignorando la zona FPS.

Lanciando Godot a mano, `TINDUSTRY_PLAYTEST` richiede `TINDUSTRY_DATA_DIR`; `TINDUSTRY_CAPTURE_DIR` cambia dove vanno gli screenshot (default `godot/artifacts/`).

## Save / load

| Azione | UI | Hotkey | Slot file |
| --- | --- | --- | --- |
| Salva continua | chip **F5** | F5 | `continua.json` |
| Carica continua | chip **F9** | F9 | `continua.json` |
| Salva slot | chip **F6** | F6 | `slot-1.json` |
| Carica slot | chip **F7** | F7 | `slot-1.json` |

Path: `%LocalAppData%/tIndustry/godot-saves/` (Linux: `~/.local/share/tIndustry/godot-saves/`), oppure `godot-saves/` dentro `TINDUSTRY_DATA_DIR` se impostata. Formato alla versione 11 (metadati + istantanea): i salvataggi più vecchi non sono compatibili.  
Persists belts (+items, bridge partner, sorter filter), buildings, wallet, nextItemId, core, regole di partita, drone con la coda dei piani e cantieri a metà.

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
