# Logistica come Mindustry

- **Stato**: attuata (2026-11-03; [diario](../../diario/2026-11-03-logistica-mindustry.md)). Approvata dall'utente il 3 novembre 2026. Issue [#252](https://github.com/Juan0177/tIndustry/issues/252), [#253](https://github.com/Juan0177/tIndustry/issues/253), [#254](https://github.com/Juan0177/tIndustry/issues/254), [#255](https://github.com/Juan0177/tIndustry/issues/255), [#270](https://github.com/Juan0177/tIndustry/issues/270).
- **Sostituisce**: incrocio a capacità 2, sdoppiatore orientato (`splitter`), selezionatore orientato col filtro predefinito, estrattore orientato che preleva solo dal CORE con le lastre di ferro predefinite, sprite dei nastri larghi.

## Obiettivo

Incrocio, router, selezionatori ed estrattore si comportano e appaiono come in Mindustry: nessuno ha più una direzione, i filtri si scelgono col clic sul blocco piazzato, e i nastri prendono la forma stretta (variante C) scelta dall'utente.

Successo: due flussi si incrociano senza bloccarsi; un router distribuisce a turno senza rimandare indietro; un selezionatore smista per filtro e alterna i lati; un estrattore svuota un forno o il CORE verso più blocchi; nessun oggetto si perde o si duplica; tutto deterministico.

## Decisioni prese con l'utente

| Tema | Decisione |
|---|---|
| Selezionatore invertito | **Sì**, blocco a parte (`sorter-inverted`), sbloccato in Ricerca dopo il selezionatore. |
| Estrattore | **Come l'Unloader**: senza direzione, prende dai blocchi attaccati, mette a turno in quelli che accettano, mai indietro da dove ha preso. |
| Numeri | Come Mindustry: incrocio coda 6 per direzione e 26 tick di attraversamento; router 1 oggetto e 8 tick; selezionatori istantanei; estrattore 11 oggetti al secondo. |
| Senza filtro | **Come Mindustry**: si piazzano senza filtro; estrattore senza filtro = qualsiasi oggetto a turno; selezionatore senza filtro = tutto ai lati, invertito = tutto dritto. |
| Router | L'id `splitter` diventa `router` (decisione del 20 ottobre). Con la regola dei salvataggi (versioni vecchie rifiutate) non serve nessuna conversione. |
| Nastri stretti | La **variante C** (#252) entra negli sprite di tutti i tier e pezzi. |

## Regole

Nessuno di questi blocchi ha una direzione: si piazzano senza ruotarli (`R` non ha effetto) e una linea trascinata li piazza come oggi.

### Incrocio (`junction`)

- Un oggetto che entra da un lato esce dal lato opposto.
- Una coda per ciascuna delle quattro direzioni d'ingresso, **6 oggetti** ciascuna; un oggetto la attraversa in **26 tick**, poi esce appena il blocco dall'altra parte lo accetta. Le code non si bloccano fra loro.
- Accetta da nastri, ponti, edifici e da un altro incrocio.

### Router (`router`)

- Accetta da ogni lato (nastri ed edifici), **1 oggetto** alla volta.
- Dopo **8 tick** prova i lati in ordine a turno (nord, est, sud, ovest, ripartendo dal lato dopo l'ultimo usato) e passa l'oggetto al primo che lo accetta, **mai al lato da cui è arrivato**. Se nessuno accetta, lo tiene e riprova al tick dopo.
- Una catena di router passa gli oggetti avanti (e anche indietro fra router, come Mindustry: il divieto vale solo verso il blocco da cui l'oggetto è arrivato).

### Selezionatore (`sorter`) e invertito (`sorter-inverted`)

- Passaggio **istantaneo**, senza coda: l'oggetto entra solo se nello stesso tick può uscire.
- Selezionatore: l'oggetto uguale al filtro prosegue **dritto** (lato opposto all'ingresso); gli altri escono **a sinistra o a destra**, alternando; se un lato non accetta usano l'altro; se nessuno accetta non entra.
- Invertito: al contrario (uguale al filtro ai lati, gli altri dritti).
- **Senza filtro**: il selezionatore manda tutto ai lati, l'invertito tutto dritto.
- Due selezionatori attaccati non si passano l'oggetto (un selezionatore non accetta da un selezionatore), come Mindustry.

### Estrattore (`extractor`, come l'Unloader)

- **11 oggetti al secondo** (un oggetto ogni 60/11 tick, con resto intero portato avanti).
- Prende l'oggetto del filtro (o, senza filtro, il prossimo oggetto a turno fra quelli disponibili, in ordine di id oggetto) da un blocco attaccato che ne ha: CORE (anche tramite magazzini collegati), magazzini a sé, uscite di forni e assemblatori. A parità, i blocchi sorgente a turno.
- Lo mette nel prossimo blocco attaccato che lo accetta, a turno, **mai nello stesso blocco da cui l'ha preso**. Se nessuno accetta, non preleva.
- Non sposta mai da un magazzino (CORE o deposito) a un altro magazzino: niente andata e ritorno fra magazzini; dalle uscite delle macchine al CORE o a un deposito sì.
- Non preleva dai nastri né da altri estrattori.

### Configurazione col clic

- Con lo strumento cursore, un clic su un selezionatore, un invertito o un estrattore apre un pannello con la **griglia delle icone** degli oggetti e la casella **«nessuno»**; la scelta manda un comando di configurazione.
- Un comando unico `SetBlockFilter(PlayerId, Tile, ItemId?)` sostituisce `SetSorterFilter`; rifiutato su blocchi senza filtro o con un oggetto sconosciuto.
- Il filtro scelto si vede sul blocco (icona dell'oggetto al centro, come Mindustry).

## Nastri stretti (#252)

La variante C di `tools/art/proposte_nastri_stretti.py` (battistrada 12 px, piastre laterali 4 px, frecce piccole, a 32 px) passa in `tools/art/blocchi/logistica.py` per i quattro tier (base, veloce, espresso, rinforzato) e per tutti i pezzi (dritto, curva, T, unioni laterali, unioni con edifici). Incrocio, router, selezionatori ed estrattore hanno sprite nuovi senza frecce, coerenti col nastro stretto. Solo da `python tools/art/genera.py`.

## Simulazione

- `BeltGrid`: incrocio con quattro code, router e selezionatori senza direzione con i loro turni (`RouterTurn`, `SorterToggle`) salvati; il router sostituisce `LogisticsKind.Splitter`.
- `Extractor` riscritto: senza direzione, sorgenti e destinazioni attaccate a turno, accumulatore intero del ritmo.
- `SetBlockFilter` al posto di `SetSorterFilter`; filtri nullable.
- Contenuti: `router` al posto di `splitter` (Ricerca e costi dello sdoppiatore), `sorter-inverted` nuovo (Ricerca: dopo `sorter`, costi come il selezionatore).
- Agenti: `piazza` accetta il filtro opzionale; comando `filtro` (tile, oggetto o nessuno).
- Salvataggi **v18**: turni, code dell'incrocio, filtri nullable, accumulatore dell'estrattore; i v17 non si aprono.

## Client

- Pannello del filtro (tema `UiTheme`, testi da `testi.csv`), aperto dal clic con lo strumento cursore.
- Palette: router, selezionatore, invertito, estrattore senza rotazione; via lo sdoppiatore.
- Sprite nuovi e icona del filtro sui blocchi.
- Testi corretti (filtro col clic, non «F»).

## Test

- Incrocio: due flussi incrociati a regime senza blocchi, coda piena a 6, 26 tick, ingresso da un edificio.
- Router: turno fra i lati, mai indietro, catena di router, oggetto trattenuto quando tutto è pieno.
- Selezionatori: filtro dritto, alternanza dei lati, lato bloccato, nessun lato libero, senza filtro, invertito, due selezionatori attaccati.
- Estrattore: 11 al secondo, dal CORE, da un forno a un altro forno, a turno fra più destinazioni, mai indietro, senza filtro a turno, niente prelievo senza destinazione.
- Conservazione: nessun oggetto perso o duplicato in tutte le configurazioni.
- `SetBlockFilter` e rifiuti; salvataggio v18 avanti e indietro; v17 rifiutato; determinismo.

## Fuori ambito

Overflow e underflow gate, ponti di Mindustry diversi da quello di oggi, il drone comandato dal giocatore e lo strumento cursore completo come Mindustry (specifica a parte).
