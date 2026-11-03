# Logistica come Mindustry — piano di implementazione

> **Per gli agenti:** SOTTO-SKILL RICHIESTA: superpowers:subagent-driven-development (consigliata) o superpowers:executing-plans, task per task. I passi usano le caselle (`- [ ]`).

**Obiettivo:** incrocio a quattro code, router al posto dello sdoppiatore, selezionatore e selezionatore invertito senza direzione, estrattore come l'Unloader, filtri col clic, nastri stretti (variante C), salvataggi v18.

**Architettura:** i blocchi logistici restano celle di `BeltGrid` (incrocio, router, selezionatori) con stato proprio e turni interi salvati; l'estrattore resta un edificio (`Extractor`) riscritto senza direzione, che usa le stesse funzioni di consegna degli altri edifici. Un solo comando di configurazione (`SetBlockFilter`) per tutti i filtri.

**Tecnologie:** C# .NET 8 (`src/TIndustry.Shared`, xUnit), `data/content.json`, client Godot 4.4 .NET, sprite da `tools/art/genera.py`.

**Specifica:** [`docs/superpowers/specs/2026-11-03-logistica-mindustry-design.md`](../specs/2026-11-03-logistica-mindustry-design.md)

## Vincoli globali

- Incrocio: coda di **6** oggetti per ciascuna delle 4 direzioni d'ingresso, attraversamento **26 tick**.
- Router (`router`, al posto di `splitter`): **1** oggetto, **8 tick**, lati provati a turno N, E, S, O dal lato dopo l'ultimo usato, mai verso il lato d'arrivo.
- Selezionatore (`sorter`) e invertito (`sorter-inverted`): istantanei, senza coda; ai lati alternando; un selezionatore non accetta da un selezionatore; senza filtro: normale tutto ai lati, invertito tutto dritto.
- Estrattore: **11 oggetti/s** (accumulatore intero: +11 a tick, un oggetto ogni 60); sorgenti e destinazioni attaccate a turno, mai indietro nella stessa sorgente; senza filtro: oggetti a turno in ordine di id.
- Nessuno di questi blocchi ha direzione: `R` non li ruota; la loro `Direction` salvata è ignorata (tenuta a `North`).
- `SetBlockFilter(PlayerId, GridPosition Tile, string? ItemId)` sostituisce `SetSorterFilter`; filtri nullable ovunque.
- `sorter-inverted`: Ricerca e costi uguali a `sorter`, prerequisito `sorter`.
- Salvataggi **v18** (`CurrentVersion = MinReadableVersion = 18`).
- Regole di sempre (CLAUDE.md): determinismo (niente ordine di `Dictionary`/`HashSet`), interi, testi da `testi.csv` it+en, stili dal tema, sprite solo da `genera.py`, commenti in inglese, commit in italiano con `Co-Authored-By:`, STATO + mappa + barre.

## Punti da sorvegliare in revisione

1. **Conservazione**: smontando un incrocio o un router pieno gli oggetti dentro si perdono, come oggi per le celle dei nastri, ma non si duplicano mai; a regime nessun oggetto sparisce — test in Task 2 e 3.
2. **Router che si rimandano gli oggetti** in un anello di router: niente blocco permanente né crescita di oggetti — test in Task 3.
3. **Selezionatore fra due nastri che vanno verso di lui** (ingresso da due lati nello stesso tick): ordine fisso e un oggetto per ingresso — test in Task 4.
4. **Estrattore attaccato solo a nastri**: non preleva nulla e non fa errori — test in Task 5.
5. **Linea trascinata** di router o selezionatori: nessuna direzione richiesta e nessun incrocio automatico sbagliato (`AutoJunction`) — test in Task 3 e 4.

---

### Task 1: Sprite dei nastri stretti e dei blocchi logistici

**File:** `tools/art/blocchi/logistica.py`, `tools/art/proposte_nastri_stretti.py` (sorgente della variante C), `tools/art/test_genera.py`, PNG rigenerati in `godot/assets/`
- [ ] **Passo 1:** test in `test_genera.py`: misure dei nastri stretti (battistrada 12 px, piastre 4 px su 32 px) per i quattro tier e i pezzi (dritto, curva, T, unioni laterali, unioni con edifici); sprite nuovi senza frecce per `junction`, `router`, `sorter`, `sorter-inverted`, `extractor` (con lo spazio al centro per l'icona del filtro); niente più `splitter`.
- [ ] **Passo 2:** implementazione in `logistica.py`, `python tools/art/genera.py`, `--controlla --pixel` pulito, test verdi. Gli id e i nomi dei file che il client usa per lo sdoppiatore vanno rinominati in `router` anche nel client (solo i riferimenti agli sprite), così la build resta 0/0.
- [ ] **Passo 3: commit** `feat(arte): nastri stretti (variante C) e blocchi logistici come Mindustry`.

### Task 2: Incrocio a quattro code

**File:** `src/TIndustry.Shared/Grid/BeltGrid.cs` (incrocio), test nuovi `tests/TIndustry.Shared.Tests/JunctionMindustryTests.cs` (aggiornare `JunctionSideTests`, `AutoJunctionTests`)
- [ ] **Passo 1: test che falliscono**: due flussi incrociati a regime (nessun blocco reciproco, portata piena su entrambi), coda piena a 6 per direzione, uscita dopo 26 tick, ingresso da un edificio (forno attaccato), uscita bloccata che riempie solo la sua coda, conservazione su 5 minuti.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione (stato per direzione: lista di oggetti con il tick d'ingresso). **Passo 4:** test verdi.
- [ ] **Passo 5: commit** `feat(logistica): incrocio come la Junction di Mindustry`.

### Task 3: Router al posto dello sdoppiatore

**File:** `BeltGrid.cs` (`LogisticsKind.Router` al posto di `Splitter`), `Simulation/Commands.cs` (`BuildingKind.Router`), `Construction/BuildSpec.cs`, `data/content.json` (`router` al posto di `splitter`: costi, Ricerca, nome), `Factory.cs`, `Agent/AgentGame.cs`, `godot/` (palette, testi), test nuovi `RouterTests.cs`
- [ ] **Passo 1: test che falliscono**: turno fra tre uscite, mai indietro, oggetto trattenuto se tutto pieno, catena di router in linea, anello di 4 router con un ingresso e un'uscita (conservazione, nessun blocco), linea trascinata senza direzione, `splitter` non esiste più nei contenuti.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi (Shared, AgentPlay, `TestiTests`).
- [ ] **Passo 5: commit** `feat(logistica): router come Mindustry al posto dello sdoppiatore`.

### Task 4: Selezionatori e filtro col comando

**File:** `BeltGrid.cs` (selezionatore senza direzione, invertito), `Grid/SorterFilters.cs`, `Simulation/Commands.cs` (`SetBlockFilter`, via `SetSorterFilter`; `BuildingKind.SorterInverted`), `SimHost.cs`, `data/content.json` (`sorter-inverted`), `Factory.cs`, `AgentGame.cs` (`piazza` col filtro opzionale, comando `filtro`), test nuovi `SorterMindustryTests.cs`
- [ ] **Passo 1: test che falliscono**: filtro dritto, altri alternati sinistra/destra, lato bloccato → l'altro, nessuno libero → non entra, senza filtro (normale tutto ai lati, invertito tutto dritto), invertito col filtro, selezionatore non accetta da selezionatore, due ingressi nello stesso tick, `SetBlockFilter` su selezionatore e rifiuti (blocco senza filtro, oggetto sconosciuto), piazzamento senza filtro.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi.
- [ ] **Passo 5: commit** `feat(logistica): selezionatore e invertito come Mindustry, filtro col comando`.

### Task 5: Estrattore come l'Unloader

**File:** `Buildings/Extractor.cs` (riscritto), `Simulation/Systems/ExtractionSystem.cs`, `Factory.cs` (piazzamento senza direzione e senza filtro), `SimHost.cs` (`SetBlockFilter` sull'estrattore), test nuovi `UnloaderTests.cs` (aggiornare i test esistenti dell'estrattore, compresi quelli dei magazzini a sé)
- [ ] **Passo 1: test che falliscono**: 11 oggetti/s esatti su 10 s, dal CORE (anche tramite magazzino collegato), da un magazzino a sé, da un forno a un altro forno, a turno fra più destinazioni, mai nella stessa sorgente, senza filtro a turno per id, nessuna destinazione → niente prelievo, solo nastri attaccati → niente, conservazione.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi.
- [ ] **Passo 5: commit** `feat(logistica): estrattore come l'Unloader di Mindustry`.

### Task 6: Salvataggi v18

**File:** `Save/FactorySave.cs`, `Factory.cs` (snapshot/restore di code dell'incrocio, turni, filtri nullable, accumulatore dell'estrattore), `CLAUDE.md` (riga dei salvataggi: «versione 18»), test `SaveRoundTripTests`, `SaveVersionTests`, `DeterminismTests`
- [ ] **Passo 1: test che falliscono**: avanti e indietro con incrocio pieno, router a metà turno, selezionatori senza filtro, estrattore a metà accumulatore (stesso checksum); v17 rifiutato; determinismo 5 minuti con tutti i blocchi.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi.
- [ ] **Passo 5: commit** `feat(salvataggi): versione 18 con la logistica come Mindustry`.

### Task 7: Client

**File:** pannello del filtro nuovo in `godot/scripts/ui/panels/` (griglia di icone e «nessuno», tema `UiTheme`), `World.Input.cs`/`World.Visuals.cs` (clic con lo strumento cursore su selezionatori ed estrattori apre il pannello; icona del filtro sui blocchi), palette (nessuna rotazione per questi blocchi, invertito nuovo), `godot/lingue/testi.csv` (nomi, suggerimenti, pannello, rifiuti; via «F filtro»), `tools/AgentPlay/README.md`
- [ ] **Passo 1:** client e testi; build 0/0, `TestiTests` verdi.
- [ ] **Passo 2 (PC di casa):** playtest `ui`, `tutorial`, `agenti`; a mano filtri col clic, incroci, router, estrattore fra due forni.
- [ ] **Passo 3: commit** `feat(client): filtri col clic e logistica come Mindustry`.

### Task 8: Documenti, stato e diario

**File:** `docs/wiki/Logistica.md`, `docs/wiki/Comandi.md`, `godot/README.md`, `README.md`, `docs/ARCHITETTURA.md`, `docs/wiki/Salvataggi-formato.md`, sito e pagina Asset se nominano lo sdoppiatore (`tools/sito/`), `CHANGELOG.md`, `docs/STATO.md` (aspetto 3: caselle di #252–#255 e #270), mappa, barre, diario `docs/diario/2026-11-03-logistica-mindustry.md`, specifica «attuata».
- [ ] **Passo 1:** testi coi numeri dei vincoli.
- [ ] **Passo 2:** `python tools/sito/stato.py`, `python tools/dev/mappa_canvas.py`, `python tools/dev/controlla_wiki.py`, test del sito.
- [ ] **Passo 3: commit** `docs: logistica come Mindustry`.
