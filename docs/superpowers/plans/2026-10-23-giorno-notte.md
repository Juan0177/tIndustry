# Giorno e notte — piano di implementazione

> **Per gli agenti:** SOTTO-SKILL RICHIESTA: superpowers:subagent-driven-development (consigliata) o superpowers:executing-plans, task per task. I passi usano le caselle (`- [ ]`).

**Obiettivo:** ciclo di giorno e notte scelto alla creazione del mondo (ciclo, solo giorno «Pacifica», solo notte col contatore del primo attacco), deterministico, salvato, mostrato nell'HUD e nella luce del mondo.

**Architettura:** la simulazione tiene solo due dati, la regola `DayNight` e il tick del primo blocco piazzato; fase, luce e tempo al prossimo cambio sono funzioni pure di questi e del tick (`DayNightClock`). Un sistema emette `FirstAttackDue` una volta in solo notte. Il client legge fase e luce dalla vista e non decide niente da sé.

**Tecnologie:** C# .NET 8 (`src/TIndustry.Shared`, xUnit), `data/campaign.json`, client Godot 4.4 .NET.

**Specifica:** [`docs/superpowers/specs/2026-10-23-giorno-notte-design.md`](../specs/2026-10-23-giorno-notte-design.md)

## Vincoli globali

- Ciclo: periodo **50.400 tick** (14 min a 60 tick/s): Giorno 0–34.800 (9:40), Tramonto 34.800–36.000 (fino a 10:00), Notte 36.000–49.200 (fino a 13:40), Alba 49.200–50.400.
- Luce intera 0–1000: 1000 di giorno, 0 di notte, lineare nelle due sfumature da 1.200 tick.
- Prima del primo blocco: Giorno, luce 1000, nessun conto. Primo attacco in solo notte a **36.000 tick** (10 min) dal primo blocco, evento una volta sola.
- `GameRules.DayNight`: `Cycle` (default), `AlwaysDay`, `AlwaysNight`; via `GameRules.Night`.
- Campagna: `dayNight` per livello fra `"ciclo"`, `"giorno"`, `"notte"`; livelli 1–3 `giorno`, 6 e 10 `notte`, gli altri `ciclo`; via `night`.
- Salvataggi **v16** (`CurrentVersion = MinReadableVersion = 16`).
- Regole di sempre (CLAUDE.md): determinismo, interi, testi da `testi.csv` (it+en), stili dal tema, commenti in inglese, commit in italiano con `Co-Authored-By:`, `docs/STATO.md` + mappa + barre in ogni modifica.

## Punti da sorvegliare in revisione

1. **Primo blocco annullato**: un cantiere aperto e poi annullato conta già come primo blocco (il tempo è partito); un comando rifiutato no — test in Task 1.
2. **Pausa e velocità dev**: il contatore è in tick, quindi si ferma in pausa e corre a ×20 senza saltare l'evento — test in Task 1 (evento emesso anche se il tick 36.000 cade dentro un frame da 100 tick).
3. **Salvataggio a metà tramonto**: ricaricato, la luce è la stessa al tick — test in Task 2.
4. **Partita degli agenti e playtest**: i playtest con gli effetti spenti non devono mostrare la notte (oggi `PlaytestForcesEffectsOff`) — Task 3 mantiene la regola.
5. **Campagna senza `dayNight`**: un livello che non lo dichiara è un errore di caricamento chiaro, non un default muto — test in Task 2.

---

### Task 1: Tempo del mondo nella simulazione

**File:**
- Crea: `src/TIndustry.Shared/Simulation/DayNightClock.cs`, `src/TIndustry.Shared/Simulation/Systems/DayNightSystem.cs`
- Modifica: `Construction/GameRules.cs`, `Simulation/GameState.cs`, `Factory.cs` (primo blocco), `Simulation/SimEvents.cs`, `Simulation/Simulation.cs` (sistema in coda), `Simulation/IGameView.cs`, `Simulation/NewGame.cs`, `Agent/AgentGame.cs` (stato), `Agent/AgentJournal.cs` (regole del diario)
- Test: crea `tests/TIndustry.Shared.Tests/DayNightTests.cs`; aggiorna `SystemOrderTests` e i test che usano `GameRules(Night: …)`

**Interfacce:**
- `public enum DayNightMode { Cycle, AlwaysDay, AlwaysNight }`; `GameRules(int RefundPercent = 100, bool InstantBuild = false, DayNightMode DayNight = DayNightMode.Cycle)`.
- `public enum DayNightPhase { Day, Dusk, Night, Dawn }`.
- `public static class DayNightClock`: costanti dei vincoli; `DayNightPhase PhaseAt(DayNightMode mode, long? firstBuildTick, long tick)`; `int LightAt(...)`; `long? TicksToNextChange(...)` (Cycle: alla prossima Notte se Giorno/Tramonto, al prossimo Giorno se Notte/Alba; AlwaysNight: al primo attacco finché non è passato, poi null; AlwaysDay e prima del primo blocco: null); `bool IsNight(...)` (= fase Notte).
- `GameState.FirstBuildTick : long?` (impostato una volta dal primo piazzamento accettato: cantiere aperto o piazzamento istantaneo, nastri compresi); `GameState.FirstAttackRaised : bool`.
- `sealed record FirstAttackDue(int PlayerId) : SimEvent(PlayerId)` (PlayerId 0).
- `IGameView`: `DayNightMode DayNight`, `DayNightPhase DayNightPhase`, `int Light`, `long? TicksToNextChange`, `long? FirstBuildTick`.
- `NewGame.Sandbox/Campaign`: la regola arriva da `rules.DayNight` (la campagna dal livello nel Task 2; qui il livello resta com'è con `Night` → `AlwaysNight`).

- [ ] **Passo 1: test che falliscono** in `DayNightTests`: fasi e luce ai tick 0, 34.799, 34.800, 35.400 (luce 500), 36.000, 49.199, 49.200, 49.800 (500), 50.400 (Giorno del secondo giro), 86.400 (Notte del secondo giro); prima del primo blocco Giorno/1000/null; AlwaysDay sempre Giorno/1000/null; AlwaysNight sempre Notte/0 e `TicksToNextChange` = 36.000 − t, poi null; `FirstBuildTick` col primo nastro (SimHost, InstantBuild), col primo edificio senza InstantBuild (cantiere aperto), non con un comando rifiutato, invariato dal secondo piazzamento; `FirstAttackDue` una sola volta in AlwaysNight anche avanzando a ×20 (`Speed = 20`) con il tick 36.000 dentro un frame, mai in Cycle o AlwaysDay.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione (tutto intero; `DayNightSystem` alla fine dell'ordine dei sistemi). **Passo 4:** build 0/0, Shared + AgentPlay verdi.
- [ ] **Passo 5: commit** `feat(tempo): giorno e notte nella simulazione`.

### Task 2: Campagna e salvataggi v16

**File:**
- Modifica: `data/campaign.json` (`dayNight` per livello, via `night`), `Campaign/CampaignCatalog.cs` (`DayNightMode DayNight`, errore se manca o non valido), `Simulation/NewGame.cs` (regola del livello), `Save/FactorySave.cs` (v16, `Rules.DayNight` come stringa, `FirstBuildTick`, `FirstAttackRaised`; via `Night`), `Factory.cs` (snapshot/restore), `CLAUDE.md` (riga dei salvataggi: «versione 16»)
- Test: `CampaignMapTests` o nuovo test del catalogo, `SaveRoundTripTests`, `SaveVersionTests`

- [ ] **Passo 1: test che falliscono**: ogni livello ha `dayNight` valido; livelli 1–3 `AlwaysDay`, 6 e 10 `AlwaysNight`, gli altri `Cycle`; un catalogo senza `dayNight` dà un errore che nomina il livello; salvataggio a metà tramonto (tick primo blocco + 35.400) ricaricato con la stessa luce e lo stesso checksum; `FirstAttackRaised` salvato (nessun secondo evento dopo il caricamento); v15 rifiutato con `OlderVersion`.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi, `dotnet run --project tools/MapTool -- valida` OK.
- [ ] **Passo 5: commit** `feat(tempo): giorno e notte nella campagna e nei salvataggi v16`.

### Task 3: Client — creazione del mondo, HUD e luce

**File:**
- Modifica: `godot/scripts/ui/panels/NewGamePanel.cs` (al posto della spunta «Notte», due spunte che si escludono «Solo giorno (Pacifica)» e «Solo notte», più una riga di spiegazione; `StartRequested` passa un `DayNightMode`), il chiamante in `godot/scripts/world/` che crea la partita, `godot/scripts/ui/hud/CorePanel.cs` (riga del tempo), `godot/scripts/visuals/effects/NightLayer.cs` e `godot/scripts/world/World.cs` (oscurità e luci dalla `Light` della vista, sfumate; regola dei playtest senza effetti mantenuta), `godot/lingue/testi.csv`
- Test: `TestiTests`; playtest solo sul PC di casa

- [ ] **Passo 1:** testi (it+en): «Solo giorno (Pacifica)», «Solo notte», spiegazione («Ciclo: giorno 10 min, notte 4 min · Solo notte: primo attacco dopo 10 min»), «Il tempo parte dal primo blocco», «Notte fra {0}», «Giorno fra {0}», «Primo attacco fra {0}», «Attacco!», «Notte».
- [ ] **Passo 2:** pannello, HUD (formato m:ss da tick; «Attacco!» per 5 s dopo `FirstAttackDue`), luce: l'oscurità di `NightLayer` diventa proporzionale a `1000 − Light`, lampade ed effetti notturni accesi quando `Light < 500`.
- [ ] **Passo 3:** build 0/0, `TestiTests` verdi. Sul PC di casa: playtest `ui`, `tutorial`, `agenti`.
- [ ] **Passo 4: commit** `feat(client): giorno e notte nella creazione del mondo, nell'HUD e nella luce`.

### Task 4: Documenti, stato e diario

**File:** `godot/README.md`, `docs/wiki/Campagna-e-mappe.md`, `docs/wiki/Impostazioni-e-accessibilita.md` o la pagina della nuova partita, `docs/wiki/Salvataggi-formato.md`, `docs/ARCHITETTURA.md` (tempo del mondo), `CHANGELOG.md`, `docs/STATO.md` (aspetto 13: casella del giorno e notte), mappa, barre, diario `docs/diario/2026-10-23-giorno-notte.md`, stato della specifica «attuata».

- [ ] **Passo 1:** testi coi numeri dei vincoli globali.
- [ ] **Passo 2:** `python tools/sito/stato.py`, `python tools/dev/mappa_canvas.py`, `python tools/dev/controlla_wiki.py`, test del sito.
- [ ] **Passo 3: commit** `docs: giorno e notte`.
