# Costruzione

Come nascono e spariscono i blocchi, come in Mindustry: piani, drone, cantieri, costi presi man mano e rimborsi allo smontaggio.

![Rettangolo di smontaggio col tasto destro](https://raw.githubusercontent.com/Juan0177/tIndustry/pr-media/pr-nastri-9/ui-smontaggio-rettangolo.png)

## Piani

Piazzare un blocco non lo crea subito: diventa un **piano**, una sagoma semitrasparente col contorno giallo. I piani si mettono in coda e le risorse **non** servono per pianificare: servono dopo, mentre il drone costruisce. Un piano viene rifiutato solo se il blocco non è sbloccato o la cella non è libera o non è costruibile.

| Terreno | Si costruisce? |
|---|---|
| Suolo normale | Sì |
| Acqua bassa (vicino alle rive) | Sì |
| Acqua profonda (blu scuro) | No ("Acqua profonda"); il ponte la scavalca |
| Parete | No ("C'è una parete") |
| CORE | No ("Non puoi costruire sul CORE") |

L'anteprima rifiutata è rossa con una X e in basso compare il motivo.

## Il drone

Il drone parte dal CORE e vola verso il primo piano della coda, sopra pareti e acqua. Vola a 22,5 celle al secondo e ha un raggio di costruzione di 27,5 celle: non deve stare sopra il piano, gli basta arrivare abbastanza vicino. Quando arriva apre un **cantiere**: lo sprite si disegna dal basso mentre il raggio parte dal drone. Con la coda vuota torna accanto al CORE.

Se quando arriva il piano non si può più costruire (cella occupata, blocco non più sbloccato) lo scarta e un avviso dice il motivo.

## Costi presi man mano

Il costo esce dal magazzino del CORE un pezzo alla volta, mentre il cantiere avanza. Se manca un materiale il cantiere si **ferma** esattamente lì e, passando col mouse, dice cosa manca ("Mancano: 3 Ferro grezzo"). Riparte da solo quando il materiale arriva.

Se un cantiere resta fermo per un secondo e in coda ci sono altri piani, il drone lo lascia com'è (con quanto ha già speso) e passa avanti: il piano va in fondo alla coda e il cantiere riprende quando ritocca a lui.

## Coda nell'HUD

In alto a sinistra, sotto le risorse del CORE, un riquadro mostra il blocco in lavorazione con la sua barra (rossa se si smonta, ambra con "Manca: …" se è fermo per le risorse) e "+N" piani in attesa. Un clic porta la visuale sul cantiere. Sparisce quando la coda è vuota.

## Tempi di costruzione

Come in Mindustry il tempo dipende dal costo: *(20 + somma di quantità × peso del materiale) ÷ 0,5* passi di simulazione, che sono 60 al secondo. Pesi: Ferro grezzo 0,5, Rame grezzo 0,5, Filo di rame 0,5, Lastra di ferro 0,7, Piombo grezzo 0,7, Silicio 0,8, Titanio grezzo 1, Grafite 1.

| Blocco | Tempo con il drone |
|---|---|
| Nastro T1 | 0,68 s |
| Nastro T2 | 0,74 s |
| Nastro T3 | 0,76 s |
| Nastro rinforzato | 0,81 s |
| Incrocio, Sdoppiatore | 0,72 s |
| Selezionatore | 0,75 s |
| Nastro ponte (due estremi) | 1,15 s |
| Minatore T1 | 0,73 s |
| Minatore T2 | 0,97 s |
| Forno | 0,77 s |
| Assemblatore | 0,89 s |
| Estrattore | 0,71 s |
| Generatore | 0,85 s |
| Nodo T1 | 0,70 s |
| Nodo T2 | 0,87 s |
| Lampada | 0,73 s |

A questo si aggiunge il volo del drone fino al piano. Lo smontaggio va alla stessa velocità.

## Smontare

Col **tasto destro** (vedi [Comandi](Comandi)):

| Su cosa | Effetto |
|---|---|
| Un piano non ancora iniziato | Lo cancella |
| Un cantiere in costruzione | Lo annulla: il drone lo smonta e restituisce man mano **tutto** quello già speso |
| Un blocco o un nastro | Lo segna in rosso: il drone lo smonta e rimborsa man mano; gli oggetti dentro vanno persi |
| Un blocco già segnato | Toglie il segno (solo con un clic singolo) |

Il rettangolo del destro segna tutti i blocchi che tocca, cancella i piani e annulla i cantieri, una volta per blocco, ma non toglie mai un segno.

## Rimborsi

| Partita | Rimborso allo smontaggio |
|---|---|
| Partita libera | 100% |
| Campagna, livelli 1–3 | 100% |
| Campagna, livelli 4–10 | 50% |
| Cantiere annullato (sempre) | 100% di quanto speso |

Il rimborso si calcola per ogni materiale e si arrotonda per eccesso: al 50% un Nastro T1, che costa 1 Ferro grezzo, restituisce 1 Ferro grezzo.

## Casi particolari

- **Nastro accanto al CORE**: l'ultimo nastro di una linea (o di un clic) accanto al CORE è pianificato già rivolto verso il CORE.
- **Nastro sopra nastro**: girarlo è gratis e immediato; cambiare tier apre un cantiere di sostituzione dove paghi solo la differenza (vedi [Logistica](Logistica)).
- **Incrocio automatico**: il drone smonta il nastro e poi costruisce l'incrocio.

## Vedi anche

- [Comandi](Comandi)
- [Logistica](Logistica)
- [Economia e Ricerca](Economia-e-Ricerca)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Energia](Energia) · [Torna alla Home](Home) · [Pagina successiva: Economia e Ricerca →](Economia-e-Ricerca)

<!-- /navigazione -->
