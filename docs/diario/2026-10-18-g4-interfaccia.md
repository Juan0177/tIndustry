# Sotto-progetto G4 — Interfaccia in stile Mindustry

- **Specifica**: [2026-10-18-g4-interfaccia-design](../superpowers/specs/2026-10-18-g4-interfaccia-design.md)
- **Piano**: [2026-10-18-g4-interfaccia](../superpowers/plans/2026-10-18-g4-interfaccia.md)
- **ADR**: [0006 — Interfaccia, lingue e carattere](../decisioni/0006-interfaccia-lingue-carattere.md)
- **Periodo**: 2026-10-18
- **Esito**: in `main` con le PR [#194](https://github.com/Juan0177/tIndustry/pull/194), [#195](https://github.com/Juan0177/tIndustry/pull/195), [#198](https://github.com/Juan0177/tIndustry/pull/198), [#200](https://github.com/Juan0177/tIndustry/pull/200), [#201](https://github.com/Juan0177/tIndustry/pull/201) e [#202](https://github.com/Juan0177/tIndustry/pull/202) (documenti più ondata finale di correzioni). La base di catture candidata `.dev/baseline-g4-candidata` (italiano e inglese) attende ancora l'OK dell'utente.

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-10-18-g4-interfaccia/progress.md`), dai rapporti, dalle revisioni e dalle riletture dei task (`task-N-report.md`, `task-N-review.md`, `task-N-rereview.md`), dalla revisione finale (`final-review.md`), dalla rilettura dell'ondata finale (`fixwave-rereview.md`) e dalla storia git (`4200a64..main`). Dove le fonti non dicono qualcosa qui non è inventato.

## Obiettivo

Un'interfaccia coerente con l'arte dei blocchi, nello stile di Mindustry, con un carattere pixel nostro, testi in italiano e inglese, scala regolabile e opzioni di accessibilità; spariscono le ultime icone di terze parti. Ambito: HUD, pannelli (Ricerca, Mercato, Campagna, Impostazioni, salvataggi), menu principale, icone. Scelte e motivi nell'[ADR 0006](../decisioni/0006-interfaccia-lingue-carattere.md).

## Task, commit e PR

Un branch per task (`feat/g4-<n>-...`), ciascuno fuso in `main` a revisione pulita e CI verde. Opus per implementatori, revisioni dei task 2–5 e revisione finale; sonnet per la revisione dei task 1 e 6 e per le riletture.

| Task | Contenuto | Commit | PR |
|---|---|---|---|
| 0 | `main` dopo la #193; elenco dei dati reali (`$TEMP/g4-realdata-before.txt`, 79 righe); base di catture `.dev/baseline-g3-candidata` | — | — |
| 1 | Carattere pixel (BMFont, 2 misure) e icone `ui-*` generati da `tools/art` | `e03d57e` | [#194](https://github.com/Juan0177/tIndustry/pull/194) |
| 2 | Tema unico, passaggio alle icone nuove, glifi per frecce e segni, rimozione della pulizia GC forzata all'uscita, fantasma di piazzamento stile Mindustry, palette senza blocchi bloccati, «Continua» solo con un salvataggio | `5f9c4fc`, `619d8e5`, correzioni `2a9ae4f` | [#195](https://github.com/Juan0177/tIndustry/pull/195) |
| 3 | Scala della sola interfaccia (75%–200%), dimensioni dei testi, minori parcheggiati dal T2 | `75048f8` | [#198](https://github.com/Juan0177/tIndustry/pull/198) |
| 4 | Italiano e inglese (`godot/lingue/testi.csv`), rifiuti e errori di salvataggio come codici (`RefusalCode`, `SaveLoadError`), righe «Scava:» dai dati, test di copertura dei testi | `e9abba3`, correzioni `854c3c2` | [#200](https://github.com/Juan0177/tIndustry/pull/200) |
| 5 | Accessibilità (tre varianti di colore, segni non solo di colore), fantasma rosso sul titanio tramite `CanPlace`, texture delle caselle dai tool, menu principale con logo e mappa che scorre | `72168d9`, `80bb767`, correzioni `a2e3c2a` | [#201](https://github.com/Juan0177/tIndustry/pull/201) |
| 6 | Documenti (ADR 0006, ARCHITETTURA, CLAUDE.md, README con catture it/en, godot/README, tools/art/README, CHANGELOG, canvas), sezione «Catture» del sito, base candidata `.dev/baseline-g4-candidata` it/en | `1e6d7dc`, `118aac0` | [#202](https://github.com/Juan0177/tIndustry/pull/202) |
| Fix finali | Ondata di correzioni dalla revisione del T6 e dalla revisione finale, sulla stessa PR | `4d6d465` | [#202](https://github.com/Juan0177/tIndustry/pull/202) |

CI `build-publish.yml` lanciata a mano e verde su ogni task: T1 39122419867, T2 39125238680 (correzioni 39127363127), T3 39130469319, T4 39137087323 (correzioni 39138580073), T5 verde (correzioni 39143528759), T6 39145016178, ondata finale 39147425329. Ripubblicazione del sito dopo la fusione: `pages.yml` 39147776982.

Test superati (rapporti dei task): T2 361 (2 ignorati); T3 361; T4 385, dopo le correzioni 386; T5 386; T6 e ondata finale 386 (2 ignorati). Test Python di `tools/art`: 32 al T1, 34 al T2, 35 al T5, 34 al T6; 12 test del sito al T6. Dal T2: `StyleBoxFlat` nel codice da 15 a 0, override locali da 272 a 0.

## Giri di revisione

- **T1** — conforme, approvato (sonnet), solo Minor: scaling_mode del BMFont non intero (da sistemare nel T2); `UI_TERZI` e le voci di terzi in `ATTRIBUTION.md` da togliere nel T2; maiuscole alte 7 px invece di 8 (accettate). L'utente ha approvato carattere e icone prima della fusione.
- **T2** — conforme, «Needs fixes» (opus): 2 Important (layout della Ricerca rotto a 1280×720; prova del fantasma con scala di rendering ridotta indebolita) e 8 Minor. **Giro di correzione 1** (`2a9ae4f`): i due Important, il fantasma stile Mindustry richiesto dall'utente, palette senza blocchi bloccati, «Continua» solo con un salvataggio, i Minor; 25 uscite pulite. Rilettura (sonnet): tutto risolto, 4 Minor passati al T3 (categoria attiva della palette quando il pulsante si nasconde; livello del fantasma sopra i piani; suggerimento «H = vista intera» contro lo zoom minimo 1,0; fascia vuota nel pannello Home senza «Continua»).
- **T3** — conforme (un piccolo vuoto: playtest del tutorial senza controllo dei rettangoli), approvato (opus). Minor passati all'ondata finale: controllo dei rettangoli del tutorial a 150% (poi verificato come già fatto), test del clic destro che passa banalmente, sottoscrizione di `UiScale` alla dimensione della finestra, commenti vecchi di `RenderScale`, riga del README sulle icone a 64 px, toast nel controllo delle sovrapposizioni, obiettivi a 5 px dal Core a 150% in campagna.
- **T4** — conforme, «Needs fixes» (opus): 1 Important (gli errori di caricamento si riducevano a «non leggibile») e 7 Minor. **Giro di correzione 1** (`854c3c2`): `SaveLoadError` a 10 codici, inglese rifinito, test di formato, cultura della durata delle ricette, controllo in CI dell'export di `.translation`; 386 test, 351 chiavi. Rilettura (sonnet): tutto risolto, Minor all'ondata finale (controllo di `Testi.Secondi` per una terza lingua; formato dei migliaia in `HudStyle` secondo la cultura corrente).
- **T5** — «Needs fixes» (opus): due vuoti rispetto alla specifica (fantasma del ponte non coerente con il clic, perché controllava solo la cella di ingresso e non tutta l'impronta; cantieri in smontaggio distinti solo dal colore) e una voce della specifica non presente nel piano (oggetti nella palette per daltonici, poi caduta per scelta dell'utente). **Giro di correzione 1** (`a2e3c2a`): i due Important più contrasto dell'alto contrasto, contorno del Core, «Home» dalle Impostazioni, prova della deriva, colori delle caselle da una sola fonte, fantasma una volta a frame, toast; contrasto Alto contrasto 3,64, bordo UI 3,48. Rilettura (sonnet): tutto risolto, Minor all'ondata finale (ridisegno del livello dei piani a ogni tick con cantieri; caso limite `OpenSavesManager`/`_settingsFromHome`; nessun playtest per il tratteggio di smontaggio né per la deriva sotto Salvataggi).
- **T6** — conforme, approvato dopo correzione (sonnet): 1 Important sui documenti (l'ADR 0006 indicava `CampaignMapMissing` come ultimo valore di `SaveLoadError`, è `CampaignMapChanged`) e 1 Important sulle maschere di `compare_shots.py`, troppo grandi nelle catture delle varianti di colore e del clic a 200% (nascondevano proprio i segni da controllare: da fermare nei playtest l'orologio dello shader dei cantieri, il minatore e gli oggetti sui nastri, poi restringere le maschere); Minor sul docstring del sito, una cella vuota nella tabella del README, un toast dissolvente dietro il Mercato. Tutto all'ondata finale.
- **Revisione finale** (opus, `4200a64..118aac0`): **si può unire, con correzioni**. 0 Critical, 1 Important, 6 Minor. Punti di forza: tema ricostruito sul posto, varianti di colore che non toccano mai i layout, colori letti dal vivo dagli overlay del mondo, `Shared` pulito e salvataggi v12 invariati, traduzione completa (355 chiavi, nessuna vuota, segnaposto uguali in it/en), documenti esatti.
  - **Important**: le risorse Godot create in C# fuori dall'interfaccia restavano al finalizzatore (`DroneVisual.Enlarged` con `GetImage()`, il `ShaderMaterial` di ogni cantiere in `ConstructionLayer`, `Sprites.Placeholder`). Era la premessa con cui la pulizia GC forzata era stata tolta (ADR 0005): possibile crash in uscita dopo sessioni lunghe.
  - **Minor**: sfondo della barra della coda con colore letto una sola volta; `Theme.Clear()` seguito da circa 400 `Set*` con un possibile ritardo su «Applica»; ADR 0006 con l'ultimo valore sbagliato; ADR 0005 che descriveva ancora la pulizia GC; grigio fisso `Modulate` delle caselle bloccate (ormai nascoste); il 75% mai impaginato né controllato nel playtest.
- **Ondata finale di correzioni** (`4d6d465`, sulla PR #202, opus): `using` su `Image` in `DroneVisual` e `Sprites.Placeholder`; `Dispose` del materiale a ogni cantiere rimosso e in `Clear()`/`_ExitTree`, più i materiali di `EffectManager` e di `TerrainChunks`; barra della coda aggiornata a ogni sync; `UiTheme.Refresh` con un solo `MergeWith`; grigio delle caselle bloccate tolto; 75% nella sonda delle scale; ADR 0005 e 0006 corretti; `formato.decimale` nel CSV; livello dei piani ridisegnato solo se cambia `PlansSignature`; `_settingsFromHome` azzerato in `OpenSavesManager`; `UiScale.Detach`; nuovi playtest (tratteggio di smontaggio, Salvataggi sopra Impostazioni); orologio dello shader dei cantieri, minatore e oggetti dei nastri fermi nei playtest, maschere ristrette. Perdite: 0 su 18 esecuzioni (11 del tutorial, 7 di ui; «Leaked unsafe reference» 0, «rc_owner» 0, ERROR 0). Rilettura (sonnet): **tutto risolto**, nessun Critical o Important nuovo; revisione finale: pulita.
  - Residui accettati: commento XML di `Look()` finito sopra `_ExitTree` in `EffectManager`; commento in `World.PlaytestScala.cs` sul drone «volato» (con il tempo fermo non vola); `formato.decimale` mancante in una terza lingua mostrerebbe il testo della chiave. Non toccati: commenti vecchi di `RenderScale` e toast nel controllo delle sovrapposizioni.

## Fuori piano, nella stessa sessione

- **Sito su GitHub Pages** (PR [#196](https://github.com/Juan0177/tIndustry/pull/196), `ebe5946`, `f60dfcc`, `e8c9afc`, `2bd5372`): prima pagina con la pagina degli asset, con i blocchi composti come in gioco, gli stati, il popover dei pezzi e gli Ambienti. Pubblicata: <https://juan0177.github.io/tIndustry/>. Correzioni di revisione: permessi del workflow, nota sulla pubblicazione, intestazione fissa, browser senza popover. Dopo la PR #202 il sito ha anche la sezione «Catture» (file copiati, pagina da 9,5 KB), ripubblicata con `pages.yml`.
- **Drone nitido** (PR [#197](https://github.com/Juan0177/tIndustry/pull/197), `f602ef5`): il drone appariva sfocato (filtro lineare su 24 px ×2). Texture ingrandita ×4 e rimpicciolita con il filtro lineare, creata per nodo e liberata in `_ExitTree`.
- **Schermo intero in release, finestra 1280×720 in sviluppo, palette su misura** (PR [#199](https://github.com/Juan0177/tIndustry/pull/199), `7c32bd7`, `9440659`): decisione dell'utente. In release schermo intero a risoluzione nativa; in sviluppo e nei test finestra 1280×720 con interfaccia al 100%. Il mondo resta 1:1; scala iniziale dell'interfaccia dall'altezza dello schermo (sotto 1400: 100, sotto 2000: 150, altrimenti 200); i playtest forzano finestra e 100%. Decisioni dell'utente sulla palette: **griglia fissa** a 4 colonne con righe costanti, **caselle vuote lasciate in bianco** (dimensione fissa mantenuta), **icone da 32 px** native, a ×1, in caselle compatte. Revisione (sonnet): 3 Important (ripristino della finestra con clamp, controllo della dimensione del playtest fragile rispetto all'ambiente, nota per chi aggiorna) più Minor, tutti corretti in `9440659` (CI 39133971289); rilettura: solo note Low.
- **Base di catture**: `.dev/baseline-g4-candidata` (italiano e inglese, con maschere nuove per file) attende l'OK dell'utente. La base ufficiale resta `$TEMP/baseline-g2b`.

## Decisioni prese in autonomia

- **Decisione**: un branch per task da `main`, ciascuno fuso a revisione pulita e CI verde; opus per implementatori e revisori, sonnet per riletture e per la revisione dei task 1 e 6. — come per G2b e G3. — se sbagliata: solo costo.
- **Decisione**: aggiungere al carattere i glifi di → — – ✓ ↔ ↖ ↑ ↓ ← ° ≥ e un test permanente di copertura (dati JSON e letterali C#, esteso nel T4 a `testi.csv`). — l'utente aveva segnalato i caratteri speciali mancanti. — se sbagliata: simboli sostituiti da quadrati.
- **Decisione**: fantasma di piazzamento stile Mindustry (sprite traslucido ruotato con freccia di direzione e impronta chiara o rossa, nessuna etichetta nel mondo, leggibile di notte); segno X rinviato al T5. — richiesta dell'utente dopo la revisione del T2. — se sbagliata: ritocco grafico.
- **Decisione**: scala dell'interfaccia predefinita a 100 (era 125, che scalava anche il mondo, rendendolo circa il 20% più piccolo della G3). — il mondo resta 1:1; la scala vale solo per l'interfaccia. — se sbagliata: cambiare un default; a 200% i pannelli non entrano del tutto (il piano richiedeva 100 e 150).
- **Decisione**: nessuna migrazione del valore 125 già salvato e nessuno zoom iniziale maggiorato della camera (rinviabile). — raccomandazione al controllore, accettata. — se sbagliata: chi ha 125 salvato vede un'interfaccia più grande del previsto.
- **Decisione**: l'inglese del T4 è una traduzione dell'implementatore, letta e approvata dall'utente. — il tono è suo. — se sbagliata: correggere `testi.csv`.
- **Decisione**: colori delle varianti per daltonici dalla tavolozza Okabe-Ito, scelti dall'implementatore del T5. — tavolozza nota e accessibile. — se sbagliata: ritoccare `UiColors`. Accettato che «Insufficiente» (`#F07B3F`) e «Fermo» (`#E69F00`) siano arancioni vicini: il testo accanto resta.
- **Decisione**: i segni di rimozione diventano un tratteggio e non una X; il fantasma del ponte senza rosso sull'uscita era una lacuna, corretta nel giro del T5. — il significato non deve dipendere dal solo colore. — se sbagliata: ritocco di due overlay.
- **Decisione dell'utente**: font e icone del T1 approvati; stile del T2 approvato, con dimensioni dei testi da sistemare nel T3. — giudizio sulle catture. — se sbagliata: un altro giro.
- **Decisione dell'utente**: nascondere i blocchi bloccati dalla palette e «Continua» senza salvataggio. — meno rumore in interfaccia. — se sbagliata: ripristino di due condizioni.
- **Decisione dell'utente**: dopo il T5 aggiornare tutto (documenti, README, sito). — copertura completa della G4 nel T6, con sezione «Catture» sul sito. — se sbagliata: lavoro in più.
- **Decisione dell'utente**: gli oggetti nella tavolozza per daltonici non servono (bastano le sagome); la specifica è corretta nel T6 con una nota. — niente costo di arte. — se sbagliata: aggiungere varianti degli oggetti.
- **Decisione**: accettati i residui della rilettura dell'ondata finale (posizione del commento di `Look()`, commento sul drone nei playtest, `formato.decimale` per una terza lingua). — cosmetici. — se sbagliata: una riga di correzione.

## Problemi rinviati

- **Base di catture** `.dev/baseline-g4-candidata`: attende l'OK dell'utente (vedi «Da fare»).
- **Una terza lingua**: `Testi.Secondi` e `formato.decimale` andranno controllati; la copertura per colonne del CSV potrebbe già bastare (non verificato).
- **Eccezioni di `Shared` in italiano**: i dettagli delle eccezioni restano in italiano (`SaveLoadFailure.Detail` è per il log); idem le due `FileNotFoundException` del client in `World.cs`.
- **Scala 200%**: i pannelli non entrano del tutto (solo segnalato nei playtest).
- **Commenti vecchi di `RenderScale`** e toast nel controllo delle sovrapposizioni: non risolti.
- **Chiavi delle ricette** non usate, euristica per le stringhe dimenticate, `.translation` ignorato: dal rapporto del T4.
- **Generatore `biomi-2`** (dalla G3): invariato, dopo la G4.
- **`CorePanel.SyncCoreUpgrade`** (chiave di cache senza materiali), LINQ per frame in `UpdateHud`, closure di `CardLayout.Follow` mai sciolte: precedenti alla G4 o innocue finché i pannelli vivono per tutta la partita; non assegnati.

## Verifiche finali

Dai rapporti del T6 e dell'ondata finale:

- Test: 386 superati al T5; test Python 34 OK e test del sito 12 OK al T6; `python tools/art/genera.py --controlla` OK.
- Playtest `ui` e `tutorial`, in italiano e inglese, al 100% e al 150%: PLAYTEST OK, 0 ERROR, «rettangoli ok» (a 200% i fuori misura sono solo segnalati).
- Perdite di risorse all'uscita: 0 su 18 esecuzioni dell'ondata finale.
- CI `build-publish.yml` lanciata a mano, verde (ultima esecuzione 39147425329).
- Documenti controllati dal revisore del T6 contro il codice (15 codici di rifiuto, 10 di salvataggio, livelli, scale, link relativi, assenza di emoji); canvas rigenerato byte per byte identico.

## Da fare

- **L'utente giudica le catture della G4** (it/en) e solo con l'OK si promuove `.dev/baseline-g4-candidata`. Fino ad allora la base ufficiale è `$TEMP/baseline-g2b`.
- Dopo la G4 resta in roadmap il generatore `biomi-2`.
