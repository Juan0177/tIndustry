# Sotto-progetto 3 — Costruzione come Mindustry

- **Specifica**: [2026-10-17-costruzione-in-corso-design](../superpowers/specs/2026-10-17-costruzione-in-corso-design.md)
- **Piano**: [2026-10-17-costruzione-in-corso](../superpowers/plans/2026-10-17-costruzione-in-corso.md)
- **ADR**: [0003 — Costruzione come Mindustry](../decisioni/0003-costruzione-come-mindustry.md)
- **Periodo**: 2026-10-17 → 2026-10-18
- **Esito**: in `main` con le PR [#159](https://github.com/Juan0177/tIndustry/pull/159), [#160](https://github.com/Juan0177/tIndustry/pull/160), [#161](https://github.com/Juan0177/tIndustry/pull/161), [#162](https://github.com/Juan0177/tIndustry/pull/162), [#164](https://github.com/Juan0177/tIndustry/pull/164) e [#166](https://github.com/Juan0177/tIndustry/pull/166).

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-10-17-costruzione-in-corso/progress.md`), dalle revisioni finali, dal rapporto della correzione finale, dal rapporto della QA esplorativa e dalla storia git (`a416d05..HEAD`). Dove le fonti non dicono qualcosa qui non è inventato.

## Obiettivo

Costruire come in Mindustry: il giocatore pianifica, un drone porta i materiali dal Core e il blocco compare man mano in un cantiere; lo smontaggio è un cantiere al contrario con rimborso graduale. Regole in `GameRules` (`InstantBuild`, costo, percentuale di rimborso), salvataggi alla versione 11 con regole, cantieri e droni, grafica di piani, cantieri e drone, coda nell'HUD, tutorial aggiornato. Dettagli e scelte in [ADR 0003](../decisioni/0003-costruzione-come-mindustry.md).

## Task, commit e PR

Ogni task ha il suo branch: `feat/sp3-1-cantieri` (da `docs/sp3-spec`, così specifica e piano viaggiano con la PR 1), `feat/sp3-2-drone`, `feat/sp3-3-salvataggi`, `feat/sp3-4-gioco` (T4 + T5), `feat/sp3-5-docs` (T6, poi la serie finale).

| Task | Contenuto | Commit | PR |
|---|---|---|---|
| 0 | Base degli screenshot (`baseline-sp3` ui e tutorial), nessun codice; 108 test verdi | — | — |
| 1 | Regole di costruzione, dati, cantiere (`Construction`, `ConstructSite`, `GameRules`) | `3acf355`, correzioni `b5728ef` | [#159](https://github.com/Juan0177/tIndustry/pull/159) |
| 2 | Drone, piani di costruzione, comandi | `8dd33f8`, correzioni `30a071c` | [#160](https://github.com/Juan0177/tIndustry/pull/160) |
| 3 | Salvataggi v11 con cantieri e droni | `3233963` | [#161](https://github.com/Juan0177/tIndustry/pull/161) |
| 4 | Grafica di piani, cantieri, drone e raggio | `61b1a48` | [#164](https://github.com/Juan0177/tIndustry/pull/164) (si apre con il Task 5) |
| 5 | Costruzione vera nel gioco, coda nell'HUD, tooltip, tutorial | `3ab983a`, correzioni `7f38e8a`, `b992add` | [#164](https://github.com/Juan0177/tIndustry/pull/164) |
| 6 | Documentazione: spec approvata con link incrociati, ADR 0003, ARCHITETTURA, README | `090ae56`, `ff976fa`, `098b7c5` | [#166](https://github.com/Juan0177/tIndustry/pull/166) |
| Fix finali | Correzioni da revisione finale e QA, poi ritocchi del coordinatore | `ce757c3`, `448f0a3`, `8c95b2c`, `36acaf6` | [#166](https://github.com/Juan0177/tIndustry/pull/166) |
| Incidente | Protezioni sui dati reali dei playtest | `20b6f82`, `829e985`, `be141cf` | [#162](https://github.com/Juan0177/tIndustry/pull/162) |

Nello stesso periodo sono arrivate in `main` anche [#158](https://github.com/Juan0177/tIndustry/pull/158) (link incrociati fra piani, spec, ADR e diario), [#163](https://github.com/Juan0177/tIndustry/pull/163) e [#165](https://github.com/Juan0177/tIndustry/pull/165) (README rinnovato, poi senza icone emoji): non fanno parte del sotto-progetto e sono state riviste nelle loro PR.

## Giri di revisione

Modelli: T1, T2 e T4 con opus, T3, T5 e T6 con sonnet nel piano; poi, con un budget più ampio concesso dall'utente, T5 e T6 e tutte le revisioni restanti su opus. Revisori opus per T1, T2 e T4, sonnet per gli altri; revisione finale con opus.

- **T1** — revisione: specifica non rispettata, 2 Important. (1) l'annullamento rimborsava tutto al primo tick; (2) una spec non valida (nastro sconosciuto, ricetta errata) passava `CanPlace` e faceva lanciare `Advance` al completamento dopo aver consumato le risorse. Giro di correzione 1 (`b5728ef`): rimborso graduale, `IsValidSpec` dentro `CanPlace`, un solo floor nel rimborso, `BuildCost` da `site.Cost`, test della riga di denaro; `RefundPercent` rinominato `RefundBasisPoints`. Rilettura (sonnet): 6 su 6 risolti. CI verde, 130 test.
- **T2** — revisione: specifica non rispettata, 1 Important di prodotto (un ponte spezzato dal lato di uscita lasciava il drone fuori portata dal centro del cantiere: blocco permanente) e 1 di processo (i test scritti dopo il codice). Giro 1 (`30a071c`): drone entro portata del cantiere, `Plans`, `Factory.Builders` e `Sites` in sola lettura, guardia se il cantiere corrente è sparito, test di checksum che verifica che la costruzione sia avvenuta. Rilettura (sonnet): 5 su 5 risolti. 150 test.
- **T3** — revisione (sonnet): approvato al primo giro.
- **T4** — revisione (opus): approvato, screenshot conformi alla specifica.
- **T5** — revisione (opus): Important, il trascinamento di rimozione toglieva il segno a un piano di smontaggio quando ripassava su una cella (si ricordava solo l'ultima). Giro 1 (`7f38e8a`): 4 su 4 corretti nel prodotto, ma la rilettura ha trovato un nuovo Important: il passo di playtest sul trascinamento dipendeva dal frame rate (passava a 239 Hz, falliva a 60 Hz perché lo smontaggio del minatore finiva prima del controllo). Giro 2 (`b992add`): prova rosso→verde a 60 fps; rilettura (sonnet) senza nuovi problemi.
- **T6** — revisione (opus): conforme alla specifica, qualità approvata, 0 Critical, 0 Important, 6 Minor, rinviati alla serie finale.

## Incidente: salvataggio reale sovrascritto e PR #162

Durante il Task 2 si è visto che `%LOCALAPPDATA%\tIndustry\godot-saves\continua.json` era stato sovrascritto alle 22:55:15 da una partita avviata senza `--log-file` (il log del motore era nel profilo reale). Nessun comando degli agenti di questa sessione risultava coinvolto e `dev.sh --playtest` è stato verificato sicuro (dati invariati). Il vecchio `continua.json` (v10 delle 21:22, dell'utente) risultava perso: l'incidente è stato segnalato e l'indagine proposta come sessione a parte.

Poi l'utente ha confermato di aver avviato il gioco a mano, alle 22:54 e alle 23:45, con il profilo reale, come previsto: nessun agente aveva toccato i dati reali. La [#162](https://github.com/Juan0177/tIndustry/pull/162) ha comunque aggiunto una protezione contro le scritture dei playtest fuori dal profilo isolato, un backup `continua.prev.json` e `user://` di Godot isolato nel profilo temporaneo (`APPDATA` puntato al profilo). Il riferimento delle verifiche sui dati reali è stato aggiornato allo stato dopo l'incidente, conservando l'originale a parte.

## QA esplorativa

Un agente (opus) ha provato `2801a3f` in un worktree separato, con Godot 4.4.1 mono, uno scenario di playtest temporaneo con input sintetici, sempre su profilo temporaneo. Nessun ERROR o eccezione nei log; il playtest `tutorial` passa. Risultato: **0 Critical, 1 Important, 4 Minor, 7 osservazioni UX**.

- **B1 (Important)**: due trascinamenti di smontaggio sovrapposti si annullavano a vicenda nella sovrapposizione.
- **B2**: su un blocco 2×2 o un ponte il segno si toglieva solo dalla cella cliccata la prima volta.
- **B3**: il tooltip «Mancano» restava visibile con il mouse sull'HUD, dopo zoom o spostamento della telecamera.
- **B4**: nastro pianificato accanto al Core: il fantasma indicava una direzione, il blocco finito un'altra.
- **B5** (già prima di SP3): un trascinamento veloce lasciava buchi (9 piani su 40 celle).
- **UX**: U1 un cantiere fermo blocca tutta la coda; U2 il riquadro non segnala cosa manca; U3 il segno di smontaggio è poco visibile; U4 non si può piazzare sopra i propri piani e la direzione dei nastri è quella di R; U5 `PlanDropped` irraggiungibile in giocatore singolo; U6 l'uscita del ponte fantasma si ricalcola a ogni disegno; U7 deriva della documentazione (CLAUDE.md v9, ARCHITETTURA v10), sistemata dalla #166.
- **Verificato e funzionante**: trascinamento di 40 nastri con 10 piastre, annullamento con rimborso 100%, smontaggio di nastri con oggetti, forno e ponte, F6/F7 e Continua a metà costruzione identici, blocco bloccato dalla ricerca, rimborso di campagna (livello 1 = 4, livello 4 = 2), 184 piani con FPS minimo 181 e medio 203 e memoria e nodi stabili, clic sulla coda che centra la telecamera, tutorial chiaro.

Il rapporto non poteva essere scritto dal sotto-agente: l'ha salvato il coordinatore. Le catture a «960x720» dei task erano in realtà 960x540.

## Revisione finale e serie di correzioni

La revisione finale (opus, `a416d05..098b7c5`, in un worktree a parte: build 0/0, 177 test superati e 1 ignorato) ha dato **0 Critical, 1 Important, 10 Minor** e il parere «con correzioni». Punti di forza: nucleo di simulazione piccolo e corretto (prelievo con ceil, rimborso a un solo floor, nessun portafoglio negativo né doppio rimborso), determinismo (cantieri e droni in ordine di id, nessuna iterazione di `Dictionary`/`HashSet` in `Step()`), formato v11 solido, documentazione accurata.

- **Important (I1, coincide con B2)**: l'annullamento di un segno di smontaggio dipendeva dalla cella cliccata del blocco.
- **Minor**: valori ripristinati non validati (un cantiere senza `Tiles` mandava in errore il client a ogni frame, una spec non più valida faceva lanciare `Step()`); `Spent`/`Refunded` ripristinati per indice contro un costo ricalcolato; array pubblici su `ConstructSite`; tooltip sopra l'HUD; `CanPlace` con motivo sbagliato per un ingombro che tocca il Core; `InstantBuild` e rimborso della regola; rotazione del drone da `Atan2` nell'istantanea (rinvio al SP5); prestazioni client con centinaia di piani; commento obsoleto; frase dell'ADR su `SeedCoreDeliveryLine`.
- Il triage dei rinvii dei task ha promosso a «da correggere subito» anche i Minor del T6 (testo dei controlli nel README, array `buildings[]`/`conveyors[]` in ARCHITETTURA, dicitura «partita normale», «la più limitante», annullamento da qualsiasi cella).

Serie di correzioni unica (opus, base `098b7c5`, tre commit), con la QA nello stesso giro:

- `ce757c3` (simulazione e test): annullamento da qualunque cella del blocco (I1/B2); nuovo campo `Remove(…, OnlyMark)` perché un trascinamento di smontaggio non tolga mai un segno (B1); U1, un piano fermo passa in fondo alla coda dopo 60 tick se ce ne sono altri (`StalledTicks` salvato come campo additivo); B4, nastro accanto al Core con la direzione finale già nel piano; validazione dei valori ripristinati (`Factory.RestoreSite`); `Spent`/`Refunded` salvati per oggetto con ripiego sull'indice, **senza alzare la versione**; array interni con liste di sola lettura; motivo OnCore per ogni cella dell'ingombro; asserzioni sul ponte in `PlanTiles`.
- `448f0a3` (client e playtest): `SyncHover` ricalcola il passaggio del mouse a ogni frame e nasconde il tooltip sopra l'HUD (B3); `DragCells` con linea di Bresenham (B5); riquadro della coda con barra ambra e «Manca: …» (U2); riempimento rosso traslucido dei segni (U3); trascinamento destro che segna e clic singolo che alterna; secondo trascinamento nel playtest `ui`.
- `8c95b2c` (documenti): ADR 0003, ARCHITETTURA, README del gioco, CHANGELOG, mappa Obsidian senza emoji.

Rilettura (opus): **19 su 19 risolti, 0 Critical/Important**, 2 nuovi Minor; le preoccupazioni 2, 3, 4, 5 e 7 dell'implementatore sono accettabili. TDD: 13 test rossi prima dell'implementazione, 189 verdi dopo; CI `build-publish.yml` (esecuzione 39078476479) verde.

### Ultimo commit del coordinatore: `36acaf6`

Il coordinatore ha corretto di persona due punti piccoli, verificati (189 test, playtest `ui`/`tutorial` OK, nessun file dei dati reali più recente):

- nuovo Minor 1 della rilettura: `DragCells` ora cammina a 4 connessioni (il Bresenham diagonale riempiva a 8 connessioni, con nastri che si toccano solo agli spigoli);
- preoccupazione 1: `godot4 --import` e `--build-solutions` in `dev.sh` e nei comandi di `CLAUDE.md` con `APPDATA` temporaneo.

La seconda nasce dal fatto che un'esecuzione di `--build-solutions` (avvia l'editor, che usa il `%APPDATA%` reale) aveva aggiornato l'mtime del file vuoto `.recovery_mode_lock` in `%APPDATA%\Godot\app_userdata\tIndustry` (2026-10-18 01:14:38). Era l'unica modifica ai dati reali nella sessione ed è stata risolta da `36acaf6`.

## Decisioni prese in autonomia

- **Decisione**: nei Task 2–5 `Factory.Restore` usa `GameRules(InstantBuild: true)` se il salvataggio non porta regole (segnato `TODO(sp3-task3)`); il Task 3 poi salva e ripristina le regole vere. — il client non cambia per le partite caricate fino al Task 5. — se sbagliata: un salvataggio di sviluppo del Task 2 costruisce subito.
- **Decisione**: gli screenshot del Task 5 vanno all'utente e il lavoro prosegue fino al Task 6 e alla revisione finale senza aspettare; `baseline-sp4` si promuove solo dopo il suo OK. — esecuzione continua, e l'unione è comunque a cura dell'utente. — se sbagliata: una ripetizione visiva in un seguito.
- **Decisione**: branch `feat/sp3-1-cantieri` … `feat/sp3-5-docs` come sopra. — il piano non li nominava. — se sbagliata: si rinominano, nessun cambio di codice.
- **Decisione**: il Task 0 lo esegue il coordinatore. — nessun codice da rivedere. — se sbagliata: nessuno.
- **Decisione**: Task 1, Important 1: vince la specifica («man mano») sulla formula del piano; `due_i = max(0, R_i − ceil(Progress × A_i × RefundPercent/100))`, senza campo nuovo, DTO del T3 invariati, con un test che il primo tick di annullamento rimborsi al massimo 1 per riga. — il piano presupponeva `start=1`. — se sbagliata: solo il ritmo del rimborso.
- **Decisione**: Task 1, Minor 1: si tiene il ceil al prelievo (la specifica diceva floor) e la deviazione va nell'ADR. — pagare prima di costruire la fetta rende esatti i punti di arresto e non sfora mai. — se sbagliata: un'unità esce un tick prima.
- **Decisione**: il giro 1 del Task 1 include anche Minor 2–5 (un solo floor, variabile morta, `BuildCost` da `site.Cost`, test della riga di denaro). — toccano lo stesso codice. — se sbagliata: diff un po' più grande.
- **Decisione**: Task 2, Important 2 (ordine del TDD): si accettano i test esistenti, giudicati sostanziali dal revisore; per ogni test aggiunto nei giri di correzione si esige il rosso→verde visibile. — rifarli non migliorava la rete di test. — se sbagliata: rete più debole sugli interni del T2.
- **Decisione**: `BuilderUnit.Plans` esposto come `IReadOnlyList<BuildPlan>` con lista interna, e `Factory.Builders`/`Sites` come involucri di sola lettura; il giro include anche Minor 2 (asserire che la costruzione sia avvenuta) e Minor 5 (guardia sul cantiere mancante). — la specifica chiede `IGameView` in sola lettura. — se sbagliata: piccolo giro di API.
- **Decisione**: il riferimento dei dati reali per la verifica dal Task 3 in poi è aggiornato allo stato dopo l'incidente (originale conservato a parte). — i compiti successivi devono confrontarsi con ciò che la sessione può ancora cambiare. — se sbagliata: nessuno.
- **Decisione**: con un budget più ampio, T5 e T6 e tutte le revisioni restanti passano a opus. — richiesta dell'utente. — se sbagliata: solo costo.
- **Decisione**: i documenti Obsidian (indice ADR, `mappa-progetto.canvas`, `mappa_canvas.py`) viaggiano con la PR del README (#163) e non con il Task 6; le copie non tracciate restano nel checkout principale e si cancellano prima di passare a un branch che le contiene. — l'utente ha chiesto il README subito e Obsidian aggiornato. — se sbagliata: si spostano in una PR a sé.
- **Decisione**: nel tooltip del cantiere `n` è `Missing.Amount` (quanto manca per finire) invece della «prossima unità» del piano; registrato nell'ADR. — più utile; la specifica dice solo «Mancano: N <oggetto>». — se sbagliata: una riga di testo.
- **Decisione**: il giro 1 del Task 5 include Minor 1 (cicli a indice su `IReadOnlyList`), Minor 3 (azzeramento di `ConstructFinishedCount` in `AttachGame`) e Minor 6 (il riquadro della coda non deve sovrapporsi ai pulsanti in alto con finestra stretta o scala grande). — stessi file. — se sbagliata: diff più grande.
- **Decisione**: il Task 6 consolida i link incrociati in sospeso nella spec e nel piano del SP3 (stesso stile della #158) e segna la specifica approvata. — sono collegamenti che l'utente voleva per Obsidian. — se sbagliata: si annullano due righe.
- **Decisione**: tolte le emoji anche da etichette di gruppo e carte della mappa Obsidian; accettate come in ambito le righe «Previsto» della roadmap e l'uscita del canvas nel formato di Obsidian. — la regola «niente icone» vale per tutti i documenti che l'utente legge; igiene del diff. — se sbagliata: si rimettono le emoji.
- **Decisione**: la serie finale comprende B1, B2, B3, B4, B5. — aspettative ragionevoli di un giocatore e parità con Mindustry. — se sbagliata: piccolo ripristino dell'esperienza d'uso.
- **Decisione**: U1, un cantiere fermo passa in fondo alla coda come in Mindustry (cantiere tenuto, piano ruotato, deterministico), dopo 60 tick se ci sono altri piani; registrato nell'ADR. — l'indicazione vincolante dell'utente «il più simile possibile a Mindustry» vale più del «primo della coda» della specifica. — se sbagliata: si toglie la rotazione e la coda si blocca di nuovo.
- **Decisione**: U2 (marcatore di blocco nel riquadro con ciò che manca) e U3 (riempimento rosso traslucido dei segni di smontaggio). — economici e vicini a Mindustry. — se sbagliata: ripristino grafico.
- **Decisione**: `Spent`/`Refunded` per oggetto come campo additivo di v11 con ripiego sull'indice, senza alzare la versione. — `main` porta già v11 e l'utente può avere salvataggi v11 da prove a mano. — se sbagliata: un salto a v12 più avanti.
- **Decisione**: il coordinatore corregge di persona il nuovo Minor 1 (`DragCells` a 4 connessioni) e la preoccupazione 1 (`dev.sh` e comandi di `CLAUDE.md` con `APPDATA` temporaneo). — piccoli e verificati. — se sbagliata: si annulla un commit.

## Problemi rinviati

Al **sotto-progetto 4** (drone comandabile e interfaccia di costruzione):

- U4: piazzare sopra i propri piani li sostituisce; la direzione dei nastri segue il trascinamento;
- U6: l'uscita del ponte fantasma rispetto alle celle già pianificate;
- «Manca» non aggiornato sui cantieri in pausa dopo un caricamento o dopo la rotazione;
- bandiere di trascinamento perse se il rilascio del mouse non arriva (già prima del SP3).

Al **sotto-progetto 5** (co-op):

- `BuilderUnit.Rotation` da `Atan2`, presente nell'istantanea e nel checksum: valutare di tenerla lato client o quantizzarla.

Al **prossimo aumento di versione dei salvataggi**:

- `RestoreSite` può perdere risorse in casi limite di deriva del contenuto (tetto ai costi ricalcolati, righe per oggetti che non esistono più, spec non valida da un vecchio salvataggio v11 con struttura sconosciuta).

Rinviati senza data (Minor accettati): codice morto `Relocate` di `Crafter`/`Generator`/`Miner`/`Extractor`/`PowerNode`; scarto di un tick nel rimborso di smontaggio (`standing` double contro ceil); prestazioni con centinaia di piani (`IsPlannedTile`, `SiteOfPlan` lineare, ricostruzione completa dello strato); `Missing` nullo per un tick dopo il caricamento; ricostruzione extra rara; `_barFill.Size` scritto a ogni frame; cast del seme oltre `IGameView` (solo sviluppo); pannello degli obiettivi che salta all'apparire della coda; regola di rimborso `InstantBuild` con regola sotto 100 (oggi non esiste). U5 (`PlanDropped` in giocatore singolo): nessuna azione, riguarda il co-op.

## Verifiche finali

- `dotnet build tIndustry.sln -c Release`: 0 errori, 0 avvisi.
- `dotnet test`: 189 superati, 1 ignorato (`MeasureLargeMap`).
- Playtest `ui` e `tutorial`: OK, 0 ERROR (il passo del trascinamento anche a 60 fps). Confronto con `baseline-sp3`: `ui` 5 immagini, 0 differenze; `tutorial` 9 immagini, 2 differenze attese (`tutorial-03`, testo del tutorial sul drone; `tutorial-07`, 1×8 px).
- CI `build-publish.yml` lanciata a mano: verde.
- Dati reali del giocatore: invariati. Unica eccezione, l'mtime del file vuoto `.recovery_mode_lock`, aggiornato da un'esecuzione di `--build-solutions` (editor, `%APPDATA%` reale) e risolto da `36acaf6`.

## Da fare

- Promuovere la nuova baseline degli screenshot (`baseline-sp4`) **dopo l'OK dell'utente** sulle catture.
