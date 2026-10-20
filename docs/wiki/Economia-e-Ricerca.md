# Economia e Ricerca

Il CORE e il suo magazzino, il Mercato con i suoi prezzi e l'albero della Ricerca con tutti gli sblocchi e i loro costi.

## Il CORE

Il CORE (2×2) è il cuore della base: tutto quello che vi arriva sui nastri, o da un edificio che lo tocca, entra nel suo **magazzino**. Dal magazzino si pagano costruzioni, sblocchi e potenziamento. Il CORE non vende da solo: per guadagnare si vende al Mercato (o si accende la vendita automatica).

Il pannello in alto a sinistra mostra i **Fondi** (`$`), l'energia, gli oggetti che hai (passa col mouse per il nome) e il pulsante **Potenzia CORE**.

| Potenzia CORE | |
|---|---|
| Costo | $150 e 20 Lastre di ferro |
| Effetto | +25% su ogni vendita (CORE LV1) |
| Quante volte | Una |

## Il Mercato

![Mercato](https://raw.githubusercontent.com/Juan0177/tIndustry/main/docs/img/readme/mercato.png)

`M` apre il Mercato: per ogni oggetto nel CORE vedi quantità, prezzo e valore, con i pulsanti **Vendi 1** e **Vendi tutto**. **Vendi tutto** vende ogni pezzo al prezzo del momento. In alto il totale venduto finora.

**Vendita automatica al CORE**: se è accesa, tutto quello che arriva al CORE si vende subito invece di entrare in magazzino. Comoda per fare soldi, ma così il magazzino non cresce e non hai materiali per costruire.

### Prezzi

Il prezzo scende quando nel CORE hai molti pezzi dello stesso oggetto. Con 0 o 1 pezzo vale il **listino**; con di più scende verso il **minimo**, tanto più in fretta quanto più bassa è la **soglia**:

*prezzo = listino − (listino − minimo) × s ÷ (soglia + s)*, con *s* = pezzi nel CORE meno 1, arrotondato.

Per esempio, con 21 Lastre di ferro nel CORE una lastra vale $21 invece di $30. Con il CORE potenziato si aggiunge il 25%.

| Oggetto | Listino | Minimo | Soglia |
|---|---|---|---|
| Lastra di titanio (`titanium-plate`) | $45 | $18 | 12 |
| Lastra di ferro (`iron-plate`) | $30 | $12 | 20 |
| Silicio (`silicon`) | $28 | $11 | 16 |
| Lastra di piombo (`lead-plate`) | $18 | $7 | 18 |
| Filo di rame (`copper-wire`) | $16 | $6 | 20 |
| Titanio grezzo (`titanium-ore`) | $12 | $5 | 16 |
| Grafite (`graphite`) | $10 | $4 | 20 |
| Ferro grezzo (`iron-ore`) | $8 | $3 | 24 |
| Rame grezzo (`copper-ore`) | $6 | $2 | 24 |
| Piombo grezzo (`lead-ore`) | $5 | $2 | 24 |
| Carbone (`coal`) | $4 | $1 | 30 |

Una lastra di ferro vale $30, i due Ferro grezzo che servono per farla $16: trasformare conviene.

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
