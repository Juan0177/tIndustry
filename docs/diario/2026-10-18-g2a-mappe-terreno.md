# Sotto-progetto G2a — Mappe e terreno

- **Specifica**: [2026-10-18-g2a-mappe-terreno-design](../superpowers/specs/2026-10-18-g2a-mappe-terreno-design.md)
- **Piano**: [2026-10-18-g2a-mappe-terreno](../superpowers/plans/2026-10-18-g2a-mappe-terreno.md)
- **ADR**: [0005 — Mappe: generate da seme e prestabilite in file](../decisioni/0005-mappe-seme-e-file.md)
- **Periodo**: 2026-10-18 (una sola esecuzione notturna, in modalità Auto)
- **Esito**: in `main` con le PR [#175](https://github.com/Juan0177/tIndustry/pull/175), [#176](https://github.com/Juan0177/tIndustry/pull/176), [#177](https://github.com/Juan0177/tIndustry/pull/177), [#178](https://github.com/Juan0177/tIndustry/pull/178), [#179](https://github.com/Juan0177/tIndustry/pull/179) e [#180](https://github.com/Juan0177/tIndustry/pull/180). Le mappe della campagna, l'aspetto del terreno e la base candidata attendono ancora l'OK dell'utente (vedi «Da fare»).

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-10-18-g2a-mappe-terreno/progress.md`), dai rapporti e dalle revisioni dei task (`task-N-report.md`, `task-N-review.md`), dalla revisione finale (`final-review.md`) e dalla storia git (`2f44d6a..main`). Dove le fonti non dicono qualcosa qui non è inventato.

## Obiettivo

Dare al gioco un terreno vero: pareti e acqua profonda che bloccano la costruzione, un generatore a biomi da seme (Media 1000², Grande 1500², Enorme 2000²), le 10 mappe della campagna in file PNG con validatore e strumento (`tools/MapTool`), salvataggi v12 che sanno come ricostruire il terreno, e un client con terreno a pezzi da 64×64 e il pannello «Nuova partita» (misura e seme). Scelte e motivi in [ADR 0005](../decisioni/0005-mappe-seme-e-file.md).

## Task, commit e PR

Un branch per task (`feat/g2a-<n>-...`), ciascuno fuso in `main` appena la revisione era pulita e la CI verde, come per la G1. Modello opus per implementatori T1–T5 e per tutte le revisioni; sonnet per il T6 (documenti) e per alcune riletture. Base di confronto per le catture: `.dev/baseline-g1-candidata`, non ancora approvata dall'utente.

| Task | Contenuto | Commit | PR |
|---|---|---|---|
| 0 | Elenco dei dati reali prima del lavoro, `main` verificato (G1 fusa, CI verde) | — | — |
| 1 | Regole: pareti, acqua profonda, tabella dei suoli (`terrain` in `content.json`), cella da 3 byte, generatore classico congelato | `1a5c626` | [#175](https://github.com/Juan0177/tIndustry/pull/175) |
| 2 | Generatore a biomi (rumore e algoritmo solo a interi, partenza garantita), correzioni della revisione | `09f3cbe`, correzioni `922d52e` | [#176](https://github.com/Juan0177/tIndustry/pull/176) |
| 3 | Mappe della campagna in file: `PngReader`, `CampaignMapFile`, validatore, `MapTool`, 10 mappe | `59e242f`, correzioni `bba6751` | [#177](https://github.com/Juan0177/tIndustry/pull/177) |
| 4 | Salvataggi v12 (generatore, misura, id mappa), lettura dei v11 col generatore classico | `4901a6f` | [#178](https://github.com/Juan0177/tIndustry/pull/178) |
| 5 | Client: terreno a pezzi, «Nuova partita», campagna dalle mappe in file, correzioni | `4e72f25`, correzioni `951d1a8` | [#179](https://github.com/Juan0177/tIndustry/pull/179) |
| 6 | Documenti (ADR 0005, ARCHITETTURA, README, CHANGELOG, CLAUDE.md), rinvii dei task precedenti, base candidata | `d924092` | [#180](https://github.com/Juan0177/tIndustry/pull/180) |
| Fix finali | Ondata di correzioni dalla revisione finale e dalla revisione del Task 6 | `f630cb1` | [#180](https://github.com/Juan0177/tIndustry/pull/180) |

CI `build-publish.yml` lanciata a mano e verde su ogni task: T1 39093750925, T2 39094682704 (correzioni 39095197059), T3 39096145010 (correzioni 39096641721), T4 39097540210, T5 39098891972 (correzioni 39100115091), T6 39100924306, correzioni finali 39101982527.

Test superati (rapporti dei task): T1 198, T2 225, T4 266, T5 267 (271 dopo le correzioni), T6 273, dopo le correzioni finali 276.

## Giri di revisione

- **T1** — conforme, approvato: 3 Minor e 2 note. Il revisore ha ricompilato il codice precedente: le impronte del generatore classico coincidono. Esito «DONE_WITH_CONCERNS» perché il rosso dei test di regole non era stato eseguito separatamente. Rinvii: test negativi della sezione `terrain` e caso DeepWater su impronta mista al T2; ricontrollo del terreno in `RestoreSite` e motivo vero di `PlanDropped` al T4.
- **T2** — conforme, approvato, 7 Minor (rame ribaltato vicino al bordo alto, ramo di scavo senza test, overflow in `Noise.Value2D` per celle enormi, acqua profonda a contatto con la terra dopo la pulizia della partenza, etichetta di test, tempo del test in CI, TDD a rovescio). Giro di correzione 1 (`922d52e`): colori delle pareti troppo vicini ai suoli (solo dati), riva bassa e casi limite della partenza, Minor 1–6. Rilettura (sonnet): 7 su 7 risolti, nessuna rottura.
- **T3** — conforme, «Needs fixes»: 2 Important e 7 Minor. Important 1: una lunghezza di blocco PNG ostile (`0x7FFFFFFF`) dava `OverflowException`, una di `0x7FFFFFF0` dava `OutOfMemoryException`, entrambe fuori dal contratto «`InvalidDataException` in italiano». Important 2: `MaxEdge` a 16384 allocava fino a 1 GiB prima del controllo della misura. Giro di correzione 1 (`bba6751`, «PNG ostili, errori di lettura e Core delle mappe»): irrobustimento di `PngReader` (misura letta da IHDR prima di ogni allocazione, limite per blocco) e tutti i Minor (errori di lettura dei file riportati come problemi, IHDR doppio, Core esplicito validato, collisioni di colore). Rilettura (opus) con sonda di PNG ostili: 9 su 9 risolti, allocazioni limitate. Due Minor fuori ambito (limite degli IDAT dalla misura, surplus zlib ignorato) rinviati al T6.
- **T4** — conforme, approvato, 4 Minor (ricarica del v11 risalvato con confronto del checksum, regola unica sull'id della mappa, caso del cantiere ponte, TDD a rovescio): i primi due rinviati al T6.
- **T5** — conforme, «Needs fixes»: 2 Important, 1 deviazione minore dalla spec, 6 Minor.
  - Important 1: le `ImageTexture` dei pezzi di terreno non venivano mai liberate (145 orfane nella sonda), indicate come causa dell'arresto in uscita.
  - Important 2: un errore nel caricamento della mappa di un livello chiudeva comunque la selezione e lasciava il giocatore nella partita nascosta, senza il motivo.
  - Deviazione: il motivo del fantasma doveva stare nel suggerimento in basso, stava nel tooltip accanto al mouse.
  - Giro di correzione 1 (`951d1a8`): texture liberate (`Chunk.Free`), errore della campagna sulla lista aperta, chiusura della finestra gestita (`AutoAcceptQuit` spento, `QuitGame`), sovrapposizione «Generazione della mappa…» per misure sopra Media, motivo del fantasma in basso, tetto della cache pari ad almeno i pezzi visibili, messaggio del seme con l'intervallo, test dei semi estremi. Rilettura (sonnet): tutto risolto; 2 Minor rinviati al T6 (eccezioni dei caricamenti senza attesa, Esc durante la sovrapposizione).
- **T6** — revisione (sonnet): approvato. Minor: toast del pannello Nuova partita che può finire sotto il pannello, limite degli IDAT (`raw/8`) da allargare, diario da scrivere prima di cancellare `.superpowers/sdd`, maschere di `compare_shots.py` allargate (da sostituire con il drone fermo).
- **Revisione finale** (opus, `2f44d6a..d924092`, in un worktree temporaneo): **pronto per il merge: sì, con correzioni**. 0 Critical, 1 Important, 7 Minor. Verificati dal revisore: build 0 errori e 0 avvisi, 273 test superati e 2 saltati, `GenerationTimeBudget` passato. Il gioco non è stato avviato dal revisore.
- **Ondata finale di correzioni** (`f630cb1`): vedi sotto. Rilettura (opus): **11 su 11 risolti, nessuna rottura**.

### Revisione finale: i punti

Punti di forza segnalati: salvataggi v11 al sicuro (generatore mancante porta a `"classico"`, fixture v11 vera scritta da `main`, checksum identico dopo il risalvataggio in v12); determinismo solo a interi, con l'impronta del seme 42 uguale su Linux e Windows; regole in un solo punto (`TerrainBlocks` passa da `CanOccupyFootprint`, `RefusalReason` condiviso da `CanPlace`, `SimHost` e `BuilderSystem`); `PngReader` robusto; l'utente non resta mai bloccato da una mappa mancante; prestazioni nei limiti; documenti allineati al codice.

- **I1 (Important)**: il generatore a biomi non aveva versione, ma i salvataggi «biomi» lo rigenerano da seme e lato. Qualunque modifica futura (la G2b o i Liquidi la toccheranno, è probabile) avrebbe cambiato in silenzio il terreno sotto le partite salvate, con edifici su pareti o acqua profonda e cantieri spariti.
- **M1** motivo sbagliato per un ponte senza uscita; **M2** il colore della tabella `terrain` è anche la chiave dei PNG della campagna; **M3** senza `data/maps` la campagna tornava in silenzio al generatore classico; **M4** mappa Media generata dietro la Home a ogni avvio; **M5** il tutorial gira sulla Media a biomi e non su una mappa disegnata (deviazione dalla spec, giudicata migliore); **M6** il test del v11 non provava che l'acqua classica libera restasse costruibile; **M7** `SlotMapEdge` rileggeva tutti gli slot.

Correzioni di `f630cb1`:

1. Il generatore pubblicato si chiama `"biomi-1"` ed è congelato; `"biomi"` dei primi salvataggi v12 vale uguale (alias). Il salvataggio v12 tiene `TerrainHash`: se il terreno rigenerato non coincide, il caricamento si ferma con una `SaveMapException`; senza impronta si carica come prima. Regola in ADR 0005, ARCHITETTURA e CLAUDE.md: un generatore pubblicato non si modifica, se ne aggiunge uno nuovo.
2. Ponte senza uscita su pareti o acqua profonda: motivo vero (M1).
3. Colori della tabella `terrain` = chiavi dei PNG della campagna, scritto nei documenti (M2).
4. Senza `data/maps` un livello della campagna mostra l'errore, niente ripiego al classico (M3).
5. Partita di sfondo dietro la Home a 256², seme 42 (con `TINDUSTRY_FRESH` resta la Media); tutorial sulla Media a biomi annotato nell'ADR (M4, M5).
6. Test v11: una cella d'acqua classica libera resta costruibile (M6).
7. `SlotMapEdge` legge solo lo slot che serve (`FactorySaveStore.ReadSlotInfo`) (M7).
8. Drone fermo senza oscillazione sotto `TINDUSTRY_PLAYTEST`; maschere di `compare_shots.py` tornate come prima; il playtest `ui` aspetta la fine dello smontaggio prima della cattura della campagna.
9. Errori di Nuova partita mostrati sul pannello (Minor del T6).
10. `PngReader`: limite degli IDAT più largo (`raw + raw/4 + 64 KiB`) (Minor del T6).

## Indagine sull'arresto in uscita (pezzi e HUD)

Dopo il T5 comparve, in 2 esecuzioni su 3 del tutorial, un arresto allo spegnimento («Leaked unsafe reference», «!rc_owner», codice 0xC000001D), mai visto nei 22 log precedenti. Il T5 aveva messo `QuitAfterFinalizers` (GC e attesa dei finalizzatori prima di `Quit`) come rimedio, e la revisione indicava come causa le texture dei pezzi mai liberate. Nel giro di correzione:

- le texture dei pezzi sono state liberate (`Chunk.Free`) e il rimedio tolto: il tutorial si arrestava ancora in 2 esecuzioni su 5, con gli stessi id di oggetto a ogni arresto;
- gli id puntavano a risorse create in C# nell'HUD (`StyleBoxFlat` inline nei pannelli e in `HudStyle`, `CompressedTexture2D` degli sprite caricati), non ai pezzi: wrapper non raggiungibili finalizzati dal thread del GC durante lo smontaggio del motore;
- liberare ogni risorsa inline dell'HUD è fuori dall'ambito: lo svuotamento (`GC.Collect()` e `WaitForPendingFinalizers()`) è stato spostato in un solo punto, `World._ExitTree()`, che gira su ogni via di uscita dopo l'uscita dei figli (quindi dopo la liberazione dei pezzi) e prima dello smontaggio del motore. Risultato: tutorial 6 su 6 OK, poi 5 su 5.

Costo se sbagliata: arresto raro in uscita finché la G4 non rifà l'HUD su un tema unico (vedi «Problemi rinviati»).

## Prestazioni

Misure del Task 2 (generatore, Release) e del Task 5 (client, `TINDUSTRY_PLAYTEST_PERF=1`, 1280×720, senza VSync). 240 fps è il tetto della macchina.

| Misura | Risultato |
|---|---|
| Generazione 1000² | 313–376 ms (T2); circa 0,35 s nella revisione finale |
| Generazione 2000² | 1041–1142 ms (T2); circa 1,1 s nella revisione finale |
| Fabbrica del playtest (200 nastri, 97 edifici a strati) | 230 fps medio, 161 minimo, 871 nodi (T5); 229 medio e 166 minimo dopo le correzioni |
| Enorme 2000² a zoom minimo (0,08), prima delle correzioni | fermo 240 medio e 108 minimo; panoramica 240 e 189; 256 pezzi vivi, 401 creati; processo 558 MiB; partenza 2741 ms (build di debug) |
| Enorme 2000² a zoom minimo, dopo le correzioni | fermo 239 medio e 79 minimo (primo fotogramma dopo lo spegnimento del VSync); panoramica 240 e 197; processo 553 MiB |

Obiettivi della specifica (almeno 60 fps, meno di 1,5 GB) rispettati. Una cella occupa 3 byte (12 MB per l'Enorme).

Rilievo che ha cambiato un disegno: con due cerchi per cella i marcatori dei giacimenti a zoom minimo davano sull'Enorme 70 fps fermo, 33 di media e 22 di minimo in panoramica. Sotto i 14 px per cella i cerchi sono ora una striscia colorata per ogni tratto orizzontale dello stesso minerale: 240 fps. Da vicino i marcatori sono invariati.

## Controlli del coordinatore

Il coordinatore ha guardato anteprime e catture, oltre alla revisione del codice.

- **T2** (`.dev/g2a-task2/seme42-1000-pareti.png`): biomi grandi e leggibili, laghi e fiumi, creste di pareti ai confini dei biomi, resa simile a Mindustry.
- **T3** (anteprima del livello 01 in `.dev/mappe/`): zona del Core aperta, ferro e rame vicini, laghi e pareti leggibili.
- **T5** (`.dev/g2a-task5`): zona di partenza libera con minatore e corridoio di nastri, pannello Nuova partita chiaro.

## Decisioni prese in autonomia

- **Decisione**: `NewGame.Campaign` con misure esplicite (`mapWidth`/`mapHeight`, usate dai test) tiene il generatore classico e ignora la mappa del livello; senza misure carica il file. — lascia invariati i test esistenti. — se sbagliata: servono fixture per i test.
- **Decisione**: opus per implementatori T1–T5 e per tutte le revisioni, sonnet per il T6. — il budget lo permetteva. — se sbagliata: solo costo.
- **Decisione**: un branch per task da `main`, ciascuno fuso appena revisione pulita e CI verde. — come la G1, niente pile lunghe. — se sbagliata: qualche fusione in più.
- **Decisione**: T1, l'acqua ha il bioma «acqua» (è trasversale); il generatore la assegna per bioma al T2. — l'acqua non appartiene a un solo bioma. — se sbagliata: ritocco della tabella.
- **Decisione**: T1, il blocco fuori mappa con il terreno presente è accettato (il client non l'ha mai permesso); la migrazione di un salvataggio v11 è coperta dal test del T4. — nessun cambiamento di comportamento reale. — se sbagliata: un salvataggio vecchio con edifici al bordo.
- **Decisione**: T1, `PlaceMiner` resta com'è (un minatore senza giacimento resta possibile). — comportamento di oggi. — se sbagliata: una regola in più.
- **Decisione**: T1, i rinvii dei Minor vanno ai task che toccano gli stessi file (T2: test della sezione `terrain` e caso DeepWater su impronta mista; T4: `RestoreSite` e motivo di `PlanDropped`). — stessi file. — se sbagliata: Minor lasciati più a lungo.
- **Decisione**: T2, colori delle pareti troppo vicini ai suoli (`stone-wall` contro `basalt`, `shrub-wall` contro `grass`): si cambiano solo i dati della tabella nel giro di correzione. — la leggibilità delle pareti. — se sbagliata: ritocco dei colori.
- **Decisione**: giro 1 del T2 = colori delle pareti più i Minor 1–6 (il 7 è solo di processo). — stessi file. — se sbagliata: diff più grande.
- **Decisione**: T3, il gioco non legge ancora le mappe in file fino al T5, perché serve il v12 del T4 per ricaricarle. — niente salvataggi che non si ricaricano. — se sbagliata: nessuna, il collegamento è comunque nel T5.
- **Decisione**: T3, controlli extra di validazione (almeno 50% di terra raggiungibile entro 60 celle, disco di partenza libero) e titanio escluso prima del livello 09. — senza il controllo, il livello 06 con seme 5501/5502 passava con il Core su un'isola in un lago profondo, collegata solo da un canale scavato di 1 cella; la campagna diventa più equa. — se sbagliata: mappe da rigenerare.
- **Decisione**: giro 1 del T3 = i 2 Important e tutti i 7 Minor; il titanio nelle rocce del livello 09 viene segnalato all'utente. — la sicurezza dei file di input e un giudizio estetico che non è mio. — se sbagliata: un giro in più.
- **Decisione**: T3, Minor fuori ambito della rilettura (limite degli IDAT dalla misura, surplus zlib ignorato) al T6. — stesso file ma altro compito. — se sbagliata: nessuna.
- **Decisione**: T4, i salvataggi «biomi» salvano solo seme e lato, quindi il client usa soltanto `MapGenSettings.Normal`; per impostazioni personalizzate si dovrebbe estendere il salvataggio. — formato più semplice. — se sbagliata: salvataggio da estendere.
- **Decisione**: T4, una mappa della campagna ritoccata con la stessa misura tiene gli edifici (le mappe sono dati distribuiti; gli edifici su nuove pareti restano come eredità). — evita di cancellare partite per un ritocco. — se sbagliata: edifici su pareti.
- **Decisione**: T5, giacimenti a strisce con zoom lontano. — da 70 a 240 fps sull'Enorme. — se sbagliata: tornare ai cerchi pagando la prestazione.
- **Decisione**: T5, la busta dei salvataggi guadagna `generator` e `mapId` facoltativi, senza cambio di versione. — solo metadati dell'elenco, snapshot e checksum intatti. — se sbagliata: salire alla v13.
- **Decisione**: T5, l'arresto in uscita va gestito anche alla chiusura della finestra (`NOTIFICATION_WM_CLOSE_REQUEST`) e va mostrata una sovrapposizione «Generazione della mappa…» durante le generazioni grandi. — la via dell'X non passava dal rimedio e 2,7 s senza segnale sembrano un blocco. — se sbagliata: ritocco del client.
- **Decisione**: T5, la perdita all'uscita è dei wrapper dell'HUD e non dei pezzi; lo svuotamento del GC resta in un solo posto (`World._ExitTree`) e la liberazione delle risorse dell'HUD alla fonte passa alla G4. — l'HUD si rifà su un tema unico in G4. — se sbagliata: arresto raro in uscita fino ad allora.
- **Decisione**: giro 1 del T5 = i 2 Important, la chiusura della finestra, la sovrapposizione e i Minor; se la liberazione bastava si toglieva il rimedio (non bastava, vedi sopra). — qualità. — se sbagliata: un giro in più.
- **Decisione**: T6, invece di allargare le maschere di `compare_shots.py` per il drone che oscilla, si congela l'oscillazione sotto `TINDUSTRY_PLAYTEST` (come la luce del Core in G1). — le maschere larghe nascondono differenze vere. — se sbagliata: tornare alle maschere.
- **Decisione**: revisione finale, il generatore pubblicato si congela come `"biomi-1"` con alias `"biomi"`; `TerrainHash` nel salvataggio v12 con rifiuto del caricamento se non coincide; Minor tutti nella stessa ondata sulla #180. — un cambio futuro non deve alterare le partite salvate in silenzio. — se sbagliata: salvataggi rifiutati per un cambio innocuo, risolvibile con un generatore nuovo.
- **Decisione**: i salvataggi della campagna memorizzano l'impronta ma non la impongono. — coerente con la decisione sulle mappe ritoccate. — se sbagliata: una mappa ritoccata non viene fermata.

## Problemi rinviati

- **Liberazione delle risorse dell'HUD alla fonte** (`StyleBoxFlat` inline, `CompressedTexture2D` caricate in C#): resta il rimedio unico in `World._ExitTree`. Destinazione: **G4** (UI rifatta su un tema unico).
- **Aspetto del terreno** (scacchiera erba/terra e puntini per cella non più presenti, colore piatto per cella da vicino; marcatori dei giacimenti a strisce da lontano): destinazione **G2b** (arte del terreno; `BuildChunkImage` è il punto unico dove le celle diventano pixel).
- **Impostazioni personalizzate di generazione** (percentuali di pareti, acqua e giacimenti): non sono salvabili con il formato v12 (solo seme e lato); si aggiungono estendendo il salvataggio quando serviranno (non assegnato).
- **Revisione finale, non giudicato**: bilanciamento e aspetto delle 10 mappe, FPS e memoria (il revisore non ha avviato il gioco), export reale con `data/maps/` accanto agli assembly, playtest e confronto delle catture. Il resto è sotto «Da fare».
- Tutti i Minor dei task sono stati chiusi nel T6 o nell'ondata finale.

## Verifiche finali

- `dotnet build tIndustry.sln -c Release`: 0 errori, 0 avvisi (rapporto del T6; revisione finale su `d924092` identica).
- `dotnet test`: 276 superati dopo l'ondata finale (273 superati e 2 ignorati al T6: `MeasureLargeMap` e `WritePreviews`).
- `dotnet run --project tools/MapTool -c Release -- valida`: 10 mappe su 10 valide (T6).
- Importazione e `--build-solutions` di Godot con `APPDATA` temporaneo: uscita 0, nessun `.import` modificato.
- Playtest `ui` e `tutorial` OK, 0 ERROR (T6); tutorial 9 immagini confrontate, 0 diverse; ui 8 confrontate, 0 diverse tra le esecuzioni dopo le correzioni al confronto. Base candidata `.dev/baseline-g2a-candidata` non promossa.
- Sonde `PERF` OK (numeri sopra).
- CI `build-publish.yml` lanciata a mano, verde (ultima esecuzione 39101982527).
- Dati reali del giocatore: invariati (elenco di `%LOCALAPPDATA%\tIndustry` e `%APPDATA%\Godot\app_userdata\tIndustry` identico a quello di prima, 77 righe, T6).

## Da fare

- **L'utente guarda le mappe della campagna** in `.dev/mappe/` (anteprime) e dà l'OK: in particolare nel **livello 09 il titanio sta nelle rocce e non nella neve**, una scelta del generatore da confermare o correggere.
- **L'utente giudica il nuovo aspetto del terreno** (biomi, pareti, acqua bassa e profonda) e, solo con l'OK, si **promuove** la base candidata `.dev/baseline-g2a-candidata`. Ogni cattura differisce dalla base G1 perché il terreno è nuovo.
