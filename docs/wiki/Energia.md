# Energia

Come si produce e si porta la corrente: generatore, reti di nodi T1 e T2, Batteria, Lampada, e cosa cambia nei blocchi alimentati.

## A cosa serve

La corrente non è obbligatoria: il Forno può andare a carbone e l'Assemblatore lavora anche senza. Con la corrente però:

| Blocco | Con la corrente | Consumo |
|---|---|---|
| Forno | Non brucia carbone e lavora fino al **20% più veloce** | 8 unità al secondo quando ha qualcosa da fondere |
| Assemblatore | Lavora fino al **20% più veloce** | 10 unità al secondo quando ha una ricetta da fare |
| Minatore T2 | Estrae fino al **20% più veloce** | 0,5 unità al secondo mentre scava |
| Lampada | Si accende e illumina la notte | 1 unità al secondo, solo di notte |

Il Minatore T1 e l'Estrattore non usano corrente. Un blocco che non ha niente da fare (un forno senza minerale) non chiede corrente.

## Blocchi di corrente

Fanno parte della rete il Generatore, i Nodi T1 e T2, la Batteria e ogni blocco che consuma corrente (Forno, Assemblatore, Minatore T2, Lampada). Nastri, Minatore T1, Estrattore, magazzini e **CORE** non ne fanno parte: il CORE non produce né conserva corrente.

## Generatore

| Generatore (`generator`) | |
|---|---|
| Misura | 2×2 |
| Costo | 8 Lastre di ferro |
| Sblocco | $280, 12 Lastre di ferro (nessun prerequisito) |
| Combustibile | Carbone portato da un nastro; scorta di 8 |
| Consumo | 1 Carbone ogni 8 secondi |
| Produzione | **30 unità al secondo** mentre brucia |

Un generatore è **acceso** finché ha carbone da bruciare: il rotore gira e, di notte, fa luce. Accetta solo carbone.

## La rete

Una **rete** è l'insieme dei blocchi di corrente collegati fra loro. Ci sono due modi per collegare:

1. **Contatto**: due blocchi di corrente che si toccano lato contro lato sono già collegati, senza nodi e senza laser (un forno accanto a un generatore).
2. **Laser**: un nodo si collega a un altro blocco di corrente con un laser. I nodi si passano la corrente fra loro, quindi una **catena di nodi** porta la corrente da un generatore a un forno lontano.

| Nodo | Misura | Raggio | Collegamenti massimi | Costo | Sblocco |
|---|---|---|---|---|---|
| Nodo T1 (`power-node`) | 1×1 | 6 celle | 6 | 2 Fili di rame | $140, 4 Lastre di ferro, 2 Fili di rame (dopo il Generatore) |
| Nodo T2 (`power-node-t2`) | 2×2 | 10 celle | 10 | 6 Lastre di ferro, 4 Fili di rame | $320, 12 Lastre di ferro, 6 Fili di rame (dopo il Nodo T1) |

Il raggio si misura **fra i bordi** dei due blocchi (non dal centro): un blocco grande è raggiungibile prima di uno piccolo alla stessa distanza dal centro. Un laser fra due nodi conta un collegamento su entrambi e vale se basta il raggio di uno dei due. Il contatto non consuma collegamenti.

### Collegamenti automatici

Quando un nodo è finito di costruire si collega da solo:

1. ai nodi nel suo raggio, dal più vicino (a parità, il più vecchio), finché entrambi hanno collegamenti liberi;
2. poi ai blocchi di corrente nel raggio che non sono già su una rete con un generatore o una batteria, dal più vicino, finché gli restano collegamenti.

Un nodo in costruzione non ha laser. Smontare un blocco toglie anche i suoi laser, e la rete si può spezzare in due.

### Collegamenti a mano

Clicca un **nodo**: si mostrano il raggio e i collegamenti usati (per esempio *3/6*). Poi clicca un blocco di corrente nel raggio: se il laser c'è lo toglie, altrimenti lo aggiunge. **Esc** o un clic fuori deseleziona. Il collegamento viene rifiutato, con il motivo, se il blocco è fuori raggio, se il nodo non ha più collegamenti liberi o se è il nodo stesso. Un blocco che non è di corrente toglie solo la selezione.

## Quanta corrente arriva

Ogni rete fa il suo conto a ogni istante, senza aiutare le altre:

- **Produzione** = 30 al secondo per ogni generatore acceso.
- **Richiesta** = somma dei consumi delle macchine che in quel momento vogliono lavorare.
- Se la produzione basta, tutte le macchine ricevono il **100%** e quello che avanza riempie le batterie della rete.
- Se non basta, le batterie danno la differenza finché hanno carica.
- Se non basta ancora, tutte le macchine della rete ricevono la **stessa percentuale**: (produzione + batterie) ÷ richiesta. Le batterie restano vuote.

Esempi: 1 generatore (30) e 3 forni (24) = 100%, avanzano 6 al secondo; 1 generatore e 5 forni (40) = 75%.

### Effetto sulle macchine

Con una soddisfazione `s` (da 0% a 100%):

- **Assemblatore**: lavora sempre; velocità `1 + 0,2 × s`.
- **Minatore T2**: velocità `1 + 0,2 × s`, oltre al suo doppio.
- **Lampada**: luce proporzionale a `s`, spenta a 0.
- **Forno**: con la corrente va a `1,2 × s`. Se ha carbone e `1,2 × s` è sotto 1, brucia il carbone e va a velocità 1, senza chiedere corrente in quel momento. Senza carbone e senza corrente è fermo.

## Batteria

| Batteria (`battery`) | |
|---|---|
| Misura | 1×1 |
| Costo | 10 Lastre di piombo, 4 Fili di rame |
| Sblocco | $300, 10 Lastre di piombo, 5 Fili di rame (dopo il Nodo T1), in Ricerca |
| Capacità | 1.000 unità |

Si carica con la corrente che avanza e la rende quando i generatori non bastano, così la rete resta accesa anche quando il carbone finisce. La carica di una rete si divide fra le sue batterie in proporzione alla capacità; se la rete si spezza o si unisce, ogni batteria tiene la sua carica. Lo sprite mostra la carica con una barra. Passando il mouse su un blocco di corrente il suggerimento riassume la rete: blocchi, produzione, consumo, soddisfazione e batterie.

## Lampada

| Lampada (`lamp`) | |
|---|---|
| Misura | 1×1 |
| Costo | 2 Lastre di ferro, 1 Filo di rame |
| Sblocco | $90, 4 Lastre di ferro, 2 Fili di rame (dopo il Nodo T1) |
| Consumo | 1 unità al secondo, solo quando serve luce |

Chiede corrente **solo di notte** (luce del mondo sotto la metà); di giorno non consuma. Serve nelle partite di **notte** (la notte del ciclo, la casella *Solo notte* in Nuova partita, i livelli 6 e 10 della campagna), dove contano le luci del CORE, dei forni al lavoro, dei generatori accesi, dei droni e delle Lampade.

## Uno schema semplice

```text
nastro di carbone ──▶ [Generatore] ··· [Nodo] ··· [Nodo] ··· [Nodo] ··· [Nodo] ··· [Nodo] ··· [Forno]
                                     6 celle   6 celle   6 celle   6 celle   6 celle   6 celle
```

Il generatore si collega al primo nodo, i nodi fra loro, l'ultimo al forno: con cinque nodi da 6 celle il forno a 30 celle va a corrente. Un forno messo a contatto diretto con il generatore non ha bisogno di nodi.

## Vedi anche

- [Produzione](Produzione)
- [Economia e Ricerca](Economia-e-Ricerca)
- [Domande frequenti](Domande-frequenti)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Logistica](Logistica) · [Torna alla Home](Home) · [Pagina successiva: Costruzione →](Costruzione)

<!-- /navigazione -->
