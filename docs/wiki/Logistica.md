# Logistica

Come si spostano gli oggetti: nastri e loro tier, curve e unioni, incrocio, router, selezionatore, ponte e nastro rinforzato.

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
| Incrocio (`junction`) | Coda di 6 oggetti per direzione, 26 tick per attraversarlo: circa 13,8 oggetti/s per direzione | 3 Ferro grezzo | dopo il Nastro T1 | Due linee si attraversano senza mescolarsi; prende anche dagli edifici accanto e accetta un oggetto solo se dall'altra parte c'è un blocco che può riceverlo |
| Router (`router`) | 1 oggetto alla volta, 8 tick ciascuno: 7,5 oggetti/s | 3 Ferro grezzo | dopo l'Incrocio | Distribuisce gli oggetti a turno su tutti i lati, mai verso il lato da cui arrivano |
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

## Router

Come il *Router* di Mindustry: non ha direzione (`R` non lo gira) e prende oggetti da ogni lato, da nastri ed edifici, uno alla volta. Dopo 8 tick prova i lati a turno (nord, est, sud, ovest, ripartendo dal lato dopo l'ultimo usato) e passa l'oggetto al primo che lo accetta: un nastro, un altro pezzo di logistica, il CORE, un magazzino, un forno, un assemblatore o un generatore. Non lo rimanda mai al lato da cui è arrivato e un nastro che entra nel router non riceve nulla indietro; se nessun lato accetta, lo tiene e riprova al tick dopo. Una catena di router passa gli oggetti avanti.

## Selezionatore e filtro

Come il *Sorter* di Mindustry: non ha direzione (`R` non lo gira) e passa l'oggetto subito. L'oggetto uguale al filtro va **dritto**, dal lato opposto a quello da cui entra; gli altri escono **a sinistra e a destra alternando** (se un lato non accetta, l'altro; se nessuno accetta, l'oggetto non entra). Senza filtro manda tutto ai lati. Il **selezionatore invertito** fa il contrario: l'oggetto del filtro ai lati, gli altri dritti (senza filtro tutto dritto).

- Col cursore, un **clic** su un selezionatore, un invertito o un estrattore apre sopra il blocco la griglia delle icone degli oggetti e **Nessuno**: la scelta cambia il filtro e chiude il pannello; l'oggetto già scelto, cliccato di nuovo, toglie il filtro. `Esc`, un clic altrove o un altro strumento lo chiudono.
- Il filtro è l'icona al centro del blocco; i blocchi si piazzano senza filtro.

## Ponte

Un ponte ha due estremi 1×1 a una distanza da 2 a 4 celle, come in Mindustry, quindi scavalca da 1 a 3 celle; l'uscita è la prima cella libera in quella direzione. Le celle in mezzo restano libere: un nastro ci può passare sotto. L'ingresso prende oggetti da tre lati (non da quello della campata), li passa subito all'uscita, che li dà sui suoi tre lati liberi (davanti, sinistra, destra).

Il ponte passa sopra pareti e acqua profonda, purché i due estremi stiano su celle costruibili. Smontando un estremo si smonta tutto il ponte.

**Ponte automatico.** Col ponte sbloccato, una retta di nastri trascinata che incontra pareti, acqua profonda o edifici li scavalca da sola con un ponte (fino a 3 celle di ostacolo); un ostacolo più lungo resta rosso. Non succede con una direzione fissata con `R` di lato.

## Nastro rinforzato

Veloce come il Nastro T2, ma come l'*Armored Conveyor* di Mindustry prende oggetti **dagli altri nastri** da ogni lato e **da tutto il resto** (edifici, router, selezionatori, ponti, incroci) **solo da dietro**. Una linea rinforzata passa lungo una fila di minatori o macchine senza raccogliere niente e resta dritta; con gli altri nastri fa curve, T e unioni come al solito. Si riconosce dai binari d'acciaio con bordo e frecce rossi.

![Linea T2 che raccoglie dai minatori e linea rinforzata che resta vuota](https://raw.githubusercontent.com/Juan0177/tIndustry/pr-media/pr-nastri-10/ui-nastro-rinforzato.png)

*Sopra una linea T2 che raccoglie da tre minatori; sotto una linea rinforzata accanto agli stessi blocchi, che resta vuota.*

## Container e vault

Due magazzini che si mettono sui nastri e accanto agli edifici. Il **Contenitore** (`container`, 2×2) tiene 300 per oggetto, il **Deposito** (`vault`, 3×3) 1.000; costano 40 Lastre di ferro e 20 Lastre di piombo il primo, 60 Lastre di titanio e 40 Lastre di ferro il secondo (sblocchi in [Economia e Ricerca](Economia-e-Ricerca#container-e-vault)).

- **A contatto con il CORE** (lato contro lato) allargano il suo magazzino: il loro spazio si somma al massimo di ogni oggetto e i nastri che vi consegnano consegnano al CORE.
- **Lontani dal CORE** sono depositi a sé: accettano qualsiasi oggetto da nastri ed edifici finché c'è posto, e un Estrattore attaccato li svuota (prima dal CORE, se anche lui è a contatto e ha l'oggetto, poi dal deposito con l'id più basso).

## In arrivo

Sono pianificati nastri stretti come in Mindustry, selezionatore che si comporta come in Mindustry; fino ad allora vale quanto scritto qui.

## Vedi anche

- [Comandi](Comandi)
- [Costruzione](Costruzione)
- [Domande frequenti](Domande-frequenti)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Produzione](Produzione) · [Torna alla Home](Home) · [Pagina successiva: Energia →](Energia)

<!-- /navigazione -->
