# Sotto-progetto G1 — Arte dei blocchi

- **Specifica**: [2026-10-18-g1-arte-blocchi-design](../superpowers/specs/2026-10-18-g1-arte-blocchi-design.md)
- **Piano**: [2026-10-18-g1-arte-blocchi](../superpowers/plans/2026-10-18-g1-arte-blocchi.md)
- **ADR**: [0004 — Arte generata da script](../decisioni/0004-arte-generata-da-script.md)
- **Periodo**: 2026-10-18 (una sola esecuzione notturna, in modalità Auto)
- **Esito**: in `main` con le PR [#168](https://github.com/Juan0177/tIndustry/pull/168), [#169](https://github.com/Juan0177/tIndustry/pull/169), [#170](https://github.com/Juan0177/tIndustry/pull/170), [#171](https://github.com/Juan0177/tIndustry/pull/171), [#172](https://github.com/Juan0177/tIndustry/pull/172) e [#173](https://github.com/Juan0177/tIndustry/pull/173). La grafica attende ancora l'OK dell'utente (vedi «Da fare»).

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-10-18-g1-arte-blocchi/progress.md`), dai rapporti e dalle revisioni dei task (`task-N-report.md`, `task-N-review.md`), dalla revisione finale (`final-review.md`) e dalla storia git (`6c955c3..main`). Dove le fonti non dicono qualcosa qui non è inventato.

## Obiettivo

Sostituire l'arte dei blocchi con sprite pixel art generati da uno script (`tools/art/`, Pillow 12.3), con tavolozza, forme e registro propri, e con un punto unico di accesso alle texture nel client (`Sprites`). Il lavoro copre logistica (nastri a 8 fotogrammi, ponti, selettori), produzione e potenza a strati, Core in 4 tier, droni, oggetti senza tinta su nastri, HUD e giacimenti. Scelte e motivi in [ADR 0004](../decisioni/0004-arte-generata-da-script.md).

## Task, commit e PR

Un branch per task (`feat/g1-<n>-...`), ciascuno fuso in `main` appena la revisione era pulita e la CI verde, così il task successivo partiva da `main` senza pile lunghe. Modello opus per implementatori e revisori; le riletture dopo le correzioni con sonnet (il T6 è stato rivisto da sonnet).

| Task | Contenuto | Commit | PR |
|---|---|---|---|
| 0 | Base degli screenshot (`baseline-sp4`), elenco dei dati reali prima del lavoro, `main` verificato (189 test, playtest OK) | — | — |
| 1 | Generatore (tavolozza, forme, registro) e punto unico `Sprites` per le texture | `e4b196f`, `5996639` | [#168](https://github.com/Juan0177/tIndustry/pull/168) |
| 2 | Sprite della logistica e nastri disegnati con essi | `2555b75`, `cee1808`, correzioni `8ddc05f` | [#169](https://github.com/Juan0177/tIndustry/pull/169) |
| 3 | Sprite di produzione e potenza, edifici a strati | `feb2104`, `db8eb4a` | [#170](https://github.com/Juan0177/tIndustry/pull/170) |
| 4 | Core in 4 tier con luce animata, droni come sprite | `6e619d4`, `3b28b29`, correzioni `796d5ab` | [#171](https://github.com/Juan0177/tIndustry/pull/171) |
| 5 | Sprite degli oggetti, senza tinta su nastri, HUD e giacimenti | `96b05db`, `8773677` | [#172](https://github.com/Juan0177/tIndustry/pull/172) |
| 6 | Pulizia, documenti (ADR 0004, ARCHITETTURA, README, CHANGELOG, CLAUDE.md), base candidata | `699501a`, `c8af410` | [#173](https://github.com/Juan0177/tIndustry/pull/173) |
| Fix finali | Correzioni dalla revisione finale e dalla revisione del Task 6 | `f9769ad`, `25e7171`, `b1ddeb2` | [#173](https://github.com/Juan0177/tIndustry/pull/173) |

CI `build-publish.yml` lanciata a mano e verde su ogni task: T1 39083981909, T2 39086620875 (correzioni 39087198456), T3 39088109603, T4 39089242473 (correzioni 39089687516), T5 39090632643, T6 39091542930, correzioni finali 39092792933.

## Giri di revisione

- **T1** — conforme, approvato: 0 Critical, 0 Important, 4 Minor. Esito «DONE_WITH_CONCERNS»: la cartella temporanea `D:\tmp\tmp.t97g2NHNdC` rimasta fuori dal repo (cancellazione bloccata). Minor rinviati al T6 (vedi decisioni).
- **T2** — conforme, approvato, 6 Minor. Giro di correzione 1 (`8ddc05f`): strisce dei nastri a 8 fotogrammi e Minor 1, 3, 4, 5, 6; il Minor 2 (vecchio generatore) lasciato al T6. Rilettura (sonnet): 6 su 6 risolti, nessuna rottura.
- **T3** — conforme, approvato, 5 Minor, rinviati al T6 (sovrapposizione delle teste dell'assemblatore, test del pozzo dell'estrattore, `2*T//2`, `gen_structure_blocks`/ARCHITETTURA); inquadratura delle catture accettata.
- **T4** — conforme, «Needs fixes»: 1 Important (la luce del Core non era deterministica nelle catture), 5 Minor. Giro di correzione 1 (`796d5ab`): luce guidata da un orologio del mondo che sopravvive alle ricostruzioni e fermo sul primo fotogramma sotto `TINDUSTRY_PLAYTEST`; ombra del drone a terra, texture in cache, ordine dei campi, test rigoroso del muso. Rilettura (sonnet): 5 su 5 risolti, nessuna rottura.
- **T5** — conforme, approvato, 6 Minor, tutti rinviati al T6 (righe obsolete in ARCHITETTURA, avviso del ripiego di `BeltItem`, controllo di `TryInsert` nel playtest, test duplicato, tono di ferro e piombo sul nastro).
- **T6** — revisione (sonnet): conforme, approvato, 3 Minor (dicitura della roadmap nel README, che va alla serie finale; gli altri senza azione).

## Controlli d'arte del coordinatore

Il coordinatore ha guardato le tavole e le catture, oltre alla revisione del codice.

- **T2** (tavola del task): i tier si leggono dal colore delle rotaie, i chevron sono leggibili, le curve accettabili.
- **T3** (tavola e `ui-blocchi-accesi.png`): basi distinte (minatore tondo arancione, avanzato rosso, forno in mattoni, assemblatore a telaio d'acciaio, generatore a strisce di pericolo, nodi viola e gialli). La trivella del minatore si legge come una punta a spirale con denti e non come una ventola, ma è vicina: da segnalare all'utente. La ventola del generatore è voluta.
- **T4** (tavola): i 4 tier del Core sono una famiglia visibilmente sempre più imponente; i droni alpha, beta e gamma hanno sagome distinte.
- **T5** (`.dev/g1-task5`): oggetti leggibili sui tre nastri, icone del Mercato e dell'HUD colorate e pulite. I marcatori dei giacimenti sono più affollati: segnaposto fino alla G2b, accettato.

## Prestazioni

Sonda `TINDUSTRY_PLAYTEST_PERF=1`, 1280×720. 240 fps è il tetto della macchina (resta anche con VSync spento e `--max-fps 0`), quindi la misura dice solo «nessun peggioramento visibile».

| Misura | FPS medio | FPS minimo | Nodi |
|---|---|---|---|
| T2, `main` (shader, strisce unite), 200 nastri | 240 | 228–235 | 384 |
| T2, una `Sprite2D` per cella, 200 nastri | 240 | 226–236 (212 nell'ultima esecuzione) | 526 |
| Fix finali, 200 nastri e 100 edifici a strati (101 al lavoro) | 235 | 166 | 844 |

La revisione finale aveva notato che la misura richiesta dalla specifica era a metà (solo nastri): la sonda ora piazza anche 75 edifici a griglia (25 forni, 25 assemblatori, 25 generatori) più 25 minatori base. I numeri sono nell'ADR 0004 e nel CHANGELOG.

## Revisione finale e serie di correzioni

Revisione finale (opus, `6c955c3..c8af410`, in un worktree temporaneo): **pronta per il merge: sì**, 0 Critical, 0 Important, 10 Minor. Verificati dal revisore: 12 test Python, `--controlla` con 68 sprite aggiornati e 0 da rigenerare (anche con `PYTHONHASHSEED=987`, quindi byte stabili tra processi), build 0 errori, 190 test superati e 1 ignorato, nessun PNG di terzi nella radice di `godot/assets/`, nessun riferimento rimasto a `belt_scroll`, `ItemSilhouette`, `core.png`, `ItemPalette.Tint` e simili.

Punti di forza segnalati: punto unico `Sprites` con 29 chiamanti e sprite mancante senza crash (riquadro magenta, avviso una sola volta); 2–4 nodi per edificio; generatore deterministico e solo con interi; test Python che provano proprietà reali (strisce cicliche, curve raccordate, droni simmetrici, contorni chiusi); doppia tinta risolta del tutto; documenti accurati.

Minor trovati e risolti nella serie finale sulla #173 (rilettura: **10 su 10 risolti, nessuna rottura**):

1. `CorePanel` caricava `core-1.png` con un percorso letterale: ora passa da `Sprites.For("core-1")`.
2. Le animazioni degli edifici ripartivano a ogni ricostruzione: `BuildingPad.Phase(origin)` sfasa l'orologio per cella (0 nei playtest), usato da minatore, generatore, forno e luce del Core.
3. Lavoro per cella a ogni cambio di fotogramma dei nastri: una `AtlasTexture` condivisa per tier (dritti e curve), si cambia solo `Region`.
4. Misura a metà: sonda con edifici a strati (numeri sopra).
5. Piombo con due colori: `ItemVisualColors` allineato al blu-violetto.
6. Regole dell'arte non controllate in CI: passi «Setup Python 3.12» e «Test sprite generator» (Pillow 12.3.0, unittest, `--controlla --pixel`) dentro `build-publish.yml` e `release.yml`, senza nuovi trigger; nuovo test `test_solo_sprite_generati`.
7. Frecce dei nastri in competizione con gli oggetti: bordo chiaro di 1 px per carbone, minerale e lastra di titanio, verificato da `test_oggetti`.
8. Icone sfocate nella palette: disegnate a 64 px di schermo con filtro Nearest.
9. Stato dei documenti: README con G1 «Fatto, in attesa dell'ok sulla grafica», riga separata «In corso: G2a», «Previsto: G2b–G4»; galleria e immagini rifatte dalla nuova base (sistemata anche la dicitura segnalata dalla revisione del T6).
10. Commento di `CoreLightFrame` corretto («frame index 0»).

Nello stesso giro corretto un avviso xUnit2029 introdotto dal T6 in `ContentValidationTests` (il «0 avvisi» precedente veniva da una build incrementale che non ricompilava i test).

**Incidente di CI**: la prima esecuzione sulla #173 (39092709305) è fallita nel passo «Test sprite generator»: i test passavano, ma `--controlla` vedeva tutti i 68 PNG diversi, perché su Linux Pillow 12.3.0 scrive byte diversi per gli stessi pixel. Si è aggiunto `--controlla --pixel` (confronta dimensioni e pixel RGBA, cattura una modifica di 1 pixel) da usare in CI, tenendo il controllo sui byte in locale (`b1ddeb2`). Esecuzione 39092792933 verde.

## Decisioni prese in autonomia

- **Decisione**: un branch per task, in pila sul precedente, ogni PR fusa in `main` appena revisione pulita e CI verde, così il successivo parte da `main`. — evita pile lunghe. — se sbagliata: qualche fusione in più.
- **Decisione**: modello opus per implementatori e revisori. — il budget lo permetteva e l'arte richiede giudizio. — se sbagliata: solo costo.
- **Decisione**: il T1 copia `conveyor-fast` e `bridge` come `conveyor-express` e `conveyor-bridge`, che il T2 ridisegna. — così `ContentValidationTests` passa senza `IconFileFor`. — se sbagliata: due PNG di terzi in più per un task.
- **Decisione**: T1, Minor sui byte dei PNG che dipendono da Pillow/zlib: il T6 documenta Pillow 12.3 in `tools/art/README` e nell'ADR 0004; `--controlla` resta sui byte in locale. — costo basso. — se sbagliata: falsi scarti su un'altra macchina (poi avvenuti in CI, risolti con `--pixel`).
- **Decisione**: T1, Minor sui fotogrammi non quadrati: le strisce restano quadrate (fotogramma = altezza) nei T2–T5. — `Sprites.Frames` taglia quadrati. — se sbagliata: costo minimo. Il T6 ha poi tolto l'opzione `fotogramma` invece di asserirla: più semplice.
- **Decisione**: T1, Minor sul controllo delle misure e sulla tabella `CELLE` copiata a mano: il T6 estende il test a droni e Core; `CELLE` resta a mano con un commento che rimanda a `Factory.FootprintSize`. — economico. — se sbagliata: disallineamento possibile.
- **Decisione**: velocità di scorrimento per tier (come i nastri di Mindustry). — rende leggibile il tier. — se sbagliata: una riga in `ClockFor`.
- **Decisione**: strisce dei nastri a 8 fotogrammi da 4 px (il brief diceva 4×8). — con 4 fotogrammi si vedeva lo scatto alla velocità del T1. — se sbagliata: tornare a 4 fotogrammi.
- **Decisione**: glifo del selettore non ruotato a 14/32. — corregge un vecchio difetto di inclinazione. — se sbagliata: ritocco grafico.
- **Decisione**: giro di correzione 1 del T2 = 8 fotogrammi più Minor 1, 3, 4, 5, 6; il Minor 2 resta al T6. — stessi file, costo minimo. — se sbagliata: diff più grande.
- **Decisione**: il rotore del generatore non si azzera più al consumo di combustibile ma si ferma dove si trova. — solo estetico. — se sbagliata: una riga.
- **Decisione**: `core.png` tenuto come copia fino al T6; il T6 lo ha cancellato perché inutilizzato (il client usa `core-1`). — rinviare costa poco. — se sbagliata: rimettere il file.
- **Decisione**: luce del Core con un orologio del mondo che sopravvive alle ricostruzioni, fermo sul primo fotogramma sotto `TINDUSTRY_PLAYTEST`; giro 1 del T4 include anche i Minor 1–5. — catture deterministiche senza maschere. — se sbagliata: una maschera al posto dell'orologio.
- **Decisione**: nel chip del denaro diventa rossa solo la cifra quando non ci si può permettere l'acquisto (nessuna tinta sull'icona). — evita la doppia tinta. — se sbagliata: ritocco dell'HUD.
- **Decisione**: i marcatori più affollati dei giacimenti restano come segnaposto fino alla G2b. — fuori ambito. — se sbagliata: ritocco in G2b.
- **Decisione**: i Minor di T3 e T5 vanno tutti in `task-6-parked.md` e nel T6 (11 voci). — stessi file di pulizia. — se sbagliata: T6 più grande.
- **Decisione**: piombo blu-violetto per distinguerlo dal ferro sul nastro a 10 px. — tono troppo vicino. — se sbagliata: una riga in `palette.py`.
- **Decisione**: una sola serie finale sulla #173 per i Minor 1–10 e per la dicitura del README, con passi Python di CI dentro i workflow manuali/di release esistenti (trigger verificati invariati dal coordinatore). — economica e di qualità. — se sbagliata: si toglie il passo dal workflow.
- **Decisione**: `--controlla --pixel` in CI, byte in locale. — i byte di Pillow cambiano da Windows a Linux. — se sbagliata: si torna a un confronto più debole.

## Problemi rinviati

Nessun rinvio aperto dai task o dalla revisione finale: tutti i Minor sono stati risolti nel T6 o nella serie finale. Restano solo i punti che la revisione finale dichiara di **non aver giudicato**: l'animazione reale in esecuzione, la sagoma in bianco e nero, i byte con altre versioni di Pillow o zlib, il confronto con la tavolozza di Mindustry, le maschere di `compare_shots.py`. La revisione della grafica passa all'utente (sotto), le icone di `godot/assets/ui/` alla G4.

## Verifiche finali

- `dotnet build tIndustry.sln -c Release`: 0 errori, 0 avvisi.
- `dotnet test`: 190 superati, 1 ignorato.
- `python -m unittest tools/art/test_genera.py`: 13 test OK, nessuno saltato. `genera.py --controlla` e `--controlla --pixel`: 68 aggiornati, 0 da rigenerare.
- Playtest `ui` e `tutorial` OK, 0 ERROR; eseguiti due volte, base candidata `.dev/baseline-g1-candidata` identica (5 + 9 immagini, 0 differenze). Confronto del T1 con `baseline-sp4`: 0 differenze.
- Sonde `PERF` e `BLOCCHI`: OK (numeri sopra). Importazione e `--build-solutions`: uscita 0.
- CI `build-publish.yml` lanciata a mano, verde (ultima esecuzione 39092792933).
- Dati reali del giocatore: invariati (T6).

## Da fare

- **L'utente guarda la tavola** `.dev/arte/tavola.png` (68 sprite) e dà l'OK sull'arte; solo allora si **promuove** la base candidata `.dev/baseline-g1-candidata`.
- Cartella temporanea rimasta fuori dal repo: `D:\tmp\tmp.t97g2NHNdC` (la cancellazione è stata bloccata durante il T1). La rimuove l'utente.
