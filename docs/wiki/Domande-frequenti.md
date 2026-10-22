# Domande frequenti

Risposte rapide ai problemi più comuni in partita, ricavate da come il gioco funziona davvero.

## Perché il nastro non prende gli oggetti dal lato?

Quasi sempre è un **Nastro rinforzato**: prende dagli altri nastri da ogni lato, ma da edifici, sdoppiatori, selezionatori, ponti e incroci **solo da dietro**. Usa un nastro normale (T1, T2, T3) dove vuoi raccogliere dai lati. Un'altra causa: un nastro non prende mai dal nastro che ha **di fronte** (due nastri testa a testa). Vedi [Logistica](Logistica).

## Perché il minatore non produce?

- Non è su un giacimento, o ne copre poche celle: con 2 celle su 4 va a metà velocità.
- Il **titanio** si scava solo col **Minatore T2**.
- Non ha dove mettere il minerale: serve un nastro accanto che **si allontani** dal minatore (un nastro che punta verso di lui non riceve nulla), oppure un forno, un assemblatore o il CORE a contatto diretto.
- Il nastro d'uscita è pieno perché più avanti la linea è bloccata.

## Perché il forno è fermo?

- Non ha **né carbone né corrente**. Portagli carbone con un nastro (1 Carbone ogni 6 secondi di lavoro) oppure collegalo a un generatore acceso (vedi [Energia](Energia)).
- Non ha gli ingredienti di una ricetta: per esempio 2 Ferro grezzo per una lastra.
- Sul nastro d'ingresso c'è un oggetto che il forno non usa (il Rame grezzo, per dire): resta lì e ferma tutto il resto. Separa i flussi con un selezionatore.
- Non può far uscire le lastre: manca un nastro che si allontani, o è pieno.

## Perché il forno non va più veloce con la corrente?

Il blocco deve **toccare** un generatore acceso, oppure toccare un **nodo** che abbia un generatore acceso nel suo raggio (6 celle il Nodo T1, 10 il Nodo T2). I nodi non si passano la corrente fra loro: ogni nodo deve vedere un generatore. Il generatore deve avere carbone, altrimenti è spento. Se la riserva di energia non basta, il forno torna a bruciare carbone.

## Perché il drone non finisce il blocco?

Il cantiere è fermo perché nel CORE **mancano i materiali**: passaci sopra col mouse ("Mancano: …") o guarda il riquadro della coda in alto a sinistra, che diventa ambra. Riparte da solo quando i materiali arrivano. Se hai acceso la **vendita automatica** nel Mercato, quello che arriva al CORE si vende subito e il magazzino non cresce. Vedi [Costruzione](Costruzione).

## Perché non riesco a piazzare un blocco qui?

L'anteprima rossa con la X ha il motivo scritto in basso:

| Motivo | Cosa fare |
|---|---|
| C'è una parete | Spostati, o scavalcala con un ponte |
| Acqua profonda | Costruisci sull'acqua bassa vicino alle rive, o scavalca con un ponte |
| Non puoi costruire sul CORE | Spostati di una cella |
| Cella occupata o non valida | C'è già un blocco, un piano o un cantiere |
| Serve il Minatore T2 per il titanio | Sblocca il Minatore T2 nella Ricerca |
| … bloccato: sbloccalo in Ricerca (T) | Sbloccalo con `T` |

## Perché smontando non mi torna niente?

Dal livello 4 della campagna lo smontaggio rimborsa il **50%**, arrotondato per eccesso materiale per materiale: un Nastro T1 costa 1 Ferro grezzo e al 50% rende comunque 1. Nella partita libera e nei livelli 1–3 il rimborso è del 100%. Annullare un cantiere non finito rende sempre tutto quello speso.

## Perché il selezionatore manda tutto di lato?

Va dritto solo l'oggetto uguale al **filtro** (l'icona al centro); tutti gli altri escono a sinistra o, se la sinistra è piena, a destra. Il filtro si cambia con `C` tenendo il mouse sopra il selezionatore. Si può filtrare Ferro grezzo, Rame grezzo, Lastra di ferro, Filo di rame o Carbone.

## Perché il prezzo di vendita è sceso?

Il prezzo scende quando nel CORE hai molti pezzi dello stesso oggetto: con 21 Lastre di ferro una lastra vale $21 invece di $30. Vendi a poco a poco, oppure accendi la vendita automatica: ogni pezzo si vende appena arriva, al prezzo di quel momento. Vedi [Economia e Ricerca](Economia-e-Ricerca).

## Come metto in pausa?

Non c'è un tasto di pausa. Con il cursore premi `Esc`: si apre la Home, la partita si ferma (e la Continua si salva) finché non premi **Riprendi** o di nuovo `Esc`. Ricerca, Mercato e Campagna invece lasciano scorrere il tempo.

## Vedi anche

- [Logistica](Logistica)
- [Produzione](Produzione)
- [Costruzione](Costruzione)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Salvataggi](Salvataggi) · [Torna alla Home](Home)

<!-- /navigazione -->
