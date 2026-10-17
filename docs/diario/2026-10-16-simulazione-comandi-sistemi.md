# Sotto-progetto 2 — Simulazione a comandi e sistemi

- **Specifica**: [2026-10-15-simulazione-comandi-sistemi-design](../superpowers/specs/2026-10-15-simulazione-comandi-sistemi-design.md)
- **Piano**: [2026-10-16-simulazione-comandi-sistemi](../superpowers/plans/2026-10-16-simulazione-comandi-sistemi.md)
- **Periodo**: 2026-10-15 (specifica) → 2026-10-17 (rifiniture e log di sviluppo)
- **Esito**: in `main` con le PR [#147](https://github.com/Juan0177/tIndustry/pull/147) e [#154](https://github.com/Juan0177/tIndustry/pull/154), più le rifiniture [#155](https://github.com/Juan0177/tIndustry/pull/155) e [#156](https://github.com/Juan0177/tIndustry/pull/156).

> Ricostruito a posteriori: il registro originale (`.superpowers/sdd/…/progress.md`) era stato cancellato a fine piano. Le fonti sono l'elenco delle decisioni salvato prima della cancellazione, il report delle rifiniture, la storia git e le descrizioni delle PR. Dove le fonti non dicono qualcosa (ad esempio il numero esatto dei giri di revisione di ogni task) qui non è inventato.

## Obiettivo

Simulazione a tick fisso (60 Hz), guidata da comandi e deterministica: `GameState` con sette sistemi in ordine fisso, `SimHost` con comandi ed eventi di esito, viste di sola lettura `IGameView` per il client, istantanea canonica con checksum FNV-1a, salvataggi alla versione 10. Base per il co-op (vedi [ADR 0002](../decisioni/0002-sincronizzazione-ibrida.md)).

## Task, commit e PR

Le cinque PR erano pensate come una pila (ognuna con base la precedente, la prima su `main`).

| Task | Contenuto | Commit | PR |
|---|---|---|---|
| 0 | Baseline (nessun codice): catture di riferimento per il confronto screenshot | — | — |
| 1 | Tick fisso 60/s, ordine canonico dei nastri, id stabili, `Capture()` canonica | `3007894` (con spec `71d23b4` e piano `26963ba`) | [#147](https://github.com/Juan0177/tIndustry/pull/147) |
| 2 | `GameState` e sette sistemi in ordine fisso | `c5815cd` | [#148](https://github.com/Juan0177/tIndustry/pull/148) |
| 3 | Comandi, `SimHost`, eventi di esito, viste in sola lettura (Shared) | `733bcad`, `15efd92` | [#149](https://github.com/Juan0177/tIndustry/pull/149) |
| 4 | Client Godot su `SimHost` e `IGameView` | `f693e9e`, `af3aa0b` | [#149](https://github.com/Juan0177/tIndustry/pull/149) |
| 5 | Istantanea canonica e checksum FNV-1a | `0f6945b` | [#150](https://github.com/Juan0177/tIndustry/pull/150) |
| 6 | Salvataggi v10, costruttori `NewGame`, via i nomi Slice/Spike | `074fb60`, `9c82702` | [#150](https://github.com/Juan0177/tIndustry/pull/150) |
| 7 | Documentazione: ARCHITETTURA, ADR 0002, CLAUDE.md | `7a3d971`, `46e28f7` | [#151](https://github.com/Juan0177/tIndustry/pull/151) |
| Fix finali | Esito della revisione finale (vedi sotto) | `6e21a1b` | [#151](https://github.com/Juan0177/tIndustry/pull/151) |

### Incidente sulla pila di PR

Le PR #148–#151 sono state unite nei rispettivi branch della pila invece che in `main`, quindi in `main` non era arrivato nulla oltre la #147. La [#154](https://github.com/Juan0177/tIndustry/pull/154) (`feat/sim-2-sistemi`) ha portato in `main` il contenuto di tutte e quattro: stesso albero già verificato (build 0/0, 93 test superati e 1 ignorato, playtest ui/tutorial OK, confronto screenshot 0 differenze, CI manuale verde).

## Giri di revisione

Revisori: sonnet, tranne opus per T1 e T3; revisione finale con opus.

- **T3 — primo giro di correzioni** (`15efd92`): `SimHost` robusto ai gestori che lanciano, comando `SetSorterFilter`, `CampaignProgress.AreAllObjectivesComplete` sulle viste `IWalletView`/`IResearchView`, test positivi per `PlaceBuilding` (un tipo) e `Remove` (applicato e «Niente da rimuovere.»).
- **T5 — rinvio poi ripreso**: un revisore ha fatto notare che il rinvio «`PowerCapacity` limitata al ripristino» sottostimava il problema (rompeva la garanzia di istantanea senza perdite): corretto subito (vedi Decisioni).
- **T7 — correzione di 8 righe di documentazione**: riletta dal coordinatore riga per riga, senza un sotto-agente.
- **Revisione finale** (opus): ha trovato che **l'energia accumulata oltre 24 andava persa al ripristino**. Corretto con un test di ripristino oltre il tick 600 con generatore, insieme ad altre rifiniture, in un solo commit (`6e21a1b`, sul branch in cima alla pila): il seme di L01 passa da `NewGame.Campaign`, nel caricamento l'host viene collegato per ultimo, la documentazione dice che «sola lettura» vale per convenzione fino al sotto-progetto 5.

## Decisioni prese in autonomia

- **Decisione**: nomi dei branch `feat/sim-1-determinismo` (da `docs/sim-comandi-spec`, così specifica e piano viaggiano con la PR 1), `feat/sim-2-sistemi`, `feat/sim-3-comandi` (T3+T4), `feat/sim-4-istantanea` (T5+T6), `feat/sim-5-docs` (T7); PR impilate. — il piano dava i nomi solo in parte. — se sbagliata: si rinominano i branch, nessun cambio di codice.
- **Decisione**: il Task 0 (baseline, senza codice) lo esegue il coordinatore, non un sotto-agente. — è solo preparazione, senza diff da rivedere. — se sbagliata: nessuno.
- **Decisione**: modelli: T1 opus (nucleo del determinismo), T2 sonnet, T3 opus (disegno dell'API comandi/viste), T4–T7 sonnet; revisori sonnet tranne T1/T3 opus; revisione finale opus. — il giudizio si concentra in T1 e T3. — se sbagliata: escalation nei giri di correzione 4–5.
- **Decisione**: il checksum periodico ogni 60 tick **non** è implementato dall'host; c'è solo `Checksum()` a richiesta (T5) e l'ADR documenta la cadenza (T7). — la specifica rimanda l'uso effettivo al sotto-progetto 5. — se sbagliata: un timer in `SimHost`.
- **Decisione**: ricatturare `$TEMP/baseline-sp2/tutorial` (e `ui`) da T1 (`3007894`) come riferimento per T2–T6, senza maschera. — la differenza è il tetto specificato, non un difetto. — se sbagliata: una nuova ricattura.
- **Decisione**: la cifra dell'energia in `tutorial-01` (bbox 260,79,270,93) è rumore di tempo del playtest (la durata del primo frame varia; lo stesso accade anche su T1): T4 aggiunge in `tools/dev/compare_shots.py` una maschera per quel file, con commento, insieme a `SimulationClock.Reset()` a nuova partita/caricamento. — il gioco non cambia e il controllo screenshot deve essere stabile. — se sbagliata: una regressione vera su quella cifra passerebbe inosservata solo in `tutorial-01` (le catture `ui` coprono comunque l'HUD).
- **Decisione**: si tiene il testo di oggi «Cella occupata o non valida». — il vincolo «testi italiani di oggi» vale più della parafrasi «Spazio occupato.» della specifica. — se sbagliata: una costante.
- **Decisione**: aggiunto subito il comando `SetSorterFilter(PlayerId, GridPosition Tile, string ItemId)`. — la specifica aveva un buco: il client cambiava direttamente il filtro di un selezionatore piazzato, il che in co-op desincronizza. Il client calcola l'oggetto successivo dalla vista in sola lettura e l'host applica e valida. — necessario per il «client in sola lettura». — se sbagliata: un record in più.
- **Decisione**: il primo giro di correzioni include anche `CampaignProgress.AreAllObjectivesComplete` sulle viste e i test positivi di `PlaceBuilding` e `Remove`. — T4 ne dipende. — se sbagliata: diff un po' più grande.
- **Decisione**: `SnapshotIsCanonical` varia l'ordine di inserimento di nastri, materiali del portafoglio e altre collezioni con chiave, ma piazza gli edifici nello stesso ordine. — gli id degli edifici (T1, assegnati in ordine di piazzamento) sono parte dello stato per scelta: due ordini di costruzione diversi non sono «la stessa fabbrica». — se sbagliata: solo l'ampiezza del test.
- **Decisione**: eliminato il test sugli id 0 «legacy» (portato da T1). — la v10 rifiuta ogni salvataggio precedente, quindi nessun dato vecchio arriva a `Restore`; invece `Restore` ripristina esattamente (tolta la regola «StartingMoney == 0» e il correttivo in `Snapshot.Read`: un solo percorso di ripristino). L'assegnazione difensiva di id 0 può restare. — la specifica dice che la v9 non si carica. — se sbagliata: un test legacy.
- **Decisione**: rilettura mirata della correzione di T7 fatta dal coordinatore e non da un sotto-agente. — 8 righe di documentazione, entrambi i punti controllati riga per riga. — se sbagliata: un difetto minore di documentazione arriva alla revisione finale.
- **Decisione**: il rinvio di T5 «`PowerCapacity` limitata al ripristino» va corretto subito. — rompe la garanzia di istantanea senza perdite. — se sbagliata: nessuno, correzione di una riga.
- **Decisione**: la serie finale di correzioni è un solo commit sul branch in cima alla pila (si unisce comunque in ordine): Importante 1+2, Minore 1 (seme L01 → `NewGame.Campaign`), Minore 2 (host collegato per ultimo in `LoadGame`), Minore 3 (documentazione: sola lettura per convenzione fino al SP5), Minore 5 (test di ripristino oltre il tick 600 con generatore). — evita di rieseguire i giri sulle PR intermedie. — se sbagliata: la correzione sta nella PR 5 invece che nella 4.

## Problemi rinviati

Rinviati al **sotto-progetto 5** (co-op):

- confronto **periodico del checksum** (ogni 60 tick): oggi `SimHost.Checksum()` è solo a richiesta;
- **comandi in coda per chi entra a partita in corso**: l'istantanea contiene solo lo stato applicato, senza coda comandi né resto dell'orologio;
- **`IGameView` in sola lettura a livello di tipo**: oggi lo è per convenzione;
- **scrittura non atomica dei salvataggi** — poi risolta nella #155 (vedi sotto).

Rinviato senza data: **Minore 4**, doppia vendita al Mercato nello stesso tick, insieme agli altri casi di doppio input nello stesso tick.

## Rifiniture dopo il sotto-progetto: PR #155 e #156

[#155](https://github.com/Juan0177/tIndustry/pull/155) (`fix/lavoretti-sp2`, CI `build-publish.yml` manuale: successo):

1. **Salvataggio atomico** (`70b1041`): `AtomicFile` (file `.tmp` + rinomina), usato da `FactorySave` (`Save`, `DuplicateSlot`) e `CampaignProgress`; `ListSlots` ignora i non-`.json`. 4 test; solo `FailedWriteLeavesPreviousSlotIntact` falliva prima della correzione.
2. **Doppio input nello stesso tick** (`95d6788`, `34f47a9`): `SimHost.Pending` e `SimHost.LastCommandMoneyDelta`; filtro del selezionatore, vendita automatica e guadagno di ogni vendita partono dal valore effettivo. Poi `a493ad0`: `Pending` davvero in sola lettura.
3. **Rollback del caricamento fallito** (`781dba4`): `RefreshAfterAttach` condiviso fra successo e rollback; al primo caricamento si torna a «nessuna partita». Non testabile in automatico (codice Godot): verificato con build e playtest.
4. **Potenziamento del CORE**: i testi di rifiuto dell'host erano già corretti; solo 4 test aggiunti (già verdi da subito).
5. **`MajorityDeposit`** (`d8ec8d1`): a parità di tessere sceglie l'id di giacimento più basso (ordine ordinale); test. Possibile cambio di comportamento sulle parità rispetto a prima (prima valeva il primo incontrato).
6. **Ordine dei sistemi con dt** (`d8ec8d1`): `Simulation.Step` accetta un hook di traccia e un test verifica l'intera sequenza.
7. **`SimHost.AdvanceTicks(n)`** (`5d2032f`) e `SeedCoreDeliveryLine` (`e04e06d`): gestore tolto in `finally`, commento corretto. Il test di `AdvanceTicks` è stato scritto insieme all'implementazione, non prima.
8. **Documentazione** (`9e7dfcf`): README alla v10, ARCHITETTURA, CHANGELOG.

Decisioni della #155: guadagno per vendita con la proprietà `LastCommandMoneyDelta` (valida dentro `CommandApplied`) invece di un nuovo payload dell'evento, perché più semplice; il trailer `Co-Authored-By` riporta il modello reale (Sonnet 5.5) e non quello indicato nel brief.

[#156](https://github.com/Juan0177/tIndustry/pull/156) (`3568d68`): `playtest.sh` e `dev.sh` passano `--log-file <profilo>/godot.log`, così i log di Godot non finiscono più in `%APPDATA%\Godot\app_userdata\tIndustry\logs`. Limite noto: `.recovery_mode_lock` (file vuoto) ha ancora l'mtime aggiornato all'avvio, non esiste un'opzione per spostarlo. Il controllo della #155 aveva infatti trovato log del motore scritti nel profilo reale (nessun salvataggio toccato) e un `settings.json` con mtime precedente ai playtest, quindi non attribuibile a loro.

## Verifiche finali

- `dotnet build tIndustry.sln -c Release`: 0 errori, 0 avvisi.
- `dotnet test`: dopo la #154, 93 superati e 1 ignorato (misura manuale `MeasureLargeMap`); dopo la #155, 108 superati e 1 ignorato.
- Playtest `ui` e `tutorial`: OK; `compare_shots.py` contro la baseline: 0 differenze (5 + 9 immagini).
- Dati reali del giocatore: salvataggi invariati (vedi sopra per i log del motore, risolti con la #156).
- CI `build-publish.yml` lanciata a mano: verde su #154 e #155.
