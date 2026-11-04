# Schermo di borsa, moduli della Finanza e bilanciamento (specifica 2a)

- **Specifica**: [2026-11-04-schermo-borsa-design](../superpowers/specs/2026-11-04-schermo-borsa-design.md)
- **Piano**: [2026-11-04-schermo-borsa](../superpowers/plans/2026-11-04-schermo-borsa.md)
- **Periodo**: 2026-11-04
- **Esito**: tutto sul ramo `claude/ripresa-task-notturni-5ht4tf`, issue [#268](https://github.com/Juan0177/tIndustry/issues/268). Simulazione, agenti, foglio del bilanciamento e test verdi; il client è compilato ma non è stato provato a schermo (vedi «Per il PC di casa»).

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-11-04-schermo-borsa/progress.md`) e dai rapporti dei task. Dove il registro non dice qualcosa qui non è inventato.

## Obiettivo

Uno schermo di borsa attaccato al CORE accende il jackpot in partita e mostra il mercato di un oggetto; tre moduli della Ricerca (previsione, percentuale, probabilità) aiutano a vendere al momento giusto; i costi della Ricerca si riscalano perché sbloccare tutto richieda circa 6 ore di partita senza jackpot. La sequenza animata del «Vendi tutto» è della specifica 2b, da scrivere.

## Task e commit

Base del piano: `66acbc5`.

| Task | Contenuto | Commit |
|---|---|---|
| 1 | Schermo di borsa nella simulazione: contenuti, piazzamento a contatto col CORE, uno solo, `Active` e `HasMarketScreen` | `48ac8c4` |
| — | Specifica e piano allineati alla decisione dell'utente sul CORE che cresce | `a3a17b1`, `8114c2f` |
| 2 | Oggetto seguito (`SetBlockFilter`), esito del «Vendi tutto» e LED; tolto lo smontaggio dello schermo sotto il CORE | `8636495` |
| 3 | Moduli previsione, percentuale e probabilità (`MarketInsight`, `SellAllAttempt`) | `70d2405`, correzione `85106f6` |
| 4 | Salvataggi v20 e schermo di borsa per gli agenti (diario degli agenti v6) | `2e1009a` |
| 5 | Bilanciamento: foglio «Bilanciamento», `--scrivi-costi` e `--controlla`, costi della Ricerca e campagna | `1e5b54c`, correzioni `e450abc`, `73a6237`, `9092858` |
| 6 | Client: palette, filtro, `MarketScreenVisual`, LED, moduli sul vetro e nella panoramica, sprite | `058a0af`, correzione `e4dfdf6` |
| 7 | Documenti, stato e diario | commit `docs: schermo di borsa e bilanciamento` |

## Giri di revisione

- **Task 1, Task 2, Task 4**: revisione senza rilievi da correggere (solo minori, sotto).
- **Task 3** (un giro, 3 rilievi corretti): l'importante era `SecondsToSpike` fuori dalla fase normale; ora segue la fase del picco (vedi le decisioni). Inoltre commenti sul valore della probabilità (basis points arrotondati per difetto) e sulla percentuale che può essere negativa, e un test nuovo con sette casi delle fasi del picco.
- **Task 5** (tre giri): primo giro con due importanti, il selezionatore invertito che costava più del selezionatore e gli sblocchi impliciti della campagna (forno e assemblatore delle ricette chieste dagli obiettivi) non contati; corretti, più quattro note minori sul foglio. Secondo giro: il livello 5 con 42 e poi 30 lastre aveva ancora l'obiettivo «Magazzino 20 lastre» vero al primo tick; terzo giro: obiettivo portato a 40 lastre e test che chiede un obiettivo di magazzino aperto sia in partenza sia dopo gli sblocchi.
- **Task 6** (un giro, 6 rilievi corretti): l'importante era «Crollo» nascosto dal modulo percentuale sulla riga in alto del vetro; ora «Crollo» resta sempre e la percentuale gli sta accanto o scende fra le righe dei moduli. Inoltre: chiavi di cache del vetro e del suggerimento che comprendono i moduli ricercati, niente `GetLocale()` a ogni fotogramma, `RefreshTicks` pubblico, separatore « · » da una chiave di formato, braccio esplicito del LED giallo.
- **Task 7**: la sua revisione si fonde nella revisione finale (sotto).

## Decisioni dell'utente

- **Potenziamento del CORE come Mindustry, senza eccezioni** (4/11, fra il Task 1 e il Task 2): lo schermo sulle celle nuove blocca la crescita del CORE come ogni edificio, va spostato prima. Sostituisce la scelta del Task 1, che smontava lo schermo col rimborso quando il CORE lo copriva: il Task 2 ha tolto `TakeApartScreenUnder` e le sue chiamate. Specifica e piano allineati (`a3a17b1`, `8114c2f`).
- **Spazio per Attacco e difesa** (4/11, nella specifica): il foglio ha una riga «Difesa (aspetto 13)» con un budget per fase, oggi a zero.

## Decisioni prese in autonomia

Decisione: il Task 5 controlla il tipo dei soldi; se `int` non basta li passa a `long` — perché costi fino a 25 milioni stanno in `int` ma la somma degli incassi forse no — costo se sbagliata: un cambio di tipo in più file.
Decisione (Task 5): i soldi restano `int` (fino a circa 2,1 miliardi; la Ricerca costa circa 23 milioni); incassi e jackpot sommano in `long` e si fermano a `int.MaxValue`, provato da un test, senza cambiare il formato v20 — costo se sbagliata: una partita di centinaia di ore si ferma al tetto.
Decisione: i Task 6 e 7 si fanno in cloud senza Godot, con build 0/0 e `TestiTests`; playtest e catture sul PC di casa, come nei piani precedenti — costo: errori visivi trovati solo a casa.
Decisione (Task 1, poi sostituita dall'utente): solo lo schermo finito si smontava col rimborso quando il CORE che cresce lo copriva; gli altri edifici continuavano a bloccare — costo: una regola diversa per lo schermo da spiegare.
Decisione (Task 1): «Staccato dal CORE» resta come stato di sicurezza anche se oggi il CORE cresce solo coprendo o toccando lo schermo; il test lo prova spostando il CORE con `SetCore` — costo: un testo che quasi non si vede.
Decisione (Task 2): un **piano** di schermo non blocca il potenziamento del CORE, come ogni altro piano: vince il cantiere che parte prima e il drone scarta l'altro piano con l'avviso di cella occupata — costo: un piano scartato.
Decisione (Task 2, fatta nel Task 3): un «Vendi tutto» durante un crollo già in corso accende i LED viola (l'oggetto è in crollo, la vendita non è «buona») — costo: un colore diverso da quello che il giocatore si aspetta.
Decisione (Task 3): `SecondsToSpike` solo nello stato normale; nella salita sotto la soglia vale 0 (il client dice «picco in arrivo»); in cima e in salita o discesa dalla soglia in su `SellNow` è vero e non c'è conto alla rovescia; in discesa sotto la soglia nessuno dei due — la specifica dà il conto alla rovescia solo nello stato normale — costo: un testo in più nel client.
Decisione (Task 5): il selezionatore invertito costa come il selezionatore (73.000 $, decisione della specifica della logistica); il test delle catene accetta l'uguaglianza solo per questa coppia — costo: un'eccezione nel test delle catene.
Decisione (Task 5): per la campagna contano come sblocchi necessari anche le macchine delle ricette che gli obiettivi chiedono (forno, assemblatore, Minatore T2 per il titanio), con lo stesso margine di prima e un minimo pari al loro costo — la specifica chiede livelli giocabili — costo: soldi iniziali un po' più alti in alcuni livelli.
Decisione (Task 5, corregge la precedente sul livello 5): il livello 5 parte con 30 lastre (il costo del forno) e l'obiettivo sale a «Magazzino 40 lastre» (testo it+en); il test chiede che un obiettivo di magazzino di un oggetto prodotto non sia vero né all'inizio né dopo gli sblocchi; esclusi per id i livelli 03, 04 e 08, difetto vecchio rinviato alla campagna rifatta (aspetto 8) — costo: un obiettivo più lungo nel livello 5 e tre livelli facili fino ad allora.
Decisione (Task 6): scelte dell'implementatore accettate — testi corti sul vetro e lunghi nella panoramica e nel suggerimento; trattino «–» per il segno meno (il carattere del gioco non ha «−»); «vendi tutto: N min» al posto del consiglio durante l'attesa; suggerimento al passaggio del mouse col significato dei LED — costo: testi da rivedere a schermo.
Decisione: la revisione del Task 7 (documenti) si fonde nella revisione finale, come nel piano del drone — costo: un controllo in meno sui documenti.

## Il bilanciamento in numeri

Dal foglio «Bilanciamento» (`tools/dev/genera_finanza.py`), dopo le correzioni del Task 5:

| Voce | Valore |
|---|---|
| Ricerca intera | 23.021.700 $ (prima circa 37.000 $) |
| Ore per sbloccare tutto senza jackpot | 5,86 h |
| con 1 jackpot / con 2 jackpot | 5,40 h / 5,25 h |
| Jackpot del 50/50 (10.000 lastre di titanio alla soglia del picco) | 5.500.000 $ |
| Incasso in 6 ore con le fasi all'orario | 30.524.400 $ |

Esempi: forno 1.500 $, schermo di borsa 150.000 $, CORE foundation 1.500.000 $, CORE nucleus 7.000.000 $, modulo probabilità 4.000.000 $. Le fasi partono solo quando la Ricerca delle fasi prima è comprata: la Ricerca dell'Avanzata finisce a **4 h 51 min**, non a 4 h, quindi l'incasso vero della partita senza jackpot è sotto i 30,5 milioni della tabella all'orario. Costi di costruzione invariati, CORE e schermo compresi.

Campagna (soldi e materiali di partenza): livello 3 1.530 $ e 40 lastre; 4 56.500 $ e 114 lastre; 5 1.500 $ e 30 lastre, obiettivo «Magazzino 40 lastre»; 6 13.830 $, 56 lastre, 16 fili; 7 1.570 $ e 50 lastre; 8 79.940 $; 9 206.500 $, 234 lastre, 46 fili; 10 279.500 $, 216 lastre, 40 fili.

## Rilievi minori

Risolti nei task successivi:

- `WatchedItemId` con setter pubblico: ora `internal set`, impostato da `SetBlockFilter` (Task 2).
- Rimborso dello schermo coperto dal CORE limitato dalla capienza del CORE vecchio, e «annullando il cantiere del CORE lo schermo resta smontato»: non valgono più, lo schermo blocca il potenziamento (Task 2).
- Campo dello schermo nel salvataggio senza alzare la versione: la v20 del Task 4 lo comprende, con i test avanti e indietro.
- `godot/README.md` diceva che il jackpot non scatta in partita: corretto nel Task 7.

## Problemi rinviati

- `CanPlace` rifiuta subito «cella occupata» anche coi droni solo per lo schermo (Task 1).
- Il ramo «oggetto non scambiato dal Mercato» di `SetBlockFilter` sullo schermo non è provato (forse non esiste un tale oggetto); doppio `Prepare` nel test `TwoSellAllsInOneTick_KeepTheSecond` (Task 2).
- `ModuleKind` sta in `MarketInsight` invece che nei tipi dei contenuti; il client usa `IsStructureUnlocked` (non c'è un `IsResearched`) (Task 3).
- La regola di visibilità della previsione è ripetuta in `AgentGame.AddInsight`; `ultimo_esito.jackpot` è scritto anche quando è falso; nessun test di `rigioca` con lo schermo nel diario degli agenti (la partita del diario parte senza Ricerca) (Task 4).
- Riga di documentazione lunga in `ResearchNeeded`; il livello 5 resta a 0 $ dopo lo sblocco del forno (Task 5).
- Se i moduli non hanno righe libere sul vetro, la percentuale spostata si perde («Crollo» resta); `StructureCount` non cambia se lo schermo si sposta nello stesso fotogramma (caso già esistente per altri blocchi) (Task 6).
- Livelli 03, 04 e 08 con un obiettivo di magazzino già fatto alla partenza: campagna rifatta (aspetto 8).
- Nessun separatore delle migliaia per i costi grandi (fino a 7.000.000 $): da decidere a schermo (chiave di formato `formato.migliaia` se servono).
- Nessun passo di playtest nuovo per lo schermo: cambierebbe le catture di riferimento.

## Per il PC di casa

- Importare il progetto (`--import`) per i `.import` dei PNG nuovi (`market-screen`, `market-screen-base`, `market-screen-led`, `market-screen-corners`, `finance-forecast`, `finance-percent`, `finance-odds`) e il `.uid` nuovo di `MarketScreenVisual.cs`; `--build-solutions`; ripristinare i `.import` che cambiano solo per fine riga.
- `python tools/art/genera.py --controlla` byte per byte: in cloud fallisce su 196 PNG già presenti per un Pillow diverso (pixel uguali). Se a casa segnala i PNG di questo piano, rigenerarli con `python tools/art/genera.py --solo market-screen market-screen-base market-screen-led market-screen-corners finance-forecast finance-percent finance-odds`.
- Playtest `ui`, `tutorial` e `agenti` (PLAYTEST OK, 0 ERROR) e catture di riferimento nuove: la palette Magazzino ha 5 blocchi su 2 righe (`PlaytestScala`), la Ricerca ha il ramo Finanza, il Mercato ha righe più alte.
- Le 10 prove a mano del Task 6:
  1. importazione e compilazione (sopra);
  2. playtest (sopra);
  3. lo schermo si piazza solo a contatto col CORE (fantasma rosso e messaggio altrimenti), uno solo;
  4. clic sullo schermo senza blocco scelto: scheda «Oggetto seguito · Schermo di borsa» sopra il 2×2 con i soli oggetti del Mercato; la scelta mostra icona, prezzo e grafico sul vetro; «Nessuno» → «Scegli un oggetto»;
  5. testo del vetro leggibile a zoom 1 e 2 e dentro il vetro in italiano e in inglese, con 1, 2 e 3 moduli;
  6. «Staccato dal CORE» in rosso dopo aver spostato lo schermo o smontato un pezzo;
  7. «Vendi tutto» con i tre LED (blu, viola, gialli), «JACKPOT» e angoli lampeggianti, tutto per 6 s; col Bagliore i LED fioriscono;
  8. riga «Moduli» nella panoramica del Mercato con lo schermo attivo, che sparisce staccandolo;
  9. varianti Daltonici e Alto contrasto: LED e testi del vetro cambiano subito;
  10. suggerimento col mouse sullo schermo (testi per esteso, significato dei LED durante l'esito).
- In più: la riga in alto del vetro durante il crollo («Crollo» con la percentuale accanto) in italiano e in inglese, e le cifre grandi della Ricerca nell'HUD e nel pannello di sblocco.

## Verifiche dei task

Ultimo stato riportato dai task: build Release 0 errori e 0 avvisi; `TIndustry.Shared.Tests` 1200 superati e 2 saltati (`TestiTests` verdi); `TIndustry.AgentPlay.Tests` 29 superati e 10 saltati; `test_genera.py` 48 OK; `test_genera_finanza.py` 15 OK; `genera_finanza.py --controlla data/content.json` uguale; `genera.py --controlla --pixel` OK; `MapTool valida` OK sui 10 livelli; test del sito OK. `MapGeneratorTests.GenerationTimeBudget` può fallire per tempo sotto carico e passa rilanciato.

## Revisione finale

Da fare.
