# Campagna rifatta: tutorial, capitoli e lancio del CORE

- **Stato**: approvata dall'utente il 9 novembre 2026, aggiornata lo stesso giorno (lancio con requisiti nel CORE, nodi solo della campagna); piano da scrivere dopo la specifica «Ricerca allargata», che va implementata prima (i lavori non sono ancora iniziati). Aspetto 8 «Campagna» di `docs/STATO.md`.
- **Sostituisce**: i 10 livelli di oggi (`data/campaign.json`) con obiettivi minuscoli e la Ricerca da capo a ogni livello; i soldi di partenza gonfiati a novembre per pagare i costi nuovi; i livelli 03, 04 e 08 con un obiettivo già fatto alla partenza (`CoveredAtStart` nei test).

## Obiettivo

Una campagna che dura quanto la partita libera bilanciata (circa 6 ore, più i tutorial) e che si gioca come le tappe di Mindustry: quattro livelli brevi insegnano le basi, poi quattro capitoli lunghi portano dalla corrente al CORE Nucleo. La Ricerca resta per tutta la campagna; fra un livello e l'altro si riparte solo con la base, portando nel livello nuovo quello che c'è nel CORE fino ai suoi tetti.

Successo: la campagna intera si completa in circa 7 ore; ogni livello si chiude con sblocchi e incassi veri, mai con un obiettivo già fatto alla partenza; chi chiude un livello con il CORE pieno e molti soldi parte avvantaggiato nel successivo, fino ai tetti del CORE.

## Decisioni prese con l'utente

| Tema | Decisione |
|---|---|
| Forma | **Ibrida**: 4 tutorial brevi, poi 4 capitoli lunghi. |
| Ricerca | **Resta per tutta la campagna**, tutorial compresi: si sblocca una volta. Fra un livello e l'altro si resetta solo la base (mappa, edifici, nastri, piani). |
| Capitoli | **4 capitoli da 60–90 minuti**, uno per fase del bilanciamento. |
| Obiettivi dei capitoli | **Sblocchi chiave della fase + un incasso** che richiede una fabbrica vera; mai un obiettivo già fatto alla partenza; obiettivi facoltativi con una stella. |
| Cosa passa al livello dopo | **Come il lancio di Mindustry**: le risorse dentro il CORE e i soldi, ciascuno fino al tetto del livello del CORE. |
| Lancio | **Come Mindustry**: per passare al livello dopo servono certe **risorse e soldi nel CORE** (requisiti di lancio del livello); **restano e si portano** nel livello nuovo insieme al resto, fino ai tetti. Non c'è una dotazione minima: il livello dopo parte sempre con almeno i requisiti. |
| Ricerca allargata | Blocchi, potenziamenti, ricette e oggetti nuovi, **alcuni sbloccabili solo nella campagna** (premi dei capitoli). Specifica a parte, implementata **prima** della campagna. |
| CORE e tetti | Il livello nuovo parte **con lo stesso livello di CORE** con cui si è finito. Tetti dei soldi: **50.000 $** (CORE base), **1.000.000 $** (Fondazione), **10.000.000 $** (Nucleo); risorse fino al massimo per oggetto del CORE (1.000 / 4.000 / 12.000). |

## I livelli

| # | Livello | Durata | Cosa insegna | Obiettivi |
|---|---|---|---|---|
| T1 | Primi passi | ~10 min | Drone, scavo a mano, minatore, nastri al CORE | 50 ferro grezzo consegnati al CORE |
| T2 | Il forno | ~15 min | Ricerca, forno, lastre | Sblocca il forno; 100 lastre di ferro prodotte |
| T3 | Fili e assemblatore | ~20 min | Assemblatore, ricette a due ingressi | Sblocca l'assemblatore; 100 fili di rame prodotti |
| T4 | Il Mercato | ~20 min | Vendite, picchi, crolli, vendita automatica | Guadagna 10.000 $ vendendo |
| C1 | Corrente e logistica | 60–90 min | Generatore, nodi, batteria, router, selezionatori | Sblocca generatore, nodo T1, batteria e selezionatore; guadagna 1.000.000 $ |
| C2 | Fondazione e borsa | 60–90 min | CORE Fondazione, schermo di borsa, jackpot | Sblocca CORE Fondazione, schermo di borsa e modulo previsione; guadagna 4.000.000 $ |
| C3 | Titanio e silicio | 60–90 min | Minatore T2, titanio, silicio, nodo T2, vault | Sblocca Minatore T2, nodo T2, vault e modulo percentuale; guadagna 10.000.000 $ |
| C4 | Il Nucleo | 60–90 min | CORE Nucleo, modulo probabilità | Sblocca CORE Nucleo e modulo probabilità; guadagna 20.000.000 $ |

- I numeri degli incassi sono di partenza: il piano li verifica col modello del foglio «Bilanciamento» (`tools/dev/genera_finanza.py`) perché ogni capitolo stia fra 60 e 90 minuti, e li scrive in `campaign.json`.
- «Guadagna» conta solo le vendite fatte in quel livello; «prodotti» e «consegnati» contano solo quello che arriva al CORE in quel livello (oggi `CoreDeliveredItems`), non le scorte portate.
- Uno sblocco già fatto in un livello precedente conta come completato: per questo gli obiettivi di sblocco di un capitolo sono solo nodi della sua fase, che non si possono sbloccare prima (prerequisiti) senza giocare fuori ordine; se capitasse, quell'obiettivo è semplicemente già spuntato.
- **Stelle** (facoltative, non bloccano): per esempio «vinci un jackpot» in C2 e C4, «finisci in meno di N minuti» negli altri capitoli. Si salvano con lo stato della campagna.
- Giorno e notte per livello come oggi (`dayNight`): tutorial di giorno, capitoli col ciclo o di notte. Gli attacchi notturni arriveranno con l'aspetto 13.

## Lo stato della campagna

Uno stato salvato a parte (estende `CampaignProgress`, file della campagna nel profilo):

- Ricerca sbloccata (insieme degli id).
- Livello del CORE con cui si è chiuso l'ultimo livello.
- Risorse portate (per oggetto) e soldi portati.
- Livelli completati e stelle.

**Chiusura di un livello**: quando tutti gli obiettivi sono fatti si apre il **lancio**: un pannello mostra i **requisiti di lancio** del livello (risorse e soldi che devono essere nel CORE, ciascuno con quanto ne hai) e cosa si porterà al livello dopo, con quello che si perde oltre i tetti. **«Lancia»** è attivo solo con tutti i requisiti nel CORE; **«Resta ancora»** chiude il pannello (si riapre con un pulsante) per produrre quello che manca o spendere prima di partire. Lanciando, lo stato della campagna prende Ricerca, CORE, risorse e soldi del momento, fino ai tetti; i requisiti fanno parte del carico.

**Inizio di un livello**: la mappa del livello, il CORE al livello salvato, la Ricerca salvata, le risorse e i soldi portati (che comprendono sempre i requisiti di lancio del livello prima). Il primo livello parte con la Ricerca di base (Nastro T1 e Minatore T1) e una dotazione iniziale scritta in `campaign.json`.

**Requisiti di lancio**: per ogni livello in `campaign.json` (soldi + elenco di risorse), scelti perché il livello dopo possa partire (per esempio i materiali del primo forno o del primo generatore). Devono stare sotto i tetti del CORE che si ha in quel momento.

**Nodi solo della campagna**: alcuni nodi della Ricerca allargata non si comprano in Ricerca ma si ottengono chiudendo un capitolo (premio mostrato nel pannello del lancio); nella partita libera restano bloccati o hanno un'altra strada, come dirà la specifica della Ricerca allargata.

**Rigiocare un livello già finito**: parte con la Ricerca di adesso e, come carico, esattamente i requisiti di lancio del livello prima; chiuderlo aggiorna le stelle ma **non** cambia lo stato che passa ai livelli dopo.

## Mappe

Si riusano le mappe di oggi scelte per misura e biomi (dalla 200×200 per T1 alla 1000×1000 per C4); il titanio deve comparire dal C3. Mappe nuove o ritoccate solo con `tools/MapTool` (`genera`, `valida`, `anteprima`), mai a mano; `valida` deve dire OK.

## Simulazione e dati

- `campaign.json` nuovo: 8 livelli con obiettivi, requisiti di lancio, premi (nodi solo della campagna), stelle, mappa e `dayNight`; tipi di obiettivo nuovi o rinominati per «prodotti/consegnati in questo livello».
- Avvio di un livello da stato della campagna (Ricerca, CORE, risorse, soldi) in `NewGame.Campaign`.
- Tetti dei soldi per livello di CORE in `content.json` (`cores[].carryMoney`).
- Salvataggi della partita: la versione sale se cambia il formato (stato iniziale del livello o contatori nuovi); salvataggio della campagna con versione propria.
- Agenti: `stato` mostra il livello della campagna e cosa passerebbe al livello dopo; `rigioca` e i diari restano deterministici.

## Client

- Elenco dei livelli coi due gruppi (tutorial, capitoli), stelle e stato.
- Pannello del lancio con i requisiti (avuto/richiesto), il riepilogo del trasporto, il premio del capitolo, «Lancia» / «Resta ancora».
- HUD degli obiettivi con i contatori «in questo livello».
- Testi in `testi.csv` (it + en), stili dal tema.

## Test

- Nessun obiettivo già fatto alla partenza di nessun livello (il test di oggi, senza eccezioni: via `CoveredAtStart`).
- Lancio: rifiutato senza tutti i requisiti nel CORE; trasporto di risorse fino al massimo per oggetto e soldi fino al tetto, requisiti compresi; CORE allo stesso livello; Ricerca intatta; premio del capitolo sbloccato.
- Requisiti di lancio di ogni livello sotto i tetti del CORE previsto in quel livello.
- Rigioco di un livello finito che non cambia lo stato della campagna.
- Tempi dei capitoli fra 60 e 90 minuti nel modello del bilanciamento.
- Salvataggio della campagna avanti e indietro; versioni vecchie rifiutate.
- `MapTool valida` OK; determinismo.

## Fuori ambito

Attacchi e difesa (aspetto 13); co-op (aspetto 14); mappe disegnate a mano; trama e dialoghi.
