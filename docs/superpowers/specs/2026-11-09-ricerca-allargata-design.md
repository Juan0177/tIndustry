# Ricerca allargata: blocchi, oggetti e potenziamenti nuovi

- **Stato**: approvata dall'utente il 9 novembre 2026; piani da scrivere (i lavori non sono ancora iniziati). Va implementata **prima** della [campagna rifatta](2026-11-09-campagna-rifatta-design.md), che usa i suoi premi.
- **Aspetti** di `docs/STATO.md`: 3 Logistica, 4 Produzione, 5 Energia, 11 Drone e tier, 8 Campagna (premi).
- **Tre piani in fila**, ognuno con la sua PR: (1) oggetti, ricette e macchine; (2) logistica ed energia; (3) potenziamenti, premi della campagna e bilanciamento.

## Obiettivo

Più cose da costruire e da ricercare dopo il CORE Fondazione, come in Mindustry: smistamento senza selezionatori, macchine più grandi, energia dal sole e dalla combustione, oggetti nuovi fino ai circuiti, potenziamenti del drone e della produzione. Quattro nodi sono il premio dei capitoli della campagna; nella partita libera si comprano a caro prezzo.

Successo: ogni blocco nuovo si costruisce e lavora come descritto; i nodi nuovi stanno nell'albero della Ricerca con costi dal foglio «Bilanciamento», e la partita libera resta di circa 6 ore per sbloccare tutto senza jackpot.

## Decisioni prese con l'utente

| Tema | Decisione |
|---|---|
| Blocchi | **Logistica Mindustry, macchine nuove, energia nuova**. Niente scivolo di vendita né secondo schermo. |
| Oggetti | **Sabbia e vetro, circuiti, plastica**. Niente leghe. |
| Vapore | Senza liquidi (aspetto 12) niente generatore a vapore: al suo posto un **generatore a combustione grande**. Il vapore arriverà con i liquidi. |
| Potenziamenti | **Drone, produzione, magazzino**. Niente potenziamenti del Mercato. |
| Solo campagna | **Un premio per capitolo**; nella partita libera si comprano in Ricerca a un prezzo molto alto. |

## Oggetti e ricette (piano 1)

| Oggetto | Come si ottiene | Valore B (proposta) |
|---|---|---|
| Sabbia (`sand`) | Minatore T1 o T2 su una cella di sabbia del bioma (il pavimento, come in Mindustry); niente giacimenti nuovi | 2 |
| Vetro (`glass`) | Forno: 2 sabbia → 1 vetro in 2 s | 9 |
| Plastica (`plastic`) | Plastificatore: 2 carbone → 1 plastica in 2 s, corrente 6/s | 20 |
| Circuito (`circuit`) | Assemblatore: 1 filo di rame + 1 silicio + 1 lastra di piombo → 1 circuito in 3 s | 90 |

- La sabbia viene dal **pavimento**: il terreno e i generatori di mappa pubblicati (`classico`, `biomi-1`) **non cambiano**; un minatore su sabbia dà sabbia se sotto non c'è un giacimento. Il generatore `classico` (dei test e della partita libera senza biomi) non ha sabbia: la sabbia esiste solo sulle mappe a biomi e nella campagna.
- I valori B si aggiungono a `market` in `content.json` con le regole della specifica 1 (B ≈ costo degli ingredienti con un margine; ×1,5 per gli oggetti di fine partita: circuiti).

## Macchine (piano 1)

| Blocco | Misura | Cosa fa | Corrente |
|---|---|---|---|
| Fonderia avanzata (`smelter-advanced`) | 2×2 | Le ricette del forno al doppio della velocità; senza corrente è ferma (non brucia carbone) | 20/s |
| Pressa grande (`graphite-press`) | 2×2 | 3 carbone → 2 grafite in 1,5 s | — |
| Separatore (`separator`) | 2×2 | 2 sabbia → 1 pezzo a caso fra ferro, rame, piombo grezzo (30% ciascuno) e titanio grezzo (10%), in 2 s; caso dal generatore seminato della partita | 8/s |
| Trapano a impatto (`impact-drill`) | 3×3 | Scava ogni giacimento sotto di sé, titanio compreso, 4 volte il Minatore T2 | 30/s |
| Plastificatore (`plasticizer`) | 2×2 | Ricetta della plastica | 6/s |

## Logistica (piano 2)

| Blocco | Cosa fa (come Mindustry) |
|---|---|
| Overflow gate (`overflow-gate`), 1×1, senza direzione | L'oggetto prosegue dritto; se davanti non accetta, va a sinistra o a destra (alternando); se nessuno accetta, aspetta. |
| Underflow gate (`underflow-gate`), 1×1, senza direzione | Al contrario: prima ai lati (alternando), dritto solo se i lati non accettano. |
| Nastro a pacchi (`conveyor-stack`) | Raccoglie fino a 10 oggetti uguali in un pacco e lo porta a 3 volte la velocità del nastro espresso; accetta solo dall'inizio della linea e scarica solo in fondo (in un edificio o nel CORE, non in un nastro normale). |
| Ponte lungo (`bridge-long`) | Come il ponte di oggi, fino a 8 celle, corrente 1/s; senza corrente non passa niente. |

## Energia (piano 2)

| Blocco | Misura | Produzione o capacità |
|---|---|---|
| Pannello solare (`solar`) | 1×1 | 3/s di giorno, scende con la luce del tramonto, 0 di notte (usa la luce del giorno e notte che c'è già) |
| Pannello solare grande (`solar-large`) | 3×3 | 36/s di giorno, come sopra |
| Generatore a combustione (`combustion-generator`) | 2×2 | 90/s; brucia 1 carbone ogni 2 s oppure 1 grafite ogni 4 s |
| Batteria grande (`battery-large`) | 3×3 | 20.000 di capacità |

## Potenziamenti (piano 3)

Nodi della Ricerca senza blocco (come i moduli della Finanza), due livelli ciascuno, il secondo dopo il primo:

| Gruppo | Livello 1 | Livello 2 |
|---|---|---|
| Drone: costruzione | +25% velocità di costruzione | +50% |
| Drone: stiva | +50% | +100% |
| Drone: raggio | +5 celle | +10 celle |
| Drone: scavo | +25% ritmo | +50% |
| Produzione: minatori | +10% | +20% |
| Produzione: macchine | forni, fonderie e assemblatori +15% | +30% |
| Magazzino | +25% al massimo per oggetto di CORE e depositi | +50% |

Tutti in interi (percentuali in punti base), senza float nei conti; si applicano dal tick dopo lo sblocco.

## Premi della campagna (piano 3)

| Capitolo | Premio |
|---|---|
| C1 Corrente e logistica | Generatore a combustione |
| C2 Fondazione e borsa | Nastro a pacchi |
| C3 Titanio e silicio | Ricetta dei circuiti |
| C4 Il Nucleo | «Drone potenziato»: i livelli 2 dei quattro potenziamenti del drone insieme |

- Nella campagna questi nodi non si comprano: si ottengono al lancio del capitolo (si vedono in Ricerca come «Premio del capitolo N»).
- Nella partita libera si comprano in Ricerca a un **prezzo molto alto**: il costo che il foglio darebbe al nodo, per 3.

## Ricerca e bilanciamento (piano 3)

- Ogni nodo nuovo ha la sua fase nel foglio «Bilanciamento» e il costo esce dallo script (`--scrivi-costi`, `--controlla`); i premi della campagna hanno il ×3 per la partita libera.
- Obiettivo che non cambia: **circa 6 ore** (5,5–6,5) per sbloccare tutto senza jackpot nella partita libera, premi compresi. Se i nodi nuovi allungano troppo, si accorciano i minuti dei nodi, non la regola.
- I costi di costruzione dei blocchi nuovi si scelgono a mano nella specifica del piano (materiali della loro fase), non dal foglio.

## Simulazione, salvataggi, agenti, client

- Sistemi nuovi o estesi: estrazione su pavimento (sabbia), fonderia avanzata, pressa, separatore (caso seminato), trapano a impatto, plastificatore; overflow/underflow nella `BeltGrid`; nastro a pacchi (pacchi come oggetto della cella); ponte lungo; produzione solare dalla luce del giorno e notte; generatore a combustione; potenziamenti letti dove servono (drone, minatori, macchine, massimi del CORE).
- Determinismo come sempre: interi, nessun ordine di `Dictionary`/`HashSet`, caso solo dal generatore seminato.
- Salvataggi **v21** (stato dei blocchi nuovi, pacchi sui nastri, potenziamenti come nodi della Ricerca); i v20 non si aprono.
- Agenti: `piazza` accetta i blocchi nuovi; lo `stato` mostra i potenziamenti attivi.
- Client: sprite da `genera.py`, palette, suggerimenti, testi in `testi.csv` (it + en), Wiki (Logistica, Produzione, Energia, Economia e Ricerca, Comandi).

## Test

- Per ogni blocco: il comportamento della tabella (ritmi, corrente, overflow/underflow coi lati bloccati, pacchi che non entrano nei nastri normali, solare a mezzogiorno, al tramonto e di notte, combustione con carbone e con grafite).
- Separatore: frequenze sul lungo periodo vicine al 30/30/30/10 e determinismo col seme.
- Potenziamenti: effetto esatto dei due livelli; massimi del CORE che crescono senza perdere oggetti.
- Premi: in campagna non comprabili, dati al lancio; nella partita libera al prezzo ×3.
- Bilanciamento: 5,5–6,5 ore; `--controlla` OK.
- Salvataggio v21 avanti e indietro; v20 rifiutato; determinismo con tutti i blocchi nuovi.

## Fuori ambito

Liquidi e generatore a vapore (aspetto 12); leghe; potenziamenti del Mercato; scivolo di vendita e secondo schermo di borsa; unità di difesa (aspetto 13).
