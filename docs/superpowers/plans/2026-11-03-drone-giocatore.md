# Il drone sei tu — piano di implementazione

> **Per gli agenti:** SOTTO-SKILL RICHIESTA: superpowers:subagent-driven-development (consigliata) o superpowers:executing-plans, task per task. I passi usano le caselle (`- [ ]`).

**Obiettivo:** il giocatore guida il suo drone con WASD, la visuale lo segue, il drone costruisce solo nel raggio e scava a mano; mouse e tastiera senza strumenti, come Mindustry su PC.

**Architettura:** il drone (`BuilderUnit`) riceve una direzione d'ingresso con un comando e si muove da sé a ogni tick; `BuilderSystem` non insegue più i cantieri e `ConstructSystem` lavora solo sui piani nel raggio. Lo scavo a mano è uno stato del drone (cella, stiva, accumulatore) con un sistema suo che scava e scarica nel CORE. Il client traduce WASD, clic e tasti nei comandi e non decide niente della simulazione.

**Tecnologie:** C# .NET 8 (`src/TIndustry.Shared`, xUnit), `data/content.json`, client Godot 4.4 .NET.

**Specifica:** [`docs/superpowers/specs/2026-11-03-drone-giocatore-design.md`](../specs/2026-11-03-drone-giocatore-design.md)

## Vincoli globali

- Velocità: alpha 22,5, beta 24,75, gamma 26,6 celle/s (da `units`); diagonale normalizzata; il drone resta nella mappa e vola sopra tutto.
- Raggio di costruzione 27,5 celle (distanza dal centro del drone al centro del cantiere).
- Scavo: alpha e beta ferro, rame, piombo, carbone; gamma anche titanio. Ritmo 0,7 / 0,9 / 1,1 pezzi/s; stiva 30 / 50 / 70; un oggetto alla volta; accumulatore intero in millesimi di pezzo per tick (`ritmo × 1000 / 60` arrotondato una volta per livello e documentato, oppure accumulo in sessantesimi come la vendita automatica). Scarico entro **6 celle** dal CORE, fino al massimo per oggetto.
- Il giacimento non si consuma; lo scavo richiede la cella nel raggio di costruzione.
- Comandi nuovi: `SetDroneInput(PlayerId, int Dx, int Dy)` (−1, 0, 1), `SetMining(PlayerId, GridPosition? Tile)`.
- Salvataggi **v19** (dopo la v18 della logistica).
- Comandi del mouse e della tastiera: tabella della specifica, sezione «Mouse e tastiera».
- Regole di sempre (CLAUDE.md): determinismo, interi, testi da `testi.csv` it+en, stili dal tema, commenti in inglese, commit in italiano con `Co-Authored-By:`, STATO + mappa + barre.

## Punti da sorvegliare in revisione

1. **Piano fuori raggio in testa alla coda**: non blocca i piani successivi nel raggio — test in Task 1.
2. **Drone fermo dentro un cantiere grande** (il CORE che cresce, un vault): niente incastri, la costruzione procede — test in Task 1.
3. **Stiva piena con il CORE pieno di quell'oggetto**: lo scavo si ferma, niente si perde, riprende quando il CORE si svuota — test in Task 2.
4. **Pausa e velocità dev**: movimento e scavo si fermano in pausa e corrono a ×20 senza saltare scarichi — test in Task 2.
5. **Playtest e agenti** che oggi contano sul drone che vola da solo: vanno guidati con i comandi, altrimenti si bloccano — Task 3 e Task 4.

---

### Task 1: Movimento e costruzione nel raggio

**File:** `Construction/BuilderUnit.cs` (ingresso), `Simulation/Systems/BuilderSystem.cs` (movimento dall'ingresso, via inseguimento e punto di riposo), `Simulation/Systems/ConstructSystem.cs` (primo piano nel raggio), `Simulation/Commands.cs`, `SimHost.cs`, `Simulation/IGameView.cs`; test nuovi `DroneMovementTests.cs`, aggiornare `BuilderTests`, `ConstructionTests` e gli altri che contano sul volo automatico (spostando il drone con il comando o mettendolo vicino)
- [ ] **Passo 1: test che falliscono**: velocità per livello in un secondo, diagonale normalizzata, bordi della mappa, fermo senza ingresso, ingresso tenuto fra i tick; piano fuori raggio che aspetta e parte quando ci si avvicina; piano fuori raggio in testa che non blocca quelli dietro nel raggio; smontaggio nel raggio; drone sopra un cantiere grande.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** build 0/0, test verdi (anche AgentPlay: adattare il server MCP perché i suoi scenari muovano il drone).
- [ ] **Passo 5: commit** `feat(drone): il drone si muove con l'ingresso e costruisce solo nel raggio`.

### Task 2: Scavo a mano e scarico nel CORE

**File:** `data/content.json` (`units`: oggetti scavabili, ritmo, stiva), `Content/ContentTypes.cs`, `Content/FactoryContent.cs`, `BuilderUnit.cs` (stiva, cella, accumulatore), sistema nuovo `Simulation/Systems/HandMiningSystem.cs` (registrato in `Simulation.cs`, `SystemOrderTests`), `Commands.cs` (`SetMining`, rifiuti), `SimHost.cs`, `IGameView.cs`; test nuovi `HandMiningTests.cs`
- [ ] **Passo 1: test che falliscono**: ritmo per livello su 10 s, titanio solo col gamma, stiva piena che ferma, un oggetto alla volta, scarico entro 6 celle (non a 7), massimo del CORE rispettato e resto nella stiva, CORE pieno → scavo fermo, giacimento non consumato, rifiuti (fuori raggio, non scavabile, cella senza giacimento), pausa (Advance(0)) e ×20.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi.
- [ ] **Passo 5: commit** `feat(drone): scavo a mano e scarico nel CORE`.

### Task 3: Salvataggi v19 e agenti

**File:** `Save/FactorySave.cs` (v19: ingresso, stiva, scavo, accumulatore), `Factory.cs` (snapshot/restore), `Agent/AgentGame.cs` (comandi `muovi` — dx/dy oppure una cella da raggiungere, risolta in più `SetDroneInput` dal server — e `scava`; stato del drone in `stato`), `Agent/AgentJournal.cs` (versione nuova), `tools/AgentPlay/` (strumenti e README), `CLAUDE.md` (riga dei salvataggi: «versione 19»); test `SaveRoundTripTests`, `SaveVersionTests`, `AgentGameTests`, `DeterminismTests`
- [ ] **Passo 1: test che falliscono**: v19 avanti e indietro a metà scavo e in movimento (stesso checksum); v18 rifiutato; `muovi` verso una cella lontana che arriva e si ferma; `scava` e i rifiuti; determinismo 5 minuti con movimento, costruzione e scavo.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi.
- [ ] **Passo 5: commit** `feat(drone): salvataggi v19 e comandi degli agenti`.

### Task 4: Client — input senza strumenti e visuale sul drone

**File:** `godot/scripts/world/World.cs`, `World.Input.cs`, `World.Placement.cs`, `WorldCamera.cs`, palette (`BuildPalette.cs`), playtest in `godot/scripts/dev/` (guidare il drone con i comandi), `godot/lingue/testi.csv`
- [ ] **Passo 1:** WASD → `SetDroneInput` (inviato solo quando cambia); visuale che segue il drone con un inseguimento morbido; tasto centrale tenuto trascina la visuale e al rilascio torna sul drone; rotella zoom; via `BuildTool.Cursor` e i pulsanti degli strumenti: nessun blocco scelto = clic seleziona/configura (pannello del filtro, laser dei nodi, informazioni), clic tenuto su un giacimento = `SetMining`; blocco scelto = piazza come oggi; clic destro o Esc deseleziona; clic destro trascinato smonta; clic destro su un piano lo annulla; Q sceglie il blocco sotto il cursore (al posto del tasto centrale); Spazio pausa e M mappa restano.
- [ ] **Passo 2:** build 0/0, `TestiTests` verdi; i playtest compilano e guidano il drone.
- [ ] **Passo 3 (PC di casa):** playtest `ui`, `tutorial`, `agenti`; a mano una partita dall'inizio.
- [ ] **Passo 4: commit** `feat(client): il drone si guida con WASD, comandi come Mindustry`.

### Task 5: Client — piani fuori raggio, scavo e stiva

**File:** `World.Drawing.cs`, `World.Visuals.cs`, `ConstructionLayer.cs`, HUD (`CorePanel.cs` o un riquadro nuovo), `godot/lingue/testi.csv`
- [ ] **Passo 1:** piani fuori raggio semitrasparenti con contorno «fuori raggio»; raggio del drone visibile mentre si piazza; linea di scavo dal drone al giacimento (come Mindustry); stiva nell'HUD (icona, quantità/massimo); testi it/en.
- [ ] **Passo 2:** build 0/0, `TestiTests` verdi. **Passo 3 (PC di casa):** catture.
- [ ] **Passo 4: commit** `feat(client): piani fuori raggio, scavo a mano e stiva del drone`.

### Task 6: Documenti, stato e diario

**File:** `docs/wiki/Comandi.md` (riscritta), `docs/wiki/Primi-passi.md`, `docs/wiki/Costruzione.md`, `godot/README.md`, `README.md`, `docs/ARCHITETTURA.md`, `docs/wiki/Salvataggi-formato.md`, `.claude/skills/gioca-e-testa/SKILL.md` (il drone non vola da solo), `CHANGELOG.md`, `docs/STATO.md` (aspetto 11: piano, implementazione, documenti), mappa, barre, diario `docs/diario/2026-11-03-drone-giocatore.md`, specifica «attuata».
- [ ] **Passo 1:** testi coi numeri dei vincoli.
- [ ] **Passo 2:** `python tools/sito/stato.py`, `python tools/dev/mappa_canvas.py`, `python tools/dev/controlla_wiki.py`, test del sito.
- [ ] **Passo 3: commit** `docs: il drone sei tu`.
