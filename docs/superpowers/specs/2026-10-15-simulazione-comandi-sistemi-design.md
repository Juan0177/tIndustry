# Simulazione a comandi e sistemi — design

- **Data:** 2026-10-15
- **Stato:** proposta, da approvare
- **Sotto-progetto:** 2 di 5 (roadmap in [2026-10-15-riordino-progetto-design](2026-10-15-riordino-progetto-design.md#roadmap-dei-sotto-progetti))
- **Piano:** [2026-10-16-simulazione-comandi-sistemi](../plans/2026-10-16-simulazione-comandi-sistemi.md)
- **Diario:** [2026-10-16-simulazione-comandi-sistemi](../../diario/2026-10-16-simulazione-comandi-sistemi.md)

## Perché

Il multiplayer è un obiettivo: **cooperativo** (più giocatori, ognuno col suo drone, sulla stessa fabbrica e lo stesso CORE), **ospitato da un giocatore** come in Mindustry, con server dedicato headless possibile in seguito con lo stesso codice ([ADR 0001](../../decisioni/0001-server-csharp.md): server in C# che riusa `TIndustry.Shared`).

Oggi la simulazione non è pronta:

- `Factory.Tick(deltaSeconds)` gira col tempo variabile del frame → due macchine con FPS diversi calcolano stati diversi.
- Il client modifica la simulazione direttamente in ~25 punti (`TryPlace…`, `TryRemove…`, `TryUnlockStructure`, `TrySellFromWallet`, `AutoSellAtCore =`, `TryUpgradeCore`, `Wallet.AddMaterial`).
- `BeltGrid` itera un `Dictionary<GridPosition, …>`: l'ordine di avanzamento degli oggetti dipende dalla storia degli inserimenti, quindi una partita ricaricata (o un giocatore che entra da un'istantanea) può divergere.
- Gli edifici non hanno un id stabile; tutta la logica è in `Factory.Tick`.

Questo sotto-progetto **non aggiunge la rete**: rende la simulazione deterministica, guidata da comandi e serializzabile, con il single player che gira su un "server locale" nello stesso processo.

## Decisioni prese

- **Modello di sincronizzazione (per il sotto-progetto 5): ibrido.** L'host è autoritativo e ordina i comandi (con tick di applicazione); tutti i partecipanti eseguono la stessa simulazione deterministica; l'host invia un checksum periodico; un client che diverge riceve un'istantanea completa e riparte. Le istantanee servono anche per entrare a partita in corso. Scartati: solo istantanee (banda proporzionale agli oggetti sui nastri, client in ritardo) e lockstep puro (un desync rompe la partita). Da registrare come ADR 0002.
- **Tick fisso: 60 al secondo** (`Simulation.TickRate = 60`, `TickSeconds = 1f / 60f`). La grafica mostra l'ultimo stato, senza interpolazione.
- **I calcoli restano in `float`.** Determinismo garantito da passo costante, ordine canonico e niente fonti esterne; differenze residue tra architetture CPU diverse sono rilevate dal checksum e riparate dall'istantanea. Niente virgola fissa.
- **Comandi validati all'applicazione**, non all'invio: in co-op un altro giocatore può aver speso le risorse nel frattempo. L'esito torna come evento.
- **Drone e costruzione in corso fuori portata** (sotto-progetti 3 e 4), ma i comandi sono pensati perché `PlaceBuilding` possa diventare un piano di costruzione.
- **Comportamento di gioco invariato per il giocatore.** Dove il passaggio a tick fisso sposta tempistiche, la differenza è documentata nei test.

## Architettura

Tutto in `src/TIndustry.Shared/`, senza dipendenze da Godot.

```
                 ┌──────────────── TIndustry.Shared ─────────────────┐
World (Godot) ──►│ SimHost (server locale)                            │
  Submit(cmd)    │   coda comandi ──► Simulation.Step() × N tick       │
  legge State ◄──│   eventi esito      GameState + sistemi (ordine fisso)│
                 │   Snapshot / Checksum                              │
                 └────────────────────────────────────────────────────┘
```

### `GameState`
Tutto lo stato mutabile oggi in `Factory`: griglia e nastri, edifici, portafoglio, ricerca, potenza, sessione economica, livello di campagna attivo, auto-vendita, upgrade del CORE, terreno, contatori di id, **numero di tick corrente**. Ogni entità (nastro, edificio, oggetto in transito) ha un **id progressivo** (`long`) assegnato alla creazione e salvato.

### Sistemi
`Simulation.Step()` avanza esattamente un tick ed esegue, in quest'ordine (lo stesso implicito oggi in `Factory.Tick`):

1. `BeltSystem` (avanzamento)
2. `PowerSystem` (generatori, capacità, ricarica)
3. `MiningSystem`
4. `CraftingSystem` (forni e assemblatori)
5. `ExtractionSystem`
6. `BeltSystem` (secondo passaggio, passo zero)
7. `CoreDeliverySystem`

Ogni sistema itera le entità in **ordine di id crescente**.

### Comandi
Record immutabili con `PlayerId`, `Tick` di applicazione e dati:

| Comando | Sostituisce |
|---|---|
| `PlaceBelt(pos, dir, conveyorId)` | `TryPlaceBelt` |
| `PlaceBuilding(kind, origin, dir, recipeId?, filterItemId?)` | `TryPlaceJunction/Splitter/Sorter/Bridge/Miner/Smelter/Assembler/Generator/Extractor/PowerNode` |
| `Remove(tile)` | `TryRemoveBelt`, `TryRemoveBuildingAt` |
| `UnlockStructure(structureId)` | `TryUnlockStructure` |
| `Sell(itemId, amount)` | `TrySellFromWallet` |
| `SetAutoSell(enabled)` | `AutoSellAtCore =` |
| `UpgradeCore()` | `TryUpgradeCore` |

Le risorse iniziali (oggi `Wallet.AddMaterial` all'avvio campagna) fanno parte della creazione dello stato, non sono un comando.

Esito: evento `CommandApplied(cmd)` oppure `CommandRejected(cmd, motivo)` con il testo italiano che oggi finisce nei toast.

### `SimHost`
- `Submit(cmd)`: mette in coda con tick = prossimo tick.
- `Advance(frameSeconds)`: accumulatore → esegue i tick dovuti, al massimo `MaxTicksPerFrame = 5`; all'inizio di ogni tick applica i comandi di quel tick in ordine di arrivo.
- `State`: vista in sola lettura (`IReadOnly…`) — il client non può mutare a compilazione.
- `Checksum()` e `CreateSnapshot()` / `FromSnapshot(…)`.

Nel sotto-progetto 5 lo stesso host riceverà i comandi dalla rete.

### Istantanea e checksum
- Istantanea = serializzazione completa e **canonica** di `GameState` (collezioni in ordine di id, numeri in formato invariante).
- Checksum = FNV-1a a 64 bit sui byte dell'istantanea; l'host lo calcolerà ogni 60 tick (uso effettivo nel sotto-progetto 5).
- Salvataggio = istantanea + metadati (nome slot, data — fuori dallo stato). **Versione 10**; i salvataggi v9 danno il messaggio esistente "Salvataggio di una versione precedente: non compatibile.". La logica dell'autosave su "Continua" (#145) non cambia.

### Client (Godot)
- `World` possiede un `SimHost` invece di `Factory`; `_Process` chiama `host.Advance(delta)`, poi grafica e HUD leggono `host.State`.
- Piazzamento, rimozione, trascinamento nastri, Mercato, Ricerca, upgrade del CORE inviano comandi; i toast nascono dagli eventi di esito.
- Nuove partite (sandbox, livelli di campagna) sono costruttori di `GameState`. I nomi storici (`CreateSandboxSlice`, `CreateCampaignSlice`, `CreateSpikeDemo`, `BuildSpikeLPath`, `UnlockGodotSliceDemo`, `World._slice`, `SaveSlice`/`LoadSlice`) vengono sostituiti.

## Regole di determinismo

Da riportare in `docs/ARCHITETTURA.md` e far rispettare dai test:

1. `Step()` non riceve tempo; ogni durata usa `TickSeconds` o conteggi di tick.
2. Mai dipendere dall'ordine di `Dictionary`/`HashSet`: iterazione per id crescente (o ordinamento esplicito).
3. Niente orologio di sistema, niente `Random` non seminato (un eventuale generatore vive in `GameState` con seme salvato).
4. Niente parallelismo nella simulazione; niente conversioni testo↔numero dipendenti dalla cultura.
5. Nessun riferimento a Godot o al sistema operativo in `TIndustry.Shared` (già regola ADR 0001).

## Test

Scritti prima del codice:

- **Replay:** stessa sequenza di comandi (a tick fissati) + 10.000 tick, eseguita due volte → stesso checksum.
- **Istantanea a metà partita:** dopo X tick creo un'istantanea e la ricarico; avanzo di Y tick originale e copia → checksum uguali (copre il problema dell'ordine dei `Dictionary`).
- **Indipendenza dagli FPS:** `SimHost` con tempi di frame casuali (seme fisso) vs costanti → stesso stato a parità di tick.
- **Tetto di tick per frame:** un frame lunghissimo esegue al massimo `MaxTicksPerFrame` tick.
- **Comandi rifiutati:** risorse insufficienti, spazio occupato, blocco non sbloccato, vendita oltre lo stock → evento con motivo, stato invariato (checksum uguale a prima).
- **Parità di gioco:** i 22 scenari esistenti riscritti con comandi e tick restano verdi; soglie temporali adattate solo dove il tick fisso le sposta, con motivazione nel test.
- **Salvataggio v10:** nuova partita → salva → ricarica → checksum identico; salvataggio v9 → messaggio di incompatibilità.
- **Playtest** `ui` e `tutorial` verdi e screenshot uguali alla baseline (con le maschere esistenti).

## Sequenza di PR

1. **Fondamenta del determinismo** — `Step()` a passo fisso con accumulatore, id stabili delle entità, iterazione canonica in `BeltGrid` e liste di edifici; test FPS e istantanea a metà partita usando `Capture`/`Restore` dei salvataggi già esistenti (che devono quindi conservare id e ordine). Il client usa ancora `Factory` tramite l'accumulatore.
2. **`GameState` e sistemi** — `Factory.Tick` diviso nei sette sistemi; parità con i 22 scenari.
3. **Comandi, `SimHost`, eventi** — il client passa a "invia comandi / legge stato"; playtest e confronto screenshot.
4. **Istantanea canonica, checksum, salvataggi v10** — test di replay; costruttori di `GameState` e rimozione dei nomi storici.
5. **Documentazione** — `ARCHITETTURA.md` (regole di determinismo, flusso comandi) e [ADR 0002](../../decisioni/0002-sincronizzazione-ibrida.md) (modello di sincronizzazione ibrido).

Ogni PR: build 0 errori/0 avvisi, test verdi, playtest e confronto screenshot, dati reali del giocatore intatti, CI manuale verde.

## Rischi

- **Tempistiche che cambiano col tick fisso** (es. un forno che finiva a 2,0 s ora finisce al tick 120): adattare le soglie dei test, verificare a vista nei playtest che il ritmo di gioco sia lo stesso.
- **Prestazioni dell'istantanea/checksum** su mappe 1000×1000: il checksum non serve ogni tick; misurare il costo nel PR 4 e, se serve, calcolarlo su una serializzazione binaria invece che JSON.
- **Differenze `float` tra architetture**: accettate per scelta; il recupero è l'istantanea (sotto-progetto 5).
- **Piazzamento trascinato dei nastri** che genera molti comandi in un frame: tutti applicati al tick successivo, in ordine; nessun limite artificiale.

## Fuori portata

Rete e trasporto, connessione di più giocatori, drone del giocatore, costruzione in corso, interpolazione grafica tra tick, server dedicato headless.
