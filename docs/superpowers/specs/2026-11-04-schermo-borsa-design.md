# Schermo di borsa, moduli della Finanza e bilanciamento — specifica 2a

- **Stato**: attuata (2026-11-04, ramo claude/ripresa-task-notturni-5ht4tf; [diario](../../diario/2026-11-04-schermo-borsa.md)). Approvata dall'utente il 4 novembre 2026 ([piano](../plans/2026-11-04-schermo-borsa.md)). Brainstorming con l'utente del 4 novembre 2026; issue [#268](https://github.com/Juan0177/tIndustry/issues/268). Aspetto 6 «Mercato ed economia» di `docs/STATO.md`.
- **Le specifiche del Mercato**: 1 [Mercato come una borsa](2026-10-22-mercato-borsa-design.md) (attuata), 3 [Magazzino del CORE](2026-10-23-magazzino-core-design.md) (attuata), **2a** questa, **2b** la sequenza animata del «Vendi tutto» (zoom senza HUD, bot della finanza in 2.5D, valigetta, «calcolo…», coriandoli), da specificare dopo.
- **Sostituisce**: il jackpot che non scatta mai in partita (`GameState.HasMarketScreen` sempre falso) e i costi della Ricerca di oggi (circa 37.000 $ per tutto l'albero: un solo forno li incassa in meno di un'ora).

## Obiettivo

Lo schermo di borsa, attaccato al CORE, accende il jackpot in partita e mostra il mercato di un oggetto; il ramo Finanza della Ricerca sblocca tre moduli che aiutano a vendere al momento giusto. Con i costi nuovi i soldi tornano a contare: sbloccare tutto richiede circa **6 ore** di partita ben giocata vendendo i prodotti migliori di ogni fase, e i picchi e i jackpot accorciano davvero la strada.

Successo: nel foglio «Bilanciamento» della cartella di lavoro una partita che vende il prodotto migliore di ogni fase sblocca tutta la Ricerca fra 5,5 e 6,5 ore senza jackpot; un jackpot sul 50/50 con 10.000 lastre di titanio vale quasi un CORE Nucleo; in partita, con lo schermo attaccato, un «Vendi tutto» nel picco da 400 pezzi in su può vincere il jackpot.

## Decisioni prese con l'utente

| Tema | Decisione |
|---|---|
| Portata | La specifica 2 si divide: **2a** (schermo, jackpot in partita, moduli, ramo Finanza, LED e risultato semplice sullo schermo, bilanciamento) e **2b** (sequenza animata). Il gioco è completo già con la 2a. |
| Schermo | **Uno solo**, piazzato **a contatto col CORE**. Se il CORE cresce e lo schermo resta staccato, lo schermo smette di funzionare finché non lo si riattacca. |
| Moduli | **Sbloccati in Ricerca**, in fila (previsione → percentuale → probabilità); non si piazza niente. |
| Previsione | **Prossimo picco + consiglio**: «picco fra circa N minuti», «aspetta / vendi ora» (vendi ora nel picco); durante un crollo, quanto manca alla fine. Non prevede mai il jackpot. |
| Costi del ramo Finanza | Più alti della prima proposta (vedi tabella sotto, poi riscalata dal bilanciamento). |
| Bilanciamento | **Dentro la 2a**, per tutta la Ricerca. Obiettivo **circa 6 ore** per sbloccare tutto senza jackpot, **vendendo il prodotto migliore di ogni fase**, non il ferro grezzo. |

## Schermo di borsa (`market-screen`)

- Blocco **2×2**, senza direzione e senza corrente; sprite da `tools/art/genera.py`: schermo largo senza gambe, cornice con la striscia di LED gialla e nera.
- Piazzamento:
  - rifiutato se nessuna sua cella tocca lato contro lato una cella del CORE (`RefusalCode.MarketScreenNotTouchingCore`);
  - rifiutato se c'è già uno schermo, finito o in cantiere (`RefusalCode.MarketScreenAlreadyPresent`).
- **Attivo** = finito e a contatto col CORE. `GameState.HasMarketScreen` è vero solo con uno schermo attivo; si ricalcola quando cambiano gli edifici o il CORE (costruzione, smontaggio, crescita del CORE). Il CORE si potenzia come in Mindustry (lo si sblocca in Ricerca, lo si sceglie nella palette e lo si piazza sopra il CORE): lo schermo sulle celle nuove **blocca** il potenziamento come ogni altro edificio, quindi va spostato prima (decisione dell'utente del 4 novembre). Uno schermo che resta staccato è **inattivo** e lo dice («Staccato dal CORE»).
- **Oggetto seguito**: si sceglie col clic (nessun blocco scelto in palette), con il pannello del filtro della logistica (griglia delle icone, «nessuno»); comando `SetBlockFilter` già esistente, accettato anche sullo schermo. Senza oggetto lo schermo mostra «Scegli un oggetto».
- Sullo schermo nel mondo: icona dell'oggetto, prezzo del momento con ▲▼, grafico degli ultimi 2 minuti (gli stessi dati della panoramica), «Crollo» se c'è; con i moduli sbloccati anche le loro righe (sotto).
- **Esito del «Vendi tutto»**: a ogni `SaleResolved` di un «Vendi tutto» con lo schermo attivo, la simulazione salva l'**ultimo esito** sullo schermo (oggetto, incasso, colore dei LED, jackpot sì/no, tick). Il client lo mostra per **6 secondi**: incasso e LED accesi; col jackpot la scritta «JACKPOT» e luci gialle negli angoli della cornice. La sequenza animata è della 2b.
- **LED** (calcolati nella simulazione, `marketRules`):
  - **giallo**: il «Vendi tutto» era da `ledYellowPieces` = **10.000** pezzi o più (il 50/50), vinto o perso;
  - **blu**: sotto i 10.000 pezzi, senza crollo, e il prezzo è sceso al massimo di `ledMildDrop` = **10% di B**;
  - **viola**: tutti gli altri (calo oltre il 10% di B o crollo);
  - col jackpot si accendono sempre i gialli.
- Un «Vendi tutto» senza schermo attivo resta come oggi (niente tentativo di jackpot, niente esito sullo schermo).

## Ramo Finanza della Ricerca

In fila, dopo l'assemblatore (`assembler`, fase di metà partita: lo schermo si costruisce col filo di rame). Costi di partenza scelti con l'utente; il bilanciamento li riscala con tutto il resto (sotto), mantenendo l'ordine e le proporzioni fra i materiali.

| Nodo | Sblocca | Ricerca (prima del bilanciamento) | Costruzione |
|---|---|---|---|
| `market-screen` | il blocco | 3.000 $ + 80 lastre di ferro + 40 fili di rame | 60 lastre di ferro + 25 fili di rame + 10 lastre di piombo |
| `finance-forecast` | modulo previsione | 6.000 $ + 60 lastre di piombo + 30 fili di rame | — |
| `finance-percent` | modulo percentuale | 12.000 $ + 80 lastre di titanio | — |
| `finance-odds` | modulo probabilità | 25.000 $ + 120 silicio + 80 grafite | — |

I moduli si vedono **solo con uno schermo attivo**, sullo schermo dell'oggetto seguito e nella panoramica del Mercato per ogni oggetto:

- **Previsione**: nello stato normale «picco fra circa N min» (secondi del conto alla rovescia del picco, arrotondati al minuto per eccesso; «meno di 1 min» sotto i 60 s) e il consiglio «aspetta»; nel picco «vendi ora»; durante un crollo «fine del crollo fra N min»; durante l'attesa globale dopo un tentativo a vuoto «Vendi tutto bloccato per N min».
- **Percentuale**: prezzo del momento come «+N% su B» (intero, per difetto).
- **Probabilità**: la probabilità del jackpot se ora si facesse «Vendi tutto» di tutti i pezzi di quell'oggetto nel CORE, pity compreso, calcolata con la stessa funzione della vendita (0% fuori dal picco, sotto i 400 pezzi o durante l'attesa).

## Bilanciamento

- Foglio nuovo **«Bilanciamento»** in `tools/dev/genera_finanza.py` (cartella di lavoro `docs/finanza/mercato-e-finanza.xlsx`, non versionata):
  - fasi con il prodotto venduto, i pezzi al secondo e l'incasso tipico a B;
  - per ogni nodo della Ricerca la fase, i minuti di lavoro e il costo risultante (soldi e materiali);
  - la somma delle 6 ore, il tempo per sbloccare tutto senza jackpot e con 1 o 2 jackpot.
- Fasi di partenza (da ritoccare nel foglio):

| Fase | Tempo | Prodotto venduto | Incasso tipico |
|---|---|---|---|
| Inizio | 0–30 min | lastre di ferro | 30–60 $/s |
| Metà | 30 min – 2 h | filo di rame, lastre di piombo, grafite | 200–400 $/s |
| Avanzata | 2–4 h | lastre di titanio, silicio | 1.000–1.500 $/s |
| Fine | 4–6 h | titanio e silicio in grande quantità | circa 3.000 $/s |

- Nodi per fase: **Inizio** estrattore, forno, incrocio, router, generatore, nodo T1, lampada; **Metà** assemblatore, selezionatori, ponte, nastro veloce, batteria, contenitore, minatore T2, schermo di borsa; **Avanzata** nodo T2, vault, nastro espresso e rinforzato, CORE Fondazione, previsione, percentuale; **Fine** CORE Nucleo, probabilità.
- Ordine di grandezza atteso: in 6 ore si incassano 30–35 milioni e la Ricerca intera ne costa 22–25; forno circa 1.500 $, schermo circa 150.000 $, Fondazione circa 1,5 milioni, Nucleo circa 7 milioni, probabilità circa 4 milioni. Anche i **materiali** della Ricerca crescono, così una fase non si salta vendendo; i costi di **costruzione** dei blocchi restano quelli di oggi, salvo CORE e schermo.
- Lo script scrive i costi in `data/content.json` (`structures[].unlock`) con un'opzione (`--scrivi-costi`) e li controlla con un'altra (`--controlla`), così i numeri hanno una sola fonte. Nessun numero si cambia a mano in `content.json`.
- **Spazio per Attacco e difesa** (aspetto 13, detto dall'utente il 4 novembre): torrette, munizioni e difese costeranno molto e saranno un'altra grande spesa. Il foglio ha già una riga «Difesa (aspetto 13)» con un budget di spesa per fase, oggi a zero; quando arriva l'aspetto 13 si riempie e le 6 ore si ricalcolano con quella spesa dentro, senza rifare i costi della Ricerca.
- **Campagna e tutorial**: gli obiettivi di guadagno e i soldi iniziali dei livelli si ritoccano quanto basta perché restino giocabili (`MapTool valida`, test della campagna); la campagna vera si rifà dopo, con questi numeri (aspetto 8).

## Simulazione

- Contenuti: struttura e blocco `market-screen` (`BuildingKind.MarketScreen`, 2×2), nodi `finance-forecast`, `finance-percent`, `finance-odds`; `marketRules.ledMildDrop`, `marketRules.ledYellowPieces`.
- Edificio `MarketScreen`: oggetto seguito (nullable), ultimo esito (oggetto, incasso, LED, jackpot, tick). Ricalcolo di `HasMarketScreen` in ordine fisso.
- `SetBlockFilter` accetta lo schermo; rifiutato con un oggetto che il Mercato non conosce.
- `IGameView`: schermo (posizione, attivo, oggetto seguito, ultimo esito), moduli sbloccati, e per oggetto: secondi al prossimo picco, fine del crollo, attesa globale, percentuale su B, probabilità del jackpot con «Vendi tutto».
- Salvataggi **v20**: oggetto seguito e ultimo esito dello schermo; i v19 non si aprono. Determinismo: nessun numero nuovo casuale; la probabilità mostrata usa la stessa funzione intera della vendita senza estrarre dal generatore.

## Agenti

`stato`: `schermo` (presente, attivo, oggetto, ultimo esito) e, per ogni oggetto, solo i moduli sbloccati e con lo schermo attivo (`previsione`, `percentuale`, `probabilita_jackpot`). `piazza` accetta `market-screen`; `filtro` accetta lo schermo. Diario degli agenti alla versione 6. README di AgentPlay aggiornato.

## Client

- Sprite `market-screen` da `genera.py`; disegno nel mondo di grafico, prezzo, righe dei moduli, LED e «JACKPOT» per 6 secondi; avviso «Staccato dal CORE».
- Pannello del filtro aperto anche sullo schermo.
- Panoramica del Mercato: colonne dei moduli sbloccati (con lo schermo attivo).
- Palette: categoria del Mercato (o Magazzino) con lo schermo; testi dei rifiuti.
- Testi in `testi.csv` (it + en), stili dal tema (`UiTheme`, colori dei LED in `UiColors` nelle tre varianti).

## Test

- Piazzamento: a contatto col CORE sì, staccato no, secondo schermo no (anche con il primo in cantiere).
- `HasMarketScreen`: falso in cantiere, vero finito e attaccato, falso dopo lo smontaggio e quando il CORE cresce e lo stacca; jackpot possibile solo con lo schermo attivo.
- LED: i casi del foglio «Esempi di vendita» (blu, viola, giallo, jackpot).
- Moduli: secondi al picco, fine del crollo, attesa, percentuale e probabilità uguali a quelli della simulazione; nascosti senza Ricerca o senza schermo attivo.
- Bilanciamento: `genera_finanza.py --controlla` dice che `content.json` ha i costi del foglio; il foglio dà 5,5–6,5 ore senza jackpot; i test della campagna passano.
- Salvataggio v20 avanti e indietro (stesso checksum), v19 rifiutato; determinismo con lo schermo e i «Vendi tutto».
- Agenti: `stato` coi moduli, `piazza` e `filtro` sullo schermo.

## Fuori ambito

La sequenza animata del «Vendi tutto» e la schermata JACKPOT a tutto schermo (2b); la campagna rifatta (aspetto 8); più schermi; moduli piazzabili; corrente per lo schermo.
