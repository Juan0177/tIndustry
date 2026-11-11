# Sostituzioni come Mindustry, percorso dei nastri e nastri simmetrici

- **Stato**: chiesta dall'utente l'11 novembre 2026 (messaggio con la cattura dei nastri), scritta lo stesso giorno; da fare dopo la Ricerca allargata, piano 3, e prima della campagna rifatta. Aspetti 2 «Costruzione», 3 «Logistica» e 9 «Grafica» di `docs/STATO.md`.

## Richiesta dell'utente

1. I colori dei nastri devono essere simmetrici: girando a destra i colori non combaciano.
2. Potenziare deve essere più semplice:
   - posso piazzare il Minatore T2 sopra il T1;
   - posso piazzare qualsiasi tipo di nastro sopra un altro per sostituirlo subito, come in Mindustry;
   - un doppio clic su un nastro seleziona tutto il percorso, dall'inizio alla fine. La selezione prende solo i nastri, non incroci, router o altre strutture. Compare un pannellino per sostituire tutto il percorso con un clic.

## Decisioni

| Tema | Decisione |
|---|---|
| Nastri simmetrici | **I due bordi del nastro hanno la stessa luce.** Le piastre sono uguali e specchiate, e i rivetti del nastro rinforzato stanno su tutti e due i lati. Così rotazioni e specchiature (curve a destra) non cambiano i colori. Vale per tutti i tier e per i pezzi a T e di unione. L'incrocio resta com'è. |
| Logistica sopra logistica | **Un blocco logistico 1×1 sostituisce qualunque altro blocco logistico 1×1** piazzandolo sopra, come in Mindustry. Vale per i nastri di ogni tier (rinforzato e a pacchi compresi), l'incrocio, il router, i selezionatori e gli overflow e underflow gate. Il ponte e il ponte lungo restano esclusi, perché sono fatti di due celle. Si paga la differenza, come oggi fra i nastri. Gli oggetti che ci stanno restano nel blocco nuovo, gli altri vanno al CORE. |
| Edifici sopra edifici | **Un edificio sostituisce gli edifici della sua famiglia che stanno tutti dentro la sua impronta.** Le altre celle dell'impronta devono essere libere. Le famiglie sono quattro: minatori (T1, T2, trapano a impatto), forni (forno, fonderia avanzata), generatori (generatore, generatore a combustione) e batterie (batteria, batteria grande). Per esempio il T2 sopra il T1 nella stessa impronta, oppure il trapano 3×3 sopra un minatore 2×2 che sta tutto dentro. Si paga la differenza col credito dei blocchi tolti. I laser dei nodi verso i blocchi tolti si perdono. |
| Subito | **Come Mindustry**: la sostituzione è un cantiere che il drone costruisce senza bisogno di smontare prima. Il blocco vecchio lavora finché il cantiere non parte. Con «costruzione istantanea» (test e dev) avviene subito. |
| Stesso blocco | Piazzare lo stesso blocco sopra non fa niente. Un nastro dello stesso tier ma girato lo gira, come oggi. |
| Doppio clic | **Doppio clic su un nastro** (anche rinforzato o a pacchi): seleziona il percorso di nastri dalla prima all'ultima cella. In avanti segue la direzione finché la cella dopo è un nastro. All'indietro risale finché un solo nastro porta nella cella. La selezione si ferma a incroci, router, selezionatori, gate, ponti, edifici e CORE. |
| Pannellino | Accanto al percorso compare un pannello con un pulsante per ogni nastro sbloccato (icona, nome, costo della differenza) e il numero di celle. Un clic manda la sostituzione di tutte le celle, ognuna con la sua direzione. Esc o un clic altrove lo chiude. Le celle già di quel tier si saltano. |

## Simulazione e salvataggi

- La sostituzione si generalizza: il cantiere ricorda **uno o più blocchi tolti**, ognuno con la sua descrizione, i suoi oggetti e lo stato che serve, per esempio il filtro del selezionatore.
  - Se il cantiere si annulla, i blocchi tornano com'erano.
  - Se il cantiere finisce, il blocco nuovo prende quello che ci sta e il resto va al CORE.
- **Salvataggi alla v23**, perché la sostituzione salvata cambia forma.
- Agli agenti: `piazza` sopra un blocco sostituibile sostituisce. Il risultato dice cosa è stato sostituito.
- Determinismo come sempre.

## Client

- Il fantasma del piazzamento mostra la sostituzione (verde) sopra i blocchi sostituibili.
- Il doppio clic seleziona il percorso, lo evidenzia e apre il pannellino. Testi in `testi.csv` (it e en), stili dal tema.

## Test

- Arte: le due piastre del nastro sono speculari in ogni sprite di nastro, curva, T e unione.
- Ogni coppia della tabella:
  - nastro→router e router→nastro, con gli oggetti in eccesso al CORE;
  - selezionatore→gate;
  - T1→T2, T2→trapano, forno→fonderia avanzata, generatore→combustione, batteria→batteria grande;
  - costo pari alla differenza;
  - annullamento che rimette il vecchio;
  - edifici che escono dall'impronta: rifiutato;
  - famiglia diversa: rifiutato;
  - salvataggio a metà cantiere.
- Percorso: dritto, con curve, che si ferma a incroci e router, ad anello (nessun giro infinito) e con due nastri che entrano nella stessa cella.

## Fuori ambito

Ponti, schermo di borsa, CORE (il CORE ha già i suoi livelli), contenitore e deposito, macchine di famiglie diverse.
