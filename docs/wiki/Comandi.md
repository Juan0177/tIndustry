# Comandi

Tutti i controlli di mouse e tastiera: scegliere un blocco, piazzarlo, girarlo, smontarlo, muovere la visuale e aprire i pannelli.

![Anteprima di una linea di nastri durante il trascinamento](https://raw.githubusercontent.com/Juan0177/tIndustry/pr-media/pr-nastri-8/ui-nastri-anteprima.png)

## Scegliere un blocco

La palette sta in basso a destra: a destra le categorie (Strumenti, Logistica, Produzione, Potenza), a sinistra una griglia di icone. Mostra solo i blocchi già sbloccati nella [Ricerca](Economia-e-Ricerca); passando sopra un'icona compaiono nome, tasto rapido e costi (barrati se mancano), e `?` apre il dettaglio.

| Tasto | Blocco |
|---|---|
| `1` (o `N`) | Nastro T1 |
| `2` | Minatore T1 |
| `3` (o `F`) | Forno |
| `4` | Assemblatore |
| `5` (o `J`) | Incrocio |
| `6` | Sdoppiatore |
| `7` | Generatore |
| `8` | Selezionatore |
| `9` | Nastro ponte |
| `Esc` o `` ` `` | Torna al cursore (nessun blocco in mano) |

Gli altri blocchi (Nastro T2, Nastro T3, Nastro rinforzato, Minatore T2, Estrattore, Nodi, Lampada) si scelgono dalla palette. Un tasto di un blocco non ancora sbloccato dà un avviso.

## Piazzare

| Azione | Effetto |
|---|---|
| Clic sinistro | Pianifica il blocco nella cella; il drone lo costruisce (vedi [Costruzione](Costruzione)) |
| Trascina col sinistro | Mostra l'anteprima dall'ancora al cursore; i piani partono tutti al **rilascio** |
| `R` | Ruota il blocco in mano |
| Rotellina, con un blocco orientabile in mano | Ruota come `R` (su in senso orario, giù al contrario) |
| `R` o rotellina **durante** il trascinamento | Fissa quella direzione per tutta la linea (una fila "di lato") |
| `Ctrl` durante il trascinamento (nastri) | Segue la catena di nastri esistente o fa un percorso a scalini attorno agli ostacoli |
| `Esc` o destro durante il trascinamento | Annulla la linea (il blocco resta in mano) |
| `C` | Cambia il filtro del Selezionatore; col mouse sopra un selezionatore cambia anche il suo |

Sono orientabili: nastri, sdoppiatore, selezionatore, ponte, estrattore, assemblatore.

**Come si comporta il trascinamento.** Nastri, incroci, sdoppiatori e selezionatori fanno una retta lungo l'asse più lungo fra orizzontale e verticale; ogni altro blocco è uno solo, sull'ancora, girato verso il cursore. Ogni nastro guarda il successivo; l'ultimo prende la direzione del nastro su cui finisce e, accanto al CORE, guarda il CORE. Le sagome rosse sono celle rifiutate: non partono. Anche un pannello che si apre, la finestra che perde il fuoco o un rilascio fuori dalla finestra annullano la linea senza piazzare nulla.

## Smontare e annullare

| Azione | Effetto |
|---|---|
| Clic destro su un piano | Lo cancella |
| Clic destro su un cantiere | Lo annulla: il drone restituisce quanto speso |
| Clic destro su un blocco o un nastro | Lo segna in rosso per lo smontaggio; un secondo clic toglie il segno |
| Trascina col destro | Rettangolo rosso (al massimo 64 celle per lato): al rilascio segna tutti i blocchi dentro e cancella piani e cantieri; non toglie mai un segno |

## Prendere un blocco dalla mappa

Un clic col **tasto centrale** senza muovere il mouse prende in mano il blocco sotto il cursore, con la sua direzione (e, per il selezionatore, il suo filtro). Un blocco non ancora sbloccato dà un avviso.

## Visuale

| Comando | Effetto |
|---|---|
| `W` `A` `S` `D` | Sposta la visuale |
| Tasto centrale trascinato | Sposta la visuale |
| Trascina col sinistro, con il cursore | Sposta la visuale |
| Rotellina (con il cursore, con un blocco senza direzione, o con `Ctrl`) | Zoom |
| `H` (o `Home`) | Torna al CORE |
| Clic sul riquadro della coda (in alto a sinistra) | Porta la visuale sul cantiere in lavorazione |

## Pannelli e partita

| Tasto | Effetto |
|---|---|
| `T` | Ricerca (dentro: trascina per muoverti, rotellina per lo zoom, `H` vista intera, frecce per passare da un blocco all'altro, `T` o `Esc` chiude) |
| `M` | Mercato (`M` o `Esc` chiude) |
| `G` | Campagna, scelta del livello (`G` o `Esc` chiude) |
| `I` | Impostazioni |
| `Esc` con il cursore | Salva la Continua e apre la Home; la partita si ferma finché non premi **Riprendi** (o di nuovo `Esc`) |
| `F5` / `F9` | Salva / carica la Continua |
| `F6` / `F7` | Salva / carica lo slot rapido `slot-1` |

Non c'è un tasto di pausa: la partita si ferma solo mentre la Home è aperta. Ricerca, Mercato e Campagna si aprono anche dai pulsanti in alto a sinistra (**Menu · Ricerca · Mercato · Campagna**).

**Tutorial**: `Invio` passa al passo successivo, `Backspace` lo salta.
**Gestione salvataggi**: frecce su e giù per scegliere, `Invio` per caricare.

## Vedi anche

- [Costruzione](Costruzione)
- [Logistica](Logistica)
- [Primi passi](Primi-passi)
