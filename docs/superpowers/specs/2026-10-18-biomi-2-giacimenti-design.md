# Giacimenti per bioma e partenza casuale (generatore `biomi-2`)

- **Stato**: da approvare (progetto approvato a voce il 2026-10-18)
- **Ricerca di partenza**: [confronto con Mindustry e misure di `biomi-1`](2026-10-18-biomi-2-ricerca.md)
- **Decisioni collegate**: [ADR 0005 — Mappe a seme e in file](../../decisioni/0005-mappe-seme-e-file.md) (un generatore pubblicato non si cambia), PR #191 (titanio solo col Minatore T2)

## Obiettivo

Dare ai minerali una progressione come in Mindustry, ma legata ai **biomi**: ogni bioma ha le sue risorse specifiche e le risorse comuni con frequenze proprie. La partenza delle partite normali cambia da partita a partita. I numeri di copertura scritti nei dati sono quelli che si ottengono davvero.

## Problemi di `biomi-1` (dalla ricerca)

- Nessuna scala di rarità: ogni minerale copre l'1–1,8% della mappa; il titanio è comune quanto il ferro.
- Il rame, minerale base, è fra i più rari (~1%) e sta quasi solo nella sabbia.
- Macchie grandi (media 35–45 celle, massimi ~600) e metà mappa vuota per la maschera "campo di minerale".
- La partenza è sempre al centro della mappa e su Grande ed Enorme può trovarsi a 70–90 celle dai minerali.
- Il livello 4 della campagna ("fili", chiede rame) ha il rame più povero di tutti (0,7%).

## Decisioni prese con l'utente

| Tema | Decisione |
|---|---|
| Generatore | Nuovo generatore **`biomi-2`** per le partite nuove; `biomi-1` resta congelato e i suoi salvataggi si aprono identici. |
| Biomi | **Ogni bioma è unico**: ha risorse specifiche e le risorse comuni con frequenze diverse. La distanza dal Core non conta. |
| Tabella | Coperture per bioma e minerale **nei dati** (`content.json`), con i valori di partenza qui sotto; i numeri si confermano con le misure prima di congelare. |
| Approccio | **Coperture esatte**: la soglia di ogni minerale si calcola sulla mappa per ottenere la percentuale della tabella (approccio A). |
| Seme | **Un seme è una mappa**: stesso seme, stessa mappa. |
| Partenza | **Completamente casuale** a ogni partita normale, anche con lo stesso seme; nessun pulsante per rilanciarla (se non piace, si crea un altro mondo). |
| Durezza | I minerali più duri si scavano **più lentamente**, come in Mindustry, oltre al tier già introdotto. |
| Campagna | I **10 livelli si rigenerano** con `biomi-2`. |

## Tabella dei giacimenti (valori di partenza)

Percentuale delle celle di suolo (terra, non pareti né acqua) del bioma coperte dal minerale.

| Bioma | Ferro | Rame | Carbone | Piombo | Titanio | Carattere |
|---|---|---|---|---|---|---|
| `rocce` | 3,5 | 1,5 | 2,5 | 0,5 | 0 | ferro e carbone: industria pesante |
| `sabbia` | 1,0 | 3,5 | 0,3 | 2,5 | 0 | rame e piombo: fili ed elettronica |
| `neve` | 1,0 | 1,0 | 0,5 | 0,5 | 1,5 | l'unico bioma con il titanio |
| `prati` | 2,0 | 2,0 | 2,0 | 1,0 | 0 | equilibrato |

Ferro e rame sono **comuni** (in tutti i biomi), il titanio è **specifico** della neve, il piombo quasi solo del deserto, il carbone di rocce e prati. Il bioma `acqua` non ha giacimenti.

## Progetto

### Dati

- Nuova sezione **`terrain.biomes`** in `data/content.json`: una riga per bioma di `TerrainTable.Biomes` (salvo `acqua`), con `giacimenti: { <deposito>: <percentuale> }` per ogni riga di `terrain.deposits`. Letta da `FactoryContent` con controlli ed errori in italiano: bioma sconosciuto o mancante, deposito sconosciuto o mancante, percentuale negativa, somma delle percentuali di un bioma oltre 25.
- **Durezze** in `terrain.deposits`: ferro 1, rame 1, piombo 1, carbone 2, titanio 3. **Tier** dei minatori in `buildings`: `miner` 2, `miner-advanced` 3. La regola "un minatore scava un deposito se tier ≥ durezza" di PR #191 resta: il T1 scava ferro, rame, piombo e carbone; il titanio solo il T2.

### Generatore `biomi-2`

Nuova classe accanto a `MapGenerator` (nome `"biomi-2"`), che riusa le parti non legate ai giacimenti senza cambiare `biomi-1`. Passi:

1. **Terreno di base**: biomi, acqua e pareti come `biomi-1`, con un proprio rumore.
2. **Giacimenti**: per ogni minerale un rumore a macchie piccole (cella circa 10). Per ogni bioma e minerale la soglia si sceglie con un istogramma del rumore sulle celle libere del bioma, così la copertura è quella della tabella. I minerali si posano dal più raro al più comune (titanio, piombo, carbone, rame, ferro): ogni soglia si calcola sulle celle rimaste libere, quindi le percentuali tengono anche dove i minerali si sovrapporrebbero. Mai su pareti o acqua. Fra due biomi la frequenza passa da uno all'altro in circa 8 celle. Macchie più piccole di 4 celle si tolgono, perché un minatore 2×2 deve poterci stare. Niente maschera "campo di minerale".
3. **Partenza**: area libera di raggio 10 attorno al Core, le macchie 2×2 di ferro e di rame accanto al Core e il carbone raggiungibile a terra (come `biomi-1`, regole `ClearStart` ed `EnsureLandPaths`).
4. **Impronta** del terreno nel salvataggio, come oggi.

Obiettivi misurati: copertura per bioma e minerale entro ±0,2 punti della tabella; macchia media 15–25 celle; copertura totale della mappa circa 7–9% secondo i biomi presenti; nessun titanio fuori dalla neve.

### Partenza casuale (partite normali)

- `ScegliPartenza(impostazioni, numeroCasuale)`: dal terreno di base sceglie una cella di terra a più di 40 celle dal bordo con un'area libera di raggio 10. Le celle valide si scorrono in un ordine fisso e se ne prende una col numero casuale, quindi il risultato è deterministico dato il numero. Se la mappa non ne ha, si usa la cella di terra più vicina al centro e si libera l'area.
- Il numero casuale nasce alla creazione della partita e non dipende dal seme. Può capitare qualunque bioma.
- La posizione del Core si salva già (`CoreX`, `CoreY`): al caricamento il terreno si rigenera da nome, seme, misura e Core, e l'impronta lo controlla. Il formato dei salvataggi non cambia.
- La schermata Nuova partita non cambia aspetto: seme casuale, modificabile e copiabile come oggi. La partenza non si sceglie e non si rilancia.

### Durezza e velocità di scavo

- Tempo di scavo di un minatore: `2 s × (1 + 0,25 × (durezza − 1))`, quindi ferro, rame e piombo 2,0 s, carbone 2,5 s, titanio 3,0 s. Il T2 resta due volte più veloce (titanio 1,5 s). La velocità resta proporzionale alle celle del deposito sotto il minatore.
- La durezza si legge dai dati e la simulazione resta deterministica. I salvataggi restano alla v12: i minatori già piazzati cambiano solo velocità.

### Strumenti

- **`MapTool statistiche`**: per semi e misure a scelta, e per le mappe di `data/maps/`, stampa per minerale e per bioma la copertura, il numero di macchie, la media, la mediana e la massima, la cella più vicina al Core e le macchie entro 50 e 100 celle.
- **`MapTool valida`** aggiunge un controllo di **quantità** per i minerali degli obiettivi di un livello (celle entro 60 dal Core), oltre a quello di presenza.

### Campagna

- I 10 livelli si rigenerano con `biomi-2` tramite `MapTool genera`, con la stessa misura, gli stessi biomi, la stessa posizione del Core (la partenza della campagna resta fissa, è disegnata) e l'esclusione del titanio fino al livello 8.
- Ogni livello deve passare `valida` con la quantità: per esempio il livello 4 "fili" con rame abbondante entro 60, il livello 7 "piombo" con piombo vicino. Il livello 9 deve avere neve con titanio raggiungibile.
- Un salvataggio di campagna si carica ancora (stessa misura), ma i giacimenti sotto le fabbriche già costruite possono essere cambiati: va scritto nel CHANGELOG.

### Congelamento

La tabella fa parte della definizione di `biomi-2`. Un test ne fissa i valori e un test fissa l'impronta di alcune mappe (semi e misure). Cambiare la tabella o il generatore dopo la pubblicazione vuol dire creare un nuovo generatore (`biomi-3`), come prescrive ADR 0005.

## Verifica

- Test di intervallo su 5 semi e le 3 misure: copertura per bioma e minerale entro ±0,2 della tabella; macchia media 15–25; nessun titanio fuori dalla neve; partenza su terra, lontana dal bordo, con l'area libera e le macchie 2×2.
- Determinismo: stesso seme, stesso numero di partenza e stessa misura danno la stessa impronta; due numeri diversi danno partenze diverse con lo stesso terreno di base.
- Contenuto: errori in italiano per tabella e durezze sbagliate o mancanti.
- Durezza: tempi di scavo per minerale e tier; un salvataggio v12 con minatori si carica e produce alla nuova velocità; `biomi-1` invariato (impronte esistenti).
- Campagna: `MapTool valida` OK su tutti i livelli, con le quantità.
- Catture: `MapTool anteprima` dei livelli e di alcuni semi; playtest `ui` e `tutorial` OK.

## Documenti

Nuovo **ADR 0007 — Giacimenti per bioma e partenza casuale**, con le decisioni qui sopra. Aggiornati ADR 0005 (`biomi-2` accanto a `biomi-1`), `docs/ARCHITETTURA.md` (tabella dei giacimenti, partenza, durezza), `godot/README.md` (partenza casuale, durezza), `CHANGELOG.md`, `CLAUDE.md` (generatori pubblicati: `classico`, `biomi-1`, `biomi-2`), la mappa Obsidian e la sezione Ambienti del sito.

## Fuori ambito

- Risorse nuove (sabbia dal suolo, rottami, minerali nelle pareti): la tabella lo permette con una colonna nuova, ma richiede oggetti e ricette. Si fanno dopo.
- Scelta della partenza da parte del giocatore o anteprima della mappa nella Nuova partita.
- Liquidi, tier del Core e del drone (SP4), server e co-op (SP5).
