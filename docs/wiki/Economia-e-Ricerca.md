# Economia e Ricerca

Il CORE e il suo magazzino, il Mercato con i suoi prezzi e l'albero della Ricerca con tutti gli sblocchi e i loro costi.

## Il CORE

Il CORE (2×2) è il cuore della base: tutto quello che vi arriva sui nastri, o da un edificio che lo tocca, entra nel suo **magazzino**. Dal magazzino si pagano costruzioni, sblocchi e potenziamento. Il CORE non vende da solo: per guadagnare si vende al Mercato (o si accende la vendita automatica).

Il pannello in alto a sinistra mostra i **Fondi** (`$`), l'energia, gli oggetti che hai (passa col mouse per il nome) e il pulsante **Potenzia CORE**.

| Potenzia CORE | |
|---|---|
| Costo | $150 e 20 Lastre di ferro |
| Effetto | Livello 1 del CORE. Non cambia più i prezzi: il +25% sulle vendite è stato tolto con il nuovo Mercato; servirà al magazzino e al drone |
| Quante volte | Una |

## Il Mercato

![Mercato](https://raw.githubusercontent.com/Juan0177/tIndustry/main/docs/img/readme/mercato.png)

`M` apre il Mercato, una panoramica: una riga per ogni oggetto che hai nel CORE, con quantità, prezzo del momento con la freccia (su o giù), un mini grafico degli ultimi 2 minuti, i pulsanti **Vendi 1**, **Vendi 100** e **Vendi tutto** e l'interruttore della vendita automatica di quell'oggetto. Non c'è un pulsante che vende tutto di tutti. Se un oggetto è in crollo la riga lo dice. Una spiegazione senza numeri è in [Mercato come una borsa](Mercato-come-una-borsa).

Ogni oggetto ha un mercato suo, come un titolo in borsa, con un **valore base** B che è anche il prezzo minimo: sotto B il prezzo non scende (salvo il crollo, sotto). Tutti i pezzi di una vendita valgono il prezzo del momento del clic.

### Come si muove il prezzo

- **Normale.** Fra B e 1,6×B, con una media di 1,3×B: un passo nuovo ogni 4 secondi, un richiamo verso la media e un piccolo scarto casuale.
- **Picco.** Per ogni oggetto, ogni 10-20 minuti: sale in 10 secondi fino a un massimo fra 2,8×B e 3×B, resta in cima 10 secondi e scende in 30. Da 2,8×B si dice che la vendita è «nel picco».
- **Vendite.** Ogni pezzo venduto abbassa il prezzo dello 0,04% di B, mai sotto B. In 2 minuti il prezzo torna al suo andamento.
- **Crollo.** Se in 2 minuti vendi 5.000 pezzi o più di un oggetto, quell'oggetto crolla per 20 minuti: prezzo vicino a B, nei primi 5 minuti a volte 0,6-0,7×B per 10 secondi, e niente picchi. Gli altri oggetti non cambiano.
- **Vendita automatica.** Un interruttore per oggetto. Vende al **prezzo minimo** (B, o il prezzo del momento se è sotto B in un crollo) tutto ciò che arriva al CORE, anche da nastri ed edifici attaccati. Non tocca il mercato: niente calo, niente crollo. Così il magazzino non cresce.
- **Jackpot.** Con **Vendi tutto**, nel picco, con almeno 400 pezzi e con uno schermo di borsa, una vendita può valere circa dieci volte (probabilità dal 2% al 50% secondo i pezzi, con una «rete di sicurezza» che garantisce la vittoria dopo tre tentativi grossi a vuoto). Lo schermo di borsa arriva con la specifica 2: oggi nel gioco il jackpot non scatta; vedi [Mercato come una borsa](Mercato-come-una-borsa).

### Valori base

| Oggetto | B | Normale (B - 1,6×B) | Picco massimo (3×B) |
|---|---|---|---|
| Ferro grezzo (`iron-ore`) | $6 | $6 - $9,6 | $18 |
| Lastra di ferro (`iron-plate`) | $23 | $23 - $36,8 | $69 |
| Rame grezzo (`copper-ore`) | $5 | $5 - $8 | $15 |
| Filo di rame (`copper-wire`) | $12 | $12 - $19,2 | $36 |
| Carbone (`coal`) | $3 | $3 - $4,8 | $9 |
| Piombo grezzo (`lead-ore`) | $4 | $4 - $6,4 | $12 |
| Lastra di piombo (`lead-plate`) | $14 | $14 - $22,4 | $42 |
| Titanio grezzo (`titanium-ore`) | $14 | $14 - $22,4 | $42 |
| Lastra di titanio (`titanium-plate`) | $52 | $52 - $83,2 | $156 |
| Grafite (`graphite`) | $12 | $12 - $19,2 | $36 |
| Silicio (`silicon`) | $32 | $32 - $51,2 | $96 |

Il prezzo è sempre un numero intero di dollari quando paga: gli oggetti da pochi dollari cambiano prezzo in dollari di rado (il carbone solo una decina di volte in un quarto d'ora), quelli cari di continuo. Trasformare conviene: una Lastra di ferro vale $23, i due Ferro grezzo che servono per farla $12.

## La Ricerca

![Albero della Ricerca](https://raw.githubusercontent.com/Juan0177/tIndustry/main/docs/img/readme/ricerca.png)

`T` apre la Ricerca, un albero come in Mindustry con il CORE in cima e ogni blocco sotto il suo prerequisito. Bordo dorato: puoi sbloccarlo ora. Lucchetto: prima sblocca il precedente. Passa col mouse per costo e mancanze, fai clic per il pannello di sblocco e **Conferma sblocco**. Trascina per muoverti, rotellina per lo zoom, `H` per la vista intera.

Sbloccare costa **soldi e materiali**, presi dal CORE; poi il blocco compare nella palette e si costruisce pagando il suo costo di costruzione (vedi [Produzione](Produzione) e [Logistica](Logistica)).

| Blocco | Ramo | Prerequisito | Costo di sblocco |
|---|---|---|---|
| Nastro T1 (`conveyor-basic`) | Logistica | — | iniziale |
| Incrocio (`junction`) | Logistica | Nastro T1 | $200, 13 Ferro grezzo |
| Sdoppiatore (`splitter`) | Logistica | Incrocio | $280, 13 Ferro grezzo |
| Selezionatore (`sorter`) | Logistica | Incrocio | $320, 10 Ferro grezzo, 10 Piombo grezzo |
| Nastro ponte (`conveyor-bridge`) | Logistica | Incrocio | $340, 21 Ferro grezzo, 21 Piombo grezzo |
| Nastro T2 (`conveyor-fast`) | Logistica | Nastro T1 | $450, 8 Ferro grezzo, 8 Piombo grezzo, 8 Titanio grezzo |
| Nastro rinforzato (`conveyor-armored`) | Logistica | Nastro T2 | $600, 8 Ferro grezzo, 8 Piombo grezzo, 10 Titanio grezzo, 8 Grafite |
| Nastro T3 (`conveyor-express`) | Logistica | Nastro T2 | $850, 8 Titanio grezzo, 8 Silicio, 8 Grafite |
| Minatore T1 (`miner`) | Produzione | — | iniziale |
| Estrattore (`extractor`) | Produzione | Minatore T1 | $80, 6 Lastre di ferro |
| Forno (`smelter`) | Produzione | Minatore T1 | $250, 15 Lastre di ferro |
| Assemblatore (`assembler`) | Produzione | Forno | $480, 22 Lastre di ferro, 4 Rame grezzo |
| Minatore T2 (`miner-advanced`) | Produzione | Minatore T1 | $550, 40 Lastre di ferro, 8 Fili di rame |
| Generatore (`generator`) | Energia | — | $280, 12 Lastre di ferro |
| Nodo T1 (`power-node`) | Energia | Generatore | $140, 4 Lastre di ferro, 2 Fili di rame |
| Lampada (`lamp`) | Energia | Nodo T1 | $90, 4 Lastre di ferro, 2 Fili di rame |
| Nodo T2 (`power-node-t2`) | Energia | Nodo T1 | $320, 12 Lastre di ferro, 6 Fili di rame |

Gli sblocchi valgono per la partita in corso e si salvano con lei. Ogni livello della campagna riparte con i soli Nastro T1 e Minatore T1.

## Vedi anche

- [Risorse e oggetti](Risorse-e-oggetti)
- [Campagna e mappe](Campagna-e-mappe)
- [Costruzione](Costruzione)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Costruzione](Costruzione) · [Torna alla Home](Home) · [Pagina successiva: Campagna e mappe →](Campagna-e-mappe)

<!-- /navigazione -->
