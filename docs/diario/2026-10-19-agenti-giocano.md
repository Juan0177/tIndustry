# Agenti che giocano

- **Specifica**: [2026-10-18-agenti-giocano-design](../superpowers/specs/2026-10-18-agenti-giocano-design.md)
- **Piano**: [2026-10-18-agenti-giocano](../superpowers/plans/2026-10-18-agenti-giocano.md)
- **ADR**: [0008 — Agenti che giocano: MCP e canale di controllo](../decisioni/0008-agenti-che-giocano.md)
- **Periodo**: 2026-10-18 e 2026-10-19
- **Esito**: in `main` con le PR [#210](https://github.com/Juan0177/tIndustry/pull/210), [#211](https://github.com/Juan0177/tIndustry/pull/211), [#212](https://github.com/Juan0177/tIndustry/pull/212), [#213](https://github.com/Juan0177/tIndustry/pull/213), [#214](https://github.com/Juan0177/tIndustry/pull/214) (i cinque task) e [#217](https://github.com/Juan0177/tIndustry/pull/217) (ondata di correzioni dopo il primo uso e la revisione finale). L'arte dell'unione laterale dei nastri è nella [#216](https://github.com/Juan0177/tIndustry/pull/216), ancora aperta.

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-10-18-agenti-giocano/progress.md`), dai rapporti e dalle revisioni dei task (`task-N-report.md`, `task-N-review.md`, `task-N-rereview.md`), dal rapporto del primo uso (`primo-uso-report.md`), dalla revisione finale (`review-finale.md`), dal rapporto dell'ondata di correzioni (`fix-finale-report.md`) e dalla sua rilettura (`fix-finale-rereview.md`), più la storia git (`c7fdf66..d380a72`). Dove le fonti non dicono qualcosa qui non è inventato.

## Obiettivo

Permettere agli agenti AI (Claude Code e i suoi sotto-agenti) di **giocare davvero** a tIndustry: vedere lo stato, dare comandi, usare l'interfaccia, far scorrere il tempo, fare catture e rendere riproducibile ogni bug trovato. Un server MCP (`tools/AgentPlay`) con due motori dietro la stessa superficie, la simulazione pura e il gioco vero comandato da un canale di sviluppo (`AgentBridge`). Scelte e motivi nell'[ADR 0008](../decisioni/0008-agenti-che-giocano.md).

## Task, commit e PR

Un branch per task, ciascuno fuso in `main` a revisione pulita e CI verde. Esecuzione guidata da un controllore che lancia gli implementatori (opus), i revisori (opus per i task 1–4 e per le revisioni finali, sonnet per il task 5 e per le riletture). La base del piano è `c7fdf66`.

| Task | Contenuto | Commit | PR |
|---|---|---|---|
| 1 | Nucleo comune in `Shared` (`AgentGame`, diario, protocollo) e motore simulazione | `dd73443`, `d6a68e2`, `8079c97` | [#210](https://github.com/Juan0177/tIndustry/pull/210) |
| 2 | Server MCP sul motore simulazione, lanciatore `tools/AgentPlay/avvia.py` | `b519b6a`, `8ee5b22`, `85ee75a` | [#211](https://github.com/Juan0177/tIndustry/pull/211) |
| 3 | Canale di controllo nel gioco (`AgentBridge`), playtest `agenti` | `ae908fe`, `f54fbbf`, `d67af2b` | [#212](https://github.com/Juan0177/tIndustry/pull/212) |
| 4 | Motore gioco vero (processo Godot, job object, diari spostati nel profilo) | `bae6a0b`, `7fd00a1` | [#213](https://github.com/Juan0177/tIndustry/pull/213) |
| 5 | Documenti (ADR 0008, ARCHITETTURA, CLAUDE.md, README di AgentPlay, skill `gioca-e-testa`, CHANGELOG, canvas) | `fa747e5` | [#214](https://github.com/Juan0177/tIndustry/pull/214) |
| Primo uso | Un agente gioca via MCP: 8 bug e 13 attriti | — | — |
| Ondata finale | Correzioni di primo uso e revisione finale | `fdd4e38`, `44569d6`, `54b3fea`, `295376c` | [#217](https://github.com/Juan0177/tIndustry/pull/217) |

Specifica e piano erano arrivati con le PR [#206](https://github.com/Juan0177/tIndustry/pull/206) e [#208](https://github.com/Juan0177/tIndustry/pull/208). Nel frattempo, fuori piano ma sugli stessi file, la [#209](https://github.com/Juan0177/tIndustry/pull/209) (unione a T e incrocio automatico dei nastri) è stata riletta con un giro di revisione (opus: 3 Important e 8 Minor, tutti corretti; rilettura sonnet pulita, residuo: avvisi ripetuti di risorse insufficienti, passati al piano nastri-mindustry) e unita.

CI `build-publish.yml` lanciata a mano e verde su ogni task: T1 39158582107, correzioni 39159798492, 39160244748; T2 39161398957, correzioni 39162524097; T3 39164335314, correzioni 39166690870; T4 39169173123, correzioni 39170719075; T5 39172129578 (al secondo tentativo, vedi «Problemi rinviati»); ondata finale 39177501841.

Test: 412 in `Shared` al T1, 423 dopo le correzioni; 465 al T3 e 479 dopo le correzioni, più 11 test AgentPlay; 21 test AgentPlay in locale al T4 (4 di Godot saltati in CI, che non ha Godot); 490 in `Shared` (2 ignorati) all'ondata finale.

## Giri di revisione

- **T1** — spec non rispettata (opus): mancavano gli obiettivi in `stato` e la forma dei nastri in `area`; 5 Important e 8 Minor. **Giro 1** (`d6a68e2`): tutto, più un catch-all `ErroreInterno`, la cartella dei diari, l'intestazione del diario, l'opzione `tickPerComando`. Rilettura (sonnet): tutto risolto salvo un Important (la modalità del tick non era nell'intestazione del diario) e quattro Minor. **Giro 2** (`8079c97`): accettato senza una nuova rilettura, perché piccolo e coperto da test (tranne il rilancio in `Replay`, letto dal controllore).
- **T2** — conforme, «Needs fixes» (opus): 3 Important e 10 Minor. **Giro 1** (`8ee5b22`): compilazione del lanciatore in una cartella temporanea per hash, errori di collegamento in italiano, lettore dei salvataggi in `Shared`, pulizia delle cartelle vecchie. Rilettura (sonnet): tutto risolto; il controllore ha corretto a mano due Minor del lanciatore (messaggio senza `dotnet`, gara nella pulizia).
- **T3** — conforme, «Needs fixes» (opus, ripreso dopo il riavvio del PC chiesto dall'utente): 3 Important (percorso delle catture, `ShouldStart` senza profilo temporaneo, accesso da browser via `fetch`: aggiunti segreto e chiusura alla riga HTTP) e 11 Minor. **Giro 1** (`f54fbbf`): anche gli eventi sintetici di tasto riconoscibili (`World.IsSyntheticInput`) e il saluto `{canale:tindustry}`. Rilettura (sonnet): tutto risolto; tempo limite di lettura del segreto (5 s) aggiunto dal controllore.
- **T4** — conforme con 3 lacune rispetto alla specifica (opus): 2 Important (Godot orfano, `chiudi` bloccata da `esegui`) e 9 Minor. **Giro 1** (`7fd00a1`): job object con chiusura dei figli, uscita del gioco quando cade il lanciatore, generatore e salvataggio copiati nel profilo, richiesta `esci` tolta. Rilettura (sonnet): tutto risolto; quattro Minor parcheggiati (cartella dei diari creata prima del controllo dei dati veri, perdita se `StandardInput.Close` lancia, `Close` che attende 5 s se mai connesso, una gara teorica).
- **T5** — spec rispettata, qualità approvata (sonnet); cinque Minor parcheggiati (`RequireImported` controlla solo che esista la DLL, due punti non provati, il vuoto nella numerazione degli ADR, il costruttore di `GameEngine.Open` fuori dal `try`, l'espansione di `.mcp.json` da verificare nel primo uso).

## Primo uso reale

Il passo di verifica del piano stesso: un agente (opus, in un worktree) ha giocato via MCP con un client stdio scritto a mano, in simulazione (misura 64, seme 1) e nel gioco vero, e ha riportato **8 bug e 13 attriti** (nessun bug grave: 4 medi e 4 bassi).

- **Bug medi**: (1) `input trascina` con `verso` a oggetto rifiutato, perché la validazione entrava in ogni valore oggetto anche senza `properties`; (2) `piazza` non faceva l'incrocio automatico che invece nasce trascinando; (3) Esc due volte in partita chiudeva il gioco senza conferma; (4) dopo Continua il diario ripartiva vuoto con l'intestazione del sandbox, e `rigioca` dava `ok: true` con una mappa vuota.
- **Bug bassi**: (5) `stato.direzione` e `ruota` non erano la direzione del client; (6) `ui` elencava controlli nascosti, sovrapposti o coperti dall'oscuramento della Home e non la palette; (7) toast «Nuova partita · 64 64×64»; (8) l'importazione headless di Godot termina con il codice 139 (ambiente, non nostro).
- **Attriti**: nessun evento per l'input reale di chi è alla finestra; nessun catalogo e nessun «cosa manca» nei rifiuti; `area` senza gli oggetti sui nastri; nomi a percorso fragili in `ui`; `costruito`/`smontato` senza cella; nessuna impronta in `stato`; diari con voci non rigiocabili senza avviso; nomi poco chiari (`ricerca`, `ingresso`, `obiettivi`); `forme` con ogni nastro dritto; tick dei rifiuti sfasato di uno; barre dei percorsi; schemi senza `type` e ordine instabile; fantasma rosso nelle catture.
- **Cosa ha funzionato**: avvio in pochi secondi, messaggi di errore dei parametri ottimi, codici di rifiuto giusti, `diario` + `rigioca` identici in simulazione, celle e schermo coincidenti, incrocio automatico e unione laterale disegnati bene, nessun processo Godot rimasto dopo `chiudi`, 0 ERROR nel log.

## Revisione finale e ondata di correzioni

**Revisione finale** (opus, sull'intero ramo `c7fdf66..eecbab0`): **si può unire, con correzioni**. Nessun Critical. Punti di forza: il ponte non si attiva per sbaglio (build di debug, porta, segreto e profilo fuori dai dati veri; l'export della CI è `--export-release`), canale ben difeso, ciclo di vita dei processi curato, `AgentGame` come unica superficie per i due motori, determinismo.

- **Important 1**: `rigioca` di un diario del gioco vero si fermava all'ultimo comando e perdeva il tempo trascorso dopo, proprio nel flusso principale «trovo un bug nel gioco e lo riproduco».
- **Important 2**: il diario viveva quanto l'istanza `AgentGame` del ponte; il bug 4 era solo un sintomo, perché lo stesso succedeva con Nuova partita, un livello, uno slot e dopo un comando scaduto.
- **Minor**: `GiocoTerminato` usato dove la sessione resta viva; una riga oltre 64 KiB faceva chiudere il gioco; `MoveJournal` leggeva e cancellava un percorso arrivato dal canale senza controllarlo; la pulizia all'avvio poteva cancellare la sessione viva di un altro server; il limite di `esegui` (120 s) non era nel contratto.
- **Cause più larghe dei bug del primo uso**: il bug 2 nasce dal fatto che la macchina a stati del trascinamento viveva solo nel client; il bug 6 non dipendeva da `IsVisibleInTree` (già usato) ma dalla trasparenza, dall'oscuramento della Home e dalla posizione della palette.

**Ondata di correzioni** (PR [#217](https://github.com/Juan0177/tIndustry/pull/217), 4 commit, opus): le correzioni A–J, più gli attriti scelti.

- Diario con tick finale (`fine`) e `rigioca` fino a lì; diario che segue la partita (intestazione dall'apertura del lanciatore o da un'istantanea; evento `partitaCaricata`; `rigiocabile: false` se l'istantanea fallisce).
- Codici `PartitaCambiata` e `MotoreNonDisponibile`; righe oltre 64 KiB rifiutate; `IsInside(profilo)` per `MoveJournal`; file-lucchetto `.in-uso` contro la pulizia di sessioni vive; `esegui` fino a 120 s nel contratto.
- `Arguments` entra in un oggetto solo se lo schema ha `properties`.
- Incrocio condiviso: `BeltDrag` in `Shared`, usato da `World.Placement` e da `AgentGame.Piazza`; l'ultima cella trattenuta è scartata come nel client e riportata come `Occupied`.
- Home con «Riprendi» in partita (al posto di «Continua»), Esc riprende; `ruota` e `stato.direzione` collegati al pennello del client; `ui` senza controlli nascosti, coperti o doppi, con nomi stabili e ricerca per testo o suggerimento; toast con misura numerica corretto; segfault 139 documentato.
- Attriti: evento `inputReale`, `mancano` nei rifiuti e nell'errore del catalogo, `area {oggetti}`, `costruito`/`smontato` con cella e struttura, `impronta` in `stato`, voci non rigiocabili con `avvisi`, tick dei rifiuti coerente con il diario, `type` negli schemi e ordine stabile.

**Rilettura** (opus, `39789c8..295376c`): tutte le voci A–J e gli attriti scelti sono corretti, senza nuovi Critical o Important; riletti anche determinismo, sicurezza del ponte, incrocio condiviso, `testi.csv` e stili. Restano solo i minori parcheggiati in «Problemi rinviati».

## Decisioni prese in autonomia

- **Decisione**: la funzione di trascinamento si porta in `Shared` come funzione pura, ma il client tiene il suo `DragCells` finché non è unita la #209; il passaggio del client alla funzione condivisa spetta al piano nastri-mindustry. — due branch toccavano `World.Placement.cs`. — se sbagliata: due implementazioni del trascinamento per un po'. (Poi superata, vedi la decisione sul bug 2.)
- **Decisione**: nel motore simulazione ogni comando fa un tick, così la risposta porta l'esito (il diario lo registra); nel gioco vero i comandi sono accodati e la risposta attende l'esito del tick del frame successivo, senza tick in più. — l'agente deve vedere l'esito di ogni comando, ma il gioco vero non deve avanzare da solo. — se sbagliata: avanzamenti di tempo che sorprendono l'agente.
- **Decisione**: `ruota` gira il pennello dell'agente perché in `SimHost` non esiste un comando di rotazione, in attesa della rotazione sul posto del piano nastri. — niente invenzioni nella simulazione per un comodo degli agenti. — se sbagliata: costo solo documentale (poi, nel gioco vero, `ruota` è collegato alla direzione del client, vedi la correzione G).
- **Decisione**: per la #209, regole come Mindustry: il nastro dritto è alimentato e continuato solo dalla logistica, il trascinamento segue l'asse del pennello con un predecessore sulla stessa linea, la giunzione non è più lenta del nastro rapido. — evitare incroci automatici che rompono i flussi non logistici e strozzature di portata. — se sbagliata: ritocco alle regole della #209.
- **Decisione**: il lanciatore è uno script Python (`tools/AgentPlay/avvia.py`) che compila nella cartella temporanea per hash. — Python serve già a `tools/art`. — se sbagliata: riscrivere il lanciatore in un altro linguaggio.
- **Decisione**: si copia nel profilo temporaneo anche il generatore e il salvataggio da caricare, e un crash del gioco chiude la sessione; job object con chiusura dei figli e uscita del gioco quando il lanciatore cade. — nessun processo Godot orfano e nessun accesso ai dati veri. — se sbagliata: gioco o processi rimasti a mano.
- **Decisione**: il buco tra l'ADR 0006 e l'ADR 0008 (manca lo 0007) è voluto e non si corregge. — il numero 0007 è riservato alla specifica di `biomi-2`. — se sbagliata: una rinumerazione degli ADR.
- **Decisione**: una sola ondata di correzioni copre i punti 1–7 della revisione finale, i bug 1–7 del primo uso e gli attriti scelti. — il primo uso è il passo di verifica del piano stesso. — se sbagliata: una PR più grande da rileggere.
- **Decisione**: il bug 2 si corregge spostando la decisione sull'incrocio del client in un'unica funzione in `Shared` (`BeltDrag`), usata da client e `AgentGame`, anziché copiarla nell'agente. — la specifica dei nastri la vuole comunque in `Shared` e il futuro co-op la riusa. — se sbagliata: tocca `World.Placement.cs`, che il piano nastri tocca di nuovo.
- **Decisione**: nel gioco vero il diario ha `tickPerComando: false`, scritto dal ponte, e dopo un timeout riparte da un'istantanea con un avviso, perdendo le voci di prima. — l'intestazione la conosce solo il ponte; è equivalente (impronte uguali). — se sbagliata: dopo un timeout la riproduzione può divergere, e l'avviso lo dice.
- **Decisione**: Esc non esce più dal gioco in nessun caso, e nella Home iniziale non fa nulla. — «Esc non esce mai senza passare da un pulsante». — se sbagliata: una riga di codice in `HomePanel` (vedi «Problemi rinviati»).
- **Decisione dell'utente**: la forma «unione laterale» dei nastri (dietro più lato, da nastro o edificio) ha un agente a parte (`fix/nastri-unione-laterale`, PR #216). — un lavoro d'arte che non va nell'ondata di correzioni degli agenti. — se sbagliata: due PR da riconciliare in `BeltShape`.
- **Decisione dell'utente**: attraversare un nastro perpendicolare senza giunzione sbloccata diventa un'unione, regola scritta nella specifica dei nastri ([#215](https://github.com/Juan0177/tIndustry/pull/215)); anche il nastro rinforzato, come l'armored conveyor di Mindustry ([#218](https://github.com/Juan0177/tIndustry/pull/218)). — decisioni di gioco, non degli agenti. — se sbagliata: riscrivere due paragrafi di specifica.

## Problemi rinviati

- **Esc nella Home iniziale non fa nulla** (minore, `godot/scripts/ui/panels/HomePanel.cs`): il brief aveva due regole in contrasto. L'utente ha deciso il 2026-10-19 che Esc nella Home iniziale chiede conferma prima di uscire (ramo `fix/home-esc-conferma`).
- **`SyncGame` tiene un `_game` staccato** (`godot/scripts/dev/AgentBridge.cs`, circa riga 416): con `_world.AgentHost` nullo dopo un primo caricamento fallito (`DetachGame`) i comandi andrebbero all'host staccato. Correzione indicata: `Dispose` e `_game = null`.
- **`Reachable` ignora le `Window` popup** (`AgentBridge.cs`, circa riga 1137): con un dialogo aperto i controlli dell'HUD sotto restano elencati come raggiungibili; copre solo i modali `Window`, non i `ColorRect`.
- **Messaggio «'secondi' tra 0 e 120»** (`GameEngine.cs`, `AgentBridge.cs`): lo 0 è rifiutato; la descrizione dello strumento dice giustamente «più di 0».
- **Nessun test diretto per `GameEngine.IsInside`** (`..`, prefissi simili, percorso relativo): la logica è semplice e giusta a una lettura, ma è un controllo di sicurezza.
- **Minori dei task, parcheggiati**: i quattro del T4 (cartella dei diari creata prima del controllo dei dati veri, perdita se `StandardInput.Close` lancia, `Close` che attende 5 s se mai connesso, una gara teorica) e i cinque del T5 (vedi sopra); accettato il rischio di un server vivo oltre 7 giorni per la compilazione di `avvia.py`.
- **`BeltCornerTests.CornerQueriesStayCheapOnABigFactory` instabile**: prova di tempo in `Shared` che ha fatto fallire la CI del T5 al primo tentativo e passa al secondo; non toccata da questo piano, da rendere robusta.
- **Arte dell'unione laterale dei nastri**: la [#216](https://github.com/Juan0177/tIndustry/pull/216) (`fix/nastri-unione-laterale`) è aperta e in attesa del giudizio dell'utente sull'arte («da ritoccare»). La sua rilettura (sonnet) l'ha trovata conforme, con un Important: `AgentGame.BeltShape` riporta le unioni e le T come dritte. In più va riconciliata con la #217 su `BeltShape` (la #217 restituisce null per il dritto; vanno tenuti la sua struttura e i nomi italiani della #216, con una sola grafia per la T), su `AreaShowsTerrainAndBeltShapes` e probabilmente su `CHANGELOG.md`, `docs/ARCHITETTURA.md` e `World.Playtest.cs`.
- **Incrocio senza giunzione e nastro rinforzato**: spostati nella specifica dei nastri ([#215](https://github.com/Juan0177/tIndustry/pull/215), [#218](https://github.com/Juan0177/tIndustry/pull/218)) e da fare nel piano nastri-mindustry, che in `Shared` ricomprende anche il ponte automatico.
- **Con il generatore `biomi-2`**: l'intestazione del diario ha sempre il Core al centro; quando arriva la partenza casuale dovrà portare la posizione del Core.
- **`stato.rifiuti`** riparte vuoto dopo un cambio di partita (coerente con «partita nuova») e `Collect` non scende nei figli `CanvasLayer` di `UiRoot`: noti, senza effetti oggi.

## Verifiche finali

Dai rapporti dell'ondata finale e della sua rilettura:

- Build: 0 errori e 0 avvisi, Godot compreso.
- Test: `Shared` 490 superati (2 ignorati); la rilettura ha rifatto girare `BeltDragTests|AgentGameTests|AgentBridge` (56 ok) e `AgentPlayServerTests` (13 ok). `test_genera` OK.
- Playtest `ui` (con la prova dell'incrocio), `tutorial` e `agenti`: PLAYTEST OK, 0 ERROR. Nel gioco vero: bug 1 e 2 provati da MCP, `rigioca` con la stessa impronta di `stato` dopo Nuova partita. Il caso Continua non si è potuto provare dal vivo (dopo la correzione del bug 3 nella Home in partita non c'è più «Continua»), ma passa dallo stesso percorso di ogni cambio di host.
- Non confrontate le catture dei playtest con la base di `main` (`compare_shots.py`): cambiano solo i nomi dei nodi e il `RemoveChild` della palette, nessun cambio grafico atteso.
- CI `build-publish.yml` lanciata a mano, verde (ultima esecuzione 39177501841); catture su `pr-media/agenti-fix-finale/`.
- Canvas rigenerato con `python tools/dev/mappa_canvas.py`.

## Da fare

- **L'utente giudica** l'arte dell'unione laterale (#216), poi riconciliazione di `BeltShape` con la #217.
- Piano **nastri-mindustry**: trascinamento in linea per tutti i blocchi, ponte automatico, rotazione sul posto, incrocio senza giunzione, nastro rinforzato.
- Correzione dei minori del ponte (N2, N3, N4) e del test di `IsInside`, quando passa un giro su `AgentBridge`.
- Dopo questo piano resta in roadmap il generatore `biomi-2`.
