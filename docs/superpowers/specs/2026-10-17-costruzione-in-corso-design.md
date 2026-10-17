# Sotto-progetto 3 — Costruzione in corso (come Mindustry)

Stato: approvata a voce il 2026-10-17, in attesa di revisione scritta.

## Obiettivo

Piazzare un blocco non lo crea più all'istante. Diventa un **piano**: il **drone** del giocatore lo raggiunge e lo costruisce nel tempo, prelevando le risorse dal Core man mano, come fa Mindustry. Lo smontaggio segue lo stesso percorso al contrario e rimborsa man mano.

Regola guida, dall'utente: **il più simile possibile a Mindustry**. Il gioco è un "Mindustry con un'economia un po' diversa". Dove questa spec non decide, si guarda a come lo fa Mindustry.

In questo sotto-progetto il drone è **automatico**: vola da solo verso i piani. Nel sotto-progetto 4 cambia solo chi lo muove, cioè il giocatore.

## Cosa sente il giocatore

- Trascina 20 nastri e compaiono 20 sagome fantasma. Il drone parte dal Core, vola verso la prima e la costruisce. Lo sprite del nastro si "disegna" dal basso con la linea luminosa, mentre il raggio laser parte dal drone.
- Se mancano materiali, il cantiere si ferma e mostra cosa manca. Riparte da solo quando i materiali arrivano.
- Il tasto destro su un edificio lo segna in rosso. Il drone lo smonta e i materiali tornano man mano.
- Il tasto destro su un piano lo cancella. Su un cantiere a metà lo smonta e restituisce tutto quanto già versato.

## Modello di simulazione (`src/TIndustry.Shared`)

### Piano — `BuildPlan`

Campi: `Tile` (origine), `Direction`, `StructureId` (blocco o nastro), `RecipeId?`, `FilterItemId?`, `Breaking` (bool).

Ogni drone tiene una coda ordinata (`List<BuildPlan>`). Le celle di un piano non possono sovrapporsi a edifici, nastri, cantieri o ad altri piani, di qualunque giocatore. Fanno eccezione i piani `Breaking`, che stanno per definizione su qualcosa che esiste.

### Cantiere — `ConstructSite` (il `ConstructBuild` di Mindustry)

Campi: `Id`, `Origin`, `Direction`, `StructureId`, `RecipeId?`, `FilterItemId?`, `Progress` (0–1, `float`), `Accumulator[]` per voce di costo (la frazione non ancora prelevata o rimborsata), `Spent[]` (quanto effettivamente versato), `Deconstructing` (bool), `BuilderId?`.

- Occupa le stesse celle del blocco finale e non lavora: non produce, non accetta oggetti dai nastri, non consuma né trasporta energia.
- Quando `Progress` arriva a 1, il cantiere viene rimosso e al suo posto nasce l'edificio o il nastro vero. Ha un id nuovo da `NextEntityId`, ricetta e filtro del piano e stato interno vuoto.
- Uno smontaggio trasforma l'edificio in cantiere con `Progress = 1` e `Deconstructing = true`, poi scende fino a 0 e il cantiere sparisce. Oggetti sui nastri e nei buffer dell'edificio smontato vanno persi, come in Mindustry.
- Il "riposizionamento gratuito" di oggi viene eliminato perché non è Mindustry: oggi piazzare il secondo miner sposta quello esistente se è l'unico.

### Tempo di costruzione (formula di Mindustry)

```
buildCost = (20 + Σ_requisiti quantità × buildWeight(oggetto)) × buildCostMultiplier(blocco)
ogni tick, sul cantiere in lavorazione:  Δprogress = buildSpeed(drone) / buildCost
```

- `buildWeight` è l'`item.cost` di Mindustry. Valori iniziali: `iron-plate` 0,7, `copper-wire` 0,5, `copper-ore` 0,5. Ogni altro oggetto vale 1,0 se non è indicato.
- `buildCostMultiplier` vale 1 se non è indicato.
- Il denaro (`moneyCost`) non entra nel tempo. Viene prelevato in proporzione come i materiali.
- Il drone ha `buildSpeed` 0,5 (alpha). Con il tick fisso a 60/s:
  - un `conveyor-basic` (1 `iron-plate`) richiede ≈ 42 tick, circa 0,7 s;
  - un `miner` (4 `iron-plate`) ≈ 46 tick;
  - un `miner-advanced` (10 piastre + 4 fili) ≈ 58 tick.
- Lo smontaggio procede alla stessa velocità.

### Consumo e rimborso (accumulatore di Mindustry)

- Ogni tick si calcola, per ogni voce di costo (materiali e denaro), `accumulo += Δprogress × quantità`. La parte intera viene prelevata dal Core (`Wallet`) e aggiunta a `Spent`.
- Se il prelievo di una voce non è possibile, `Δprogress` di quel tick viene limitato al massimo coperto dalle risorse disponibili, come `checkRequired` in Mindustry. Il cantiere quindi si ferma esattamente dove le risorse finiscono e riparte da solo.
- Alla fine della costruzione `Spent` è uguale al costo intero: la somma degli arrotondamenti si chiude all'ultimo tick.
- Smontaggio di un **edificio finito**: i rimborsi rientrano man mano con la stessa logica. Il totale è `costo × regolaRimborso% × refundPercent(edificio)%`, arrotondato per difetto voce per voce.
- Cancellazione di un **cantiere non finito**: lo si smonta e si rimborsa il 100% di `Spent`, man mano.

### Regole della partita — `GameRules`

- `RefundPercent` (0–100): 100 nella partita base e nella futura sandbox. In campagna è il valore del livello (`campaign.json`, campo `refundPercent`, 100 se manca). I primi livelli restano a 100, i successivi passano a 50. La tabella esatta la decide il piano, partendo da livello 1–3 a 100 e dal livello 4 in poi a 50.
- `InstantBuild` (bool, falso nel gioco): i piani si completano nel tick in cui vengono applicati, con costo intero pagato o rimborsato subito e senza volo del drone. Serve a test, demo e seed di sviluppo, così i test esistenti restano validi.

### Drone — `BuilderUnit` (l'alpha di Mindustry)

- Campi: `Id`, `Owner` (`PlayerId`), `Position` (`float` x, y in tile), `Rotation`, coda di piani, cantiere corrente.
- Ogni giocatore ha un drone, che nasce al centro del Core a inizio partita.
- Valori da `content.json`, sezione `units`, voce `drone`:
  - `buildSpeed` 0,5;
  - `buildRange` 27,5 tile, cioè i 220 di Mindustry diviso 8;
  - `speed` 22,5 tile/s, la velocità 3 dell'alpha convertita.
- Comportamento automatico:
  - se la coda non è vuota, vola in linea retta verso il primo piano finché la distanza dal centro del piano non è ≤ `buildRange − 1`, poi lavora;
  - se la coda è vuota, torna a un punto fisso accanto al Core e resta lì.
- **Inizio lavori** (il `beginPlace` di Mindustry): quando il drone raggiunge un piano, la simulazione rivalida le celle e solo allora il piano diventa cantiere.
  - Se le celle non sono più valide, il piano viene scartato con l'evento `PlanDropped(PlayerId, BuildPlan, motivo)`.
  - Per un piano `Breaking` l'edificio diventa cantiere in smontaggio. Se nel frattempo è sparito, il piano viene scartato in silenzio.
- Il drone lavora su un solo cantiere alla volta: il primo della coda. Il piano esce dalla coda quando il suo cantiere è finito o scartato.

### Ordine dei sistemi

```
Builder → Construct → Belt (avanzamento) → Power → Mining → Crafting → Extraction → Belt (passo zero) → CoreDelivery
```

- **Builder** muove i droni e avvia i cantieri.
- **Construct** fa avanzare cantieri e smontaggi, preleva, rimborsa e completa.

Restano valide tutte le regole di determinismo del sotto-progetto 2: droni, cantieri e piani si iterano in ordine di id e di coda. Le posizioni `float` del drone ricadono nella regola già accettata (ADR 0002: `float` con recupero tramite istantanea).

## Comandi ed eventi

Comandi (validati all'applicazione, come nel sotto-progetto 2):

- `PlaceBuilding` e `PlaceBelt` **accodano un piano** nella coda del drone del giocatore. Al momento della richiesta si controllano solo due cose: il blocco è sbloccato e le celle sono valide. **Le risorse non bloccano**: sparisce il rifiuto "Risorse insufficienti" al piazzamento.
- `Remove(tile)`:
  - su un piano lo cancella;
  - su un cantiere in costruzione lo trasforma in cancellazione, con rimborso del 100% di quanto speso;
  - su un cantiere in smontaggio non fa nulla;
  - su un edificio o nastro accoda un piano `Breaking`;
  - su niente resta silenzioso, come oggi.
- Il trascinamento di nastri diventa una sequenza di `PlaceBelt`, quindi piani nell'ordine del trascinamento.

Eventi nuovi:
- `PlanDropped(PlayerId, BuildPlan, motivo)`, con testi italiani in `CommandReasons`;
- `ConstructFinished(siteId, entityId)`;
- `DeconstructFinished(siteId)`.

`IGameView` espone in sola lettura i droni (posizione, rotazione, cantiere corrente), le code dei piani e i cantieri (progresso, smontaggio, risorse mancanti).

## Client (Godot, come Mindustry)

- **Piani fantasma**: sprite del blocco semitrasparente con contorno nel colore del giocatore (l'"accent" giallo-arancio di Mindustry). I piani `Breaking` sono rossi (il "remove" di Mindustry). I nastri trascinati mostrano tutte le sagome con la direzione.
- **Cantiere**: shader Godot ricavato dallo shader `build` di Mindustry. Lo sprite si rivela dal basso in base a `Progress`, la parte non costruita è scura con il solo contorno e una linea luminosa scorre sul fronte. Lo smontaggio usa lo stesso effetto in rosso e al contrario. Un cantiere fermo per risorse fa pulsare la linea più lenta. Il tooltip in hover dice "Mancano: N <oggetto>".
- **Drone**: sprite stile alpha (triangolo con ali) nel colore del giocatore, scia del motore, inclinazione nella direzione di volo e leggera fluttuazione quando è fermo. È disegnato sopra edifici e oggetti.
- **Raggio di costruzione**: linee dalla punta del drone ai quattro angoli del cantiere, con un poligono semitrasparente e un tremolio. Rosso durante lo smontaggio.
- **Coda nell'HUD**: un riquadro con l'icona del blocco in lavorazione, la sua barra e il numero di piani rimasti. Un clic sposta la telecamera sul cantiere. È nascosto se la coda è vuota.
- **Toast**: `PlanDropped` mostra il motivo. Non ci sono toast per "Risorse insufficienti" al piazzamento.
- **Prestazioni**: un unico livello per piani, cantieri e drone, aggiornato solo quando cambia. Lo shader è applicato solo ai cantieri attivi.
- **Fuori ambito**: suoni, particelle extra, telecamera che segue il drone (sotto-progetto 4) e controllo manuale del drone (sotto-progetto 4).

## Dati e salvataggi

- `content.json`:
  - mappa `buildWeights` (oggetto → peso);
  - campo facoltativo `buildCostMultiplier` per blocco e nastro;
  - sezione `units.drone` con `buildSpeed`, `buildRange`, `speed`.
- `campaign.json`: `refundPercent` per livello.
- Salvataggi **v11**: l'istantanea aggiunge `GameRules`, droni, code dei piani e cantieri, inclusi accumulatori e `Spent`. I v10 vengono rifiutati con il messaggio esistente "Salvataggio di una versione precedente: non compatibile.".

## Tutorial, demo e test

- I passi del tutorial "miner" e "forno" scattano quando l'edificio è **finito**. Il testo del primo passo di piazzamento spiega che il drone costruisce ciò che pianifichi.
- Demo, seed di sviluppo (`SeedCoreDeliveryLine`) e i test esistenti usano `InstantBuild = true`.
- I playtest `ui` e `tutorial` restano verdi. Il playtest del tutorial attende la fine dei cantieri.
- Gli screenshot di riferimento cambiano per la grafica nuova. L'utente li approva a vista prima che diventino la nuova base.

Test nuovi (xUnit, con `InstantBuild = false`):

- tempo di un `miner` = ⌈(20 + 4 × 0,7) / 0,5⌉ tick, con tolleranza di 1 tick;
- il consumo è graduale: a metà costruzione è stata prelevata circa metà del costo e alla fine esattamente il costo;
- senza risorse il cantiere si ferma, e riparte quando le risorse arrivano;
- rimborso: edificio finito con regola 100 → costo intero; con regola 50 → metà per difetto; cantiere annullato a metà → il 100% di `Spent`;
- 50 nastri trascinati senza materiali: nessuno si completa; aggiunti i materiali, si completano tutti nell'ordine della coda;
- un piano le cui celle si occupano prima dell'arrivo del drone produce `PlanDropped` e il drone passa al piano successivo;
- lo smontaggio di un edificio con oggetti dentro li perde e rimborsa secondo la regola;
- il drone vola verso un piano oltre `buildRange` e non avanza il cantiere finché non è nel raggio;
- un salvataggio e caricamento a metà costruzione dà lo stesso `Checksum` subito e dopo altri 600 tick;
- determinismo: la stessa sequenza di comandi produce lo stesso checksum dopo 10.000 tick.

## Consegna

Cinque PR impilate. Dopo ogni merge si verifica con `git merge-base --is-ancestor` che il contenuto sia davvero in `main`.

1. `GameRules`, dati (`buildWeights`, `units.drone`, `refundPercent`), `ConstructSite`, `ConstructSystem`, `InstantBuild`; test della formula e dell'accumulatore con un costruttore di prova.
2. Drone, piani, `BuilderSystem`, comandi e semantica di `Remove`, eventi, `IGameView`.
3. Salvataggi v11 e istantanea.
4. Client: fantasmi, shader, drone, raggio, coda nell'HUD, toast, tutorial, playtest e nuova base di screenshot.
5. Documentazione: ARCHITETTURA, ADR 0003 "Costruzione come Mindustry", CLAUDE.md, diario.

## Fuori ambito

- Controllo manuale del drone e drone che raccoglie minerali: sotto-progetto 4.
- Più costruttori su un cantiere, rinforzi e "assist" di Mindustry, unità combattenti.
- Sandbox: la regola 100% è pronta, ma la modalità non si crea qui.
- Bilanciamento fine dei tempi: i valori iniziali sono quelli dichiarati e vanno ritoccati solo in `content.json`.
