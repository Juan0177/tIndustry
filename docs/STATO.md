# Stato del progetto

A che punto siamo con **tutti gli aspetti del gioco** e cosa manca. **Ogni modifica al progetto aggiorna, in main, questo file e la [mappa del progetto](mappa-progetto.canvas)**; la storia sta nel [diario](diario/README.md) e nel [CHANGELOG](../CHANGELOG.md).

Le caselle della prossima sezione sono la fonte della barra di avanzamento che compare nel README, nella Wiki e nella pagina Sviluppo del sito: ogni aspetto è un pezzo della barra, con la sua percentuale, e il titolo porta alla sua documentazione. Dopo ogni modifica si lancia `python tools/sito/stato.py`, che riscrive le barre; la CI controlla che siano allineate. Un aspetto ancora da progettare ha quattro passi: specifica approvata, piano, implementazione, documenti e diario.

**Aggiornato:** 2026-11-04, sequenza del tentativo di jackpot fatta (specifica 2b: 12 s nel mondo con bot, valigetta, rulli e coriandoli; client da provare sul PC di casa); prima, 2026-11-04, schermo di borsa e bilanciamento fatti (specifica 2a: schermo attaccato al CORE col jackpot in partita, LED, ramo Finanza coi tre moduli, Ricerca per circa 6 ore, salvataggi v20; client da provare sul PC di casa); prima, 2026-11-03, il drone sei tu fatto (WASD, costruzione nel raggio, scavo a mano con la stiva, niente strumenti, salvataggi v19; client da provare sul PC di casa); prima, 2026-11-03, logistica come Mindustry fatta (nastri stretti, incrocio, router, selezionatori, estrattore; client da provare sul PC di casa); prima, 2026-10-30, rete di corrente fatta (laser fra i nodi, reti con bilancio, Batteria); prima, giorno e notte (ciclo, solo giorno, solo notte), pausa, play.ps1, HUD nascosto nella Home e schermata di caricamento.

## Aspetti del gioco

### 1 · [Fondamenta tecniche](ARCHITETTURA.md)

- [x] Riordino del progetto (sotto-progetto 1, PR #139–#146)
- [x] Simulazione a comandi, tick fisso e checksum (sotto-progetto 2, PR #147–#154)
- [x] Salvataggi con versione, fino alla v14 (PR #279)
- [x] Agenti che giocano col server MCP (PR #210–#217)
- [x] Avvio unico play.ps1 (utente o dev) con installazione delle dipendenze
- [x] Importazione automatica degli asset quando cambiano e clean.ps1 per i file generati
- [ ] Pulizia di rami, worktree e cartelle vecchie (approvata, da lanciare a mano)
- [ ] Sonda `oggetti-fluidi` del playtest `ui` stabile

### 2 · [Costruzione](wiki/Costruzione.md)

- [x] Costruzione come Mindustry: piani, drone e cantieri (sotto-progetto 3, PR #159–#166)
- [x] Trascinamento, linea con ponte automatico, rettangolo di smontaggio (PR #229–#232)

### 3 · [Logistica](wiki/Logistica.md)

- [x] Nastri come Mindustry: portata, oggetti fluidi, giro sul posto, tier (PR #224–#246)
- [x] Nastro rinforzato (PR #240)
- [x] Nastri stretti, variante C ([#252](https://github.com/Juan0177/tIndustry/issues/252))
- [x] Junction ([#253](https://github.com/Juan0177/tIndustry/issues/253))
- [x] Sorter con l'alternanza sinistra/destra ([#254](https://github.com/Juan0177/tIndustry/issues/254))
- [x] Router ([#255](https://github.com/Juan0177/tIndustry/issues/255))
- [x] Container e vault (magazzini collegati al CORE o depositi a sé)
- [x] Estrattore come l'Unloader, configurato col clic ([#270](https://github.com/Juan0177/tIndustry/issues/270))
- [x] Logistica come Mindustry: specifica approvata ([spec](superpowers/specs/2026-11-03-logistica-mindustry-design.md)) e piano ([piano](superpowers/plans/2026-11-03-logistica-mindustry.md))

### 4 · [Produzione](wiki/Produzione.md)

- [x] Minatori, forni, assemblatori ed estrattori
- [x] Titanio e Minatore T2

### 5 · [Energia](wiki/Energia.md)

- [x] Generatori, nodi di potenza e Lampada
- [x] Rete di nodi come Mindustry: specifica approvata ([#269](https://github.com/Juan0177/tIndustry/issues/269), [spec](superpowers/specs/2026-10-30-rete-corrente-design.md))
- [x] Rete di nodi come Mindustry: piano ([piano](superpowers/plans/2026-10-30-rete-corrente.md))
- [x] Rete di nodi come Mindustry: implementazione
- [x] Rete di nodi come Mindustry: documenti e diario ([diario](diario/2026-10-30-rete-corrente.md))

### 6 · [Mercato ed economia](wiki/Mercato-come-una-borsa.md)

- [x] Mercato come una borsa, specifica 1 (PR #274–#282, [diario](diario/2026-10-22-mercato-borsa.md))
- [x] Schermo di borsa, moduli della Finanza e bilanciamento, specifica 2a: specifica approvata ([spec](superpowers/specs/2026-11-04-schermo-borsa-design.md), [piano](superpowers/plans/2026-11-04-schermo-borsa.md))
- [x] Schermo di borsa, moduli della Finanza e bilanciamento, specifica 2a: implementazione ([diario](diario/2026-11-04-schermo-borsa.md))
- [x] Sequenza animata del «Vendi tutto» e bot della finanza, specifica 2b (zoom senza HUD, bot in 2.5D, valigetta, «calcolo…», coriandoli, schermata JACKPOT): specifica approvata ([spec](superpowers/specs/2026-11-04-sequenza-jackpot-design.md), [piano](superpowers/plans/2026-11-04-sequenza-jackpot.md))
- [x] Sequenza del tentativo di jackpot, specifica 2b: implementazione in cloud, client da provare sul PC di casa ([diario](diario/2026-11-04-sequenza-jackpot.md))
- [x] Magazzino del CORE e livelli, specifica 3: specifica approvata ([spec](superpowers/specs/2026-10-23-magazzino-core-design.md))
- [x] Magazzino del CORE e livelli: implementazione ([piano](superpowers/plans/2026-10-23-magazzino-core.md), [diario](diario/2026-10-23-magazzino-core.md))

### 7 · [Mappe e terreno](wiki/Mappe-e-terreno.md)

- [x] Mappe a seme, biomi, pareti e acqua (G2a, PR #175–#180)
- [x] Arte del terreno (G2b, PR #182–#184)
- [ ] Giacimenti per bioma, generatore biomi-2: specifica approvata ([spec](superpowers/specs/2026-10-18-biomi-2-giacimenti-design.md), [#204](https://github.com/Juan0177/tIndustry/pull/204))
- [ ] Giacimenti per bioma: piano
- [ ] Giacimenti per bioma: implementazione
- [ ] Giacimenti per bioma: documenti e diario

### 8 · [Campagna](wiki/Campagna-e-mappe.md)

- [x] Livelli della campagna con le loro mappe
- [x] Campagna rifatta: specifica approvata ([spec](superpowers/specs/2026-11-09-campagna-rifatta-design.md))
- [ ] Campagna rifatta: piano
- [ ] Campagna rifatta: implementazione

### 9 · [Grafica ed effetti](wiki/Arte-generata.md)

- [x] Arte dei blocchi generata da script (G1, PR #168–#173)
- [x] Effetti, ombre e luci (G3, PR #186–#189)
- [ ] Effetti e luci approvati dall'utente

### 10 · [Interfaccia](wiki/Interfaccia-e-lingue.md)

- [x] Tema, carattere, lingue, Scala UI e colori accessibili (G4, PR #194–#201)
- [x] Panoramica del Mercato (PR #281)
- [x] Pausa con Spazio e velocità ×1/×5/×10/×20 in modalità dev
- [x] HUD nascosto nella Home e schermata di caricamento all'apertura di una partita
- [ ] Telemetria, overlay delle risorse ([#234](https://github.com/Juan0177/tIndustry/issues/234))

### 11 · [Drone e tier](https://github.com/Juan0177/tIndustry#stato-e-roadmap)

Sotto-progetto 4: il drone lo comandi tu. I CORE shard → foundation → nucleus e i droni alpha → beta → gamma sono passati alla [specifica 3 del Mercato](superpowers/specs/2026-10-23-magazzino-core-design.md) (aspetto 6).

- [x] Specifica approvata ([spec](superpowers/specs/2026-11-03-drone-giocatore-design.md))
- [x] Piano ([piano](superpowers/plans/2026-11-03-drone-giocatore.md))
- [x] Implementazione
- [x] Documenti e diario ([diario](diario/2026-11-03-drone-giocatore.md))

### 12 · [Liquidi](https://github.com/Juan0177/tIndustry#stato-e-roadmap)

- [ ] Specifica approvata
- [ ] Piano
- [ ] Implementazione
- [ ] Documenti e diario

### 13 · [Attacco e difesa](https://github.com/Juan0177/tIndustry#stato-e-roadmap)

Nemici solo di notte, Game Over se cade il CORE.

- [x] Giorno e notte: specifica approvata ([spec](superpowers/specs/2026-10-23-giorno-notte-design.md))
- [x] Giorno e notte: ciclo, solo giorno, solo notte col contatore del primo attacco ([piano](superpowers/plans/2026-10-23-giorno-notte.md), [diario](diario/2026-10-23-giorno-notte.md))
- [ ] Specifica approvata
- [ ] Piano
- [ ] Implementazione
- [ ] Documenti e diario

### 14 · [Co-op](decisioni/0002-sincronizzazione-ibrida.md)

Sotto-progetto 5: server C# e sincronizzazione ibrida.

- [ ] Specifica approvata
- [ ] Piano
- [ ] Implementazione
- [ ] Documenti e diario

### 15 · [Sito web](wiki/Sito-web.md)

- [x] Vetrina «fabbrica», pagine Sviluppo e Asset (PR #233, #235)
- [x] Racconto guidato dallo scroll pubblicato in `racconto/`
- [x] Mercato del sito allineato alla borsa (PR #276)
- [x] Barra di avanzamento nella pagina Sviluppo (PR #284)
- [x] Logo nella barra in alto
- [x] Catena della vetrina intera su schermo stretto: i nastri proseguono dietro il testo fino alla sezione dopo ([#287](https://github.com/Juan0177/tIndustry/issues/287))
- [ ] Racconto approvato dall'utente e messo nella barra del sito ([spec](superpowers/specs/2026-10-20-sito-scroll-narrativo.md))

### 16 · [Wiki e README](wiki/Home.md)

- [x] Pagine per chi gioca
- [x] Pagine per chi sviluppa
- [x] Mercato come una borsa e barra di avanzamento
- [x] Pagine dello schermo di borsa e del magazzino del CORE (Mercato come una borsa, Economia e Ricerca)
- [x] Banner del README col logo pixel, generato da `tools/art/immagini_documenti.py`
- [ ] README rifatto da capo

## Ordine di lavoro in corso

Dato dall'utente il 2026-10-22: alla fine di ogni blocco ci si ferma e si chiede conferma. Fra parentesi l'aspetto della barra a cui appartiene.

| Blocco | Cosa | Stato |
| --- | --- | --- |
| A | Controlli veloci, pulizia, test intermittenti (1) | Fatto, tranne la pulizia |
| B | Mercato come una borsa, specifica 1 (6) | Fatto: PR #274–#282 |
| C | Specifiche 2 e 3 del Mercato (6), campagna rifatta (8): prima il brainstorming | **In corso**: specifica 3 attuata (2026-10-23, [diario](diario/2026-10-23-magazzino-core.md)); specifica 2a attuata in cloud (2026-11-04, [diario](diario/2026-11-04-schermo-borsa.md)), resta il controllo del client sul PC di casa; specifica 2b (sequenza del tentativo di jackpot) attuata in cloud (2026-11-04, [diario](diario/2026-11-04-sequenza-jackpot.md)), resta il controllo del client sul PC di casa; campagna rifatta: specifica approvata (2026-11-09), lavori non ancora iniziati |
| D | Logistica come Mindustry: #252–#255, #270 (3) | Fatto in cloud (2026-11-03, [diario](diario/2026-11-03-logistica-mindustry.md)); resta il controllo del client sul PC di casa |
| E | Rete di energia #269 (5): fatta (2026-10-30, [diario](diario/2026-10-30-rete-corrente.md)); telemetria #234 (10) | Telemetria più avanti |

## Cosa manca, in piccolo

- **Ricerca rotta sul PC di casa** ([#292](https://github.com/Juan0177/tIndustry/issues/292)): `MissingMethodException` su `DrawString` in `TechTreeCanvas`, il client è compilato contro un GodotSharp più nuovo del 4.4.1. Correzione difensiva fatta (`NuGet.config` con la sola fonte nuget.org, controllo della versione di GodotSharp in `play.ps1` e `dev.sh`, `clean.ps1 -Nuget`); da verificare sul PC di casa con `.\clean.ps1 -Nuget` poi `.\play.ps1`.
- **Pulizia del blocco A**, approvata dall'utente ma da lanciare a mano, perché la modalità automatica blocca le cancellazioni:
  - i worktree vecchi in `.claude/worktrees/`;
  - i rami locali già uniti;
  - il ramo `sito/fabbrica`, in locale e su GitHub;
  - le cartelle vecchie di `.superpowers/` (`sdd/2026-10-18-sito-fabbrica`, `sdd-backup`, `brainstorm`).
- **Controllo del client sul PC di casa** (il lavoro del magazzino del CORE è stato fatto in cloud, senza Godot): importare il progetto (`--import`) e generare i `.import` degli 8 PNG nuovi, lanciare i playtest `ui` e `agenti`, il `PlaytestScala` (una categoria «Magazzino» nuova nella palette), rifare le catture (la riga `core.livello` dell'HUD non va a capo e cambia le immagini) e controllare le celle di prova del playtest grafico (`World.PlaytestGrafica.cs`), che ora toccano il CORE 3×3.
- **Controllo del client del giorno e notte sul PC di casa** (fatto in cloud, senza Godot): lanciare i playtest `ui`, `tutorial` e `agenti` (anche con `TINDUSTRY_PLAYTEST_LINGUA=en` e con la Scala UI diversa dal 100%) e la sonda delle luci con `TINDUSTRY_PLAYTEST_EFFETTI=1`. A mano, due controlli: la riga del tempo si ferma con la pausa; a velocità dev ×20 si vede il tramonto. Poi: le Lampade si accendono sotto metà luce; in solo notte la riga passa da «Primo attacco fra» a «Attacco!» e a «Notte». Misurare la larghezza di «Il tempo parte dal primo blocco» con la Scala UI al 100% e al 200%.
- **Controllo del client della rete di corrente sul PC di casa** (fatto in cloud, senza Godot): importare il progetto (`--import`) per i `.import` dei PNG della batteria (`battery`, `battery-base`, `battery-charge-anim`) e il `BatteryVisual.cs.uid`; lanciare i playtest `ui`, `tutorial` e `agenti` e rifare le catture di riferimento (palette e HUD cambiano); a mano, con una catena di nodi dell'utente: selezione del nodo, raggio, clic per collegare e scollegare, rifiuti, Esc, barre di carica delle batterie, suggerimento della rete, le tre varianti di colore (Normale, Daltonici, Alto contrasto) e la notte con le Lampade. Misurare la larghezza di `SelectionInfo` (640 px accanto alla palette a 1280×720).
- **Controllo del client della logistica sul PC di casa** (fatto in cloud, senza Godot): importare il progetto (`--import`) per i `.import` dei PNG `router` e `sorter-inverted` e il `FilterPanel.cs.uid`; lanciare i playtest `ui`, `tutorial` e `agenti` e rifare le catture di riferimento (palette e nastri cambiano); il pannello del filtro con la Scala UI al 150% e al 200%; la palette a 3 righe a 1280×720; la sovrapposizione del pannello del filtro col pannello del tutorial. A mano: un incrocio fra due linee, una catena di router, un estrattore fra due forni.
- **Controllo del client del drone sul PC di casa** (fatto in cloud, senza Godot): importare il progetto (`--import`); lanciare i playtest `ui`, `tutorial` e `agenti` e `ui` con `TINDUSTRY_PLAYTEST_EFFETTI=1`, e rifare le catture di riferimento (palette senza la categoria Strumenti, HUD con la stiva); controllare i passi nuovi del playtest (drone, piano fuori raggio, clic tenuto su un giacimento); la scheda del tutorial alla Scala UI al 150%. A mano: una partita intera dall'inizio alla fine, `Q` su una tastiera AZERTY, `Esc` verso la Home tenendo premuto `W`, l'avviso dello scavo fermo per un cantiere senza materiali, il clic breve su un giacimento lontano (nessun toast).
- **Controllo del client dello schermo di borsa sul PC di casa** (fatto in cloud, senza Godot; [diario](diario/2026-11-04-schermo-borsa.md#per-il-pc-di-casa)):
  - importare il progetto (`--import`) per i `.import` dei PNG nuovi (`market-screen`, `market-screen-base`, `market-screen-led`, `market-screen-corners`, `finance-forecast`, `finance-percent`, `finance-odds`) e il `.uid` nuovo di `MarketScreenVisual.cs`; poi `--build-solutions` e il ripristino dei `.import` che cambiano solo per fine riga;
  - `python tools/art/genera.py --controlla` (byte per byte): in cloud vale solo `--pixel` (Pillow diverso); se segnala i PNG di questo piano, rigenerarli con `python tools/art/genera.py --solo market-screen market-screen-base market-screen-led market-screen-corners finance-forecast finance-percent finance-odds`;
  - lanciare i playtest `ui`, `tutorial` e `agenti` e rifare le catture di riferimento (la palette Magazzino ha 5 blocchi, la Ricerca ha il ramo Finanza, il Mercato ha righe più alte);
  - a mano, le 10 prove del Task 6: schermo solo a contatto col CORE e uno solo; scheda «Oggetto seguito · Schermo di borsa» sopra il 2×2 con i soli oggetti del Mercato, «Nessuno» → «Scegli un oggetto»; testo del vetro leggibile a zoom 1 e 2, in italiano e in inglese, con 1, 2 e 3 moduli; «Staccato dal CORE» dopo aver spostato il CORE o smontato un pezzo (solo con un salvataggio preparato o un test (`SetCore`): è uno stato di sicurezza); «Vendi tutto» coi LED blu, viola e gialli, «JACKPOT» e angoli lampeggianti per 6 s, anche col Bagliore; riga «Moduli» nella panoramica del Mercato, che sparisce smontando lo schermo; varianti Daltonici e Alto contrasto; suggerimento col mouse (testi per esteso e significato dei LED); la riga in alto durante il crollo («Crollo» con la percentuale accanto) in italiano e in inglese; i numeri grandi della Ricerca (fino a 7.000.000 $) nell'HUD e nel pannello di sblocco.
- **Controllo del client della sequenza del jackpot sul PC di casa** (fatto in cloud, senza Godot; [diario](diario/2026-11-04-sequenza-jackpot.md#per-il-pc-di-casa)):
  - importare il progetto (`--import`) per i `.import` dei PNG nuovi (`market-bot-idle-anim`, `market-bot-fly-anim`, `market-bot-surprised-anim`, `market-bot-carry-anim`, `market-bot-throw-anim`, `market-briefcase`, `market-confetti-anim`) e i `.uid` nuovi (`JackpotSequenceView.cs`, `World.PlaytestJackpot.cs`, scritti a mano: controllare che Godot li accetti); poi `--build-solutions` e il ripristino dei `.import` che cambiano solo per fine riga; nessun avviso «Sprites: manca …market-bot…»;
  - playtest `ui` (PLAYTEST OK, 0 ERROR, riga `PLAYTEST step ok: sequenza-jackpot`), `tutorial` e `agenti` (non devono cambiare); confrontare le catture di prima e aggiungere ai riferimenti `ui-jackpot-calcolo`, `ui-jackpot-risultato` e `ui-jackpot-dopo` (bot fuori dallo schermo, «calcolo…», incasso con icona, niente HUD nelle prime due); in `ui-jackpot-dopo` può finire un avviso di livello completato (il passo vende 500 piastre sul primo livello);
  - il clic al centro arriva davvero a `TrySkip` (nessun overlay che prende il mouse; il playtest lo prova con un clic sintetico, a mano con il mouse vero), e un doppio clic su «Vendi tutto» non salta;
  - a mano, le 10 prove del Task 3: tentativo perso saltato e intero, jackpot vinto (clic ed Esc ignorati, coriandoli, luci, «JACKPOT»), ×20 (F4 con `TINDUSTRY_DEV=1`: sempre circa 12 s), pausa con Spazio e con Alt+Tab, Home e caricamento a metà, le tre varianti di colore, il CORE sopra, sotto, a destra e a sinistra dello schermo con Scala UI al 150%, FPS invariati; in più il bot visibile davanti allo schermo e al CORE, la valigetta sopra il vetro, il pannello del tutorial non sopra la sequenza;
  - con gli agenti (`apri_gioco`): `vendi {tutto: true}` su un tentativo risponde dopo circa 12 s con `sequenza: true`, una `cattura` mandata intanto risponde dopo la fine, un `input` clic in parallelo salta.
- **Livelli 03, 04 e 08 con un obiettivo di magazzino già fatto alla partenza** (difetto vecchio, visto dal test della campagna del bilanciamento): «Magazzino 12 lastre di ferro» parte con 40, «Magazzino 4 fili di rame» con 6, «Magazzino 28 lastre di ferro» con 50. Esclusi per id nel test (`CoveredAtStart`); si sistemano con la campagna rifatta (aspetto 8).
- **Sonda `oggetti-fluidi`** del playtest `ui`: fallisce ogni tanto per un frame lento (1 volta su 4 il 22/10). Rilanciando passa.
- **Server MCP `tindustry` nel gioco vero**: non trova `godot4`. Serve la variabile d'ambiente `TINDUSTRY_GODOT` sull'eseguibile di Godot e un riavvio di Claude Code.

## Cosa serve dall'utente

- Lanciare la pulizia del blocco A, oppure darle il permesso.
- Il tuo parere su un aspetto distinto per gli sprite di foundation e nucleus: oggi hanno gli stessi pixel dei vecchi CORE a 2 livelli, quindi i tre CORE si distinguono solo per misura.
- Provare il client del magazzino del CORE, quello del giorno e notte, quello della rete di corrente, quello della logistica, quello del drone, quello dello schermo di borsa e quello della sequenza del jackpot sul PC di casa (vedi «Cosa manca, in piccolo»).
- Il tuo parere sui numeri del bilanciamento a schermo: costi della Ricerca fino a 7.000.000 $ senza separatore delle migliaia nel gioco, campagna che parte con somme grandi (fino a 279.500 $ al livello 10) finché non si rifà.
- L'ok su effetti e luci (G3) e, quando vuoi, sul racconto del sito.
