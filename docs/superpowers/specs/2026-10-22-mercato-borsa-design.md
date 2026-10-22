# Mercato come una borsa — specifica 1 di 3

- **Stato**: in revisione (brainstorming con l'utente del 22 ottobre 2026, issue [#268](https://github.com/Juan0177/tIndustry/issues/268)).
- **Numeri ed esempi**: cartella di lavoro `docs/finanza/mercato-e-finanza.xlsx`, generata da `python tools/dev/genera_finanza.py . docs/finanza/mercato-e-finanza.xlsx` (il `.xlsx` non si versiona). Ogni numero di questa specifica è anche nel foglio «Parametri».
- **Le tre specifiche**:
  1. **Mercato** (questa): prezzi, picchi, crolli, vendite, jackpot e pity nella simulazione, vendita automatica, valori base, salvataggi, schermata panoramica.
  2. **Schermo di borsa e bot della finanza**: blocco accanto al CORE, sequenza del «Vendi tutto» col drone 2.5D, LED, moduli e ramo Finanza della Ricerca.
  3. **Magazzino del CORE e livelli**: massimo per oggetto (1.000 / 4.000 / 12.000), linee che si fermano a CORE pieno, drone più veloce per livello.
- **Sostituisce**: il Mercato di oggi (`MarketCatalog.GetDynamicSellPrice`, prezzo che dipende solo dalle scorte del CORE) e il bonus di vendita di «Potenzia CORE».

## Obiettivo

Ogni oggetto ha un mercato suo, come un titolo in borsa: il prezzo si muove sempre, ogni tanto sale in un picco e, se si vende troppo, crolla. Vendere e basta rende poco; chi aspetta i rialzi guadagna fino al triplo, e chi tenta il «Vendi tutto» nel picco può vincere un jackpot raro e spettacolare. Fare l'errore costa: un crollo blocca l'oggetto per 20 minuti.

Successo: a parità di produzione, nella cartella di lavoro (foglio «Analisi lungo periodo», 25 pezzi al secondo per 4 ore) chi sfrutta i picchi guadagna fra 1,3 e 2,8 volte la sola vendita automatica, a seconda della fortuna; senza mai un jackpot vendere al picco rende comunque circa 2,9 volte B sui pezzi venduti.

## Decisioni prese con l'utente

| Tema | Decisione |
|---|---|
| Valore base B | Uno per oggetto, è anche la **soglia minima**. Tutte le percentuali si misurano da B. |
| Prezzo normale | Oscilla dolcemente fra **B e 1,6×B** (media 1,3×B), cambi visibili ogni pochi secondi. |
| Picco | Ogni **10–20 minuti** per oggetto. Sale in **10 s** fino a un massimo casuale fra 2,8×B e **3×B** (+200%), resta in cima **10 s**, scende in **30 s**. Da **2,8×B** (+180%) la vendita è «nel picco». |
| Vendite piccole | Ogni pezzo abbassa il prezzo dello **0,04% di B**, mai sotto B; in **2 minuti** il prezzo torna al suo andamento. |
| Crollo | Solo se in **2 minuti** si vendono **5.000 pezzi o più** di quell'oggetto, o col jackpot vinto. Dura **20 minuti**: prezzo vicino a B, nei primi **5 minuti** a volte **0,6–0,7×B**. Niente picchi e conto alla rovescia del picco fermo. |
| Prezzo pagato | **Tutti i pezzi al prezzo del momento del clic**; il prezzo scende dopo. |
| Jackpot | Solo con **«Vendi tutto»**, solo **nel picco**, solo con **almeno 400 pezzi** e solo se in partita c'è uno **schermo di borsa** (specifica 2). Probabilità **2%** a 400 pezzi, **+1% ogni 200** pezzi, tetto **50%** a 10.000. Paga per pezzo **B + 777% di B (per difetto) + (prezzo del momento − B)**: con B 10 e prezzo 30, 10 + 77 + 20 = 107. Vale per qualsiasi oggetto. |
| Pity | **Per oggetto**. Conta solo i tentativi da **3.000 pezzi** in su: ognuno a vuoto aggiunge **+25%** al tentativo successivo (anche oltre il 50%); dopo **3 a vuoto di fila** il successivo è **garantito**. Un tentativo sotto i 3.000 pezzi si può fare (probabilità base), non riceve il bonus e non tocca la serie. Si azzera al jackpot. |
| Tentativo a vuoto senza crollo | (sotto i 5.000 pezzi) Per **5 minuti** nessun tentativo di jackpot e **«Vendi tutto» bloccato su tutti gli oggetti**; il conto alla rovescia del picco di quell'oggetto **riparte da capo più 5 minuti** (15–25). |
| 50/50 perso | (10.000 pezzi o più, crollo) Nei primi **5 minuti** del crollo il prezzo torna verso B il **20% più in fretta**; il crollo dura sempre 20 minuti. |
| Vendita automatica | **Un interruttore per oggetto.** Paga il **prezzo minimo**: B, o il prezzo del momento se è sotto B (crollo). Non tocca il mercato: niente calo, niente crollo, niente jackpot. Stessa regola dai nastri e dagli edifici attaccati al CORE. |
| Bonus del CORE | **Tolto**: «Potenzia CORE» servirà al magazzino e al drone (specifica 3). |
| Valori B | B ≈ listino di oggi ÷ 1,3; **×1,5** per gli oggetti di fine partita (grafite, silicio, titanio grezzo, lastra di titanio). La campagna verrà rifatta, quindi gli obiettivi di guadagno si ritoccano solo quanto basta perché i test passino. |
| Schermata | Una **panoramica** fra i menu (al posto del pannello Mercato): per ogni oggetto posseduto icona, nome, quantità, prezzo attuale con ▲▼, **mini grafico degli ultimi 2 minuti**, «Crollo» se c'è, **«Vendi 1», «Vendi 100», «Vendi tutto»** e l'interruttore della vendita automatica. **Nessun pulsante che vende tutto di tutti.** Non mostra percentuale su B, previsione e probabilità di jackpot: sono moduli dello schermo (specifica 2). |

### Valori B proposti

| Oggetto | B | Media | Picco max | Jackpot al picco max (per pezzo) |
|---|---|---|---|---|
| Ferro grezzo | 6 | 8 | 18 | 64 |
| Lastra di ferro | 23 | 30 | 69 | 247 |
| Rame grezzo | 5 | 7 | 15 | 53 |
| Filo di rame | 12 | 16 | 36 | 129 |
| Carbone | 3 | 4 | 9 | 32 |
| Piombo grezzo | 4 | 5 | 12 | 43 |
| Lastra di piombo | 14 | 18 | 42 | 150 |
| Titanio grezzo | 14 | 18 | 42 | 150 |
| Lastra di titanio | 52 | 68 | 156 | 560 |
| Grafite | 12 | 16 | 36 | 129 |
| Silicio | 32 | 42 | 96 | 344 |

## Fuori ambito

Lo schermo di borsa, la sequenza animata, i LED, i moduli e il ramo Finanza (specifica 2); il magazzino del CORE, i livelli e i droni (specifica 3); il rifacimento della campagna; la tensione e i dettagli dell'animazione del jackpot. Finché non c'è la specifica 2 il jackpot è implementato e provato nella simulazione ma non scatta mai in partita.

## Simulazione (`src/TIndustry.Shared`)

### Dati

`data/content.json`:
- `market[]`: per oggetto `itemId`, `displayName`, **`baseValue`** (B). Spariscono `sellPrice`, `minSellPrice`, `maxSellPrice`, `softStock`.
- **`marketRules`**: tutti i numeri della tabella delle decisioni, in unità del gioco (secondi, pezzi, frazioni di B), letti in un record `MarketRules`. Nessun numero del mercato scritto nel codice.
- `economy.coreUpgrade.saleBonusPercent` sparisce.

`FactoryContent.Load` rifiuta con un errore in italiano un oggetto del mercato senza `baseValue` positivo e regole incoerenti (per esempio soglia del picco sopra il massimo, tetto del jackpot sopra il 100%), come fa oggi per la Lampada.

### Stato

- **`MarketState`** in `GameState`, con un **`MarketItemState`** per oggetto, in ordine fisso (quello di `content.market`), mai in un `Dictionary` iterato:
  - prezzo «di fondo» dell'andamento normale (× B), fase (`Normale`, `PiccoSalita`, `PiccoCima`, `PiccoDiscesa`, `Crollo`), tick di inizio e massimo del picco in corso;
  - tick del prossimo picco (conto alla rovescia, fermo durante il crollo), tick di fine del crollo, tick di fine del recupero accelerato;
  - calo da vendite ancora da recuperare (× B) e pezzi venduti negli ultimi 2 minuti (finestra a scaglioni di un secondo, per contare i 5.000 senza tenere ogni vendita);
  - contatore del pity, vendita automatica accesa o spenta;
  - storia del prezzo degli ultimi 2 minuti, un punto al secondo, per il mini grafico.
- Un'**attesa globale** dei tentativi: tick fino a cui «Vendi tutto» e jackpot sono bloccati.
- Un **generatore casuale seminato** dello stato (`MarketRandom`: xorshift o PCG a 64 bit scritto nel progetto, non `System.Random`, perché il formato deve restare uguale fra versioni di .NET), seminato dal seme della partita all'inizio e salvato col suo stato. Tutto il caso del mercato (andamento, massimo del picco, intervallo fra picchi, discese sotto B, estrazione del jackpot) passa da qui, in ordine fisso.

### `MarketSystem`

Nuovo sistema in `Simulation.cs`, dopo `CoreDeliverySystem`. A ogni tick, per ogni oggetto in ordine:
1. avanza l'andamento normale (passeggiata che torna verso la media, limitata a B–1,6×B, con un passo nuovo ogni pochi secondi e interpolazione fra i passi);
2. avanza la fase: avvio del picco allo scadere del conto alla rovescia, salita, cima, discesa; fine del crollo e del recupero accelerato;
3. recupera il calo da vendite (lineare in 2 minuti, il 20% più in fretta nel recupero accelerato);
4. aggiorna la finestra dei 2 minuti e, una volta al secondo, la storia del prezzo.

Prezzo del momento = fase (andamento normale, curva del picco o livello del crollo) − calo da vendite, mai sotto B fuori dal crollo. Il prezzo si tiene in **millesimi di B, come intero**, così è identico su ogni piattaforma e nei salvataggi; l'incasso per pezzo è `round(B × prezzo)`, come nella cartella di lavoro. «Nel picco» vuol dire prezzo del momento, al comando, di almeno 2,8×B.

### Vendite

- `Sell(PlayerId, ItemId, Amount)` resta per «Vendi 1» e «Vendi 100»; **`SellAll(PlayerId, ItemId)`** è un comando nuovo per «Vendi tutto» (vende tutte le scorte di quell'oggetto nel CORE). `SetAutoSell` diventa **`SetAutoSell(PlayerId, ItemId, Enabled)`**.
- Una vendita: incasso = pezzi × prezzo del momento; poi il calo da vendite cresce di pezzi × 0,04% di B (senza scendere sotto B) e la finestra dei 2 minuti conta i pezzi. Se la finestra arriva a 5.000 pezzi, crollo.
- «Vendi tutto» con almeno 400 pezzi, nel picco e con uno schermo di borsa in partita è un **tentativo di jackpot**: probabilità base + pity (se da 3.000 pezzi), estrazione dal generatore seminato. Jackpot: incasso per pezzo B + ⌊7,77×B⌋ + (prezzo − B), poi crollo, pity a zero. A vuoto: incasso normale; con 5.000 pezzi o più crollo (e recupero accelerato se 10.000 o più), altrimenti attesa globale di 5 minuti e picco di quell'oggetto rimandato; pity +1 se da 3.000 pezzi.
- Rifiuti nuovi (`RefusalCode`, tradotti dal client): `SellAllCooldown` («Vendi tutto» durante l'attesa). Gli altri casi usano i codici di oggi.
- Il risultato si decide al comando: la simulazione vende subito e produce un evento `SaleResolved` (oggetto, pezzi, prezzo, incasso, crollo sì/no, tentativo sì/no, jackpot sì/no, probabilità usata). La specifica 2 lo userà per la sequenza animata; ora il client lo mostra nella riga di stato e, per il jackpot, con un avviso.
- `EconomySession.RecordSale` registra il guadagno vero (anche del jackpot): `SaleIncome` e `SoldByItem` restano il riferimento degli obiettivi della campagna.

### Vendita automatica

`CoreStockSink.AcceptAtCore` (nastri) e `GameState.TryDeliverAdjacent` (edifici attaccati al CORE) usano una sola funzione: se l'oggetto ha la vendita automatica accesa, incassa il prezzo minimo (min(B, prezzo del momento)) senza toccare il mercato; altrimenti il pezzo va nel CORE. Si toglie l'incoerenza di oggi sul bonus del CORE.

### Salvataggi

`FactorySaveData.CurrentVersion = 14`. Si salvano lo stato di ogni `MarketItemState`, l'attesa globale e lo stato del generatore. `AutoSellAtCore` (interruttore unico) e `CoreSaleBonusPercent` non si scrivono più. Le versioni 11–13 si leggono ancora: il mercato parte da zero (prezzo alla media, primo picco fra 10 e 20 minuti dal seme della partita) e il vecchio interruttore unico, se acceso, accende la vendita automatica di tutti gli oggetti.

### Agenti

`stato` riporta per ogni oggetto posseduto prezzo del momento (in $ e × B), fase e, durante un crollo, i secondi che mancano; più l'attesa globale. `vendi` accetta `tutto: true` (diventa `SellAll`) e risponde con l'esito di `SaleResolved`. Nuovo `vendita_automatica {oggetto, accesa}`. Gli agenti vedono quello che vede la panoramica, non i moduli della specifica 2.

## Client (`godot/`)

- `MercatoPanel` diventa la **panoramica**: righe come in tabella; mini grafico disegnato con `Line2D` o `_Draw` dalla storia dei 2 minuti; ▲▼ e «Crollo» non solo a colore (icona e testo, varianti Daltonici e AltoContrasto); «Vendi tutto» disabilitato durante l'attesa con un suggerimento che dice quanto manca.
- Stili solo dal tema (`UiTheme`, `UiColors`), testi solo da `godot/lingue/testi.csv` (italiano e inglese), nuove chiavi `mercato.*`.
- Il jackpot, finché non c'è la specifica 2, non può uscire in partita; se uscisse (per esempio con uno schermo creato da un test) il client mostra un avviso con l'incasso.

## Verifica

`tests/TIndustry.Shared.Tests`, con `GameRules(InstantBuild: true)` dove non serve la costruzione:
- **prezzi**: con 10 ore simulate il prezzo normale resta fra B e 1,6×B; i picchi arrivano a 10–20 minuti l'uno dall'altro, salgono in 10 s, restano 10 s, scendono in 30 s, e la finestra «nel picco» c'è sempre;
- **vendite**: prezzo al clic per tutti i pezzi; calo dello 0,04% di B per pezzo e recupero in 2 minuti; mai sotto B per vendite piccole; crollo a 5.000 pezzi in 2 minuti anche in più vendite; durante il crollo niente picchi e conto alla rovescia fermo; i primi 5 minuti a volte 0,6–0,7×B;
- **jackpot e pity**: le probabilità della tabella a 399, 400, 600, 3.000, 10.000, 12.000 pezzi; nessun tentativo fuori dal picco, sotto i 400 pezzi o senza schermo; i cinque scenari del foglio «Pity» con esiti forzati (estrazione iniettata nei test); garanzia dopo 3 a vuoto; tentativi sotto i 3.000 che non toccano la serie; attesa globale e picco rimandato dopo un tentativo a vuoto senza crollo; recupero accelerato dopo un 50/50 perso; l'esempio 10 + 77 + 20 = 107;
- **vendita automatica**: per oggetto, al prezzo minimo, senza toccare il mercato, uguale da nastri ed edifici;
- **determinismo**: stessi comandi = stesso checksum (`DeterminismTests`, `SnapshotTests`, replay degli agenti), anche a cavallo di un salvataggio a metà picco e a metà crollo; il generatore dà la stessa sequenza su ogni piattaforma (valori fissati nel test);
- **salvataggi**: v14 salva e ricarica tutto lo stato del mercato; v11–13 si leggono con l'interruttore unico tradotto;
- **contenuti**: `FactoryContent.Load` rifiuta B mancanti e regole incoerenti.

Client: playtest `ui` con una cattura della panoramica (normale, picco, crollo, attesa), nelle tre varianti di colore e in inglese; `TestiTests` verde.

## Documenti

`docs/ARCHITETTURA.md` (stato del mercato, generatore seminato, `MarketSystem` nell'ordine dei sistemi), `godot/README.md` (Mercato), Wiki (Economia e Ricerca, Domande frequenti), README di AgentPlay e skill `gioca-e-testa` (`vendi tutto`, `vendita_automatica`), `CHANGELOG.md`, `docs/idee.md` (l'idea dello «stonks» passa alla roadmap), diario a fine piano.
