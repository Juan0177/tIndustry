# Il Mercato in breve

Come funziona il nuovo Mercato di tIndustry, senza i numeri di dettaglio. Le regole complete stanno nella [specifica](../superpowers/specs/2026-10-22-mercato-borsa-design.md) e nella cartella di lavoro `docs/finanza/mercato-e-finanza.xlsx` (si genera con `python tools/dev/genera_finanza.py . docs/finanza/mercato-e-finanza.xlsx`).

## L'idea

Ogni oggetto si vende come un titolo in borsa: ha un **suo prezzo**, che si muove per conto suo. Vendere e basta rende poco; chi aspetta il momento giusto guadagna molto di più. Chi esagera fa crollare il prezzo.

## Il prezzo

- Ogni oggetto ha un **valore base**: è il suo prezzo minimo. Una lastra di ferro, per esempio, vale 23 $.
- Di solito il prezzo **oscilla poco sopra il valore base**, fino a una volta e mezza circa.
- Ogni tanto, più o meno ogni quarto d'ora, arriva un **picco**: in pochi secondi il prezzo sale fino al **triplo** del valore base, resta lì una decina di secondi e poi riscende. È il momento buono per vendere.
- Ogni oggetto ha i suoi picchi: se sale il ferro, il rame resta com'è.

## Vendere

- Nella schermata del Mercato ogni oggetto ha **«Vendi 1»**, **«Vendi 100»** e **«Vendi tutto»**. Tutti i pezzi di una vendita si pagano al prezzo del momento del clic.
- Ogni vendita **abbassa un po' il prezzo**, che poi torna su da solo in un paio di minuti. Le vendite piccole non lo portano mai sotto il valore base.
- Se però in poco tempo vendi **tantissimi pezzi** dello stesso oggetto, provochi un **crollo**: per 20 minuti il prezzo resta al valore base (all'inizio anche sotto) e non arrivano picchi.
- La **vendita automatica** si accende oggetto per oggetto. È comoda per liberarsi di quello che non usi, ma paga sempre il prezzo minimo.

## Il jackpot

- Con uno **schermo di borsa** accanto al CORE (arriverà con un aggiornamento successivo), un **«Vendi tutto» fatto durante un picco** con tanti pezzi può dare il **jackpot**: ogni pezzo vale molto di più, circa dieci volte il valore base.
- Più pezzi vendi, più è probabile: si parte da poche possibilità e si arriva al massimo a una su due (il «50/50»).
- Il jackpot fa comunque crollare il prezzo, e un tentativo grande andato male anche.
- **Pity**: se i tentativi grossi vanno a vuoto, il successivo è sempre più probabile, e dopo tre tentativi a vuoto di fila il jackpot è garantito.
- Un tentativo andato male che non fa crollare blocca «Vendi tutto» per qualche minuto e rimanda il prossimo picco di quell'oggetto.

## Due esempi

### 1. Vendere al momento giusto

Hai 1.000 lastre di ferro nel CORE.

| Quando vendi | Prezzo di una lastra | Incasso |
|---|---|---|
| Subito, con la vendita automatica | 23 $ (il minimo) | 23.000 $ |
| In un momento normale | circa 30 $ | circa 30.000 $ |
| Durante un picco | circa 67 $ | circa 67.000 $ |

Aspettare il picco vale quasi il triplo della vendita automatica. Mille lastre non bastano a far crollare il prezzo, quindi non si rischia niente.

### 2. Il 50/50

Hai accumulato 10.000 pezzi di ferro grezzo (valore base 6 $) e arriva un picco. Hai lo schermo di borsa e premi «Vendi tutto»: è un 50/50.

| Esito | Incasso | Dopo |
|---|---|---|
| Jackpot | circa 630.000 $ | crollo del ferro per 20 minuti |
| Niente jackpot | circa 170.000 $ (comunque il prezzo del picco) | crollo del ferro per 20 minuti, e la prossima volta la probabilità è più alta |

Con la vendita automatica gli stessi 10.000 pezzi avrebbero reso 60.000 $. Anche quando il jackpot non esce, vendere al picco conviene; quando esce, rende dieci volte tanto. Il prezzo da pagare è il crollo: per un po' il ferro si vende solo al minimo, e conviene puntare su un altro oggetto.
