# Nastri come Mindustry

- **Specifica**: [2026-10-18-nastri-mindustry-design](../superpowers/specs/2026-10-18-nastri-mindustry-design.md) (e la [ricerca](../superpowers/specs/2026-10-18-nastri-mindustry-ricerca.md))
- **Piano**: [2026-10-19-nastri-mindustry](../superpowers/plans/2026-10-19-nastri-mindustry.md)
- **ADR**: [0003 — Costruzione come Mindustry](../decisioni/0003-costruzione-come-mindustry.md) (cantiere di sostituzione e rettangolo di smontaggio), [0008 — Agenti che giocano](../decisioni/0008-agenti-che-giocano.md) (gli agenti usano le regole del giocatore)
- **Periodo**: 2026-10-19 e 2026-10-20
- **Esito**: in `main` con le PR [#224](https://github.com/Juan0177/tIndustry/pull/224), [#227](https://github.com/Juan0177/tIndustry/pull/227), [#228](https://github.com/Juan0177/tIndustry/pull/228), [#229](https://github.com/Juan0177/tIndustry/pull/229), [#230](https://github.com/Juan0177/tIndustry/pull/230), [#231](https://github.com/Juan0177/tIndustry/pull/231), [#232](https://github.com/Juan0177/tIndustry/pull/232), [#240](https://github.com/Juan0177/tIndustry/pull/240), [#242](https://github.com/Juan0177/tIndustry/pull/242), [#245](https://github.com/Juan0177/tIndustry/pull/245), [#246](https://github.com/Juan0177/tIndustry/pull/246) (gli undici task) e [#247](https://github.com/Juan0177/tIndustry/pull/247) (correzioni della revisione finale). Piano completo.

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-10-19-nastri-mindustry/progress.md`), dalle revisioni e dai rapporti dei task (`task-N-review.md`, `task-N-report.md`), dalla revisione finale (`final-review.md`), dal rapporto delle correzioni (`final-fix-report.md`) e dalla sua rilettura (`final-fix-rereview.md`), più `tabella-approvata.md`. Dove le fonti non dicono qualcosa qui non è inventato.

## Obiettivo

Piazzare, girare, potenziare e smontare i nastri con i gesti di Mindustry: linea ancora-cursore con anteprima e direzione dal trascinamento, rotellina che ruota ogni blocco orientabile, nastro sopra nastro, ponte automatico, Ctrl, rettangolo di smontaggio, tasto centrale; oggetti che scorrono fluidi e fitti; nastro rinforzato; costi e portata della logistica ribilanciati. Stesse regole per il giocatore e per gli agenti: la linea è una funzione pura di `Shared` (`PlacementLine.Plan`), il rettangolo ha `RemovalArea`. In coda, una proposta grafica di nastri stretti.

## Task, commit e PR

Un branch per task, ciascuno fuso in `main` a revisione pulita e CI verde prima del successivo. Base del piano: `1cffb0d`.

| Task | Contenuto | Commit | PR |
|---|---|---|---|
| 1 | Giro sul posto e unione senza incrocio | `51a9468` | [#224](https://github.com/Juan0177/tIndustry/pull/224) |
| 4 | Oggetti fluidi: interpolazione fra i tick e strisce a un pixel | `1f7de5b`, `39ddc0d`, `7ee0cc4` | [#227](https://github.com/Juan0177/tIndustry/pull/227) |
| 5 | Cambio di tier con cantiere di sostituzione, salvataggi v13 | `dc088b1`, `38218fd` | [#228](https://github.com/Juan0177/tIndustry/pull/228) |
| 6 | Linea di `Shared` con direzione dal trascinamento; `piazza` degli agenti | `e177c3d`, `fa98e0a` | [#229](https://github.com/Juan0177/tIndustry/pull/229) |
| 7 | Ponte automatico e Ctrl (A* e catena) nella linea | `9919df4`, `fc1aaef` | [#230](https://github.com/Juan0177/tIndustry/pull/230) |
| 8 | Trascinamento del client con anteprima e invio al rilascio | `006cff1`, `1569868` | [#231](https://github.com/Juan0177/tIndustry/pull/231) |
| 9 | Rettangolo di smontaggio, tasto centrale, rotellina; `smonta {da, a}` | `19e032c`, `1f085a1` | [#232](https://github.com/Juan0177/tIndustry/pull/232) |
| 10 | Nastro rinforzato `conveyor-armored` | `def4150`, `589b517` (merge di `main`) | [#240](https://github.com/Juan0177/tIndustry/pull/240) |
| 2 | Costi della logistica come Mindustry (dati, campagna, sandbox) | `f753c1a`, `5444e1e` | [#242](https://github.com/Juan0177/tIndustry/pull/242) |
| 3 | Portata e spaziatura come Mindustry | `e933725`, `e22348b`, `529201d` | [#245](https://github.com/Juan0177/tIndustry/pull/245) |
| 11 | Proposta di nastri stretti e chiusura del piano | `24f25b7` | [#246](https://github.com/Juan0177/tIndustry/pull/246) |
| Finale | Correzioni della revisione finale | `c2df568`, `be20427`, `f677de8` | [#247](https://github.com/Juan0177/tIndustry/pull/247) |

CI `build-publish.yml` lanciata a mano e verde su ogni task: T1 39183484907; T4 39184978512, correzioni 39185936244, dopo il merge di `main` 39186578809; T5 39187802176, correzioni 39188943962; T6 39190592612, 39191209324; T7 39192618684, 39193452730; T8 39196148971, 39197372761; T9 39199192964, 39200007983; T10 39345406386; T2 39347603163, 39348476598; T3 39351590551 (la prima, 39351244893, era rossa: il test del sito chiedeva ancora una striscia tolta), correzioni 39353211573; T11 39354528767; finale 39356834399.

## Ordine di esecuzione e perché

Ordine seguito: **1, 4, 5, 6, 7, 8, 9, 10, poi 2, 3, poi 11.**

- L'utente aveva autorizzato la corsa notturna **tranne** i task che toccano la tabella dei costi (2 e 3): la tabella andava approvata da lui.
- Il task 3 cambia le portate e quindi i tempi dei playtest; farlo dopo i task che fissano quei tempi (8, 9) evita di rifare le attese.
- Il task 10 usa un costo provvisorio del rinforzato preso dal piano; il task 2 lo riscrive se la tabella cambia (una riga di dati).
- Il task 11 è la chiusura (proposta grafica e documenti) e va per ultimo.
- Corsa fermata a circa l'88-90% dell'uso settimanale (l'utente chiede di tenere il 10% di riserva).
- Durante il task 10 l'utente ha messo in pausa il lavoro (implementatore fermato a metà, ramo `feat/nastri-10-rinforzato` forse parziale). Ripresa il 2026-10-20 con «vai avanti fino a prima di fare biomi-2»: riletto il worktree, fusione di `main` (`a3501bb`), finito il task. Subito dopo l'utente ha approvato la tabella e si sono sbloccati 2 e 3.

## Tabella dei costi approvata dall'utente

L'utente (2026-10-20): «Spero intendi ferro grezzo perché altrimenti senza un forno attivo non faccio nulla», poi ha scelto «Anche edifici grezzi». Mappa Mindustry verso il nostro: rame è `iron-ore` (grezzo), piombo è `lead-ore`, titanio è `titanium-ore`, i materiali alti sono `graphite` e `silicon` (lavorati). Sblocchi: soldi come prima; materiali con la formula del piano sul costo nuovo (circa dieci volte). Fonte: `tabella-approvata.md`.

| Blocco | Costo | Sblocco |
|---|---|---|
| `conveyor-basic` | ferro grezzo 1 | sbloccato |
| `conveyor-fast` | ferro grezzo 1, piombo grezzo 1, titanio grezzo 1 | $450, ferro 8, piombo 8, titanio 8 |
| `conveyor-express` | titanio grezzo 1, silicio 1, grafite 1 | $850, titanio 8, silicio 8, grafite 8 |
| `conveyor-armored` | ferro 1, piombo 1, titanio 2, grafite 1 | $600, ferro 8, piombo 8, titanio 10, grafite 8 |
| `junction` | ferro 3 | $200, ferro 13 |
| `splitter` | ferro 3 | $280, ferro 13 |
| `sorter` | ferro 2, piombo 2 | $320, ferro 10, piombo 10 |
| `conveyor-bridge` (per estremo) | ferro 6, piombo 6 | $340, ferro 21, piombo 21 |
| `miner` | ferro grezzo 4 (era lastre di ferro 4) | invariato |
| `smelter` | ferro grezzo 6 (era lastre di ferro 6) | invariato |

Pesi di costruzione: ferro grezzo 0,5, piombo grezzo 0,7, titanio grezzo 1,0, grafite 1,0, silicio 0,8. Ricette delle macchine e altri edifici invariati. Campagna: i livelli devono poter costruire con quello che hanno o estraggono, senza toccare le mappe.

Applicazione nel task 2: tutti gli otto blocchi di logistica più minatore e forno, `unlock.materials` in `structures` e nella copia di `conveyors`; ferro grezzo aggiunto ai `startingMaterials` dei livelli 2-10 (10 nel livello 2, il cui obiettivo è accumulare 12 ferro grezzo; 20 negli altri; il livello 1 lo riceve già da `NewGame.CampaignFactory`); `MapTool valida` OK. Fuori elenco ma necessari: la sandbox parte con 48 ferro grezzo (senza, nessuno poteva costruire il primo minatore e tutorial e nuova partita restavano bloccati); `EnsureDemoBuildStock` rifornisce i grezzi, la grafite e il silicio per demo, self-test e playtest.

### Portata (task 3)

Velocità in celle/s, spaziatura 0,4, capacità 3; portata in oggetti/s = velocità / spaziatura.

| Nastro | Celle/s | Oggetti/s attesi | Misurati (linea satura di 12 celle nel Core) |
|---|---|---|---|
| base | 2,1 | 5,25 | 5,30 (finestra di 10 s, quantizzata) |
| veloce | 4,8 | 12 | 12,00 |
| rinforzato | 4,8 | 12 | 12,00 |
| espresso | 7,2 | 18 | 18,00 |

Giunzione 18 oggetti/s (capacità 2), splitter e sorter 8, ponte 5,25 (capacità 1). Le quattro direzioni danno lo stesso risultato. Sonda `oggetti-fluidi`: 400 fotogrammi, 133,9-134,7 px/s (rapporto 1,01), 6 passaggi di cella, 144 fps.

## Giri di revisione

- **T1** (sonnet): spec rispettata, qualità approvata. Tre minori parcheggiati: M1 il caso cantiere di `BusyCellsAreNeverTurnedOrReplaced` non passa mai dal guard di `TryGetSiteAt` (corretto nel T5); M2 il giro può voltare un nastro via dal Core (regola del Core spostata nella linea, T6/T8); M3 l'ultima cella scartata del trascinamento era riportata `Occupied` in `AgentGame` (sparito nel T6). Fuori piano: il playtest `ui` falliva già su `main` (scala 150, `ObjectivesPanel` sopra `TutorialCard`), passato a un agente di correzione a parte.
- **T4** (sonnet): approvato; tre minori corretti in `39ddc0d` (reset di `DroppedTicks`, aiuto puro `ItemMotion` con test, sonda con due passaggi). `AgentPlay GameSessionTakesAShot` falliva solo in locale (finestra 1084, risolto nella #225). La PR è andata in conflitto con `main` dopo #225/#226: merge `b666da6` e tolleranza sui float `7ee0cc4`. Segnalato per il T3: `BeltLane.Advance` tagliava il progresso a 1 perdendo il resto, e ogni oggetto si fermava un tick a ogni cella.
- **T5** (opus): spec rispettata, approvato; giro 1 (`38218fd`): risultato di `TryCharge`, fallimento di `RestoreReplacedBelt`, test su rimborso dispari, annullamento a 0, cantiere salvato e caricato, ADR. Parcheggiati: credito su cantieri scartati da un salvataggio non valido, doppio ciclo `IsPlannedTile`. Visto un flake: la sonda `oggetti-fluidi` con un passo indietro di -0,002 px, passa alla seconda (sistemata con la tolleranza).
- **T6** (sonnet): approvato; giro 1 (`fa98e0a`): incrocio accanto a una cella rifiutata, guard su `First()`, commento e diario superati, parità di `MaxCells`, documenti (ambito del replay, direzione pianificata). Residuo accettato: un incrocio accanto a una cella rifiutata solo dalla simulazione parte comunque (la linea non può saperlo). Parcheggiati: test che confronta `PlacementLine` con se stessa fino al T8, bias di `CellOf` nella sonda, segnaposto (T9).
- **T7** (sonnet): un Important, `FixedDirection` sul percorso A*; giro 1 (`fc1aaef`) con test e commento. Parcheggiato: dedupe di A* contro il caso d'angolo di `MaxCells`.
- **T8** (opus): servono correzioni. Important: un clic rifiutato non dava nessun toast (regola: un toast per un clic singolo; al rilascio senza nulla inviato, un toast); inoltre un elenco superato in ARCHITETTURA, la documentazione dell'aiuto e l'incrocio sopra una cella pianificata; verificati focus perso e rilascio sopra l'HUD. Giro 1: `1569868`.
- **T9** (sonnet): servono correzioni. Important: regola di annullamento col tasto destro; minori: accumulo della rotellina, test piano più blocco e cantieri, parole in ARCHITETTURA. Giro 1: `1f085a1`. Accettato: l'agente rifiuta più di 64 celle mentre il client taglia; scansioni lineari di `RemovalArea`, da tenere d'occhio su fabbriche grandi.
- **T10** (sonnet): approvato. Per il T3: oggetto sperduto una cella oltre una linea cieca (`World.Visuals.cs:393`, preesistente); asset morto `conveyor-armored-join-building-both-anim` con le sue strisce; test del nastro semplice di testa contro il rinforzato. Per il T2: costo e sblocco del rinforzato in grezzi.
- **T2** (sonnet): superato, approvato; la campagna è finibile (verificata a mano). Giro 1 (`5444e1e`): accredita le righe vecchie in lastre non corrispondenti nei cantieri caricati, test e CHANGELOG, scorte iniziali fissate, test onesto sulla campagna. Parcheggiato: quantità scritte a mano nei test di Builder e Construction.
- **T3** (opus): approvato; salvataggio e caricamento a metà corsa verificati con una sonda. Giro 1 (`529201d`): conversione della velocità nel riporto, `Fact` di salvataggio con nastri saturi, conservazione in giunzione, ponte e sorter, rifiuto frontale per tutti i nastri. Il test frontale del T10 ha rivelato un errore vero: un nastro semplice di testa entrava nel rinforzato e rimbalzava; `ArmoredAccepts` corretto. Parcheggiati: partenza asimmetrica dell'oggetto nuovo, disegno a metà velocità a fine linea, percorso `Feed`, test v12 rietichettato.
- **T11**: rivisto solo dal controllore (script solo per sviluppo e documenti, nessun cambio al gioco).
- **Finale** (opus, `1cffb0d..952235f`): si può unire, con correzioni. Nessun Critical. Un Important e sette minori. Punti di forza: una sola regola di linea per client e agenti; nastro sopra nastro coerente a ogni livello (`BeltOverBeltAt` come unica fonte); risorse conservate nella sostituzione; riporto deterministico (il `Carry` non si salva perché nasce e muore nel tick); rinforzato in un solo punto (`ArmoredAccepts`); salvataggio v13 con v11 e v12 ancora leggibili.
  - Important 1: `piazza {cella, direzione}` fissava la direzione anche per una cella sola e spegneva la regola del Core, quindi un agente otteneva un nastro che non scarica e il giocatore con lo stesso gesto sì.
  - Minori: 2 anteprima di Ctrl con A* a ogni fotogramma; 3 rettangolo diverso su cambio di tier in attesa o già iniziato; 4 `godot/README.md` senza il cambio di tier; 5 commento superato di `MayCross`; 6 codice morto (`JunctionLocked`, parametro `onlyMark`); 7 ponti non concatenati; 8 `tier: 2` dipendente dall'ordine dei contenuti.
- **Correzioni finali** (PR #247, `c2df568`, `be20427`, `f677de8`), rilette da opus: punti 1-6 e 8 risolti, nessun nuovo Critical o Important.
  - 1: `fixedWay` richiede ora un trascinamento; test `LoneBeltWithDirectionBesideTheCoreIsThePlayersClick` (stesso checksum del clic del giocatore).
  - 2: la cache dell'anteprima ha per chiave richiesta, un contatore degli ingressi e `Sites.Count`.
  - 3: il rettangolo dà lo stesso esito nei due stati (due `Remove`, smontaggio accodato su un cantiere già iniziato), regola in ADR 0003 e nei due README; test a due varianti.
  - 4, 5, 6: README, commento, codice morto tolto.
  - 8: `ConveyorOfTier` esclude il rinforzato; il rinforzato si chiede con `tipo: "conveyor-armored"`, scritto in `Tools.cs`, nel README di AgentPlay e nella skill.
  - 7: rinviato.

## Decisioni prese in autonomia

- **Decisione**: ordine 1, 4-10, poi 2 e 3 dopo l'OK dell'utente sulla tabella, poi 11. — l'utente ha autorizzato la corsa notturna salvo i task della tabella. — se sbagliata: il task 3 cambia i tempi dei playtest fissati dai task seguenti e le attese vanno rifatte.
- **Decisione**: il task 10 usa il costo del rinforzato proposto dal piano, in via provvisoria. — non bloccare il task sulla tabella. — se sbagliata: il task 2 riscrive una riga di dati.
- **Decisione**: fermare la corsa notturna a circa l'88-90% dell'uso settimanale. — l'utente vuole il 10% di riserva. — se sbagliata: il piano si ferma prima del dovuto.
- **Decisione dell'utente**: tabella dei costi con minerali grezzi, e minatore e forno in ferro grezzo. — senza grezzi nessuno costruirebbe il primo minatore, e senza un forno acceso non c'è lastra di ferro. — se sbagliata: una riga di dati per blocco e le scorte iniziali dei livelli.
- **Decisione**: un clic singolo rifiutato dà un toast; al rilascio senza nulla inviato, un toast. — la riga non deve restare muta (T8). — se sbagliata: ritocco ai toast del client.
- **Decisione**: rilasciare il mouse sopra l'HUD invia la linea. — Mindustry invia al rilascio. — se sbagliata: una riga per annullare.
- **Decisione**: l'agente rifiuta più di 64 celle (e 200 per il rettangolo) dove il client taglia. — un agente deve saperlo e non ricevere meno di quanto ha chiesto in silenzio. — se sbagliata: differenza visibile fra client e agenti.
- **Decisione**: un incrocio accanto a una cella rifiutata solo dalla simulazione parte comunque. — la linea non può conoscere i rifiuti della simulazione. — se sbagliata: un nastro mancante da correggere a mano.
- **Decisione**: il T11 lo rivede il solo controllore. — script di sviluppo e documenti, nessun cambio al gioco. — se sbagliata: un foglio di proposte con difetti, senza effetti sul gioco.
- **Decisione**: nel T3 l'ultima cella di una linea cieca è disegnata con gli oggetti compressi su mezza cella, non tagliati a 0,5. — con tre oggetti per cella un taglio metterebbe il secondo oltre la testa. — se sbagliata: oggetti disegnati più fitti su quella cella.
- **Decisione**: i salvataggi di sviluppo con un cantiere di sostituzione v13 anteriore alla tabella non si migrano. — non esiste una release dopo v0.3.4 (prima della base). — se sbagliata: crediti di righe in lastre senza equivalente.
- **Decisione**: il rinforzato si chiede con `tipo` e non con `tier: 2`. — i nastri di tier 2 sono due. — se sbagliata: confusione fra veloce e rinforzato per gli agenti.

## Problemi rinviati

- **Ponti automatici in catena** (minore 7 della revisione finale, `PlacementLine.cs:164`): `!entry.Spanned` vieta un secondo ponte che parta dall'uscita del primo; due ostacoli separati da una cella libera lasciano rosso il secondo. Funzione futura, non richiesta dalla specifica; in Mindustry i ponti si concatenano.
- **Minori residui della rilettura finale** (non portanti, già giudicati):
  - N1: il ramo nuovo di `BuilderSystem` (`BuilderSystem.cs:137-145`, piano di smontaggio in fondo alla coda con il cantiere ancora sulla cella) non ha test e serve solo con i droni di due giocatori; restringere la condizione a `site.Deconstructing && site.Replaced is not null` e aggiungere un test a due giocatori.
  - N2: il doppione di smontaggio si cerca solo nella coda del giocatore che manda il comando; senza danni.
  - N3: l'anteprima può restare ferma su un piano di smontaggio scartato in silenzio finché il mouse non si muove.
- **Minori dei task parcheggiati**: credito su cantieri scartati da un salvataggio non valido; partenza asimmetrica dell'oggetto nuovo; disegno a metà velocità a fine linea; percorso `Feed`; test v12 rietichettato; quantità scritte a mano nei test di Builder e Construction; scansioni lineari di `RemovalArea` su fabbriche grandi.
- **Nastri stretti: scelta in attesa dell'utente.** Il T11 ha prodotto una tavola (`tools/art/proposte_nastri_stretti.py`, fuori dal registro di `genera.py`, scrive in `.dev/arte/`) con tre varianti: A «binari sottili» (battistrada di 10 px, binari di 2 px), B «canale» (10 px, bordi scuri) e C «Mindustry» (12 px, piastre laterali di 4 px). Immagine nel ramo `pr-media` (`pr-media/pr-nastri-11/nastri-stretti.png`). Nessuna è applicata: tocca all'utente scegliere, poi si cambia lo script dell'arte e si rigenerano gli sprite.
- **Base delle catture da promuovere.** La base ufficiale dei playtest (`%TEMP%/baseline-g4`) è vecchia: contro di essa 85 catture `ui` su 94 e 9 su 9 del `tutorial` differiscono per ragioni estranee al T3 (costi, interfaccia). Servono il giudizio e l'OK dell'utente per promuovere una base nuova; fino ad allora il confronto si fa contro `main` prima del task.
- **Da pulire a mano**: il worktree `agent-ac721e0d6c6f6169b` (task 10) ha permessi rotti per un indice rimasto indietro; lo ripulisce l'utente.

## Verifiche finali

Dalla rilettura delle correzioni finali (`952235f..f677de8`) e dal rapporto di #247:

- `dotnet build tIndustry.sln -c Release`: 0 errori, 0 avvisi.
- `Shared.Tests`: 679 superati, 2 ignorati (`MeasureLargeMap`, `WritePreviews`), 0 falliti (676 prima delle correzioni).
- `AgentPlay.Tests`: 28 superati, 10 ignorati (quelli col gioco vero), 0 falliti.
- CI `build-publish.yml` lanciata a mano, verde su ogni PR (ultima 39356834399).
- Playtest Godot, `genera.py --controlla` e `MapTool valida` non rieseguiti nella revisione finale (verdi per task in CI secondo il registro; `valida` OK nel T2).
- Canvas rigenerato con `python tools/dev/mappa_canvas.py`.

## Da fare

- **L'utente sceglie** la variante dei nastri stretti (A, B o C) e dà l'OK per promuovere la nuova base delle catture.
- Ponti automatici in catena, se li si vuole.
- Test a due giocatori per il ramo di `BuilderSystem` (N1) e restringimento della sua condizione.
- Dopo questo piano resta in roadmap il generatore `biomi-2`.
