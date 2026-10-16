# 0002 — Sincronizzazione multiplayer ibrida

**Stato:** accettata, 2026-10-15

## Contesto

Il multiplayer è cooperativo: più giocatori, ognuno col suo drone, sulla stessa fabbrica e lo stesso CORE. La partita è **ospitata da un giocatore**, come in Mindustry; un server dedicato headless è possibile in seguito con lo stesso codice (ADR 0001: server in C# che riusa `TIndustry.Shared`).

La simulazione deve quindi girare identica su più macchine, anche con FPS diversi, e un giocatore deve poter entrare a partita in corso. Il trasporto di rete arriva nel sotto-progetto 5; questo ADR fissa il modello a cui la simulazione si è già adeguata (sotto-progetto 2).

## Opzioni

1. **Solo istantanee.** L'host simula e invia lo stato ai client. La banda cresce con gli oggetti sui nastri e i client vedono lo stato in ritardo.
2. **Lockstep puro.** Tutti eseguono la stessa simulazione e si scambiano solo i comandi. Banda minima, ma un solo errore di determinismo (un `float` che differisce tra CPU, un bug) fa divergere la partita senza rimedio.
3. **Ibrido.** Come il lockstep, più un controllo periodico e un recupero tramite istantanea.

## Decisione

Modello **ibrido**:

- l'host è **autoritativo e ordina i comandi**, assegnando a ciascuno il tick di applicazione;
- tutti i partecipanti eseguono la **stessa simulazione deterministica** (tick fisso a 60 al secondo, sistemi in ordine fisso);
- l'host calcola un **checksum ogni 60 tick** (FNV-1a a 64 bit sull'istantanea canonica) e i client lo confrontano col proprio;
- un client che diverge riceve un'**istantanea completa** e riparte da lì; la stessa istantanea serve a chi **entra a partita in corso**.

Stato di attuazione nel codice:

- Ci sono già: tick fisso (`SimulationClock`), comandi con tick di applicazione, `SimHost`, istantanea canonica e checksum (`Snapshot`), salvataggi che usano l'istantanea.
- Il checksum **periodico ogni 60 tick è la cadenza decisa ma non è ancora implementato**: oggi `SimHost.Checksum()` si chiama a richiesta. Il confronto periodico arriva con il sotto-progetto 5 (multiplayer).
- L'istantanea contiene solo lo **stato applicato**: niente coda dei comandi, niente resto dell'orologio. È valida ai confini di tick. Chi entra a partita in corso dovrà ricevere anche i comandi già in coda per tick futuri (questione del sotto-progetto 5).

## Conseguenze

- Valgono le **regole di determinismo** (elencate in `docs/ARCHITETTURA.md`): niente tempo in `Step()`, niente dipendenza dall'ordine di `Dictionary`/`HashSet`, niente orologio né `Random` non seminato, niente parallelismo né formati numerici dipendenti dalla cultura, niente riferimenti a Godot o al sistema operativo in `TIndustry.Shared`. I test (`DeterminismTests`, `SystemOrderTests`, `SnapshotTests`) le fanno rispettare.
- I **comandi sono validati all'applicazione**, non all'invio: in co-op un altro giocatore può aver speso le risorse nel frattempo. L'esito torna come evento (`CommandApplied` / `CommandRejected` con il motivo in italiano). Che il client non muti il gioco se non con i comandi vale oggi per convenzione: `IGameView` espone ancora entità concrete (`BeltGridCell.SetFilterItem`, `Miner`, `Crafter`, `MarketCatalog`), e il confine di sola lettura a livello di tipo arriva col sotto-progetto 5.
- I calcoli restano in **`float`**: determinismo garantito da passo costante, ordine canonico e assenza di fonti esterne. Le differenze residue tra architetture CPU diverse sono accettate; si rilevano col checksum e si riparano con l'istantanea. Niente virgola fissa.
- Salvataggio e istantanea coincidono (versione 10: metadati più istantanea in base64): i salvataggi v9 sono rifiutati con "Salvataggio di una versione precedente: non compatibile.".
- Il single player gira su un "server locale" nello stesso processo (`SimHost`); la rete aggiungerà solo il modo in cui i comandi arrivano all'host.
- Costo: l'istantanea è JSON; su mappe molto grandi il checksum periodico potrebbe richiedere una serializzazione binaria (da misurare nel sotto-progetto 5).
