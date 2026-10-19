# Changelog

Tutte le note di versione rilevanti per [tIndustry](https://github.com/Juan0177/tIndustry).
Il formato è ispirato a [Keep a Changelog](https://keepachangelog.com/); il versioning segue SemVer dove ha senso per un prototipo.

## [Unreleased]

### Corretto

- **Agenti che giocano, correzioni dopo la revisione e il primo uso**: `rigioca` arriva fino al tick in cui si è salvato il diario (anche il tempo dopo l'ultimo comando); nel gioco vero il diario riparte con l'intestazione giusta a ogni cambio di partita (nuova partita, livello, salvataggio, Continua da una persona, comando scaduto) e `eventi` lo dice (`partitaCaricata`, `inputReale`); `piazza {da, a}` fa l'incrocio automatico come il trascinamento (stessa funzione `BeltDrag`); `input {verso: {x, y}}` funziona; `stato` e `ruota` usano la direzione del giocatore; `ui` non elenca più controlli trasparenti, coperti o doppi, elenca la palette e clicca anche per testo o suggerimento (`ui {clic: "Ricerca"}`); codici `PartitaCambiata` e `MotoreNonDisponibile` dove la sessione resta viva; richieste oltre 64 KiB ed `esegui` oltre 120 s rifiutati senza chiudere il gioco; la pulizia all'avvio non cancella più le sessioni vive di un altro server (file `.in-uso`). Nuovi: strumento `catalogo`, campi `mancano` nei rifiuti, `impronta` in `stato`, `area {oggetti}`, `avvisi` di `rigioca`; `area.forme` elenca solo curve e T; `tools/list` in ordine stabile
- **Agenti che giocano, ultimi ritocchi della revisione finale**: se un primo caricamento fallisce e la partita viene staccata, i comandi rispondono `MotoreNonDisponibile` invece di agire sulla partita staccata; `ui` non elenca più i controlli coperti da una finestra popup aperta; il messaggio di `esegui` dice "più di 0 e al massimo 120", come il controllo; test diretti per il controllo dei percorsi del diario (`GameEngine.IsInside`)
- **Esc nella Home non esce più dal gioco**: durante una partita la Home ha **Riprendi** al posto di Continua (torna alla partita senza ricaricare il salvataggio automatico) ed Esc la richiude; nella Home iniziale Esc apre «Uscire dal gioco?» (Invio o **Esci** esce, Esc o **Annulla** annulla), mai un'uscita diretta
- Il toast di Nuova partita con una misura numerica non ripete più il numero ("64×64", non "64 64×64")

### Aggiunto

- **Agenti: cattura nella risposta, risposte compatte, avvio più veloce**: `cattura` restituisce l'immagine dentro la risposta MCP (PNG ridotto entro 640×360), con `alta: true` un vero rendering 1920×1080 (la finestra disegna per tre frame a quella misura) e con `regione` (pixel) o `da`/`a` (celle) un ritaglio; `percorso` salva ancora il file sotto `<profilo>/catture`. Le risposte di `stato`, `eventi`, `piazza`, `diario` e `ui` sono compatte (niente valori vuoti o predefiniti, costruzioni di fila in una voce, nomi di `ui` più corti; `ui` da 3614 a 1914 caratteri, `eventi` da 3446 a 884), `dettaglio: true` dà la forma intera. `apri_gioco` passa da circa 1,8 a 1,4 s (collegamento appena il gioco ascolta, cache degli shader condivisa fra le partite degli agenti) e `cattura` da circa 140 a 75 ms (conversione sRGB a tabella)
- **Agenti che giocano** (PR #210–#213 e #214): nuovo server MCP `tindustry` (`tools/AgentPlay`, avviato da Claude Code con `.mcp.json`) che fa giocare gli agenti AI con gli stessi comandi del giocatore. Tre motori con gli stessi strumenti in italiano: **simulazione** senza grafica e deterministica (`apri_simulazione`), **gioco vero** in una finestra 1280×720 con lingua, scala e colori a scelta (`apri_gioco`: catture, `telecamera`, `input` simulato, `ui` per trovare e cliccare i controlli, `log`) e **co-op** come segnaposto. Per vedere `stato`, `area` (fino a 64×64 celle), `trova`, `eventi`; per agire `piazza` (anche linee di nastri come il trascinamento), `smonta`, `ruota`, `sblocca`, `vendi`, `potenzia_core`; il tempo con `avanza`, `esegui`, `pausa`, `riprendi`; ogni bug si riproduce con `diario` e `rigioca` (stessa impronta della partita; gli input simulati non sono nel diario). Il canale del gioco (`AgentBridge`) esiste solo nelle build di sviluppo, con un profilo temporaneo e un segreto nuovo a ogni partita, ascolta solo su `127.0.0.1` e rifiuta le richieste di un browser; il gioco si chiude da solo quando il server se ne va. Nuovo playtest `agenti`, skill di progetto `gioca-e-testa` (ricette per cercare bug e fare catture) e ADR 0008. Senza `godot4` nel PATH di Windows basta la variabile d'ambiente `TINDUSTRY_GODOT`, che `.mcp.json` passa al server; con il progetto mai importato `apri_gioco` dice subito quali comandi eseguire

- **Nastri: unione a T, incrocio automatico, incrocio senza frecce** (come Mindustry): un nastro senza nulla dietro e con un ingresso su entrambi i lati (nastri o edifici che vi scaricano) si disegna a T, con le nuove strisce `conveyor-*-t-anim` di `tools/art` (solo grafica: gli oggetti si muovono come prima; la forma viene da `IGameView.GetBeltShape`). Trascinando una linea di nastri sopra un nastro semplice perpendicolare, in quella cella si piazza un **incrocio** (pagato come incrocio; coi piani il drone smonta il nastro e costruisce l'incrocio), così le due linee continuano a scorrere; nastri paralleli, sdoppiatori, selezionatori, ponti e incroci restano come prima, e un clic singolo non cambia nulla. Se l'incrocio non è ancora sbloccato il nastro resta e un toast, una volta per trascinamento, dice di sbloccarlo. Solo dove l'incrocio fa il lavoro del nastro (nastro dritto fra due pezzi di logistica, linea che prosegue oltre, niente edifici, curve o nastri segnati); annullare uno dei due piani li annulla entrambi. L'incrocio passa da 0,75 a 4 oggetti/s per direzione, così non rallenta nessun nastro. La regola è nella simulazione (`Factory.BeltCrossingAt`, `BeltLine`, `PlaceBelt.Crossing`, nuovo `RefusalCode.CrossingNeedsJunction`). Lo sprite dell'incrocio è ridisegnato senza le quattro frecce: due nastri che si incrociano con un piccolo perno al centro; ogni lato senza ingresso né uscita tiene il suo binario (`junction-base` più `junction-bordo` ruotato, lati da `IGameView.IsJunctionSideConnected`). La T è composta dalle due curve, con il binario dietro chiuso. Il playtest `ui` cattura `ui-nastro-a-t` e `ui-incrocio-automatico`; il sito mostra lo stato "T" dei nastri
- **Interfaccia in stile Mindustry** (G4, completa: PR #194, #195, #198, #199, #200, #201): un solo tema per tutto il client con pannelli quasi neri, bordo giallo-arancio e angoli squadrati; carattere pixel nostro con le lettere accentate, icone, logo e caselle di spunta generati da `tools/art` (nessun asset di terzi); anteprima di piazzamento come Mindustry; palette a griglia fissa che mostra solo i blocchi sbloccati; Scala UI 75–200% solo dell'interfaccia, col mondo sempre 1:1; schermo intero all'avvio nel gioco esportato e finestra 1280×720 in sviluppo; italiano e inglese in tutto il gioco, con i rifiuti della simulazione e gli errori dei salvataggi come codici tradotti dal client; colori per daltonici e ad alto contrasto, segni che non dipendono dal colore; Home col logo e la mappa che scorre. Decisioni nell'ADR 0006. Le voci per PR sono qui sotto
- **Italiano e inglese, rifiuti come codici** (G4 4/6): tutti i testi visibili passano da chiavi di `godot/lingue/testi.csv` (italiano predefinito, inglese completo), compresi nomi di blocchi, oggetti e ricette, livelli e obiettivi della campagna; nuova voce **Lingua** nelle Impostazioni, applicata subito ai pannelli aperti. La simulazione non scrive più frasi: i rifiuti sono `RefusalCode` (con la struttura bloccata o ciò che manca) e gli errori di caricamento `SaveLoadError`, tradotti dal client; i test confrontano codici. I nomi degli strumenti nei toast sono quelli della palette ("Incrocio", "Sdoppiatore"). `TestiTests` controlla chiavi, segnaposto e che nel client non restino frasi scritte nel codice; i playtest girano anche in inglese (`TINDUSTRY_PLAYTEST_LINGUA=en`)
- **Carattere e icone dell'interfaccia** (G4 1/6): carattere pixel nostro (`tools/art/carattere.py`, ASCII, lettere accentate e i segni dei testi) esportato come BMFont in due misure, e 13 icone `ui-*` a 16 e 32 px generate da `tools/art`, con la tavola di confronto `.dev/arte/tavola-ui.png`; un test controlla che ogni carattere dei testi del gioco abbia il suo glifo
- **Accessibilità e menu principale** (G4 5/6): nuova voce **Colori** nelle Impostazioni (Normali, Daltonici per deuteranopia e protanopia, Alto contrasto), applicata subito a tema, HUD, pannelli e segni nel mondo. Segni che non dipendono dal colore: X sull'anteprima rifiutata, tratteggio sui blocchi da smontare, barra sul denaro mancante, "!" sull'energia bassa, freccia sul prezzo in calo. L'anteprima del Minatore T1 su un giacimento di solo titanio è rossa, col motivo di `Factory.CanPlace` ("Serve il Minatore T2 per il titanio"). La Home ha il logo pixel generato da `tools/art` e la mappa che scorre piano dietro, senza input del mondo; le caselle di spunta sono sprite di `tools/art`. Il playtest `ui` cattura ogni variante (`ui-colori-*`), la Home e il fantasma sul titanio
- **Schermo intero all'avvio, finestra in sviluppo, palette a griglia fissa**: il gioco esportato parte a schermo intero alla risoluzione dello schermo (mondo 1:1, più mappa sugli schermi grandi) e al primo avvio sceglie la Scala UI dall'altezza dello schermo (100/150/200%); in sviluppo e nei test resta in finestra 1280×720 al 100% (i playtest sempre). Nuova voce **Schermo intero** nelle Impostazioni. La palette di costruzione è una griglia fissa come in Mindustry (4 colonne, tante righe quante ne servono alla categoria più grande con tutto sbloccato; le caselle libere restano vuote), con icone più piccole (32 px) e la stessa misura in ogni categoria, con ogni sblocco e in ogni partita; corretto un errore che lasciava un riferimento alla casella del Cursore dopo aver lasciato Strumenti. Chi aggiorna da una versione precedente parte a schermo intero e tiene la sua Scala UI: la può cambiare in Impostazioni (Interfaccia)
- **Scala solo dell'interfaccia** (G4 3/6): la Scala UI (75–200%, passo 5, cursore nelle Impostazioni; predefinita 100%) ingrandisce HUD, pannelli e suggerimenti e non più il mondo, che resta della sua misura; il clic col mouse cade sulla cella giusta a ogni scala. Pannelli centrati che si adattano alla finestra (scorrono se serve), scheda del tutorial compatta in basso a sinistra (non copre le celle vicino al CORE), Home alta quanto i suoi pulsanti, righe di Gestione salvataggi su due righe. La palette torna a una categoria visibile quando la sua sparisce, H nella Ricerca mostra tutto l'albero, l'anteprima di piazzamento sta sopra i piani. Il playtest `ui` apre ogni pannello a 100, 125, 150 e 200% e controlla che nessun testo esca dal suo rettangolo e che i pannelli stiano nella finestra; `TINDUSTRY_PLAYTEST_SCALA=150` fa un playtest intero a quella scala
- **Interfaccia in stile Mindustry** (G4 2/6): un solo tema per tutto il client (pannelli quasi neri con bordo giallo-arancio, angoli squadrati, carattere pixel nostro, icone pixel bianche generate da `tools/art`), al posto degli stili sparsi e delle icone di terzi. Anteprima di piazzamento come Mindustry: lo sprite del blocco semitrasparente, ruotato per i nastri, con il contorno del suo ingombro (giallo se si può piazzare, rosso se no) e una freccia per i blocchi con una direzione; anche i piani di un trascinamento si vedono così, su uno strato che la notte non scurisce. La palette mostra solo i blocchi già sbloccati in Ricerca (una categoria senza blocchi sbloccati sparisce; un blocco appena sbloccato compare subito), e la Home mostra "Continua" solo se c'è un salvataggio da continuare. La Ricerca tiene in vista il blocco scelto e scrive i nomi su due righe
- **Sito su GitHub Pages** (primo pezzo): pagina d'ingresso e sezione **asset** con tutti gli sprite del gioco, costruita da `tools/sito/costruisci.py` a partire dal registro di `tools/art` (mai ritoccata a mano) e pubblicata dal nuovo workflow `Pages`, solo a mano o sui tag di release. In futuro la sezione asset sarà una parte del sito completo
- **Sito: blocchi come in gioco e ambienti**: nella sezione asset ogni blocco (minatori, forno, assemblatore, estrattore, generatore, Lampada, nastri, selezionatore, ponte, Core) è una scheda sola composta con gli stessi strati, spostamenti e velocità del client (tabella in `tools/sito/composti.py`, una voce per blocco con il file C# che rispecchia), con i bottoni degli stati (Al lavoro / Fermo, Con corrente / Senza, Accesa / Spenta, Dritto / Curva…) animati o fermi; i pezzi che lo compongono si aprono con la lente in una finestra (Esc o clic fuori per chiudere), senza script. Nuova sezione **Ambienti**: un pezzo di mappa composto per ogni bioma, con la regola del terreno di `tools/art`
- **Droni nello stile di Mindustry**: alpha, beta e gamma ridisegnati a mano pixel per pixel (piastre grigie a strati, smussi a 45°, cella della squadra luminosa al centro) al posto delle sagome a freccia che sembravano aerei; nel gioco ruotano con il filtro lineare e hanno un motore che pulsa sotto la coda
- **Effetti, ombre e luci** (G3): particelle di lavoro (fumo e fuoco del forno, scintille di trivella e pressa, vapore del generatore, sbuffi dell'estrattore, polvere del Core), di costruzione (scintille, lampo e polvere del cantiere, sbriciolamento allo smontaggio) e d'ambiente per bioma (neve, sabbia, foglie), tutte con `CPUParticles2D` da un pool e con un tetto per schermata (Alti 4000, Bassi 1200, Spenti 0), solo dentro la vista più 4 celle. Ogni effetto è una riga di `godot/effetti/effetti.json`, validata da `EffectRules`. Ombre sotto ogni blocco e bagliore (glow con soglia HDR) su forni, Core, Lampada e scintille. **Notte** opzionale: casella in Nuova partita e livelli 6 e 10 della campagna; il mondo si scurisce e restano le luci di Core, forni al lavoro, generatori, droni e della nuova **Lampada** (1×1, consuma 1 di corrente al secondo, si sblocca dopo il Nodo T1), con le lucciole sui prati. Impostazioni grafiche: Effetti (Alti / Bassi / Spenti), Ombre, Bagliore, Animazioni dei blocchi, Limite FPS (30–144) e Scala rendering (100 / 75 / 50%) per i PC deboli; i vecchi file di impostazioni restano validi. Nuovi sprite `fx-*`, `lamp` e `luce` da `tools/art`. Nei playtest effetti, ombre, bagliore e notte sono spenti (le catture restano stabili); `TINDUSTRY_PLAYTEST_EFFETTI=1` lancia il probe con effetti Alti. Salvataggi sempre alla v12: `night` e le lampade si scrivono solo se presenti. PR #186–#189
- **Arte del terreno** (G2b): suoli dei 4 biomi con 4 varianti ciascuno, bordi tra suoli diversi (priorità `edgePriority` nella tabella `terrain`), pareti in rilievo che si raccordano a doppia griglia (16 pezzi per parete), acqua bassa e profonda con increspature in uno shader in coordinate del mondo, giacimenti a puntini (il carbone con un riflesso chiaro) e decorazioni per bioma, tutto generato da `tools/art/` come gli altri sprite. `TerrainLook` in `TIndustry.Shared` porta in C# la regola del riferimento Python e una fixture (`terrain-look-24.json`) li confronta. Il client ha due livelli di dettaglio: sotto 16 px per cella i pezzi a colori da 64×64 celle, da 16 px in su pezzi d'arte da 16×16 celle (cache LRU di 96, al massimo 4 composizioni per frame). La griglia sparisce al livello d'arte e compare tenue solo con uno strumento di costruzione. Enorme a 240 FPS di media a zoom minimo, 235 scorrendo a zoom 1, circa 555 MiB. PR #182–#184
- **Mappe e terreno** (G2a): le partite normali si generano dal seme con un generatore a biomi (rocce e basalto, sabbia e deserto, neve e ghiaccio, prati e paludi) in tre misure, Media 1000×1000, Grande 1500×1500 ed Enorme 2000×2000, con laghi, fiumi e creste di pareti. Le pareti e l'acqua profonda non si costruiscono ("C'è una parete", "Acqua profonda"), l'acqua bassa sì, il ponte scavalca tutto e il drone vola sopra. Partenza garantita: area libera attorno al CORE, ferro e rame vicini, strada a terra verso ferro, rame e carbone. Nuova schermata **Nuova partita** con misura e seme (casuale, modificabile, copiabile) e "Generazione della mappa…" per le mappe grandi. I 10 livelli della campagna hanno mappe prestabilite in `data/maps/` (PNG del terreno e dei giacimenti più json), da 200×200 a 1000×1000, create e controllate con il nuovo `tools/MapTool` (`genera`, `valida`, `anteprima`). Il terreno si disegna a pezzi di 64×64 celle, solo attorno alla vista: mappa Enorme a 240 FPS di media a zoom minimo. Gestione salvataggi mostra il tipo di mappa di ogni slot. PR #175–#180
- **Arte dei blocchi generata da script** (G1): edifici, nastri, Core, drone e oggetti sono pixel-art originale in stile Mindustry, scritta da `python tools/art/genera.py` (32 px per cella, mostrati ×2 senza filtro), al posto delle icone di terzi e del vecchio `tools/gen_structure_blocks.py`. Edifici a strati con parti animate (trivella, bocca del forno, bracci dell'assemblatore, rotore del generatore, lampada del minatore avanzato); nastri a 8 fotogrammi che scorrono alla velocità del loro tier, curve raccordate, `conveyor-express` col suo sprite, ponte con campata ripetuta; Core in 4 tier (2×2…5×5, in partita il 2×2) con luce lenta sul nucleo; droni alpha, beta e gamma; ogni oggetto con icona da 32 px e sprite da 10 px per i nastri, senza tinta. Un solo accesso alle texture nel client (`Sprites`, riquadro magenta e avviso se manca un PNG). PR #168–#173
- **Costruzione come Mindustry: piani, drone, cantieri, smontaggio con rimborso** (sotto-progetto 3, parte 4). Le partite nuove usano la costruzione vera: piazzare o trascinare nastri crea piani fantasma, il drone vola a costruirli uno alla volta e il blocco si disegna dal basso; senza materiali il cantiere si ferma (tooltip "Mancano: N oggetto") e riparte quando arrivano. Tasto destro: cancella un piano, annulla un cantiere (rimborso di quanto speso) o pianifica lo smontaggio, in rosso, anche trascinando su più blocchi. Nuovo riquadro della coda in alto a sinistra, sotto le risorse del Core (blocco in lavorazione, barra, "+N" piani; clic = camera sul cantiere). Un piano scartato mostra il motivo; niente più "Risorse insufficienti" al piazzamento. Il tutorial avanza quando il minatore è finito e spiega il drone. Correzioni della revisione finale: un cantiere fermo per i materiali da un secondo va in fondo alla coda se altri piani aspettano, come in Mindustry, e riprende da dove era quando ritocca a lui; il riquadro della coda colora d'ambra un cantiere fermo e scrive "Manca: <materiale>"; i blocchi segnati per lo smontaggio hanno un riempimento rosso, il segno si toglie da qualunque cella del blocco (anche dall'altro capo di un ponte) e un trascinamento col destro non lo toglie mai (lo fa solo il clic singolo); i trascinamenti veloci non lasciano più buchi; un nastro accanto al Core è pianificato già rivolto verso il Core; il tooltip "Mancano" segue zoom e visuale e non compare sopra l'HUD; un blocco che copre il Core in una cella qualsiasi dà "Non puoi costruire sul Core"

### Documentazione

- **Interfaccia, lingue e carattere** (G4 6/6): nuovo ADR `docs/decisioni/0006-interfaccia-lingue-carattere.md` (tema dal codice, tavolozza semantica e varianti, carattere BMFont a misure intere, icone e logo generati, Scala UI solo dell'interfaccia e mondo 1:1, regola di avvio, traduzioni per chiave, codici di rifiuto ed errori dei salvataggi, segni non di colore, scelte prese con l'utente). `docs/ARCHITETTURA.md` ha la sezione "Interfaccia (G4)" e "Come aggiungere un testo o una lingua"; `CLAUDE.md` le regole "testi solo da `testi.csv`" e "stili solo dal tema"; `godot/README.md` la Lingua e le icone generate; `tools/art/README.md` icone, caselle, logo e copertura del carattere; README con le catture nuove della G4 e la G4 fatta; indice delle decisioni, specifica (gli oggetti fuori dalla tavolozza per daltonici, deciso con l'utente) e mappa Obsidian aggiornati. Il sito ha una sezione **Catture** nella pagina d'ingresso con le catture del README. Il nome del cantiere nella coda dell'HUD ora è tradotto. Maschere di `compare_shots.py` misurate per le catture nuove; base candidata degli screenshot in `.dev/baseline-g4-candidata` (italiano e inglese), da approvare. Ondata finale della G4: immagini e materiali creati in C# (drone, cantieri, particelle, acqua, segnaposto degli sprite) liberati subito, così l'uscita non lascia nulla al GC (0 "Leaked unsafe reference" in 11 playtest `tutorial` e 7 `ui`); nei playtest restano fermi anche trivelle, nastri, frecce del ponte, shader dei cantieri e raggio del drone, e le maschere di `compare_shots.py` passano da riquadri fino a 400×200 px a pochi pixel; il tema cambia variante con una sola notifica; barra della coda, separatore decimale per lingua (`formato.decimale`), piani ridisegnati solo quando cambiano, Gestione salvataggi aperta da Impostazioni della Home non riapre la Home sotto; nuovi controlli dei playtest: Scala UI al 75%, cantieri in smontaggio tratteggiati, mappa che scorre sotto Gestione salvataggi
- **Revisione finale della G3**: una Lampada senza consumo di corrente positivo è rifiutata dal contenuto e non si accende più gratis; il Core di notte si calcola una volta per partita; il bagliore spento non crea più un `World3D` sul viewport ridotto; adiacenza ai generatori con un controllo rapido del riquadro (stesso risultato, la notte sulla mappa Enorme con 574 Lampade passa da 5 a 237 FPS); il probe degli effetti controlla che il pool di emettitori resti entro il 3 % e il probe PERF misura la notte sulla mappa Enorme; ADR 0004, ARCHITETTURA, indice delle decisioni (notte facoltativa e grafica per PC deboli) e mappa del progetto (la G4 solo tra i Prossimi) allineati.
- **Effetti e luci** (G3 4/4): sezione "Effetti e luci (G3)" nell'ADR 0004 (sprite delle particelle senza contorno, ombre, bagliore, Lampada e luce, prestazioni), "Effetti, ombre e luci", impostazioni grafiche e "Come aggiungere un effetto" in `docs/ARCHITETTURA.md` (con `LightingSystem` nell'ordine dei sistemi), impostazioni, notte e Lampada in `godot/README.md`, README con la G3 fatta e il sito web su GitHub Pages in roadmap, mappa Obsidian aggiornata. Base candidata degli screenshot in `.dev/baseline-g3-candidata`, da approvare
- **Arte del terreno** (G2b 3/3): sezione "Terreno (G2b)" nell'ADR 0004 (nomi e pezzi degli sprite, bordi con priorità, pareti a doppia griglia, acqua con shader, `TerrainLook` e fixture, due livelli di dettaglio, prestazioni), "Come aggiungere lo sprite di un suolo" in `docs/ARCHITETTURA.md`, README con catture vere del terreno e roadmap, mappa Obsidian con la G2b fatta e la G3 in corso. Ondata finale di correzioni: ARCHITETTURA descrive i due livelli di dettaglio (`TerrainComposer`, `TerrainLook`, `water.gdshader`); i pezzi a colori usano il colore medio dell'arte di ogni suolo e parete (niente salto di tono cambiando zoom; colori della tabella invariati); composizione dei pezzi d'arte limitata anche a 3 ms per frame (scorrendo a zoom 1 il frame più lento passa da 23 a 14 ms); decorazioni della sabbia a ciuffi secchi e lastre chiare, non più ovali color rame; cattura dei minerali a zoom 0,5 con tutti e 5; la prova PERF misura anche la memoria video (al massimo 132 MiB); il playtest segnala ERROR se l'arte non è pronta; terreno nelle attribuzioni. Base candidata degli screenshot in `.dev/baseline-g2b-candidata`, da approvare
- **Mappe a seme e in file** (G2a 6/6): nuovo ADR `docs/decisioni/0005-mappe-seme-e-file.md` (cella di 3 byte, tabella `terrain`, regole in un solo punto, generatore classico congelato, generatore a biomi con rumore intero e partenza garantita, mappe della campagna in file, salvataggi v12). `docs/ARCHITETTURA.md` ha la sezione "Terreno e mappe" e "Come aggiungere un suolo, una parete o un livello"; `CLAUDE.md` le mappe in `data/maps/` e il comando di `MapTool`; `godot/README.md` la schermata Nuova partita, le misure, il seme, pareti e acqua; README, indice delle decisioni, spec e mappa Obsidian aggiornati (G2a fatta, G2b in corso). Rifiniture dalle revisioni: `PngReader` limita i dati compressi alla misura dell'intestazione e rifiuta i dati in eccesso; un id di mappa nel salvataggio segue la stessa regola dei file delle mappe; il caricamento di una partita dalla Home o da Gestione salvataggi mostra l'errore invece di perderlo, e tasti e clic sono ignorati mentre la mappa si genera. Ondata finale di correzioni della G2a: il generatore a biomi ha un nome con versione (`biomi-1`, congelato; `biomi` dei primi salvataggi vale uguale) e il salvataggio tiene l'impronta del terreno, così un terreno rigenerato diverso ferma il caricamento con un messaggio chiaro; un ponte senza uscita su pareti o acqua profonda dice il motivo vero; senza la cartella `data/maps` un livello della campagna mostra l'errore invece di ripiegare sul generatore classico; la partita di sfondo dietro la Home è una mappa piccola (256², più veloce all'avvio); gli errori di Nuova partita compaiono sul pannello; il drone fermo non oscilla nei playtest
- **Arte generata da script** (G1 6/6): nuovo ADR `docs/decisioni/0004-arte-generata-da-script.md` (perché da script, tavolozza e luce, nomi e tier, strisce quadrate, dipendenza da Pillow 12.3), sezione "Grafica e sprite" con "Come aggiungere lo sprite di un blocco" in `docs/ARCHITETTURA.md`, regola in `CLAUDE.md` (sprite solo da `tools/art/genera.py`, mai PNG ritoccati a mano), README con catture vere della grafica nuova e roadmap aggiornata, mappa Obsidian con la G1 fatta e la G2a in corso. Pulizia: tolti `core.png` (copia non usata di `core-1`) e `ui/settings.png` (non usata); i bracci chiusi dell'assemblatore ora si toccano senza sovrapporsi; il minerale e le lastre di piombo tendono al blu-violetto per non confondersi col ferro sui nastri; un `-belt.png` mancante dà un avviso
- **Rifiniture finali della G1**: le parti animate (trivelle, forni, rotori, luce del Core) partono da una fase legata alla posizione del blocco, così blocchi vicini non vanno all'unisono; i nastri di un tier condividono una sola texture animata (un aggiornamento per tier, non per cella); carbone e titanio sui nastri hanno un bordo chiaro dentro il contorno scuro; le icone dei blocchi nella palette sono nitide (scala intera, filtro Nearest); i colori piatti del piombo (giacimenti, filtro dell'estrattore) seguono il blu-violetto degli sprite; l'icona Campagna passa da `Sprites`. In CI (workflow manuali e di release) girano i test del generatore e `--controlla --pixel` (confronto dei pixel: su Linux i byte dei PNG sono diversi), con un controllo che in `godot/assets/` ci siano solo sprite generati. Il playtest di prestazioni misura anche 100 edifici a strati al lavoro (235 FPS di media, 166 di minimo, 844 nodi)
- **Costruzione come Mindustry** (sotto-progetto 3, parte 5): nuovo ADR `docs/decisioni/0003-costruzione-come-mindustry.md` (cantiere separato e drone automatico, con le scelte prese durante i lavori: prelievo per eccesso, rimborso in punti base, annullamento graduale, celle del cantiere salvate, ordine di `Remove`). `docs/ARCHITETTURA.md` ha la sezione "Costruzione" (piani, drone, cantieri, formula, prelievo e rimborso, eventi, client), l'ordine dei sistemi con `Builder` e `Construct`, i salvataggi v11 e "Come aggiungere un blocco" con `BuildSpec` e `Factory.PlaceInstant`. `CLAUDE.md` passa ai salvataggi v11 e spiega `InstantBuild` nei test; `godot/README.md` spiega piani, cantieri, tasto destro e coda nell'HUD. Spec del sotto-progetto 3 approvata; indice delle decisioni, README e mappa Obsidian aggiornati (`tools/dev/mappa_canvas.py` scrive la mappa nel formato di Obsidian)
- **README rinnovato**: banner, galleria di screenshot, anteprima della nuova pixel-art, tabella dei controlli, roadmap; dettagli di sviluppo in sezioni richiudibili
- **Per Obsidian**: indice delle decisioni (`docs/decisioni.md`) e mappa del progetto (`docs/mappa-progetto.canvas`, rigenerata da `tools/dev/mappa_canvas.py`)
- **Diario di lavoro** in `docs/diario/`: un file per piano di implementazione completato (task, PR, revisioni, decisioni autonome, rinvii, verifiche), con il diario del sotto-progetto 2; `.gitattributes` forza LF sui `.md` per non vederli modificati dopo l'apertura in Obsidian

### Sviluppo

- **Dati del giocatore protetti nei playtest**: con `TINDUSTRY_PLAYTEST` ogni scrittura di salvataggi, impostazioni e progressi passa da `GameDataPaths.EnsureWritable` e viene rifiutata se cade fuori da `TINDUSTRY_DATA_DIR`; senza profilo, o con un percorso relativo, il gioco esce subito con errore (codice 3). Prima della prima scrittura automatica di una sessione la vecchia **Continua** viene copiata in `continua.prev.json` (stessa cartella, visibile e caricabile da Gestione salvataggi)
- **`user://` isolato nei playtest e nelle partite con profilo**: `playtest.sh` e `dev.sh` (senza `--editor`) puntano `APPDATA` al profilo, così `shader_cache`, `vulkan`, log e `.recovery_mode_lock` non finiscono più in `%APPDATA%\Godot\app_userdata\tIndustry`
- **Costruzione in corso (sotto-progetto 3, parte 1)**: base della simulazione per i cantieri come Mindustry. Nuove regole di partita `GameRules` (`RefundPercent`, `InstantBuild`), `BuildSpec` (cosa costruire e dove) e cantieri `ConstructSite` con `Construction`: tempo `(20 + Σ quantità × peso) × moltiplicatore` diviso per la velocità del drone, risorse prelevate dal Core man mano, cantiere fermo quando mancano e ripartenza da solo, smontaggio con rimborso graduale (edificio finito: regola di partita × rimborso dell'edificio; cantiere annullato: il 100% di quanto speso). I cantieri occupano le loro celle. Dati: `buildWeights` e `units.drone` in `content.json`, `refundPercent` per livello in `campaign.json` (livelli 1–3 al 100%, dal 4 al 50%). I comandi di piazzamento passano da `Factory.PlaceInstant`; il gioco non cambia ancora: drone e piani arrivano nella parte 2
- **Costruzione in corso (sotto-progetto 3, parte 2)**: ogni partita nuova ha il **drone** del giocatore (`BuilderUnit`) al centro del Core, con una coda di piani (`BuildPlan`). Con la costruzione vera (`InstantBuild` spento) `PlaceBuilding` / `PlaceBelt` accodano un piano controllando solo sblocco e celle libere (anche dai piani di qualunque giocatore), senza più il rifiuto "Risorse insufficienti"; il drone vola verso il primo piano (22,5 tile/s), a distanza ≤ `buildRange − 1` ricontrolla le celle e avvia il cantiere, poi torna a riposo accanto al Core. `Remove` cancella un piano, annulla un cantiere in costruzione (rimborso del 100% di quanto speso) o accoda lo smontaggio di un edificio o nastro. Nuovi sistemi `Builder` e `Construct` in testa all'ordine fisso, eventi `PlanDropped` / `ConstructFinished` / `DeconstructFinished` (`SimHost.EventRaised`) e in `IGameView` droni, cantieri e code. Il client usa ancora la costruzione istantanea fino alla parte 5
- Tolto il "riposizionamento gratuito": piazzare un secondo minatore, forno, assemblatore o generatore su un'area occupata dall'unico esistente non lo sposta più gratis

### Cambiato

- **Unione laterale dei nastri**: un nastro che riceve da dietro e da un fianco non si disegna più come un dritto chiuso, che sembrava scollegato: si disegna come il "blocco col punto" dell'incrocio, aperto verso il fianco da cui entrano gli oggetti (una linea secondaria che si unisce alla principale, o un edificio), con il bordo del nastro che scorre, e chiuso sull'altro fianco come un dritto. Con nastri su entrambi i fianchi e dietro si disegna la T con il binario dietro aperto; con un edificio su almeno uno dei due fianchi, il blocco col punto aperto su entrambi. Come in Mindustry gli edifici si uniscono ai nastri per capacità: minatori, forni, assemblatori, estrattori, generatori e Core fanno curve, T e unioni anche quando non scaricano proprio in quel nastro (lampade e nodi no); è solo grafica, la simulazione e l'incrocio automatico non cambiano. Nuove strisce `conveyor-*-join-left-anim`, `-join-right-anim`, `-join-both-anim`, `-join-building-both-anim` da `tools/art`, nuove forme `BeltShape.JoinLeft`, `JoinRight`, `JoinBoth`, `JoinBuildingBoth` (anche nello strumento `area` degli agenti: `t`, `unione-sinistra`, `unione-destra`, `unione-doppia`, `unione-edificio-doppia`); gli oggetti si muovono come prima e restano visibili. Il sito mostra le unioni fra le parti dei nastri. **Basamento sotto i blocchi**: minatori, forno, assemblatore, estrattore, generatore e Core stanno su una piastra rettangolare scura che riempie gli angoli smussati, così accanto ai nastri non si vede più il terreno. Nuove catture `ui-nastro-accanto-minatore`, `ui-nastro-unione-laterale` e `ui-nastro-tre-lati` nel playtest `ui`; cambiano `ui-curva-uscita` e le catture con minatori, forni, assemblatori, generatori, estrattori e Core
- **Curva dei nastri all'uscita degli edifici**: come in Mindustry, un nastro che riceve da un minatore, un forno, un assemblatore o un estrattore sul suo fianco e poi prosegue in un'altra direzione si disegna curvo; un nastro con chi lo alimenta da dietro resta dritto anche se ha qualcosa sul fianco (prima poteva diventare curvo per l'ordine delle direzioni), e con alimentatori su entrambi i fianchi resta dritto (incrocio a T). Contano anche le uscite laterali di sdoppiatore, selezionatore e fine del ponte, e un incrocio quando dietro di lui c'è chi lo alimenta; Core, generatori, nodi e lampade no. I nastri si ridisegnano appena un edificio vicino viene finito o smontato. Nuova cattura `ui-curva-uscita` nel playtest `ui`
- **Titanio solo con il Minatore T2**: come in Mindustry, dove il titanio vuole una trivella di tier più alto, il Minatore T1 non scava più il titanio. Un T1 dove sotto c'è solo titanio viene rifiutato con "Serve il Minatore T2 per il titanio" (comando, piano e piano scartato dal drone); su celle miste scava gli altri minerali e conta solo le loro celle; senza minerale resta permesso come prima. Il Minatore T2 scava tutto. Durezza e tier sono dati: `tier` dei minatori in `buildings` e nuova tabella `terrain.deposits` con la `hardness` di ogni minerale (titanio 2, gli altri 1), controllati al caricamento. Il livello 9 della campagna parte con 950 di fondi (erano 400), 95 lastre di ferro (45) e 30 fili di rame (18): quanto basta per sbloccare e costruire un Minatore T2 senza allungare il livello, e la descrizione lo dice. Palette e Ricerca scrivono cosa scava ogni minatore. Salvataggi sempre alla v12, formato invariato: un T1 già piazzato sul titanio in una partita salvata continua a scavarlo (scelta voluta, per non rompere le fabbriche esistenti); un cantiere di T1 sul solo titanio salvato prima della regola, che non potrebbe mai finire, sparisce al caricamento e rimborsa, come uno su una parete
- **Salvataggi v12** (G2a): il salvataggio dice come rifare il terreno (generatore classico, a biomi con seme e lato, o mappa della campagna). I salvataggi v11 si caricano ancora sul terreno di prima (generatore classico); v10 e precedenti restano rifiutati. Se la mappa di un livello manca o ha cambiato misura il caricamento si ferma con un messaggio chiaro. Un cantiere salvato su una parete, sull'acqua profonda o fuori mappa sparisce al caricamento e rimborsa
- **Salvataggi v11** (costruzione in corso, parte 3): il salvataggio e l'istantanea portano le regole di partita, i droni con la coda dei piani e i cantieri (con le loro celle e quanto già speso o rimborsato). I salvataggi v10 non si caricano più ("Salvataggio di una versione precedente: non compatibile."); una partita caricata mantiene le regole con cui è stata salvata. Speso e rimborsato di un cantiere sono salvati per oggetto (i v11 scritti prima si leggono ancora) e il caricamento valida i cantieri: valori fuori scala limitati, celle mancanti ricalcolate, un cantiere non più costruibile tolto col suo piano e rimborsato

### Tutorial / UI

- **Ricerca a rami per tipologia** (come Mindustry): sotto il CORE un ramo **Logistica** (Nastro T1 → T2 → T3; Incrocio → Sdoppiatore / Selezionatore / Ponte), uno **Produzione** (Minatore → Estrattore, Forno → Assemblatore, Minatore T2) e uno **Energia** (Generatore → Nodo T1 → Nodo T2). Un solo genitore per blocco: Nastro T2, Generatore e Minatore T2 non richiedono più il Forno (restano vincolati dal costo in lastre); costi invariati
- **HUD stile Mindustry**: colonna in alto a sinistra con pulsanti etichettati **Menu · Ricerca · Mercato · Campagna** (al posto di 7 icone senza nome); pannello CORE con **Fondi** (non più "Magazzino $"), energia e **tutte** le risorse possedute come icona + numero; obiettivi sotto; tolti i contatori tecnici "consegnati / nastro"
- **Palette**: categorie a icone al posto di St/Lo/Pr/Po; niente numeri tagliati sugli slot, il tasto rapido è nel pannello info (`[2]`) e nel tooltip
- **Ricerca stile Mindustry**: albero dall'alto con **CORE** come radice e ogni blocco sotto il suo prerequisito (layout ad albero ordinato, linee a gomito, prerequisiti extra tratteggiati); nodi quadrati con il **nome sotto**; bordo dorato = sbloccabile ora, **lucchetto** finché non sblocchi il precedente; tooltip con costo e cosa manca; selezione iniziale sul primo blocco sbloccabile; trascina col sinistro, rotella = zoom, H = vista intera
- **Mercato**: tabella compatta con icona, quantità, prezzo (arancione se sotto listino), valore, **Vendi 1 / Vendi tutto**; pannello alto quanto il contenuto; interruttore ON/OFF ben visibile
- **Nastri**: gli oggetti sono a mezza casella con sagoma piena e contorno scuro (non si vede più la belt attraverso), passano **sotto** il CORE all'ingresso invece di sovrapporsi; frecce della belt più discrete
- **Tutorial non bloccante**: scheda in basso a sinistra, il gioco resta giocabile; ogni passo avanza **da solo** quando lo completi (visuale, palette, minatore, nastro→CORE, Mercato, Ricerca, forno) con "✓ fatto"; testi corretti sui comandi reali
- **Comandi**: trascina col tasto sinistro per muovere la visuale in modalità cursore (prima solo rotella premuta, ma il tutorial diceva "trascina"); tolti gli alias in conflitto **A** (assemblatore, ma A = pan WASD) e **G** (generatore, ma G = Campagna)
- **Impostazioni**: overlay risorse (FPS/CPU/RAM/GPU) **spento di default** per i nuovi giocatori

### Pulizia

- Simulazione: lo stato di gioco passa in `GameState` e la logica per tick in sette sistemi (`Belt`, `Power`, `Mining`, `Crafting`, `Extraction`, `Belt` a passo zero, `CoreDelivery`) eseguiti in ordine fisso da `Simulation.Step`; `Factory` resta la facciata per piazzamento ed economia. Solo spostamento, nessun cambio di comportamento
- Comandi e `SimHost`: ogni modifica al gioco (piazzare, rimuovere, sbloccare, vendere, vendita automatica, potenziare il CORE, filtro del selezionatore) è un comando validato all'applicazione, con esito come evento `CommandApplied` / `CommandRejected` con i testi italiani di prima. Nuovo comando `SetSorterFilter` e lista condivisa `SorterFilters`. Il client Godot tiene solo un `SimHost`, legge lo stato tramite `IGameView` e invia comandi; pannelli Ricerca e Mercato, toast e grafica reagiscono agli eventi. Nessun cambio di gioco: i rifiuti mostrano gli stessi toast (rimuovere su cella vuota resta silenzioso, trascinando non compare "occupata"); il tick parte da 0 a ogni nuova partita, livello o caricamento
- Documentazione della simulazione: `docs/ARCHITETTURA.md` descrive tick fisso, `GameState`, sistemi, comandi, `SimHost`, istantanea e checksum, con le cinque regole di determinismo e il ciclo di frame aggiornato; nuovo ADR `docs/decisioni/0002-sincronizzazione-ibrida.md` (host autoritativo che ordina i comandi, simulazione deterministica, checksum ogni 60 tick, recupero e ingresso a partita in corso via istantanea; il checksum periodico non è ancora implementato, arriva col multiplayer)
- Rimosse le modalità di cattura `TINDUSTRY_CAPTURE` / `TINDUSTRY_CAPTURE_MODE` (ereditate da Cursor); restano `TINDUSTRY_PLAYTEST` e `TINDUSTRY_CAPTURE_DIR`
- Rimossi `StaticMiner`/`StaticAssembler`/`StaticSmelter`, il nodo `Miner` della scena, lo shader `belt_corner.gdshader`, le cartelle `.cursor/` e `assets/` in radice (attribuzioni spostate in `godot/assets/ATTRIBUTION.md`)
- Rinomine con i nomi veri: `SpikeWorld`→`World`, `SpikeCamera`→`WorldCamera`, `Spike.tscn`→`Main.tscn` (nodo radice `Main`), `FactorySlice`→`Factory`, `FactorySliceSaveData/Store`→`FactorySaveData/Store`, `SmelterStub`→`Crafter`, `GeneratorStub`→`Generator`, `ExtractorStub`→`Extractor`, `PowerNodeStub`→`PowerNode`, `MinerProducer`→`Miner`, `StructureDefinition.Requires`→`Prerequisites`; test su percorsi dei dati e riferimenti del progetto Godot. Nessun cambio di comportamento né di formato dei salvataggi
- Cartelle per area: `godot/scripts/` in `world/`, `visuals/{belts,buildings,items}/`, `ui/{hud,panels}/`, `settings/`, `dev/`; `src/TIndustry.Shared/` in `Content/`, `Grid/`, `Buildings/`, `Power/`, `Economy/`, `Research/`, `Campaign/`, `Save/` (resta `Factory.cs` in radice); `SystemMonitor` passa al progetto Godot (`TIndustry.Godot`). Solo spostamenti, nessun cambio di comportamento
- HUD in componenti: `FactoryHud` (1.626 righe) diventa `Hud` che compone `CorePanel` (colonna in alto a sinistra), `BuildPalette` (categorie e griglia), `BlockInfoPanel` (striscia info e dettaglio `?`) e `HudStyle` (colori e widget condivisi). Aspetto e comportamento invariati
- Dati in un solo posto: `godot/data/` esce da git (è una copia di `data/` che l'export rigenera da sé; in sviluppo il gioco legge `data/`). Nuovi test di validazione dei contenuti (id unici, prerequisiti esistenti e senza cicli, icone presenti per strutture e oggetti del mercato, obiettivi di campagna su id noti) e test di salvataggio/ricarica

### Correzioni

- **Salvataggio atomico**: lo slot si scrive su un file temporaneo e poi si rinomina, quindi un crash durante il salvataggio non lascia più un salvataggio troncato
- **Doppio input nello stesso tick**: due pressioni di **C** su un selezionatore, due clic sull'interruttore di vendita automatica o due vendite ravvicinate non si annullano più e ogni vendita mostra il proprio guadagno
- **Caricamento fallito**: se il caricamento di un salvataggio fallisce, la partita precedente torna completa (ricerca, livello di campagna, visuale, oggetti sui nastri); al primo caricamento si resta senza partita
- **Potenziamento del CORE**: il rifiuto dell'host dice il motivo preciso (già al livello massimo, fondi o materiali mancanti)
- **Energia accumulata non più persa**: caricando un salvataggio (o un'istantanea) la riserva di energia sopra il minimo del CORE tornava a 24 perché veniva limitata prima di ripristinare la capacità; ora capacità e riserva tornano identiche
- **Caricamento più robusto**: un salvataggio di una versione futura o con contenuti illeggibili mostra "Salvataggio non leggibile." invece di testo tecnico, e un caricamento fallito lascia intatta la partita in corso
- Livello 1 della campagna: le 8 unità di minerale di ferro iniziali sono ora parte della creazione del livello (`NewGame.Campaign`), non del solo client

### Documentazione

- Nuovi `docs/ARCHITETTURA.md` (mappa delle cartelle, ciclo di un frame, dati, salvataggi, come aggiungere un blocco, test), `docs/decisioni/0001-server-csharp.md` (server multiplayer in C#) e `CLAUDE.md` (istruzioni per gli agenti); README aggiornati ai nuovi nomi e percorsi

### Cambiato

- **Salvataggi versione 10**: il file contiene i metadati dello slot più l'istantanea canonica dello stato (`FactorySaveStore.Save(slot, SimHost)` / `TryLoad(content, slot, …)`); i salvataggi v9 e precedenti non si caricano più. I salvataggi di versioni precedenti non sono più compatibili: caricandoli compare il messaggio "Salvataggio di una versione precedente: non compatibile." e la partita corrente resta com'è. Le partite nuove si costruiscono con `NewGame.Sandbox/Campaign/Demo`; spariti i nomi `Slice`/`Spike` (`CreateSandboxSlice`, `CreateSpikeDemo`, `UnlockGodotSliceDemo`, `SaveSlice`/`LoadSlice`…)
- **Continua non più sovrascritto a vuoto**: l'autosave alla chiusura (Esci, chiusura finestra, Menu/Esc → Home) scrive su Continua solo se in questa sessione hai giocato (nuova partita, livello di campagna o caricamento riuscito). Prima, aprire il gioco e uscire subito sostituiva Continua con la sandbox di avvio, anche quando il salvataggio era solo di una versione non compatibile

### Sviluppo

- **Simulazione a passo fisso (1/5 del sotto-progetto 2)**: la fabbrica avanza a tick fissi di 1/60 s (`Factory.Step()`, contatore `CurrentTick`); `SimulationClock` converte il tempo di frame in tick con un tetto di 5 per frame. I nastri sono percorsi in ordine canonico (riga, poi colonna) e gli edifici hanno un id stabile salvato (`Id`, `NextEntityId`, `CurrentTick` nel salvataggio; i file senza id li ricevono al caricamento). Nuovi test di determinismo (FPS diversi, ordine di piazzamento, salvataggio a metà partita, ritmo di gioco). Ritmo di gioco invariato per il giocatore
- **Avvio in dev all-in-one**: `dev.cmd` (Windows) / `tools/dev/dev.sh` controlla .NET 8 e Godot 4.4 mono, compila, importa gli asset se serve e avvia il gioco; opzioni `--profilo-temp`, `--nuova`, `--editor`, `--test`, `--playtest ui|tutorial`, `--solo-build`, `--import`
- **Testo e icone nitidi**: la UI usa il filtro lineare con mipmap (la mappa resta Nearest per la pixel art); prima icone 64px ridotte a 16–26px e testo alla scala 125% erano sgranati
- **Playtest isolati**: con `TINDUSTRY_PLAYTEST` gli input reali (mouse/tastiera sulla finestra) vengono ignorati; un click o Esc durante la prova la faceva fallire a caso. Il playtest `ui` lo verifica
- Rimossi 4 eventi HUD inutilizzati (0 avvisi di build)
- `SpikeWorld.cs` diviso in partial per responsabilità; i self-test di `FactorySlice` spostati nei test (−1.300 righe dal codice di gioco)
- `TINDUSTRY_DATA_DIR`: profilo dati isolato (impostazioni, salvataggi, campagna) per prove senza toccare i dati del giocatore
- `TINDUSTRY_PLAYTEST=tutorial`: playtest scriptato con input simulati (richiede `TINDUSTRY_DATA_DIR`), un'immagine per passo
- Le modalità `TINDUSTRY_CAPTURE` sono state rimosse in favore dei playtest scriptati (`TINDUSTRY_PLAYTEST`); gli screenshot dei playtest vanno in `TINDUSTRY_CAPTURE_DIR` o `godot/artifacts/` (ignorata da git)

### Rimosso

- **Client Raylib legacy** (`TIndustry.Logistics`, `src/App|Simulation|UI|Content`): Godot 4 .NET è l'unico client. Ultimo codice Raylib nel tag `v0.3.4`; niente più zip `tIndustry-raylib-legacy-*` nelle Release.

### Aggiunto / migliorato

- **Test**: progetto xUnit `tests/TIndustry.Shared.Tests` che esegue i 22 scenari self-test della sim Shared (prima mai lanciati) — sostituisce il `--self-test` Raylib come gate CI
- **Shared**: `FactorySliceSaveStore.SavesDirectoryOverride` — i test non toccano più i salvataggi reali del giocatore
- **CI**: workflow non più automatici su push/PR — `build-publish.yml` solo manuale; `release.yml` (tag `v*` o manuale) esegue i test Shared prima dell'export
- **Repo**: `tIndustry.sln` alla radice (Shared + test + Godot); CI e release solo .NET 8 / Godot

## [0.3.4] — 2026-09-26

Parity mondo Raylib: minerali, CORE centrato, H→core, bordi mappa, terrain, **tinte item distinte**. Zip ufficiali restano **export Godot**.

### Aggiunto / migliorato

- **Godot/Shared**: parity mondo Raylib — minerali icone/scout patches, CORE centrato + camera H/Home, bordi mappa + clamp, terrain color Raylib, no tutorial spam in campagna ([#129](https://github.com/Juan0177/tIndustry/pull/129))
- **Godot/Shared**: tinte item/minerali distinte (Raylib `ItemColor`) — silhouettes bianche modulate via `ItemVisualColors` su depositi, cargo, HUD stock, sorter filter ([#130](https://github.com/Juan0177/tIndustry/pull/130))

### Come giocare (download)

1. Scarica [`tIndustry-win-x64.zip`](https://github.com/Juan0177/tIndustry/releases/latest) (o linux)
2. Estrai → avvia `tIndustry.exe` (Windows) / `tIndustry.x86_64` (Linux)

Da sorgente: `godot4 --path godot` (vedi README).

## [0.3.3] — 2026-09-26

Godot QoL + mondo grande: gestione salvataggi completa, overlay risorse sistema, mappe **1000×1000** seed-based. Zip ufficiali restano **export Godot**.

### Aggiunto / migliorato

- **Godot**: Gestione salvataggi full — autosave Continua (Esc/Home/Esci), Salva come, seed in lista, ↑↓, Conferma elimina ([#125](https://github.com/Juan0177/tIndustry/pull/125))
- **Godot/Shared**: overlay **risorse sistema** (FPS · CPU · RAM · GPU · tempo processo) da Impostazioni ([#126](https://github.com/Juan0177/tIndustry/pull/126))
- **Godot/Shared**: mondo **1000×1000** seed-based (campagna + sandbox), clamp 1000, draw con **viewport culling** ([#127](https://github.com/Juan0177/tIndustry/pull/127))

### Come giocare (download)

1. Scarica [`tIndustry-win-x64.zip`](https://github.com/Juan0177/tIndustry/releases/latest) (o linux)
2. Estrai → avvia `tIndustry.exe` (Windows) / `tIndustry.x86_64` (Linux)

Da sorgente: `godot4 --path godot` (vedi README).

## [0.3.2] — 2026-09-26

Parity patch: Godot recupera i gap Raylib (P0–P1) — costi, multi-ricetta, power, tutorial, crumbs mid-game. Zip ufficiali restano **export Godot** (come 0.3.1).

### Aggiunto / migliorato

- **Godot/Shared**: costi di piazzamento + rimborso come Raylib (`FactorySlice` spende `BuildCost` / rimborsa su remove); sandbox Nuova partita parte con lastre/fili; toast IT se stock insufficiente ([#114](https://github.com/Juan0177/tIndustry/pull/114))
- **Godot/Shared**: forno/assemblatore **multi-ricetta** (auto-pick `smelt-*` / `craft-*` come Raylib) — piombo, titanio, grafite, silicio craftabili ([#115](https://github.com/Juan0177/tIndustry/pull/115))
- **Godot**: palette/place **Nastro T3** (`conveyor-express`) + **Nodo potenza T2** (`power-node-t2`) ([#116](https://github.com/Juan0177/tIndustry/pull/116))
- **Godot/Shared**: **CORE upgrade** (+25% vendite) da `economy.coreUpgrade`, chip HUD, save v5 ([#117](https://github.com/Juan0177/tIndustry/pull/117))
- **Godot/Shared**: forno **carbone OR corrente** (Raylib): buffer fuel + burn, craft bloccato senza; **+20%** se alimentato; save v6 ([#118](https://github.com/Juan0177/tIndustry/pull/118))
- **Godot/Shared**: **power buffer/capacity** (Raylib thin): core+gen ricarica, spend on craft, soft brownout → forno torna al carbone; HUD `pot. buf/cap`; save v7 ([#119](https://github.com/Juan0177/tIndustry/pull/119))
- **Godot**: **tutorial first-run** (8 step IT, Avanti/Salta, persist) + **Impostazioni** (VSync, scala UI, FPS overlay, rivedi tutorial) da Home / tasto I ([#120](https://github.com/Juan0177/tIndustry/pull/120))
- **Shared**: **I/O adiacente** edificio↔edificio/core (Raylib): miner/forno transferiscono senza nastro se i footprint si toccano ([#121](https://github.com/Juan0177/tIndustry/pull/121))
- **Godot/Shared**: **map size da campagna** (`level.MapWidth/Height`, core centrato, clamp 128× per draw Godot); save v8 seed/WxH ([#122](https://github.com/Juan0177/tIndustry/pull/122))
- **Godot**: **Gestione salvataggi** (lista slot, Carica/Elimina/Duplica Continua) da Home ([#123](https://github.com/Juan0177/tIndustry/pull/123))

### Come giocare (download)

1. Scarica [`tIndustry-win-x64.zip`](https://github.com/Juan0177/tIndustry/releases/latest) (o linux)
2. Estrai → avvia `tIndustry.exe` (Windows) / `tIndustry.x86_64` (Linux)

Da sorgente: `godot4 --path godot` (vedi README).

## [0.3.1] — 2026-09-26

Patch release: i download ufficiali della Release sono **export Godot**, non più Raylib.

### Cambiato

- **Release assets**: `tIndustry-win-x64.zip` / `tIndustry-linux-x64.zip` = client **Godot** (`tIndustry.exe` / `tIndustry.x86_64`)
- Alias `tIndustry-godot-*-x64.zip` (stesso contenuto)
- Raylib solo come `tIndustry-raylib-legacy-*-x64.zip`
- CI `release.yml`: install Godot 4.4.1 mono + templates → export Windows/Linux; Raylib in job separato
- `godot/export_presets.cfg`, `godot/TIndustry.Godot.sln`, seed in `godot/data/`, script `tools/ci/setup-godot.sh` + `export-godot.sh`

### Come giocare (download)

1. Scarica [`tIndustry-win-x64.zip`](https://github.com/Juan0177/tIndustry/releases/latest) (o linux)
2. Estrai → avvia `tIndustry.exe` (Windows) / `tIndustry.x86_64` (Linux)

Da sorgente: `godot4 --path godot` (vedi README).

## [0.3.0] — 2026-09-25

**Godot 4 .NET diventa il client ufficiale.** Raylib resta legacy (zip release ancora disponibili). Port slice completo: fabbrica, UI Mindustry, campagna, art, logistica.

### Cambiato

- **Godot 4 .NET = path ufficiale** per build & play ([#111](https://github.com/Juan0177/tIndustry/pull/111))
- Client Raylib (`TIndustry.Logistics`) demoted a **legacy** (manutenzione opzionale)
- CI: job **Godot + Shared**; publish zip Raylib invariato (etichettato legacy)

### Godot — gameplay & sistemi

- Factory slice: nastri (corner gallery), craft (forno/assy), junction/splitter, power stubs ([#66](https://github.com/Juan0177/tIndustry/pull/66)–[#87](https://github.com/Juan0177/tIndustry/pull/87))
- **Save/load** continua + slot ([#88](https://github.com/Juan0177/tIndustry/pull/88))
- **Sorter + ponte** (decision 14 thin span) ([#89](https://github.com/Juan0177/tIndustry/pull/89))
- **Ricerca** unlock + tech-tree grafo ([#91](https://github.com/Juan0177/tIndustry/pull/91)–[#92](https://github.com/Juan0177/tIndustry/pull/92))
- **Mercato** sell + `$` HUD ([#93](https://github.com/Juan0177/tIndustry/pull/93)); **auto-sell** al Core ([#99](https://github.com/Juan0177/tIndustry/pull/99))
- **Campagna** select + obiettivi HUD ([#94](https://github.com/Juan0177/tIndustry/pull/94))
- **Home splash** Continua / Campagna / Nuova partita ([#98](https://github.com/Juan0177/tIndustry/pull/98))
- **T2** nastro/miner, estrattore, power-node ([#100](https://github.com/Juan0177/tIndustry/pull/100))
- **Map seed** terrain da campagna/sandbox ([#101](https://github.com/Juan0177/tIndustry/pull/101))

### Godot — UI Mindustry

- Palette angolo + categorie St/Lo/Pr/Po · info strip · tech tree icon-only ([#95](https://github.com/Juan0177/tIndustry/pull/95)–[#97](https://github.com/Juan0177/tIndustry/pull/97))
- Cursor-default toolbar ([#90](https://github.com/Juan0177/tIndustry/pull/90))

### Godot — art

- Block sprites 64×64 coerenti mappa + palette ([#102](https://github.com/Juan0177/tIndustry/pull/102))
- Nastro T2 single chevron ([#103](https://github.com/Juan0177/tIndustry/pull/103))
- Logistics redesign splitter/sorter/bridge ([#104](https://github.com/Juan0177/tIndustry/pull/104)–[#105](https://github.com/Juan0177/tIndustry/pull/105))
- Miner gear animato ([#106](https://github.com/Juan0177/tIndustry/pull/106)–[#109](https://github.com/Juan0177/tIndustry/pull/109))
- Estrattore / forno / assemblatore / generatore / core redesign ([#110](https://github.com/Juan0177/tIndustry/pull/110))

### Limitazioni

- Combat / unità ancora deferred
- Zip GitHub Release = client **Raylib legacy**; per Godot: `godot4 --path godot` (vedi README)
- Art polish opzionale in corso

### Come giocare (ufficiale — Godot)

```bash
dotnet build src/TIndustry.Shared/TIndustry.Shared.csproj
dotnet build godot/TIndustry.Godot.csproj
godot4 --path godot
```

Richiede Godot **4.4+** .NET. Dettagli: [`godot/README.md`](godot/README.md).

### Legacy Raylib

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

O scarica gli zip dalla Release (`tIndustry-win-x64.zip` / `tIndustry-linux-x64.zip`).

## [0.2.11] — 2026-09-21

Polish completo sopra **0.2.10**: UX/copy IT, feedback, HUD, ghost placement — niente feature P2.

### Aggiunto / migliorato

- **Copy IT**: tutorial Potenza, dock Sdop/Selez/Pot., Ingresso/Uscita, magazzino (no “stock”), toast potenza/raggio, Menu principale, Ricerca, segnaposto
- **Font HUD**: atlas con — → … ’ ✓ Δ (niente più `?` ripetuti); atlas 160×scale + Bilinear per testo meno granuloso; spacing più stretto
- **Overflow**: truncate toast/home/ricerca/campagne/save/header; CORE upgrade sempre testo compatto (niente icone sovrapposte); Mercato righe dinamiche
- **Short labels**: Pb/Ti lastre → `Pb Ls` / `Ti Ls` nella strip
- **Feedback**: toast errore in ambra; refuse place nastri/logistica; salvataggio Esc con “Partita salvata.”; elimina slot a doppia conferma
- **HUD**: strip risorse overflow (+N, priorità stock); Fabbrica Pot. + tooltip legenda M/F/A/N/G/P; Home centrata; save manager responsive
- **Ghost**: silhouette anche se invalido; cerchio raggio nodi T1/T2
- **Campagna**: obiettivo hit-test (no click-through); label Magazzino; vittoria “Menu principale”
- **Docs**: README allineato a 0.2.11

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.10] — 2026-09-21

P1 gap close sopra **0.2.9**: campagna estesa, atlas icone, polish tech-tree UX.

### Aggiunto / migliorato

- **Campagna 10 livelli**: dopo Economia Foundry → Corrente (gen+nodo) → Piombo → Logistica (sorter) → Titanio/silicio → Espansione T2; bilanciamento starter/obiettivi mid-game
- **AppData campagna**: merge id seed mancanti + sync name/obiettivi/`unlocksNext` (come `content.json`)
- **Tech-tree UX**: Ctrl+rotella zoom (verso cursore), path highlight su prerequisiti/dipendenti, H/Home reset pan/zoom
- **Atlas icone**: `GameIcons` packa ~40 PNG in una sola texture (UV draw); API `Draw`/`TryDraw` invariata
- **Docs**: README allineato a 0.2.10

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.9] — 2026-09-21

P0 gap close sopra **0.2.8**: mercato dinamico, più minerali/ricette, polish animazioni/lighting.

### Aggiunto / migliorato

- **Mercato dinamico**: prezzi reagiscono allo stock (curva supply / softStock); bonus CORE applicato sul prezzo dinamico; Mercato mostra fino a 6 voci (stockate in alto)
- **Minerali**: depositi **piombo** + **titanio** (noise + starter/scout); item grezzi + lastre
- **Ricette**: forno multi-ricetta (`smelt-iron` / `smelt-lead` / `smelt-titanium`); assemblatore (`craft-copper-wire` / `craft-graphite` / `craft-silicon`)
- **Grafica**: glow forno gated su fuel/power + bloom; trivella idle rock a 0%; ombre soft a 2 layer
- **Docs**: README allineato a 0.2.9

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.8] — 2026-09-21

Release di contenuto sopra **0.2.7**: mid-game Phase 6, I/O, tech tree, sorter, potenza a nodi, forno carbone-o-corrente, icona minatore **trivella**.

### Aggiunto / migliorato

- **I/O belt-uscente** + trasferimento **edificio adiacente** (miner|forno, forno|assy, →CORE); overlay amber/ciano; tutorial **14** passi ([#38](https://github.com/Juan0177/tIndustry/pull/38))
- **Tech tree a grafo**: nodi/archi da `prerequisites`; hit-test unlock + sync prereq AppData ([#40](https://github.com/Juan0177/tIndustry/pull/40), [#47](https://github.com/Juan0177/tIndustry/pull/47))
- **Selezionatore (sorter)**: filtro item (F), routing Mindustry ([#39](https://github.com/Juan0177/tIndustry/pull/39))
- **Grafica mondo (Raylib)**: silhouettes TI-inspired, terrain micro-variazione ([#42](https://github.com/Juan0177/tIndustry/pull/42))
- **Mid-game T2/T3**: Minatore T2, Nastro T3, fuel generatore (carbone) ([#41](https://github.com/Juan0177/tIndustry/pull/41))
- **Icone**: ore rock+tint ([#44](https://github.com/Juan0177/tIndustry/pull/44)); minatore = **trivella / auger** Lorc `drill` (non trapano a pistola); T2 riusa lo stesso PNG
- **AppData**: merge id seed mancanti (no crash) ([#45](https://github.com/Juan0177/tIndustry/pull/45)); sync `displayName` tier ([#46](https://github.com/Juan0177/tIndustry/pull/46))
- **Overlay versione** semi-trasparente in play HUD ([#48](https://github.com/Juan0177/tIndustry/pull/48))
- **Nodi potenza** T1/T2 (linee dirette + auto-link); cavi tile superseduti; grafo esclude CORE ([#49](https://github.com/Juan0177/tIndustry/pull/49)–[#51](https://github.com/Juan0177/tIndustry/pull/51))
- **Forno = carbone OR corrente** (+20% craft se alimentato da rete) ([#52](https://github.com/Juan0177/tIndustry/pull/52))
- Naming UI a **codici tier** (Minatore T1/T2, Nastro T1/T2/T3)
- **Docs**: README **revamp** per 0.2.8 (pitch mid-game, download first, controlli/power aggiornati)

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.7] — 2026-09-19

Patch UX sopra **0.2.6**: bottone upgrade **CORE** mostra costo completo (×N lastre + $), come il footer dock. ([#35](https://github.com/Juan0177/tIndustry/pull/35))

### Aggiunto / migliorato

- **Fabbrica / CORE**: label Peak-style `CORE · ×N [icona lastre] · +$cost`; toast di afford cita `×N lastre` (non più "+ lastre" vago) ([#35](https://github.com/Juan0177/tIndustry/pull/35))
- **Docs**: README allineato a 0.2.7

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.6] — 2026-09-19

Patch UX sopra **0.2.5** (Campagna già inclusa): polish Mercato/dock, ricette Input/Output nel footer, fix conteggi Fabbrica e upgrade CORE. ([#31](https://github.com/Juan0177/tIndustry/pull/31), [#32](https://github.com/Juan0177/tIndustry/pull/32), [#33](https://github.com/Juan0177/tIndustry/pull/33))

### Aggiunto / migliorato

- **Mercato / dock**: padding e accent polish; seeding capture per still HUD ([#31](https://github.com/Juan0177/tIndustry/pull/31))
- **Dock footer**: riga **Input / Output** (icone ×N + durata ricetta) quando tieni Forno/Assemblatore; sotto resta il costo build ([#32](https://github.com/Juan0177/tIndustry/pull/32))
- **Fabbrica HUD**: conteggi `M/F/A/N/G` restano sopra **CORE** a scala UI di default; click CORE con feedback afford / già potenziato ([#33](https://github.com/Juan0177/tIndustry/pull/33))
- **Docs**: README allineato a 0.2.6

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.5] — 2026-09-19

Patch sopra **0.2.4**: modalità **Campagna** a livelli + fix clip Mercato e costi build a icone nel dock. ([#28](https://github.com/Juan0177/tIndustry/pull/28), [#29](https://github.com/Juan0177/tIndustry/pull/29))

### Aggiunto / migliorato

- **Campagna**: Home → **Campagna** → selezione livelli data-driven (`data/campaign.json`); obiettivi (earn/stock/unlock/sell); HUD **OBIETTIVO**; progressione in AppData `campaignProgress.json`; 5 livelli starter ([#28](https://github.com/Juan0177/tIndustry/pull/28))
- **Mercato**: bottoni `1` / `tutti` inset e scale-safe — etichette non più tagliate dal bordo giallo ([#29](https://github.com/Juan0177/tIndustry/pull/29))
- **Dock footer**: costi build Peak-style (`Nome · ×N [icona] · +$cost`); **Rimuovi** con label chiaro senza costi finti ([#29](https://github.com/Juan0177/tIndustry/pull/29))
- **Docs**: README allineato a 0.2.5

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.4] — 2026-09-19

Patch UX sopra **0.2.3**: pannello Mercato dedicato, icone più chiare, tutorial stock-first a 6 step, HUD scale-safe. ([#26](https://github.com/Juan0177/tIndustry/pull/26))

### Aggiunto / migliorato

- **Mercato**: pannello dedicato (stock + vendi 1/tutti + prezzi + auto-sell), separato dalla strip **Fabbrica** (tips / CORE)
- **Icone**: item/dock più leggibili con etichette IT brevi; icona vendita UI
- **Tutorial**: **6 step** produce → stock → spendi/vendi → ricerca (prima 5)
- **UI scale**: Mercato soft-scale + scroll Impostazioni — layout valido a **100–200%** senza overlap HUD
- **Docs**: README allineato a 0.2.4

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.3] — 2026-09-19

Patch economia sopra **0.2.2**: core stock-first, vendita esplicita Mercato, auto-sell opzionale. ([#24](https://github.com/Juan0177/tIndustry/pull/24))

### Changed

- **Economia stock-first**: item al core → magazzino (wallet); vendita esplicita dal Mercato (`1` / `tutti`) o toggle **Vendita automatica** (OFF di default, persistito in `settings.json`) ([#24](https://github.com/Juan0177/tIndustry/pull/24))
- **Docs**: README allineato a 0.2.3

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.2] — 2026-09-19

Patch logistica + tutorial sopra **0.2.1**: sdoppiatore T-fork, miner round-robin, tutorial ripartibile. ([#21](https://github.com/Juan0177/tIndustry/pull/21), [#22](https://github.com/Juan0177/tIndustry/pull/22))

### Aggiunto / migliorato

- **Sdoppiatore**: flusso lungo il facing + alternanza sinistra/destra al handoff; drag-connect non ruota più lo splitter (come gli incroci) ([#21](https://github.com/Juan0177/tIndustry/pull/21))
- **Miner**: eject **round-robin** sui nastri adiacenti che accettano — più uscite ricevono ore nel tempo ([#21](https://github.com/Juan0177/tIndustry/pull/21))
- **Tutorial**: **Nuova partita → Conferma** riparte sempre da Tutorial 1/5; **Impostazioni → Rivedi tutorial** azzera `tutorialCompleted`; Salta/Fine restano per la run corrente ([#22](https://github.com/Juan0177/tIndustry/pull/22))
- **Docs**: README allineato a 0.2.2

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.1] — 2026-09-19

Patch QoL / playability sopra **0.2.0**: nastri leggibili, UI scale, tutorial, miner multi-lato, toast, layout `src/`. ([#15](https://github.com/Juan0177/tIndustry/pull/15), [#16](https://github.com/Juan0177/tIndustry/pull/16), [#17](https://github.com/Juan0177/tIndustry/pull/17), [#18](https://github.com/Juan0177/tIndustry/pull/18), [#19](https://github.com/Juan0177/tIndustry/pull/19); splash ~8s già in 0.2.0 via [#13](https://github.com/Juan0177/tIndustry/pull/13))

### Aggiunto / migliorato

- **Nastri**: chip item saturi + icona sopra gli edifici; viaggio lungo `RoutedExit` — i minerali in transito si vedono di nuovo ([#16](https://github.com/Juan0177/tIndustry/pull/16))
- **UI scale**: Impostazioni **100% / 125% / 150% / 200%** (default **125%**), atlas DejaVu riscalato; layout Impostazioni senza overlap ON/OFF ([#16](https://github.com/Juan0177/tIndustry/pull/16), [#19](https://github.com/Juan0177/tIndustry/pull/19))
- **Tutorial**: banner IT a 5 step alla prima Nuova partita; **Salta** / Backspace; `tutorialCompleted` nel save ([#16](https://github.com/Juan0177/tIndustry/pull/16))
- **Piazzamento**: edifici su qualsiasi terra libera (non acqua); miner off-deposito a **0%**; click su Forno bloccato non ruba più lo strumento ([#16](https://github.com/Juan0177/tIndustry/pull/16))
- **Risorse sistema**: overlay **CPU · GPU · RAM** (FPS/CPU/RAM; GPU via `nvidia-smi` se c’è); strip inventario sempre a parte ([#16](https://github.com/Juan0177/tIndustry/pull/16))
- **Miner**: eject su **tutti e quattro** i lati adiacenti (qualsiasi nastro che accetta); freccia facing rimossa ([#17](https://github.com/Juan0177/tIndustry/pull/17))
- **Direzione nastro**: chevron unidirezionali allineati al facing (niente hash bidirezionali) ([#17](https://github.com/Juan0177/tIndustry/pull/17))
- **HUD**: FPS unico (angolo XOR overlay sistema); overlay sistema solo in play; font più nitido a scale alte ([#17](https://github.com/Juan0177/tIndustry/pull/17))
- **Status toast**: auto-clear ~3.5s con fade; **Esc** lo chiude ([#18](https://github.com/Juan0177/tIndustry/pull/18))
- **Repo**: sorgenti sotto `src/{App,Simulation,Content,UI}/`; `.csproj` in root ([#19](https://github.com/Juan0177/tIndustry/pull/19))
- **Docs**: README allineato a 0.2.1 ([#15](https://github.com/Juan0177/tIndustry/pull/15) + refresh release)

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.0] — 2026-09-19

Minor sopra **0.1.1**: first-launch AppData, icon pack HUD, splash + ottimizzazioni hot-path, tooling MCP. ([#9](https://github.com/Juan0177/tIndustry/pull/9), [#10](https://github.com/Juan0177/tIndustry/pull/10), [#11](https://github.com/Juan0177/tIndustry/pull/11), [#12](https://github.com/Juan0177/tIndustry/pull/12), [#13](https://github.com/Juan0177/tIndustry/pull/13); polish UI [#8](https://github.com/Juan0177/tIndustry/pull/8))

### Aggiunto / migliorato

- **Contenuti first-launch**: niente Excel nel publish; seed `content.json` → AppData al primo avvio (`~/.local/share/tIndustry/content/`) ([#9](https://github.com/Juan0177/tIndustry/pull/9))
- **Home**: **Continua** nascosto se non c’è autosave valido ([#9](https://github.com/Juan0177/tIndustry/pull/9))
- **Dock**: rimossa categoria **Strumenti**; **Rimuovi** in Produzione; tooltip hover IT (nome + hint) ([#9](https://github.com/Juan0177/tIndustry/pull/9))
- **Grafica**: icon pack HUD/dock/nastri (game-icons.net CC BY 3.0 + Kenney CC0); pannelli più nitidi; label **Δ sessione** ([#10](https://github.com/Juan0177/tIndustry/pull/10))
- **Splash**: schermata avvio con `assets/thumbnail.png`, brand tINDUSTRY, dismiss click/tasto o auto (~8s) ([#12](https://github.com/Juan0177/tIndustry/pull/12), [#13](https://github.com/Juan0177/tIndustry/pull/13))
- **Performance**: culling terreno, cache dock/home, intake nastro O(footprint), bridge O(1), meno alloc per frame ([#12](https://github.com/Juan0177/tIndustry/pull/12))
- **Tooling**: `.cursor/mcp.json`, raccomandazioni C# VS Code, bootstrap `dnx` in `install.sh` ([#11](https://github.com/Juan0177/tIndustry/pull/11))
- **UI polish**: loading, icone dock, fix Tools/miner su rotazione ([#8](https://github.com/Juan0177/tIndustry/pull/8))

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.1.1] — 2026-09-19

Patch UI/QoL dopo 0.1.0: dock Mindustry, controlli camera, core centrato, Impostazioni display estese. ([#7](https://github.com/Juan0177/tIndustry/pull/7))

### Aggiunto / migliorato

- **Dock Mindustry** (basso-destra): colonna categorie (Produzione / Logistica / Potenza / Strumenti / Inventario) + griglia piazzabili/item con bordo di selezione giallo/arancio
- **Header snello** + strip risorse (se overlay attivo); pannello Mercato compatto invece della barra piena a destra
- **Camera**: zoom solo con **Ctrl+rotella**; **rotella** sola ruota la direzione di piazzamento; **niente edge pan** (restano WASD / drag centrale / Shift+drag)
- **Core al centro** della mappa 1000×1000; la camera parte sul core
- **Impostazioni**: preset **2K** (2560×1440) e **4K** (3840×2160), **Auto risoluzione**, **VSync** on/off, **limite FPS** (30→600) + **Illimitato** — persistiti in `settings.json`

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.1.0] — 2026-09-19

Prima release pubblica-ish: fasi **0–6** sul prototipo C# / Raylib (`.NET 10`), più Impostazioni e CI publish.

### Aggiunto

- **Mondo**: mappa **1000×1000**, camera pan/zoom, culling viewport, depositi ferro e rame
- **Home / sessione**: Continua, Nuova partita, Gestione salvataggi, Impostazioni, Esci
- **Nuova partita**: selezione scenario (Classico / Espanso / Arcipelago) + seed numerico
- **Produzione**: minatore, **forno** (`smelt-iron`), **assemblatore** (rame + lastra → fili)
- **Logistica**: nastro base / **veloce**, **incrocio**, **sdoppiatore**, **ponte** (span 2–4)
- **Economia**: wallet (denaro + lastre + fili), prezzi mercato content-driven, rimborso rimozione 100%, **potenziamento core** (+25% vendite)
- **Ricerca**: sblocchi strutture data-driven, a pagamento, persistenti nel save
- **Power (stub)**: pool globale (baseline core + **Generatore**); forni/assemblatori consumano potenza (brownout = craft in stall)
- **QoL**: overlay FPS/risorse (Impostazioni), onboarding tip contestuali, UI in italiano, `--self-test` esteso
- **Persistenza**: save JSON versionato (**v6**: edifici, ponti, unlock, economia, generatori, PowerBuffer)
- **CI**: publish self-contained `win-x64` / `linux-x64` su push/PR; release job su tag `v*`

### Limitazioni note

- Niente **fuel / coal grid** né cavi di potenza (solo pool globale)
- Niente **combat**, unità, fog of war
- `miner-advanced` resta **stub** (nel content/ricerca, non giocabile)
- Bilanciamento ancora grezzo; poche ricette mid-game
- Slot nominati solo via **Duplica Continua** (niente “Salva come…” in-game)
- Grafica procedurale (niente sprite atlas)

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

[0.3.4]: https://github.com/Juan0177/tIndustry/releases/tag/v0.3.4
[0.3.3]: https://github.com/Juan0177/tIndustry/releases/tag/v0.3.3
[0.3.2]: https://github.com/Juan0177/tIndustry/releases/tag/v0.3.2
[0.3.1]: https://github.com/Juan0177/tIndustry/releases/tag/v0.3.1
[0.3.0]: https://github.com/Juan0177/tIndustry/releases/tag/v0.3.0
[0.2.2]: https://github.com/Juan0177/tIndustry/releases/tag/v0.2.2
[0.2.1]: https://github.com/Juan0177/tIndustry/releases/tag/v0.2.1
[0.2.0]: https://github.com/Juan0177/tIndustry/releases/tag/v0.2.0
[0.1.1]: https://github.com/Juan0177/tIndustry/releases/tag/v0.1.1
[0.1.0]: https://github.com/Juan0177/tIndustry/releases/tag/v0.1.0
