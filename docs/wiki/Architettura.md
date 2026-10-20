# Architettura

Come sono fatti simulazione e client, come passa un frame e perché la simulazione deve restare deterministica.

## Due metà

- **Simulazione** (`src/TIndustry.Shared/`, .NET 8): tutto lo stato del gioco e le sue regole. Non dipende da Godot né dal sistema operativo ([ADR 0001](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0001-server-csharp.md)): è il codice che un giorno girerà anche su un server dedicato senza grafica.
- **Client** (`godot/`): disegna leggendo una vista in sola lettura della simulazione e trasforma l'input in comandi. Non modifica mai il gioco direttamente.

## Stato, sistemi e comandi

- **`GameState`** contiene tutto lo stato mutabile (griglia e nastri, edifici, portafoglio, ricerca, mercato, energia, droni e cantieri, contatori di id, tick corrente). `Factory` ne è la facciata.
- **Sistemi.** `Simulation.Step` avanza esattamente un tick eseguendo i sistemi di `Simulation/Systems/` in un ordine fisso (costruzione, nastri, energia, estrazione, produzione, luci, consegna al Core…). L'ordine è bloccato dal test `SystemOrderTests`.
- **Comandi.** Ogni modifica al gioco è un record `SimCommand` (`PlaceBelt`, `PlaceBuilding`, `Remove`, `UnlockStructure`, `Sell`, `SetAutoSell`, `UpgradeCore`, `SetSorterFilter`) con giocatore e tick. I comandi sono **validati quando si applicano**, non quando si inviano.
- **`SimHost`** è il «server locale»: `Submit` mette in coda un comando, `Advance` esegue i tick dovuti applicando i comandi all'inizio di ciascuno. L'esito arriva come evento: `CommandApplied` oppure `CommandRejected` con un codice (`RefusalCode`) che il client traduce; gli eventi nati durante il tick (piano scartato, costruzione finita…) arrivano con `EventRaised`.
- **`IGameView`** è la vista in sola lettura che il client usa per disegnare e per l'HUD.

La costruzione segue Mindustry: piazzare crea un piano, il drone lo raggiunge e un cantiere lo costruisce nel tempo prelevando dal Core ([ADR 0003](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0003-costruzione-come-mindustry.md)).

## Tick fisso a 60 Hz

`SimulationClock` trasforma il tempo del frame in tick da 1/60 s, al massimo 5 per frame (il tempo in eccesso si scarta, senza spirale di recupero). La simulazione non vede mai il tempo del frame. Il movimento fluido degli oggetti sui nastri è **solo grafico**: il client interpola fra due tick e quell'interpolazione non entra mai nello stato.

## Regole di determinismo

Stesso stato e stessi comandi devono dare lo stesso risultato su ogni macchina. Il codice che gira in `Step()`:

1. non riceve tempo: ogni durata è in tick o in `TickSeconds`;
2. non dipende dall'ordine di `Dictionary` o `HashSet`: itera per id crescente o con un ordinamento esplicito;
3. non usa l'orologio di sistema né `Random` senza seme;
4. non usa parallelismo né conversioni testo-numero che dipendono dalla cultura;
5. non fa riferimento a Godot o al sistema operativo.

I test `DeterminismTests`, `SnapshotTests` e `SystemOrderTests` fanno rispettare queste regole. I calcoli restano in `float`.

## Istantanea e sincronizzazione

`SimHost.CreateSnapshot()` produce un'istantanea canonica (JSON compatto in ordine stabile) dello stato applicato; `SimHost.Checksum()` ne calcola l'impronta FNV-1a a 64 bit. La stessa istantanea è il cuore dei salvataggi (vedi [Formato dei salvataggi](Salvataggi-formato)).

Il modello scelto per il co-op è **ibrido** ([ADR 0002](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0002-sincronizzazione-ibrida.md)): l'host ordina i comandi, tutti eseguono la stessa simulazione deterministica, un checksum periodico scopre le divergenze e un'istantanea completa le ripara (e fa entrare chi arriva a partita in corso). Oggi esistono tick fisso, comandi, `SimHost`, istantanea e checksum; il controllo periodico e la rete arrivano con il sotto-progetto del multiplayer.

## Il ciclo di un frame

1. L'input arriva dai callback di Godot (`World._UnhandledInput`) e diventa comandi inviati a `SimHost.Submit`.
2. `World._Process` chiama `SimHost.Advance(delta)`: l'orologio decide quanti tick eseguire, l'host applica i comandi e chiama `Factory.Step()`. Gli eventi tornano a `World` (toast, aggiornamento di grafica e pannelli).
3. Il client aggiorna la costruzione (piani, cantieri, droni), ricostruisce i visual di nastri ed edifici se la griglia è cambiata, muove gli sprite degli oggetti e le animazioni.
4. L'HUD si aggiorna (fondi, energia, risorse, palette, coda del drone).
5. Il terreno si disegna a pezzi, a due livelli di dettaglio, solo attorno alla vista.

Con la Home aperta o senza partita il frame salta i passi dal 2 al 5.

## Il client Godot

- `godot/scenes/Main.tscn` è la scena principale; lo script `World` (con i suoi file parziali in `godot/scripts/world/`) tiene il `SimHost` e coordina tutto.
- `godot/scripts/visuals/`: sprite (unico accesso tramite `Sprites`), nastri, edifici, costruzione, effetti.
- `godot/scripts/ui/`: tema, colori, testi, HUD e pannelli (vedi [Interfaccia e lingue](Interfaccia-e-lingue)).
- Effetti, ombre, bagliore e notte vivono solo nel client e non entrano nel checksum.

## Per approfondire

- [docs/ARCHITETTURA.md](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md): sezioni Simulazione, Regole di determinismo, Costruzione, Ciclo di un frame
- [ADR 0001](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0001-server-csharp.md), [ADR 0002](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0002-sincronizzazione-ibrida.md), [ADR 0003](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0003-costruzione-come-mindustry.md)
- [Contenuti e dati](Contenuti-e-dati) · [Formato dei salvataggi](Salvataggi-formato) · [Test e playtest](Test-e-playtest)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Compilare e avviare](Compilare-e-avviare) · [Torna alla Home](Home) · [Pagina successiva: Contenuti e dati →](Contenuti-e-dati)

<!-- /navigazione -->
