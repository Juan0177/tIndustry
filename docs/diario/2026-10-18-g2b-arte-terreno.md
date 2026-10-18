# Sotto-progetto G2b — Arte del terreno

- **Specifica**: [2026-10-18-g2b-arte-terreno-design](../superpowers/specs/2026-10-18-g2b-arte-terreno-design.md)
- **Piano**: [2026-10-18-g2b-arte-terreno](../superpowers/plans/2026-10-18-g2b-arte-terreno.md)
- **ADR**: [0004 — Arte generata da script](../decisioni/0004-arte-generata-da-script.md) (sezione «Terreno (G2b)»)
- **Periodo**: 2026-10-18 (una sola esecuzione notturna, in modalità Auto)
- **Esito**: in `main` con le PR [#182](https://github.com/Juan0177/tIndustry/pull/182), [#183](https://github.com/Juan0177/tIndustry/pull/183) e [#184](https://github.com/Juan0177/tIndustry/pull/184). L'aspetto del terreno e la base candidata attendono ancora l'OK dell'utente (vedi «Da fare»).

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-10-18-g2b-arte-terreno/progress.md`), dai rapporti e dalle revisioni dei task (`task-N-report.md`, `task-N-review.md`), dalla revisione finale (`final-review.md`) e dalla storia git (`ba0bc05..main`). Dove le fonti non dicono qualcosa qui non è inventato.

## Obiettivo

Dare al terreno della G2a un aspetto disegnato: sprite di suoli, pareti, bordi, giacimenti e decorazioni generati da `tools/art`, una regola di composizione unica (riferimento Python, copia fedele in C#) e un client con due livelli di dettaglio (pezzi a colori da lontano, pezzi d'arte da vicino) e acqua animata da shader. Scelte e motivi nella sezione «Terreno (G2b)» dell'[ADR 0004](../decisioni/0004-arte-generata-da-script.md).

## Task, commit e PR

Un branch per task (`feat/g2b-<n>-...`), ciascuno fuso in `main` appena la revisione era pulita e la CI verde, come per la G2a. Modello opus per implementatori e revisioni dei task 1 e 2 e per la revisione finale; sonnet per il task 3 (documenti) e per le riletture.

| Task | Contenuto | Commit | PR |
|---|---|---|---|
| 0 | Elenco dei dati reali prima del lavoro; `main` con la G2a fusa (#175–#180); base di confronto `.dev/baseline-g2a-candidata`, non ancora approvata | — | — |
| 1 | Sprite del terreno nel generatore (98 PNG nuovi, 166 sprite in totale), rampe in `palette.py`, `edgePriority` in `content.json` e validazione in C#, tavola `.dev/arte/tavola-terreno.png` | `c4e8754`, correzioni `960b762` | [#182](https://github.com/Juan0177/tIndustry/pull/182) |
| 2 | Client: `TerrainLook` (copia della regola Python), `TerrainComposer`, due livelli di dettaglio, shader dell'acqua, catture e prova di prestazioni | `8c1ec8f`, correzioni `64ca56e` | [#183](https://github.com/Juan0177/tIndustry/pull/183) |
| 3 | Documenti (ADR 0004, ARCHITETTURA, README con tre catture, CHANGELOG, canvas), base candidata `.dev/baseline-g2b-candidata` | `8f86099` | [#184](https://github.com/Juan0177/tIndustry/pull/184) |
| Fix finali | Ondata di correzioni dalla revisione finale (10 punti) | `77d701a` | [#184](https://github.com/Juan0177/tIndustry/pull/184) |

CI `build-publish.yml` lanciata a mano e verde su ogni task: T1 39103490637 (correzioni 39104436597), T2 39106001939 (correzioni 39106763664), T3 39107080151, correzioni finali 39108113823.

Test superati (rapporti dei task): T1 281 (2 ignorati), T2 287 (2 ignorati), invariati fino alla fine; test Python di `tools/art`: 17 al primo giro del T1, 21 dopo le correzioni, 24 nel T2.

## Giri di revisione

- **T1** — conforme, «Needs fixes»: 1 Important e 5 Minor. Important: i pezzi d'angolo dei bordi venivano disegnati anche sopra i lati dello stesso suolo, e il labbro scuro di 1 px lasciava un segno a ogni giunzione lungo i confini dritti. Verificato dal revisore: i colori della tabella `terrain` erano identici byte per byte (chiavi dei PNG della campagna al sicuro). Minor: id dell'acqua scritti a mano, test delle varianti troppo debole (media di una colonna entro 8), `_poligono` privato importato, due decorazioni «a pillola», nessun controllo che i 5 minerali coincidano con `DepositKind`.
  - **Controllo del coordinatore sulla tavola**: pavimenti, bordi, giacimenti e decorazioni buoni e nello stile di Mindustry, ma le pareti sembravano pastiglie tonde piatte o stagni, e la parete di ghiaccio sembrava acqua. Decisione: le pareti devono leggersi come roccia in rilievo.
  - **Giro di correzione 1** (`960b762`): pareti ridisegnate in rilievo (impronta per cella con rientro di 2 px, smusso sugli angoli esterni, parete scura alta 5 px in basso, lato destro più scuro, faccia superiore a sfaccettature, rampa nuova per il ghiaccio); **regola degli angoli**: un angolo si disegna solo se il suolo diagonale ha priorità più alta e nessuno dei due lati accanto è lo stesso suolo, e le frange non hanno più il labbro scuro; composizione di riferimento `tavola.componi` e regola scritta in `tools/art/README.md` per il task 2; `ACQUA` ricavata dal bioma `acqua`, `test_stili_terreno` (anche sui minerali rispetto all'enum C#), `test_varianti_ripetibili` più forte, `poligono` spostato in `forme.py`. 21 PNG rigenerati, nessun nome nuovo. Controllo del coordinatore: le pareti ora sono blocchi in rilievo, la parete di ghiaccio si legge come ghiaccio. Rilettura (sonnet): tutto risolto.
- **T2** — conforme, approvato: 0 Critical, 0 Important, 5 Minor. Il revisore ha confrontato riga per riga `CellHash`, i sali, la regola dei bordi e la doppia griglia con il riferimento Python, e ha letto la fixture (1938 pezzi, tutti i tipi di pezzo). Deviazioni d'interfaccia accettate dal coordinatore (vedi «Decisioni»). Minor: residuo di formattazione in `tavola.py`, `PlaytestTerrainReady` che leggeva i contatori di prima dello zoom, giacimenti senza marcatori mentre un pezzo d'arte è in attesa, `DecorationDensity` duplicata senza controllo testuale, `% 3600.0` che faceva saltare le increspature ogni ora.
  - **Controllo del coordinatore sulle catture** (`.dev/g2b-task2/perf`): aspetto nello stile di Mindustry, con tre cose da correggere: (a) la griglia si vedeva sopra l'arte sui suoli chiari; (b) i sassolini di carbone su basalto si leggevano poco; (c) il tutorial parlava di ferro «rocce grigie».
  - **Giro di correzione 1** (`64ca56e`): griglia nascosta dal livello d'arte (con uno strumento di costruzione attivo, molto tenue); carbone con luccichio e lato illuminato (`test_carbone_si_legge`; un primo tentativo con il bordo chiaro sembrava un buco ad anello ed è stato scartato); testo del tutorial «sassolini grigio-azzurri» (l'agente ha notato che nella cattura i puntini arancio erano il rame lì accanto e non il ferro: la nota precedente era sbagliata); i 5 Minor, compresi `HasArtAt` per i marcatori e il periodo esatto dell'acqua (`WaterPeriod = 25600`). Frame più lento scorrendo circa 23 ms. Rilettura (sonnet): 8 su 8 risolti.
- **T3** — approvato dalla revisione (sonnet), solo Minor (ARCHITETTURA cita `TerrainSprites` dentro `TerrainComposer.cs`, accettabile).
- **Revisione finale** (opus, `ba0bc05..8f86099`, worktree temporaneo): **si può unire: sì, dopo le correzioni**. 0 Critical, 1 Important, 9 Minor. Verificati dal revisore: build 0 errori e 0 avvisi (soluzione e progetto Godot), 287 test superati e 2 ignorati, 24 test Python, `genera.py --controlla` (166 aggiornati) e fixture aggiornata, `compare_shots` 12 confrontate e 0 diverse sia tra due esecuzioni sia contro la base candidata. Il gioco non è stato avviato dal revisore.
- **Ondata finale di correzioni** (`77d701a`): vedi sotto. Rilettura (sonnet): **10 su 10 risolti**.

### Revisione finale: i punti

Punti di forza segnalati: Python e C# bloccati insieme in entrambe le direzioni (una modifica in Python fa fallire `test_fixture_terreno_aggiornata`, una in C# fa fallire `MatchesPythonReference`); cuciture corrette per costruzione e provate da `ChunkSeamsMatch` su una mappa 40×37; livelli di dettaglio senza buchi; cache LRU che non supera il limite e texture sempre liberate; shader dell'acqua con periodo esatto; chiavi di colore della campagna intatte (in `content.json` cambia solo `edgePriority`); validazione del contenuto con un test per ogni errore.

- **I1 (Important)**: `docs/ARCHITETTURA.md` descriveva ancora il terreno di prima (un pixel per cella, un solo punto in cui le celle diventano colore, mappa delle cartelle senza `TerrainLook`, `TerrainComposer` e `water.gdshader`).
- **M1** `.dev/arte/tavola.png` al posto di `tavola-terreno.png` e manca il passo della rampa nel «come aggiungere un suolo»; **M2** `ATTRIBUTION.md` senza le famiglie del terreno; **M3** cattura dei minerali a zoom 1 invece di 0,5, con 4 minerali su 5 (il rame non si vedeva); **M4** le decorazioni della sabbia somigliavano ai sassolini del rame; **M5** il limite di 4 composizioni per frame è un numero e non un tempo; **M6** la prova non misura la memoria video; **M7** il tetto di 96 pezzi non è assoluto (in 4K a zoom 0,25 circa 190); **M8** `PlaytestTerrainReady` scade senza dirlo; **M9** le pareti cambiano molto di luminosità al cambio di livello di dettaglio (colore della tabella quasi nero contro grigio medio dell'arte).

Correzioni di `77d701a`:

1. ARCHITETTURA riscritta: mappa delle cartelle, sezione Client, passo 5 del ciclo di un frame (due livelli di dettaglio, `BuildChunkImage` e `TerrainComposer.Compose` come i due soli punti in cui le celle diventano pixel, `TerrainLook` con la fixture, shader), descrizione della prova PERF (I1).
2. «Come aggiungere lo sprite di un suolo»: guarda `tavola-terreno.png` e ha il passo della rampa in `palette.py` (M1).
3. `ATTRIBUTION.md` elenca `terreno-*`, `parete-*`, `giacimento-*`, `deco-*`, `acqua-*-anim` (M2).
4. `ui-terreno-minerali` a zoom 0,5 con una finestra più larga: `PLAYTEST terreno: 5 minerali su 5` (M3).
5. `deco-sabbia` ridisegnata: ciuffo secco più scuro della sabbia, due ciuffi, lastra chiara angolosa; niente più ovali marrone-arancio vicini al rame (M4).
6. `TerrainChunks.ArtFrameBudgetMs = 3`: dopo il primo pezzo del frame non se ne inizia un altro se la composizione ha già superato 3 ms; il tetto di 4 resta (M5).
7. La riga `PLAYTEST perf arte` riporta anche la memoria video massima (M6).
8. Il tetto `max(96, pezzi toccati dalla vista + margine)` è scritto in `MaxArtChunks`, ARCHITETTURA e ADR 0004 (M7).
9. `PlaytestTerrainReady` fa `GD.PushError` se dopo 600 frame mancano pezzi, così `playtest.sh` fallisce (M8).
10. I pezzi a colori usano il colore medio dei pixel opachi dell'arte (4 varianti per suolo, pezzo 15 della striscia per parete), calcolato una volta per tabella; senza arte resta il colore della tabella, che non cambia. Le pareti non saltano più da quasi nere a grigie al cambio di zoom (M9).

Aggiornati anche ADR 0004 (decorazioni, cache, colori medi, marcatori, prestazioni) e CHANGELOG; catture del README `terreno-biomi.png` e `terreno-minerali.png` rigenerate (`terreno-partenza.png` identica); `.dev/baseline-g2b-candidata` rifatta da `.dev/g2b-task2/ui-run1` e `tutorial-run1` (cambiano `ui-campagna`, `ui-terreno-biomi` e `ui-terreno-minerali`; tutorial 0 differenze).

## Prestazioni e memoria

Misure del Task 2 e della sua ondata finale (`TINDUSTRY_PLAYTEST_PERF=1`, 1280×720, senza VSync, build Debug come i playtest). 240 fps è il tetto della macchina.

| Misura | Task 2 (`8c1ec8f`) | Dopo l'ondata finale (`77d701a`) |
|---|---|---|
| Fabbrica del playtest (200 nastri, 97 edifici a strati) | 229 fps medio, 161 minimo | non riportata |
| Enorme 2000² a zoom 0,08, fermo | 240 medio, 103 minimo | 240 medio, 108 minimo |
| Enorme 2000², panoramica | 240 medio, 174 minimo | 240 medio, 198 minimo |
| Enorme, processo / video | 550 MiB / 42 MiB | 550 MiB / 42 MiB |
| Arte, zoom 0,08–2–0,08 | 239 medio, 89 minimo; frame più lento 11,2 ms; 40 pezzi d'arte al massimo | 239 medio, 124 minimo; frame più lento 8,0 ms; 35 pezzi |
| Arte, scorrimento a zoom 1 | 235 medio, 46 minimo; frame più lento 21,9 ms; 96 pezzi; pezzo più lento 14,6 ms | 239 medio, 73 minimo; frame più lento 13,8 ms; 90 pezzi; pezzo più lento 3,2 ms |
| Memoria massima del processo | 558 MiB | 557 MiB |
| Memoria video massima | non misurata | 132 MiB |

Prima della G2b (G2a, task 6): Enorme fermo 240/109, panoramica 240/116, processo 549 MiB. Obiettivi della specifica (Enorme a zoom minimo almeno 200 fps medi, zoom 1 scorrendo almeno 120, nessun pezzo oltre 4 ms, al massimo 4 pezzi nuovi per frame, processo sotto 900 MiB) rispettati. Il 14,6 ms del Task 2 era un'unica creazione di texture nuova (13,4 ms in `ImageTexture.CreateFromImage`, allocazione del driver), sempre lo stesso pezzo nella stessa sequenza; nel giro di correzione non si è ripresentato, ma non è stato dimostrato sparito. Con il budget di 3 ms il frame più lento scorrendo scende da 23,2 a 13,8 ms.

## Decisioni prese in autonomia

- **Decisione**: il task 1 non cambia i colori della tabella `terrain`; le rampe dell'arte stanno in `tools/art/palette.py`, ricavate dalla tabella ma indipendenti. — i colori sono le chiavi dei PNG delle mappe della campagna (G2a). — se sbagliata: mappe della campagna da rifare.
- **Decisione**: un branch per task da `main`, ciascuno fuso a revisione pulita e CI verde; opus per implementatori e revisori dei task 1–2, sonnet per il task 3. — come la G2a. — se sbagliata: solo costo.
- **Decisione**: pareti ridisegnate come roccia in rilievo (faccia superiore chiara, parete scura di 4–6 px a sud e a est, contorno più angoloso) al posto dei blocchi tondi a «metaball». — la parete di ghiaccio sembrava acqua e le pareti sembravano stagni. — se sbagliata: un altro giro sulle pareti.
- **Decisione**: giro 1 del T1 = pareti in rilievo, regola degli angoli, Minor e regola di composizione documentata in `tools/art/README.md` per il task 2. — il task 2 copia la regola in C#, va sistemata prima. — se sbagliata: il difetto si sarebbe copiato in C#.
- **Decisione**: la ripetizione delle increspature dell'acqua si risolve nel task 2 con uno shader in coordinate del mondo. — in tavola e nel client tutte le celle d'acqua avrebbero lo stesso fotogramma. — se sbagliata: pattern ripetuto sul mare.
- **Decisione**: accettato che il primo pezzo d'arte richieda 14,6 ms (allocazione della prima texture); niente preallocazione di 96 MiB. — la preallocazione costerebbe 96 MiB di video anche a zoom minimo. — se sbagliata: un singolo scatto per sessione.
- **Decisione**: accettate le deviazioni d'interfaccia del task 2 rispetto al piano: `TerrainLook` costruito dalla tabella, `Variant` senza seme, `WallMask` che prende anche il tipo di parete, maschera dell'acqua nell'alfa della stessa texture, increspature procedurali nello shader, Python aggiornato di conseguenza. — mantengono la parità con il riferimento Python e dimezzano texture e caricamenti. — se sbagliata: ritocco dell'interfaccia.
- **Decisione**: giro 1 del T2 = griglia, carbone, tutorial sul ferro più i 5 Minor. — emersi dalle catture e dalla revisione. — se sbagliata: un giro in più.
- **Decisione**: ondata finale unica per tutti e 10 i punti della revisione finale (compresi tempo di composizione, memoria video nella prova, tetto `max(96, vista)`, errore di scadenza e colori a livello lontano presi dalla media dell'arte con la tabella intatta). — stessi file, una sola rilettura. — se sbagliata: diff più grande.

## Problemi rinviati

- **Strisce `acqua-*-anim` non usate dal client**: restano generate e coperte dai test, mentre l'acqua si anima con lo shader. Il rapporto del T2 lasciava aperto se toglierle o usarle per l'impostazione Effetti; il T3 non ha deciso. Destinazione possibile: **G3** (effetti), non assegnato.
- **Sfaccettature delle pareti uguali in ogni pezzo** (trama periodica per pezzo): su creste lunghe si vede un motivo regolare a contrasto basso (nota del T1).
- **Non giudicato dalla revisione finale** perché il gioco non è stato avviato: tremolio e scalettature in movimento a zoom 0,25–0,5 (filtro Nearest senza mipmap), comparsa progressiva dei pezzi d'arte dopo un salto di zoom (circa 10–18 frame di colori su 1080p), prestazioni su altre macchine. Da valutare giocando.
- **Impostazione «Effetti» per fermare l'acqua** (la specifica la rimanda alla G3).
- Restano aperti i rinvii della G2a: liberazione alla fonte delle risorse dell'HUD (G4) e impostazioni personalizzate di generazione (non assegnato).
- Tutti i Minor dei task e della revisione finale sono stati chiusi.

## Verifiche finali

Dai rapporti dell'ondata finale (`77d701a`) e della revisione finale:

- `dotnet build tIndustry.sln -c Release` e Debug: 0 errori, 0 avvisi.
- `dotnet test`: 287 superati, 2 ignorati.
- `python -m unittest tools/art/test_genera.py`: 24 OK; `genera.py --controlla` e `--controlla --pixel`: 166 sprite aggiornati, 0 da rigenerare; `fixture_terreno.py --controlla`: aggiornata.
- `godot4 --headless --import` con `APPDATA` temporaneo: uscita 0, 0 errori; `.import` cambiati solo per fine riga ripristinati.
- Playtest `ui` e `tutorial` due volte, tutti OK, 0 ERROR; `compare_shots.py` tra le due esecuzioni: ui 12 confrontate e 0 diverse, tutorial 9 e 0.
- Sonde `PERF` OK (numeri sopra).
- CI `build-publish.yml` lanciata a mano, verde (ultima esecuzione 39108113823).
- Dati reali del giocatore: invariati (elenco di `%LOCALAPPDATA%\tIndustry` e `%APPDATA%\Godot\app_userdata\tIndustry` identico a quello di prima, `g2b-realdata-before.txt`).

## Da fare

- **L'utente giudica l'aspetto del terreno** (suoli, bordi, pareti in rilievo, giacimenti, decorazioni, acqua): `.dev/arte/tavola-terreno.png` e le catture del README (`docs/img/readme/terreno-partenza.png`, `terreno-biomi.png`, `terreno-minerali.png`).
- Solo con l'OK, **promuovere la base candidata** `.dev/baseline-g2b-candidata`. Fino ad allora la base resta `.dev/baseline-g2a-candidata`, a sua volta non promossa; ogni cattura del terreno differisce dalla G2a perché l'arte è nuova.
