# Sostituzioni come Mindustry, percorso dei nastri e nastri simmetrici — piano di implementazione

> **Per gli agenti:** SOTTO-SKILL RICHIESTA: superpowers:subagent-driven-development (consigliata) o superpowers:executing-plans, task per task. I passi usano le caselle (`- [ ]`).

**Obiettivo:** nastri con i due bordi uguali; sostituire blocchi logistici e edifici della stessa famiglia piazzandoci sopra; doppio clic su un nastro per selezionare il percorso e sostituirlo tutto.

**Architettura:** la sostituzione di oggi (solo nastro su nastro: `BeltOverBeltAt`, `ReplacedBelt`, `BeginReplace`, `SettleReplacement`) si generalizza a un elenco di blocchi tolti (`ReplacedBlocks`) e a due regole: logistica 1×1 su logistica 1×1, edificio su edifici della sua famiglia tutti dentro l'impronta. Il percorso dei nastri è una funzione pura in `Shared` usata dal client.

**Tecnologie:** C# .NET 8 (`src/TIndustry.Shared`, xUnit), Python 3 (`tools/art`), client Godot 4.4 .NET.

**Specifica:** [`docs/superpowers/specs/2026-11-11-sostituzioni-design.md`](../specs/2026-11-11-sostituzioni-design.md).

**Briefing tecnico:** `.superpowers/sdd/2026-11-11-sostituzioni/briefing.md`.

## Vincoli globali

- Nastri simmetrici: righe delle piastre uguali e specchiate sui due bordi; rivetti del nastro rinforzato su tutti e due i lati; tutti i tier e i pezzi a T e di unione; incrocio invariato.
- Logistica 1×1 sostituibile fra loro: nastri di ogni tier (base, veloce, espresso, rinforzato, a pacchi), `junction`, `router`, `sorter`, `sorter-inverted`, `overflow-gate`, `underflow-gate`. Esclusi `conveyor-bridge` e `bridge-long`.
- Famiglie di edifici: minatori (`miner`, `miner-advanced`, `impact-drill`), forni (`smelter`, `smelter-advanced`), generatori (`generator`, `combustion-generator`), batterie (`battery`, `battery-large`). Ogni blocco tolto deve stare **tutto** dentro l'impronta nuova; le altre celle libere come per un piazzamento normale.
- Costo: si paga la differenza per riga di costo (credito = minimo fra nuovo e vecchio sommato su tutti i blocchi tolti); l'eccesso del vecchio si rende alla quota della partita, come oggi.
- Oggetti: nel blocco nuovo quelli che ci stanno (nastri, router; il buffer di forni e generatori va nel nuovo se lo accetta), il resto al CORE.
- Stesso id sopra: nessun effetto (stesso tier girato: lo gira, come oggi).
- Annullare un cantiere di sostituzione rimette tutti i blocchi tolti, gratis, col loro stato.
- Doppio clic: percorso dalla prima all'ultima cella di nastri (`LogisticsKind.Belt` o `Stack`); avanti finché la cella dopo è un nastro; indietro finché **un solo** nastro porta nella cella; mai due volte la stessa cella (anelli).
- Salvataggi **v23**.
- Regole di sempre (CLAUDE.md).

## Punti da sorvegliare in revisione

1. **Selezionatore sostituito durante un passaggio**: nessun oggetto perso né duplicato quando un nastro con oggetti diventa un selezionatore o un gate (che non tengono oggetti: tutti al CORE) — Task 2.
2. **Laser dei nodi verso un T2 sostituito da un trapano**: tolti puliti, nessun laser verso un id che non c'è più, anche dopo il caricamento — Task 3.
3. **Rimozione a rettangolo sopra un cantiere di sostituzione di più edifici**: annulla e rimette tutto — Task 3.
4. **Percorso con un anello**: il doppio clic termina — Task 4.
5. **Pannellino con la Scala UI al 200%**: resta dentro lo schermo (`PlaceNear`) — Task 5.

---

### Task 1: Nastri simmetrici

**File:** `tools/art/blocchi/logistica.py` (`RIGHE` speculari, rivetti su due lati), `tools/art/test_genera.py` (test nuovo: ogni sprite di nastro, curva, T e unione ha le righe del bordo superiore uguali a quelle inferiori specchiate), PNG rigenerati con `python3 tools/art/genera.py`

- [ ] **Passo 1:** test che fallisce (`test_nastri_simmetrici`). **Passo 2:** FAIL. **Passo 3:** script. **Passo 4:** `python3 -m unittest tools/art/test_genera.py`, `python3 tools/art/genera.py --controlla --pixel` (dopo la rigenerazione), test del sito (`tools/sito`) verdi.
- [ ] **Passo 5: commit** `fix(arte): nastri coi due bordi uguali, le curve a destra combaciano`.

### Task 2: Logistica sopra logistica

**File:** `Factory.cs` (`BeltOverBeltAt` → classificazione per tutta la logistica 1×1, presa e consegna dei blocchi tolti con stato e oggetti, rimborso), `Construction/ConstructSite.cs` (`ReplacedBlocks` al posto di `ReplacedBelt`, con lo stato che serve: oggetti, filtro, turni), `Construction/Construction.cs`, `SimHost.cs` (`ApplyPlaceBelt` e `ApplyPlaceBuilding` per la logistica 1×1), `Save/FactorySave.cs` (**v23**: `ReplacedSaveDto` con l'elenco), `Agent/AgentGame.cs` (risposta di `piazza`), `CLAUDE.md` (riga dei salvataggi), test nuovi `LogisticsReplaceTests.cs`; aggiornare `BeltReplaceTests`, `BeltOverBeltTests`

- [ ] **Passo 1: test che falliscono**: nastro→router, router→nastro, nastro→selezionatore (oggetti al CORE), selezionatore→gate, incrocio→nastro, nastro→nastro a pacchi e ritorno; differenza di costo; annullamento che rimette il vecchio con oggetti e filtro; ponte mai sostituito; stesso id = niente; v23 avanti e indietro a metà cantiere; v22 rifiutato.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi (Shared, AgentPlay).
- [ ] **Passo 5: commit** `feat(costruzione): ogni blocco logistico si sostituisce piazzandoci sopra`.

### Task 3: Edifici della stessa famiglia

**File:** `Factory.cs` (classificazione: blocchi della famiglia tutti dentro l'impronta, resto libero; `CanPlace`, `RefusalReason`), `SimHost.cs` (`ApplyPlaceBuilding`, `QueuePlan`), `Construction` (cantiere con più blocchi tolti; corrente: `PowerBlocksChanged`, laser verso i tolti rimossi), contenuto o codice per le famiglie (una tabella in un posto solo), test nuovi `BuildingReplaceTests.cs`

- [ ] **Passo 1: test che falliscono**: T1→T2 stessa impronta (costo = differenza, scava lo stesso giacimento); T2→trapano con il T2 tutto dentro (sì) e con un T2 a cavallo del bordo (rifiutato); forno→fonderia avanzata (buffer conservato o al CORE); generatore→combustione (carbone conservato); batteria→batteria grande con tre batterie dentro (la carica delle tre si somma nella batteria nuova, fino alla sua capacità); famiglia diversa rifiutata; laser verso il blocco tolto rimosso; annullamento e rettangolo di rimozione rimettono tutto; salvataggio a metà.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi.
- [ ] **Passo 5: commit** `feat(costruzione): minatori, forni, generatori e batterie si potenziano piazzandoci sopra`.

### Task 4: Percorso dei nastri

**File:** nuovo `src/TIndustry.Shared/Grid/BeltPath.cs` (`BeltPath.Walk(IReadOnlyDictionary<GridPosition, BeltGridCell> belts, GridPosition start) -> IReadOnlyList<GridPosition>` dalla prima all'ultima cella), test nuovi `BeltPathTests.cs`

- [ ] **Passo 1: test che falliscono**: dritto, con curve, fermo a incrocio, router, selezionatore, gate, ponte; anello (termina, ogni cella una volta); due nastri che entrano nella stessa cella (si ferma lì all'indietro); cella iniziale che non è un nastro → vuoto.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi.
- [ ] **Passo 5: commit** `feat(logistica): percorso di un nastro dall'inizio alla fine`.

### Task 5: Client

**File:** `godot/scripts/world/World.Input.cs` (doppio clic su un nastro), pannellino nuovo `godot/scripts/ui/panels/BeltPathPanel.cs` (sul modello di `FilterPanel`: pulsanti dei nastri sbloccati con icona, nome e costo della differenza, numero di celle; `.uid` scritto a mano come gli altri del cloud), evidenziazione del percorso (`World.Drawing`), fantasma verde sopra i blocchi sostituibili (`World.Drawing`/`PlacementPreview`), `godot/lingue/testi.csv`, `UiTheme` se serve una variante

- [ ] **Passo 1:** client; build 0/0; `TestiTests`, `GodotProjectReferencesTests` verdi.
- [ ] **Passo 2: commit** `feat(client): doppio clic sul nastro e sostituzione del percorso`.

### Task 6: Documenti

**File:** `docs/wiki/Costruzione.md`, `Logistica.md`, `Comandi.md`, `Salvataggi-formato.md`, `godot/README.md`, `CHANGELOG.md`, `docs/STATO.md`, mappa e barre, diario `docs/diario/2026-11-11-sostituzioni.md`

- [ ] **Passo 1:** documenti; mappa e barre; test del sito verdi.
- [ ] **Passo 2: commit** `docs: sostituzioni, percorso dei nastri e nastri simmetrici`.
