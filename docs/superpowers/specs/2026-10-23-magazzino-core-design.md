# Magazzino del CORE e livelli — specifica 3 di 3

- **Stato**: da approvare. Brainstorming con l'utente del 23 ottobre 2026; segue la [specifica 1 del Mercato](2026-10-22-mercato-borsa-design.md), che ha tolto il bonus di «Potenzia CORE» e rimandato qui il magazzino.
- **Assorbe** una parte dell'aspetto 11 di `docs/STATO.md` («Drone e tier»): i tre CORE shard → foundation → nucleus e i droni alpha → beta → gamma. All'aspetto 11 restano il drone comandato a mano e quello che non è in questa specifica.
- **Sostituisce**: il CORE 2×2 senza limiti, «Potenzia CORE» (`economy.coreUpgrade`, `CoreUpgradeLevel`, comando `UpgradeCore`), la vendita automatica che vende ogni pezzo all'arrivo, i salvataggi fino alla v14.

## Obiettivo

Il CORE ha un magazzino con un massimo per oggetto, come in Mindustry: a CORE pieno la linea si ferma. Il CORE cresce in tre livelli sbloccati da un ramo suo della Ricerca e piazzati sopra il CORE vecchio, e ogni livello porta un drone migliore. Container e vault allargano il magazzino se attaccati al CORE, o fanno da depositi a sé. La vendita automatica diventa un ritmo che cresce col CORE.

Successo:
- una linea che porta al CORE più di quanto si venda si ferma quando l'oggetto è al massimo, e riparte quando scende;
- un giocatore che vuole tentare il jackpot pieno (10.000 pezzi, specifica 1) deve arrivare al nucleus o collegare magazzini;
- la vendita automatica regge da sola una linea piccola all'inizio e una grande solo col CORE potenziato.

## Decisioni prese con l'utente

| Tema | Decisione |
|---|---|
| Livelli | Tre CORE come Mindustry: **shard 3×3**, **foundation 4×4**, **nucleus 5×5**. Ogni partita parte dallo shard. |
| Magazzino | Massimo **per oggetto**: **1.000 / 4.000 / 12.000**. I soldi non hanno limite. |
| CORE pieno | La linea di quell'oggetto **si ferma, sempre**: il CORE non lo accetta, i nastri si riempiono, gli edifici attaccati smettono di consegnarlo. Gli altri oggetti arrivano normalmente. |
| Potenziamento | Ramo **CORE** nella Ricerca: «CORE foundation», poi «CORE nucleus». Sbloccarne uno aggiunge il blocco alla palette; il blocco si piazza **solo sopra il CORE attuale** e con l'area **libera**, altrimenti è rifiutato. È un cantiere che il drone costruisce. Il pulsante «Potenzia CORE» sparisce. |
| Drone | Potenziando il CORE si ottiene il drone successivo: **alpha → beta → gamma**, coi numeri di Mindustry. Di serie resta un drone solo. |
| Costi | Tabella in «Livelli»; da ritoccare quando si rifà la campagna. |
| Vendita automatica | Vende **dal magazzino** al ritmo del CORE, **10 / 25 / 60 pezzi al secondo per oggetto**, al prezzo minimo come nella specifica 1. Spunta per oggetto **«solo a CORE pieno»**: parte solo quando l'oggetto è al massimo. |
| Magazzini | **Container 2×2 da 300** e **vault 3×3 da 1.000** per oggetto, come Mindustry. A contatto col CORE ne allargano il magazzino; lontani sono depositi a sé, svuotati dall'estrattore. |
| Salvataggi | **v15**. Fino alla v14 non si aprono più (il gioco non è ancora uscito): il codice di conversione v11–v13 sparisce. |

## Livelli

| CORE | Misura | Massimo per oggetto | Vendita automatica | Drone | Ricerca | Costruzione |
|---|---|---|---|---|---|---|
| shard | 3×3 | 1.000 | 10/s | alpha | — (di partenza) | — |
| foundation | 4×4 | 4.000 | 25/s | beta | 5.000 $ + 200 lastre di ferro, 100 fili di rame | 800 lastre di ferro, 500 fili di rame, 400 lastre di piombo |
| nucleus | 5×5 | 12.000 | 60/s | gamma | 25.000 $ + 400 lastre di titanio, 300 silicio | 3.000 lastre di ferro, 2.000 lastre di piombo, 1.500 silicio, 1.000 lastre di titanio |

Ogni oggetto della costruzione costa meno del massimo del CORE che la costruisce (1.000 per il foundation, 4.000 per il nucleus).

| Drone | Costruzione | Velocità (celle/s) | Raggio (celle) |
|---|---|---|---|
| alpha | 0,5 | 22,5 | 27,5 |
| beta | 0,75 | 24,75 | 27,5 |
| gamma | 1,0 | 26,6 | 27,5 |

## Regole di gioco

### Magazzino e CORE pieno

- Il **massimo** di un oggetto è quello del livello del CORE più lo spazio dei magazzini collegati.
- Un oggetto **al massimo** non entra: un nastro che punta al CORE tiene il pezzo in uscita (e si riempie dietro), un edificio attaccato al CORE lo tiene nel suo buffer d'uscita e si ferma quando è pieno. Appena il magazzino scende sotto il massimo, il primo pezzo in attesa entra.
- Il drone che costruisce o smonta prende e rende i materiali dal magazzino. I **rimborsi** (smontaggio, cantiere annullato) entrano fino al massimo; il resto va perso, come in Mindustry.
- Se il massimo scende sotto quanto c'è (un magazzino collegato smontato), niente sparisce: l'oggetto resta «pieno» finché non scende sotto il nuovo massimo.
- Nell'HUD e nella panoramica del Mercato un oggetto al massimo si vede pieno (`1.000/1.000` col colore di avviso del tema).

### Vendita automatica

- Per ogni oggetto: interruttore della vendita automatica e spunta **«solo a CORE pieno»** (attiva solo con l'interruttore acceso).
- Accesa, vende dal magazzino fino al **ritmo del CORE** (10 / 25 / 60 pezzi al secondo per oggetto), al prezzo minimo della specifica 1 (B, o il prezzo del momento se è sotto B). Non tocca il mercato: niente calo, niente crollo, niente jackpot.
- Con la spunta vende solo mentre l'oggetto è **al massimo o a meno di un secondo di vendita dal massimo** (massimo − ritmo): in pratica tiene il magazzino pieno, pronto per un picco, e lascia scorrere la linea fino al ritmo.
- Se la produzione supera il ritmo, il magazzino sale fino al massimo e la linea si ferma come senza vendita automatica.

### Potenziamento del CORE

- Il blocco «CORE foundation» (poi «CORE nucleus») entra nella palette quando è sbloccato nella Ricerca. Si piazza solo sopra il CORE: il fantasma si aggancia al CORE e non si può mettere altrove.
- **Crescita**: lo shard 3×3 all'origine (x, y) diventa foundation 4×4 all'origine (x, y), una fila in più a destra e in basso; il foundation 4×4 diventa nucleus 5×5 all'origine (x−1, y−1), una fila in più a sinistra e in alto. Il nucleus è centrato sullo shard di partenza.
- Le celle nuove devono essere **libere e costruibili**: niente edifici, nastri, cantieri, pareti, acqua profonda, bordo della mappa. Se no il piazzamento è rifiutato col motivo (codici nuovi in `RefusalCode`: livello non sbloccato, potenziamento già in corso, area occupata, fuori mappa).
- Il potenziamento è un **cantiere** come gli altri: il drone lo costruisce prendendo i materiali a poco a poco. Intanto il CORE vecchio funziona del tutto (riceve, vende, costruisce).
- A cantiere finito: il CORE occupa la misura nuova, tiene il magazzino, il massimo e il ritmo salgono, il drone prende le statistiche del livello. Le celle in più diventano CORE: i nastri e gli edifici che toccano il CORE nuovo gli consegnano.
- Annullare il cantiere rende tutto lo speso (come oggi). Il CORE non si smonta e non scende di livello. Un solo potenziamento alla volta.

### Container e vault

| Blocco | Misura | Spazio per oggetto | Ricerca | Costruzione |
|---|---|---|---|---|
| container | 2×2 | 300 | 400 $ + 20 lastre di ferro, 10 lastre di piombo (dopo l'incrocio) | 40 lastre di ferro, 20 lastre di piombo |
| vault | 3×3 | 1.000 | 900 $ + 20 lastre di titanio (dopo il container) | 60 lastre di titanio, 40 lastre di ferro |

- **Collegato**: un magazzino che tocca il CORE lato contro lato. Il suo spazio si somma al massimo di ogni oggetto; non ha un contenuto suo, è parte del magazzino del CORE (vendite, «Vendi tutto», jackpot, crollo e costruzione vedono un magazzino solo). I nastri che gli consegnano consegnano al CORE. Ha l'aspetto «collegato» (cornice del CORE).
- **A sé**: un magazzino che non tocca il CORE. Ha un contenuto suo per oggetto, fino allo spazio del blocco; accetta da nastri ed edifici qualsiasi oggetto finché c'è posto; l'estrattore attaccato lo svuota come oggi svuota il CORE. Smontandolo il contenuto va perso, il rimborso dei materiali è quello normale.
- Il collegamento si ricalcola quando si piazza, si smonta o si potenzia qualcosa. Un magazzino a sé che diventa collegato (perché il CORE è cresciuto fino a toccarlo) versa il suo contenuto nel CORE fino al massimo, il resto va perso.
- Il potenziamento del CORE richiede l'area libera: un magazzino collegato sulla fila di crescita va spostato prima. Il nucleus cresce a sinistra e in alto, quindi conviene attaccare i magazzini a destra e in basso.

## Fuori ambito

Drone comandato a mano, più droni, unità di difesa (aspetti 11 e 13); lo schermo di borsa (specifica 2); la campagna rifatta (aspetto 8), che ritoccherà i costi; l'Unloader con la configurazione col clic (#270), che resta a sé: qui l'estrattore di oggi svuota i depositi come svuota il CORE.

## Simulazione (`src/TIndustry.Shared`)

### Dati (`data/content.json`)

- Nuovo blocco `cores`: per ogni livello `id` (`core-shard`, `core-foundation`, `core-nucleus`), `size`, `itemCapacity`, `autoSellPerSecond`, `unit` (id in `units`), `unlock` (soldi e materiali della Ricerca, assente per lo shard), `buildCost` (materiali del cantiere).
- `units`: `alpha`, `beta`, `gamma` coi numeri della tabella (oggi un solo `drone`).
- `structures`: `container` e `vault` di tipo nuovo `storage` con `itemCapacity`; `core-foundation` e `core-nucleus` di tipo nuovo `core-upgrade`, col prerequisito della radice CORE e del livello prima.
- Via `economy.coreUpgrade` e la nota relativa in `refundPolicyNote`.

### Stato e regole

- `CoreUpgradeLevel` diventa `CoreTier` (0 shard, 1 foundation, 2 nucleus). Celle del CORE = origine + misura del livello (`CoreStockSink.MakeCoreTiles`); il drone legge le statistiche dal livello.
- **Un solo ingresso**: `CoreStorage.TryAccept(itemId)` (in `Economy/`), usato dai nastri (`CoreStockSink.Drain`) e dagli edifici attaccati (`GameState`, oggi `Wallet.AddMaterial` diretto). Ritorna falso al massimo; chi chiama tiene il pezzo. I rimborsi passano da `CoreStorage.AddClamped`.
- `CoreStorage.Capacity(itemId)` = massimo del livello + spazio dei magazzini collegati; i magazzini collegati si ricalcolano in un punto solo (dopo piazzamenti, smontaggi e potenziamenti), in ordine di id.
- **Vendita automatica**: `MarketItemState` aggiunge `AutoSellWhenFull` e un accumulatore intero in millesimi di pezzo. A ogni tick `+= ritmo × 1000 / 60`; i pezzi interi si vendono dal magazzino (con la spunta, solo finché la quantità è almeno `massimo − ritmo`). Via `MarketSales.TryAutoSell` all'arrivo. Tutto intero e deterministico.
- **Comandi**: `PlaceCoreUpgrade(PlayerId)` (al posto di `UpgradeCore`; la posizione la decide la regola di crescita), `SetAutoSellWhenFull(PlayerId, ItemId, bool)`. I magazzini si piazzano col comando normale.
- **Cantiere del CORE**: `ConstructSite` col blocco `core-upgrade`; a fine lavori `CoreTier++`, origine aggiornata, celle ricalcolate, magazzini ricollegati.
- **Magazzini**: `StorageBlock` (in `Buildings/`) con `Contents` per oggetto (vuoto quando collegato). Accetta dai nastri e dagli edifici come un edificio qualsiasi; l'estrattore lo svuota.
- **Ricerca**: `TechTreeLayout` mette i nodi `core-upgrade` in un ramo sotto la radice CORE; `ResearchState` li sblocca come le strutture.

### Salvataggi

- `FactorySaveData.CurrentVersion = 15`. Si leggono solo i v15; gli altri danno un `SaveLoadError` nuovo, «versione non più supportata», tradotto dal client. Via il codice di conversione v11–v14 e `CoreUpgradeLevel`/`AutoSellAtCore`.
- Nuovi campi: `CoreTier`, contenuto dei magazzini a sé, `AutoSellWhenFull` e accumulatore per oggetto. `CoreSize` resta (ora deriva dal livello e si controlla al caricamento).
- I generatori di terreno pubblicati (`classico`, `biomi-1`) non cambiano: ricevono già le celle del CORE come dato.

### Mappe della campagna

- `CampaignMapFile.CoreSize = 3`; il punto `core` delle mappe resta. `dotnet run --project tools/MapTool -- valida` deve dire OK su tutte: dove una parete o l'acqua cade nella nuova fila si ritocca la mappa con `MapTool`.

## Client (`godot/`)

- Sprite di shard, foundation, nucleus, container, vault (normale e collegato) e dei droni beta e gamma da `tools/art/genera.py`.
- Palette: i blocchi del CORE compaiono quando sbloccati; il fantasma si aggancia al CORE e mostra in rosso le celle occupate.
- Panoramica del Mercato: quantità `x/massimo`, spunta «solo a CORE pieno» accanto all'interruttore.
- HUD: risorse col massimo; il pulsante «Potenzia CORE» sparisce.
- Testi nuovi in `godot/lingue/testi.csv` (italiano e inglese): nomi dei blocchi, motivi di rifiuto, spunta, errore del salvataggio.
- Server MCP degli agenti: `vendita_automatica` prende anche `solo_a_pieno`; il comando `potenzia_core` diventa il piazzamento del blocco.

## Test

- CORE pieno: un nastro che porta ferro a uno shard con 1.000 ferro si ferma e riparte quando se ne vende; un minatore attaccato si ferma; gli altri oggetti entrano.
- Massimo con container e vault collegati e scollegati; magazzino a sé che si riempie e si svuota con l'estrattore; smontaggio di un collegato oltre il massimo; deposito che diventa collegato.
- Vendita automatica: 10 / 25 / 60 pezzi al secondo esatti su 10 secondi, prezzo minimo, mercato intatto; con la spunta tiene l'oggetto al massimo.
- Potenziamento: rifiutato non sbloccato, con l'area occupata, fuori mappa, già in corso; cantiere e materiali; crescita a destra/in basso e poi a sinistra/in alto; drone beta e gamma; nastri che toccano il CORE nuovo.
- Rimborsi oltre il massimo persi.
- Salvataggi: v15 avanti e indietro uguale; v14 rifiutato con l'errore nuovo.
- Mappe: `MapTool valida` OK col CORE 3×3.
- Determinismo: checksum uguale in due corse con potenziamento, magazzini e vendita automatica.

## Ordine delle PR

1. Dati e CORE a livelli col massimo (`CoreStorage`, ingresso unico, linee che si fermano).
2. Vendita automatica a ritmo con la spunta.
3. Potenziamento del CORE e ramo della Ricerca, droni per livello.
4. Container e vault.
5. Salvataggi v15 e mappe col CORE 3×3.
6. Client: sprite, palette, panoramica, HUD, testi, MCP.
7. Documenti, Wiki, `docs/STATO.md`, CHANGELOG e diario.
