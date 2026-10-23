# Logistica

Come si spostano gli oggetti: nastri e loro tier, curve e unioni, incrocio, sdoppiatore, selezionatore, ponte e nastro rinforzato.

## Nastri

In una cella di nastro stanno fino a **3 oggetti**, a 0,4 di cella l'uno dall'altro; una linea piena porta *velocità ÷ 0,4* oggetti al secondo, uguale in tutte le direzioni.

| Nastro | Velocità (celle/s) | Portata (oggetti/s) | Costo per cella | Sblocco |
|---|---|---|---|---|
| Nastro T1 (`conveyor-basic`) | 2,1 | 5,25 | 1 Ferro grezzo | iniziale |
| Nastro T2 (`conveyor-fast`) | 4,8 | 12 | 1 Ferro grezzo, 1 Piombo grezzo, 1 Titanio grezzo | dopo il Nastro T1 |
| Nastro T3 (`conveyor-express`) | 7,2 | 18 | 1 Titanio grezzo, 1 Silicio, 1 Grafite | dopo il Nastro T2 |
| Nastro rinforzato (`conveyor-armored`) | 4,8 | 12 | 1 Ferro grezzo, 1 Piombo grezzo, 2 Titanio grezzo, 1 Grafite | dopo il Nastro T2 |

## Pezzi speciali

| Pezzo | Portata | Costo | Sblocco | Cosa fa |
|---|---|---|---|---|
| Incrocio (`junction`) | 18 oggetti/s per direzione | 3 Ferro grezzo | dopo il Nastro T1 | Due linee si attraversano senza mescolarsi |
| Sdoppiatore (`splitter`) | 8 oggetti/s | 3 Ferro grezzo | dopo l'Incrocio | Manda gli oggetti a sinistra e a destra, alternando |
| Selezionatore (`sorter`) | 8 oggetti/s | 2 Ferro grezzo, 2 Piombo grezzo | dopo l'Incrocio | L'oggetto del filtro va dritto, gli altri di lato |
| Nastro ponte (`conveyor-bridge`) | 5,25 oggetti/s | 6 Ferro grezzo, 6 Piombo grezzo **per estremo** (12 e 12 in tutto) | dopo l'Incrocio | Scavalca da 1 a 3 celle |

I costi di sblocco sono in [Economia e Ricerca](Economia-e-Ricerca).

## Curve, unioni e T

Un nastro prende oggetti da dietro e dai lati: da altri nastri e dagli edifici che gli scaricano sopra. Non prende mai dal nastro che ha **di fronte** (due nastri testa a testa non si passano nulla).

- Un solo ingresso di lato e niente dietro: il nastro si disegna a **curva**.
- Ingresso dietro e di lato: è un'**unione**, le due linee diventano una.
- Ingressi su entrambi i lati e niente dietro: il nastro si disegna a **T**.

## Nastro sopra nastro

| Cosa piazzi sopra un nastro semplice | Cosa succede |
|---|---|
| Lo stesso nastro, direzione diversa | Il nastro gira subito, gratis, e tiene i suoi oggetti |
| Lo stesso nastro, stessa direzione | Niente |
| Un nastro di un altro tipo (altro tier o il rinforzato) | Lo **sostituisce**: il drone apre un cantiere, paghi solo la **differenza** dei costi, il nastro nuovo tiene gli oggetti del vecchio; se il nastro vecchio costava di più, la parte in più torna al CORE con la quota di rimborso |

Annullare un cambio di tier col destro rimette il nastro vecchio con i suoi oggetti. Per potenziare una catena intera trascina con `Ctrl` dal primo all'ultimo nastro con il nuovo tier in mano: la linea segue la catena e ogni nastro tiene la sua direzione.

## Incrocio

Un oggetto che entra da un lato esce dal lato opposto: le due linee proseguono senza mescolarsi, ognuna fino a 18 oggetti al secondo, e se un'uscita è bloccata l'altra direzione continua. Gli edifici non ci scaricano dentro.

**Incrocio automatico.** Trascinando una linea di nastri che attraversa un nastro perpendicolare e prosegue oltre, in quella cella va un incrocio al posto del nastro: paghi l'incrocio e il nastro viene rimborsato come uno smontaggio. Succede solo dove l'incrocio fa lo stesso lavoro del nastro (un nastro dritto fra due pezzi di logistica, non accanto al CORE o a un edificio che lo alimenta, non su curve o T, non sull'ultima cella della linea né con un clic singolo). Annullare uno dei due piani li annulla entrambi. Se l'incrocio non è ancora sbloccato, il nastro attraversato gira nella direzione della linea e la linea attraversata vi entra di lato: diventa un'unione.

## Sdoppiatore

Ogni oggetto che riceve esce a sinistra o a destra rispetto alla sua direzione, alternando; se un lato è pieno usa l'altro. Non manda nulla dritto.

## Selezionatore e filtro

L'oggetto uguale al filtro va **dritto**; gli altri escono **a sinistra** o, se la sinistra è piena, **a destra**. Se l'uscita giusta è piena l'oggetto aspetta. Il filtro è l'icona al centro del blocco.

- `C` cambia il filtro del blocco in mano e, se il mouse è sopra un selezionatore, anche il suo.
- I filtri disponibili, nell'ordine in cui `C` li scorre: Ferro grezzo, Rame grezzo, Lastra di ferro, Filo di rame, Carbone.
- Il tasto centrale su un selezionatore lo prende in mano con il suo filtro.

## Ponte

Un ponte ha due estremi 1×1 a una distanza da 2 a 4 celle, come in Mindustry, quindi scavalca da 1 a 3 celle; l'uscita è la prima cella libera in quella direzione. Le celle in mezzo restano libere: un nastro ci può passare sotto. L'ingresso prende oggetti da tre lati (non da quello della campata), li passa subito all'uscita, che li dà sui suoi tre lati liberi (davanti, sinistra, destra).

Il ponte passa sopra pareti e acqua profonda, purché i due estremi stiano su celle costruibili. Smontando un estremo si smonta tutto il ponte.

**Ponte automatico.** Col ponte sbloccato, una retta di nastri trascinata che incontra pareti, acqua profonda o edifici li scavalca da sola con un ponte (fino a 3 celle di ostacolo); un ostacolo più lungo resta rosso. Non succede con una direzione fissata con `R` di lato.

## Nastro rinforzato

Veloce come il Nastro T2, ma come l'*Armored Conveyor* di Mindustry prende oggetti **dagli altri nastri** da ogni lato e **da tutto il resto** (edifici, sdoppiatori, selezionatori, ponti, incroci) **solo da dietro**. Una linea rinforzata passa lungo una fila di minatori o macchine senza raccogliere niente e resta dritta; con gli altri nastri fa curve, T e unioni come al solito. Si riconosce dai binari d'acciaio con bordo e frecce rossi.

![Linea T2 che raccoglie dai minatori e linea rinforzata che resta vuota](https://raw.githubusercontent.com/Juan0177/tIndustry/pr-media/pr-nastri-10/ui-nastro-rinforzato.png)

*Sopra una linea T2 che raccoglie da tre minatori; sotto una linea rinforzata accanto agli stessi blocchi, che resta vuota.*

## Container e vault

Due magazzini che si mettono sui nastri e accanto agli edifici. Il **Contenitore** (`container`, 2×2) tiene 300 per oggetto, il **Deposito** (`vault`, 3×3) 1.000; costano 40 Lastre di ferro e 20 Lastre di piombo il primo, 60 Lastre di titanio e 40 Lastre di ferro il secondo (sblocchi in [Economia e Ricerca](Economia-e-Ricerca#container-e-vault)).

- **A contatto con il CORE** (lato contro lato) allargano il suo magazzino: il loro spazio si somma al massimo di ogni oggetto e i nastri che vi consegnano consegnano al CORE.
- **Lontani dal CORE** sono depositi a sé: accettano qualsiasi oggetto da nastri ed edifici finché c'è posto, e un Estrattore attaccato li svuota (prima dal CORE, se anche lui è a contatto e ha l'oggetto, poi dal deposito con l'id più basso).

## In arrivo

Sono pianificati nastri stretti come in Mindustry, incrocio e selezionatore che si comportano come in Mindustry e uno sdoppiatore che diventa un distributore (router); fino ad allora vale quanto scritto qui.

## Vedi anche

- [Comandi](Comandi)
- [Costruzione](Costruzione)
- [Domande frequenti](Domande-frequenti)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Produzione](Produzione) · [Torna alla Home](Home) · [Pagina successiva: Energia →](Energia)

<!-- /navigazione -->
