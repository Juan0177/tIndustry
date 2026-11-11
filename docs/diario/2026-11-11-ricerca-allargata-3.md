# Ricerca allargata, piano 3: potenziamenti, premi e bilanciamento

- **Specifica**: [2026-11-09-ricerca-allargata-design](../superpowers/specs/2026-11-09-ricerca-allargata-design.md)
- **Piano**: [2026-11-11-ricerca-allargata-3](../superpowers/plans/2026-11-11-ricerca-allargata-3.md)
- **Periodo**: 2026-11-11
- **Esito**: tutto sul ramo `claude/loving-planck-vjf9sv`, senza PR al momento della scrittura. Simulazione, dati, agenti e test verdi; il client è compilato ma non provato a schermo (manca Godot in cloud, vedi «Per il PC di casa»). Con questo piano la Ricerca allargata (piani 1, 2 e 3) è finita.

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-11-11-ricerca-allargata-3/progress.md`) e dai rapporti dei task. Dove il registro non dice qualcosa qui non è inventato.

## Obiettivo

L'ultimo dei tre piani della Ricerca allargata: undici potenziamenti senza blocco (drone, minatori, macchine, magazzino), i quattro premi dei capitoli della campagna, comprabili al triplo nella partita libera, e il bilanciamento di tutta la Ricerca a circa 6 ore senza jackpot, con la plastica e i circuiti fra i prodotti delle fasi.

## Task e commit

Base del piano: `bb7ea1a`. Fra la base e il Task 1 sul ramo c'è anche `d464aab` (`docs: specifica e piano delle sostituzioni come Mindustry e dei nastri simmetrici`), che non fa parte di questo piano.

| Task | Contenuto | Commit |
|---|---|---|
| 1 | Potenziamenti nella simulazione: undici nodi di tipo `upgrade` in `content.json` e `FactoryContent.ValidateUpgrades`; `Research/Upgrades.cs` con id e bonus (livello 2 che sostituisce il livello 1, `drone-boost` livello 2 dei quattro del drone), `Scale` intero per difetto e `Factor` uguale a 1 a zero; drone da `GameState.EffectiveUnit` (costruzione, cantieri, scavo a mano, raggio dei piani, stiva al ripristino, agenti, cerchio del raggio e stiva dell'HUD), `UnitDefinition.MineRatePercent`; `Miner.RatePercent` e `Crafter.BonusPercent` messi a ogni tick; massimo del CORE e dei depositi a sé scalato (`StorageBlock.BaseCapacity`); `stato.potenziamenti` degli agenti; icone da `tools/art/blocchi/potenziamenti.py`; testi it e en; 30 test in `UpgradeTests` | `259dc7b` |
| 2 | Premi della campagna: `campaignReward` (1 generatore a combustione, 2 nastro a pacchi, 3 ricetta dei circuiti, 4 Drone potenziato) con `ValidateCampaignRewards`; `RefusalCode.CampaignRewardOnly` in campagna, `CanUnlock(…, inCampaign)`, `Factory.GrantCampaignReward`; `premio` nel `catalogo` e in `mancano` degli agenti; testi del rifiuto e «Premio del capitolo N» nel pannello della Ricerca; `REWARDS` e `REWARD_FACTOR = 3` in `genera_finanza.py` con le colonne del foglio; 16 test in `CampaignRewardTests` | `d3f58b4` |
| 3 | Bilanciamento: plastica nella fase Avanzata e circuiti nella fase Fine, tutti i minuti del foglio rifatti, costi riscritti con `--scrivi-costi`; test delle ore a 6,0 ± 0,2, jackpot che accorciano davvero, soldi ±5% e prodotti delle fasi; costi fissati nei test C# aggiornati | `26ec7f7` |
| 4 | Client: premi in campagna col bordo `UiColors.RicercaPremio` (tre varianti) e il numero del capitolo (`TechTreeCanvas.InCampaign`), legenda della campagna; riga «Effetto» dei potenziamenti in `ResearchPanel` (numero di adesso → dopo lo sblocco); `BlockInfoPanel` coi numeri potenziati (minatori, tre macchine, depositi, CORE) | `9441fe8` |
| 5 | Documenti, stato e diario | commit `docs: Ricerca allargata, piano 3 fatto` |

## Giri di revisione

- **Prima dei task**: un controllo incrociato del piano non ha trovato conflitti (minuti dei nodi nuovi fra il Task 1 e il Task 3, prezzo ×3 dei premi fra il Task 2 e il Task 3, unità effettiva del drone fra il Task 1 e il Task 4, etichetta del premio fra il Task 2 e il Task 4); tre decisioni lo hanno fissato prima di partire (sotto).
- **Task 1, 2, 3, 4**: una revisione ciascuno, senza rilievi da correggere; solo minori, rinviati (sotto).
- **Revisioni in parallelo**: la revisione del Task 3 è andata in parallelo con l'implementatore del Task 4, quella del Task 4 con questo Task 5; sempre revisori in sola lettura, su file diversi.
- **Riporti fra task**: dal Task 1 al Task 4 i pannelli del client che mostravano velocità di minatori e macchine senza potenziamenti (fatto: `BlockInfoPanel`); dal Task 3 al Task 5 la tabella della Ricerca nella wiki e la riga del piano 2 nel CHANGELOG coi costi vecchi (fatto).
- **Task 5**: la sua revisione si fonde nella revisione finale (sotto).

## Decisioni prese in autonomia

Decisione: i Task 1 e 2 tengono verde ogni commit con minuti provvisori molto piccoli per i nodi nuovi, accorciando se serve nodi esistenti della stessa fase; il Task 3 mette i minuti veri — la regola delle ore sotto 6,5 vale a ogni commit e il bilanciamento è un task a sé — costo se sbagliata: nessuno.
Decisione: il livello 2 del drone è un nodo solo, `drone-boost` «Drone potenziato», dopo i quattro livelli 1 — la tabella della specifica dà un livello 2 per gruppo, ma il premio del capitolo 4 è «i livelli 2 dei quattro potenziamenti del drone insieme», e solo così il premio è un nodo — costo se sbagliata: quattro nodi da aggiungere.
Decisione: il Task 1 parte in parallelo con la rilettura in sola lettura delle correzioni finali del piano 2 — lavori su file diversi — costo se sbagliata: una correzione del piano 2 arriva dopo il Task 1.
Decisione (Task 1): per restare sotto 6,5 ore coi nodi nuovi il Task 1 accorcia Nodo T2 da 15 a 3 minuti, plastificatore da 6 a 4, ricetta dei circuiti da 8 a 5 e trapano a impatto da 4 a 1 (il Nastro rinforzato no: il suo costo è fissato in `LogisticsCostsTests`) — sono provvisori, il Task 3 li rifà tutti — costo se sbagliata: nessuno, superati dal Task 3.
Decisione (Task 1): i bonus non si salvano (`Miner.RatePercent`, `Crafter.BonusPercent` messi a ogni tick prima dell'uso, fuori da salvataggi e impronta) e la stiva si ripristina con l'unità effettiva, perché ricerca e livello del CORE tornano prima dei droni — gli sblocchi sono già salvati, quindi i salvataggi restano v22 — costo se sbagliata: nessuno.
Decisione (Task 2): `CanUnlock` prende un parametro `inCampaign` facoltativo invece di leggere il modo di gioco, che `ResearchState` non conosce; decidono `Factory` e `SimHost` con `ActiveCampaignLevelId` — costo se sbagliata: un parametro da passare in più punti.
Decisione (Task 2): il rifiuto `CampaignRewardOnly` viene dopo `AlreadyUnlocked` e prima di `MissingPrerequisites` — un premio in campagna non si compra mai, anche coi prerequisiti fatti, ma un premio già dato dice «già sbloccato» — costo se sbagliata: un messaggio diverso in un caso raro.
Decisione (Task 2): nel nastro a pacchi `campaignReward` sta solo nella struttura, la copia del costo in `conveyors` resta senza — lo script riscrive le due copie del prezzo — costo se sbagliata: nessuno.
Decisione (Task 2): minuti provvisori della ricetta dei circuiti da 5 a 1,7 e del nastro a pacchi da 8 a 2,7, circa un terzo, così il prezzo ×3 resta vicino a quello di prima; tetto dei soldi nei test a 27,63 milioni — costo se sbagliata: nessuno, superati dal Task 3.
Decisione (Task 3): la plastica entra nella fase Avanzata (6 al secondo) e i circuiti nella fase Fine (8 al secondo), con i pezzi del piano; incassi $1.128/s e $3.712/s — la plastica si fa dal plastificatore (Avanzata) e i circuiti dalla ricetta dei circuiti (Avanzata), quindi ogni prodotto si fa nella sua fase — costo se sbagliata: le ore si spostano e i minuti vanno rifatti.
Decisione (Task 3): il test delle ore vuole 5,8–6,2 senza jackpot (la regola della specifica resta 5,5–6,5) e jackpot che accorciano davvero (prima bastava uguale) — l'obiettivo del piano è 6,0 ± 0,2 — costo se sbagliata: un test più stretto da allargare.
Decisione (Task 3): forno ($1.500), schermo di borsa ($150.000), CORE foundation ($1.500.000), modulo probabilità ($4.000.000) e CORE nucleus ($7.000.000) restano esatti come nella specifica, e i nodi chiesti dagli obiettivi della campagna costano come prima, così la campagna non cambia — costo se sbagliata: nessuno.
Decisione (Task 3): CORE foundation ($1.500.000) e modulo previsione ($1.400.000) restano vicini — sono due nodi «grandi» su rami diversi — costo se sbagliata: nessuno.
Decisione (Task 3): al prezzo del foglio (×1) il nastro a pacchi ($670.000) costerebbe meno del Nastro T3 ($740.000); il prezzo scritto, ×3 ($2.010.000), rispetta la catena, ed è quello che il test controlla — in campagna il premio è gratis — costo se sbagliata: nessuno.
Decisione (Task 4): un premio in campagna non prende mai il bordo dorato; ha il bordo `RicercaPremio` (Normale `B98CFF`, Daltonici `56B4E9` dell'Okabe–Ito, Alto contrasto `FF66FF`) e il numero del capitolo nell'angolo; nella partita libera i premi si disegnano come gli altri — il prezzo ×3 sta già nei dati — costo se sbagliata: un segno in più nella partita libera.
Decisione (Task 4): le descrizioni fisse (per esempio «300 per oggetto» del contenitore, «4000 per oggetto» dei CORE) restano coi numeri di base; i numeri potenziati stanno nelle righe I/O nuove del pannello del blocco — cambiare i segnaposto di tante chiavi non valeva — costo se sbagliata: qualche testo da riscrivere.
Decisione (Task 4): la riga «Effetto» di un livello 1 del drone dopo che la campagna ha dato `drone-boost` mostra valori uguali (per esempio «Stiva: 60 → 60») — è vero, il livello 2 sostituisce il livello 1 — costo se sbagliata: una riga che confonde.
Decisione (Task 5): la wiki dice chiaro che oggi i premi in campagna non si ottengono: i dieci livelli non hanno capitoli e `Factory.GrantCampaignReward` lo chiamerà il lancio della campagna rifatta; nessun obiettivo di oggi chiede un premio — costo se sbagliata: nessuno.
Decisione (Task 5): «Sostituzioni come Mindustry e nastri simmetrici» va nell'aspetto 2 (Costruzione) di `STATO.md`, con una casella fatta per specifica e piano e una da fare per il resto, anche se tocca anche Logistica e Grafica — una casella in un aspetto solo, per non contarla tre volte nella barra — costo se sbagliata: la percentuale di un aspetto si sposta un poco.
Decisione (Task 5): la riga del piano 2 nel CHANGELOG resta la storia di allora, con un rinvio ai numeri del piano 3, invece di essere riscritta — il CHANGELOG racconta cosa è successo — costo se sbagliata: nessuno.

## Tabella finale della Ricerca

Il foglio «Bilanciamento» (`tools/dev/genera_finanza.py`) ha 50 nodi. Incassi a B: Inizio $46/s, Metà $304/s, Avanzata $1.128/s (12 Lastre di titanio, 12 Silicio, 6 Plastica), Fine $3.712/s (36 Lastre di titanio, 35 Silicio, 8 Circuiti). Ore senza jackpot **6,0025**, con un jackpot 5,63, con due 5,2572 (erano 6,487 / 6,024 / 5,562 dopo il Task 2). Fasi che partono a 0, 30, 120 e 281,35 minuti (la Fine aspetta la Ricerca dell'Avanzata). Tutta la Ricerca **30.193.700 $** (forbice dei test 28,68–31,7 milioni); incasso in 6 ore con le fasi all'orario 36.572.400 $.

| Fase | Nodi (minuti → costo scritto) |
|---|---|
| Inizio | forno 0,55 → $1.500; estrattore 1 → $2.800; generatore 2 → $5.500; incrocio 2,5 → $6.900; Nodo T1 3 → $8.300; router 3,5 → $9.700; lampada 4 → $11.000 |
| Metà | assemblatore 3 → $55.000; pressa grande 3,5 → $64.000; Nastro T2 e selezionatori 4 → $73.000; overflow gate 1 → $18.000; underflow gate 1,2 → $22.000; ponte e batteria 5 → $91.000; pannello solare 2 → $36.000; generatore a combustione 2 → $108.000 (premio, ×3); i quattro del drone 2 → $36.000; contenitore 6 → $110.000; Minatore T2 e schermo di borsa 8 → $150.000; Minatori I 10 → $180.000 |
| Avanzata | ponte lungo 2 → $140.000; pannello solare grande e batteria grande 3 → $200.000; Macchine I e Magazzino I 4 → $270.000; Minatori II e Nodo T2 5 → $340.000; fonderia avanzata 7 → $470.000; separatore 8 → $540.000; plastificatore 6 → $410.000; ricetta dei circuiti 5 → $1.020.000 (premio, ×3); nastri rinforzato e T3 11 → $740.000; deposito 13 → $880.000; CORE foundation 22 → $1.500.000; previsione 20 → $1.400.000; percentuale 25 → $1.700.000 |
| Fine | trapano a impatto 3 → $670.000; nastro a pacchi 3 → $2.010.000 (premio, ×3); Drone potenziato 2,5 → $1.680.000 (premio, ×3); Macchine II 4 → $890.000; Magazzino II 6 → $1.300.000; probabilità 18 → $4.000.000; CORE nucleus 31,5 → $7.000.000 |

I materiali non sono cambiati (solo minuti e prodotti); ogni oggetto chiesto si fa nella sua fase.

## Problemi rinviati

- Task 1: `Upgrades.Scale(long, int)` non è usato; i test dei potenziamenti messi male nei dati controllano solo che il caricamento fallisca, non il messaggio; il bonus del forno a carbone vuol dire più lastre per ogni carbone (nota per il bilanciamento); l'impronta di `UnitDefinition` non comprende `MineRatePercent`.
- Task 2: `FactoryContent.CampaignChapters = 4` è una seconda fonte del numero dei capitoli; il pannello della Ricerca in campagna mostra «Premio del capitolo N» anche sopra i prerequisiti mancanti; `IGameView.CanUnlock` ha `inCampaign` falso per default.
- Task 3: il test delle catene controlla solo i prezzi scritti (non quelli del foglio); manca un commento che dica voluto il ×3 sopra il Nastro T3 (il nastro a pacchi al prezzo del foglio costa meno del suo genitore).
- Task 4: le descrizioni fisse coi numeri di base (sopra); `DrawRewardBadge` crea la stringa del capitolo a ogni frame; nessun test per `FormatUpgradeEffect` e `BlockIoLine` con la ricerca (sono nel client, senza Godot in cloud); un livello 1 del drone dopo `drone-boost` mostra «60 → 60».
- Campagna: i quattro premi oggi in campagna non si ottengono (nessun capitolo): li darà il lancio della campagna rifatta, che è il prossimo lavoro della campagna, dopo le sostituzioni.
- Le 11 icone nuove non hanno il loro `.import` (li crea Godot a casa, da committare dopo il primo import).

## Per il PC di casa

- Importare il progetto (`--import`) per i `.import` delle 11 icone: `upgrade-drone-build`, `upgrade-drone-hold`, `upgrade-drone-range`, `upgrade-drone-mine`, `drone-boost`, `upgrade-miners-1`, `upgrade-miners-2`, `upgrade-machines-1`, `upgrade-machines-2`, `upgrade-storage-1`, `upgrade-storage-2`. Poi `--build-solutions`, ripristino dei `.import` che cambiano solo per fine riga e commit dei `.import` nuovi.
- `python tools/art/genera.py --controlla` byte per byte: in cloud vale solo `--pixel` (Pillow diverso). Se segnala le icone di questo piano, rigenerarle con `python tools/art/genera.py --solo` seguito dai nomi.
- Playtest `ui`, `tutorial` e `agenti` (PLAYTEST OK, 0 ERROR). Le catture della Ricerca (undici nodi in più) e del pannello del blocco (righe I/O nuove per minatori, depositi e CORE) possono cambiare: confrontarle con `compare_shots.py` e rifare i riferimenti.
- A mano:
  1. le icone dei potenziamenti nell'albero, leggibili allo zoom solito;
  2. in un livello della campagna: i quattro premi col bordo `RicercaPremio` e il numero del capitolo nell'angolo (posto e misura a zoom 1 e zoomando), in Normale, Daltonici e Alto contrasto (contrasto sul fondo del nodo, bloccato o disponibile), la legenda della campagna, il pulsante disabilitato «Premio del capitolo N», il rifiuto se si prova a sbloccare;
  3. nella partita libera i premi disegnati come gli altri, col prezzo ×3;
  4. la riga «Effetto» dei potenziamenti (Drone potenziato ne ha quattro: la scheda scorre), in italiano e in inglese, alla Scala UI al 100% e al 150%;
  5. il pannello del blocco coi numeri potenziati: pezzi al secondo dei tre minatori (anche il T2, che prima non aveva la riga), tempi delle ricette di forno, fonderia avanzata e assemblatore, massimo dei magazzini e del CORE («più i depositi collegati»);
  6. dopo uno sblocco: la stiva dell'HUD e il cerchio del raggio del drone che crescono subito, un minatore e un forno più veloci, il massimo del CORE più alto nell'HUD e nel Mercato.

## Verifiche dei task

Ultimo stato riportato dai task: build Release 0 errori e 0 avvisi (progetto Godot compreso); `TIndustry.Shared.Tests` 1445 superati e 2 saltati (Task 3 e 4); `TIndustry.AgentPlay.Tests` 29 superati e 10 saltati (le prove del gioco vero, senza Godot); `test_genera.py` OK e `genera.py --controlla --pixel` 0 da rigenerare; `test_genera_finanza.py` 19 OK e `genera_finanza.py --controlla data/content.json` uguale al foglio; test del sito OK e `node --test` 25 superati; `MapTool valida` OK per tutti i livelli (Task 3). Task 5: build 0 errori e 0 avvisi, `TIndustry.Shared.Tests` 1445 superati e 2 saltati, test del sito 71 OK e `node --test` 25 superati, `controlla_wiki.py` OK, mappa del progetto e barre rigenerate (avanzamento 72%, 63 passi su 88). Nessun playtest (manca Godot in cloud).

## Revisione finale

In corso.
