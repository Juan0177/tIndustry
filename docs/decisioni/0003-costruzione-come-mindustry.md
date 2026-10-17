# 0003 — Costruzione come Mindustry

**Stato:** accettata, 2026-10-17

## Contesto

Fino al sotto-progetto 2 un blocco piazzato nasceva all'istante e pagava tutto il costo subito; senza risorse il piazzamento era rifiutato con "Risorse insufficienti". La rimozione restituiva subito i materiali.

Il drone del giocatore era previsto nel sotto-progetto 4 (drone comandato e tier) e il co-op nel sotto-progetto 5. La regola guida dell'utente è **il più simile possibile a Mindustry**: un blocco diventa un piano, un'unità costruttrice lo raggiunge e lo costruisce nel tempo, prelevando le risorse dal Core man mano. Serviva decidere come rappresentare nella simulazione un blocco "a metà" senza rompere determinismo, istantanea e salvataggi ([ADR 0002](0002-sincronizzazione-ibrida.md)).

Specifica: [2026-10-17-costruzione-in-corso-design](../superpowers/specs/2026-10-17-costruzione-in-corso-design.md). Piano: [2026-10-17-costruzione-in-corso](../superpowers/plans/2026-10-17-costruzione-in-corso.md).

## Opzioni

1. **Cantiere separato** (il `ConstructBuild` di Mindustry). Un'entità a sé occupa le celle del blocco finale e non lavora; a progresso 1 viene sostituita dal blocco vero. Gli edifici non cambiano.
2. **Flag "in costruzione" sugli edifici.** Ogni edificio e nastro ha un progresso e i sistemi lo saltano finché non è finito. Tocca ogni tipo di edificio, ogni sistema e il formato di ognuno nel salvataggio; un nastro a metà dovrebbe comunque rifiutare gli oggetti.
3. **Solo grafica.** La simulazione resta istantanea e il client mostra un'animazione di costruzione. Nessun costo nel tempo, niente cantiere che si ferma per mancanza di risorse, niente drone nella simulazione: non è Mindustry e in co-op ogni client vedrebbe una cosa diversa.

## Decisione

**Cantiere separato e drone automatico**, come Mindustry.

- `PlaceBuilding` e `PlaceBelt` accodano un piano (`BuildPlan`) sul drone del giocatore (`BuilderUnit`). Si controllano solo sblocco e celle libere da blocchi, nastri, cantieri e piani di qualunque giocatore; le risorse non bloccano il piazzamento.
- Il drone vola da solo verso il primo piano. Entro `buildRange − 1` dal centro del piano rivalida le celle e avvia il cantiere (`ConstructSite`), oppure scarta il piano con `PlanDropped`. Con la coda vuota torna a riposo accanto al Core. Nel sotto-progetto 4 cambia solo chi lo muove.
- Tempo come Mindustry: `buildCost = (20 + Σ quantità × buildWeight) × buildCostMultiplier`, e ogni tick `Δprogress = buildSpeed / buildCost`. Lo smontaggio va alla stessa velocità.
- Il costo esce dal Core man mano. Il cantiere si ferma esattamente dove le risorse finiscono (il `checkRequired` di Mindustry) e riparte da solo.
- Lo smontaggio trasforma il blocco in un cantiere a progresso 1 che scende a 0 e rimborsa man mano.
- Due nuovi sistemi in testa all'ordine fisso: `Builder` (muove i droni e avvia i cantieri) e `Construct` (fa avanzare cantieri e smontaggi, preleva, rimborsa e completa).

### Scelte prese durante l'attuazione

Queste scelte cambiano o precisano la specifica come era scritta. Il registro completo dei lavori finirà nel diario del sotto-progetto 3.

- **Prelievo arrotondato per eccesso.** La specifica diceva di prelevare la parte intera dell'accumulo (per difetto). Il codice preleva `ceil(progress × quantità) − Spent`: ogni unità si paga **prima** di costruire la sua fetta. Così il punto in cui il cantiere si ferma è esatto e il prelievo non supera mai il costo. Non serve un campo `Accumulator[]`: bastano `Spent[]` e, per lo smontaggio, `Refunded[]` (`Construction.AdvanceBuild`).
- **Quota di rimborso in punti base.** `ConstructSite.RefundBasisPoints` (0–10000) vale `GameRules.RefundPercent × refundPercent(edificio)` per un edificio finito e 10000 per un cantiere annullato. Il totale reso per voce ha un solo arrotondamento per difetto: `floor(quantità × R% × proprio%)`, cioè `Spent × punti base / 10000` (`Factory.RefundBasisPointsFor`).
- **Annullamento graduale.** Un cantiere annullato a metà rimborsa man mano, non tutto al primo tick. Ogni tick resta "in piedi" `standing = ceil(progress × quantità × punti base / 10000)` e si rende `totale − standing`, quindi il rimborso parte da dove il cantiere è arrivato (`Construction.AdvanceDeconstruct`).
- **Celle del cantiere salvate.** `ConstructSite.Tiles` è salvato (v11) e non ricalcolato al caricamento: un ponte a metà mantiene l'uscita scelta all'avvio anche se intanto un'altra cella libera è comparsa più vicino. Al completamento il ponte viene posato su quell'uscita.
- **Niente riposizionamento gratuito.** Prima, piazzare un secondo minatore, forno, assemblatore o generatore sull'unico esistente lo spostava gratis. Non è Mindustry ed è stato tolto.
- **Ordine di `Remove`** (`SimHost.ApplyRemove`): cancella il primo piano non ancora avviato del giocatore che copre la cella; altrimenti annulla un cantiere in costruzione; su un cantiere in smontaggio non fa nulla (rifiuto silenzioso); altrimenti accoda un piano di smontaggio, una volta sola per blocco; su una cella vuota non fa nulla. Con `InstantBuild` resta la rimozione istantanea di prima.
- **Punto di riposo del drone.** Il centro della cella una tile a ovest del Core, a metà del lato ovest (con un Core di lato pari, la cella centrale più in alto) (`Factory.BuilderRestPoint`).
- **Tooltip del cantiere fermo.** "Mancano: N oggetto" mostra quanto manca ancora per **finire** il blocco sulla voce che lo ferma (`ConstructSite.Missing.Amount`), non la prossima unità. Il denaro si scrive "Mancano: $N", come gli altri costi (`World.SyncSiteTooltip`).
- **Smontaggio trascinato.** Tenendo premuto il tasto destro e trascinando, il client invia `Remove` una sola volta per blocco in ogni trascinamento (`World.RemoveOnceInDrag`). Ripassare su un blocco non annulla il piano appena creato.
- **Coda nell'HUD.** `BuildQueuePanel` sta nella colonna in alto a sinistra dell'HUD, sotto le risorse del Core (`CorePanel.PlaceBelowStock`).

## Conseguenze

- **`InstantBuild` per test, demo e seed di sviluppo.** `GameRules.InstantBuild` completa i piazzamenti nel tick del comando, con costo intero pagato subito e rimozione istantanea, come prima. Lo usano `NewGame.Demo` (vero per default), i test che non riguardano la costruzione e il seed di sviluppo `SeedCoreDeliveryLine`. Le partite del client usano la costruzione vera.
- **Rimborso come regola della partita.** `GameRules.RefundPercent` vale 100 nella partita base e nella futura sandbox; in campagna è il campo `refundPercent` del livello in `campaign.json` (livelli 1–3 al 100%, dal 4 al 50%). Il rimborso proprio di ogni edificio (`refundPercent` in `content.json`) si moltiplica alla regola. Un cantiere annullato rende sempre il 100% di quanto speso.
- **Salvataggi v11.** Istantanea e salvataggio portano le regole di partita, i droni (posizione `float`, rotazione, coda, cantiere corrente) e i cantieri (celle, progresso, `Spent`, `Refunded`, quota di rimborso). I v10 sono rifiutati con "Salvataggio di una versione precedente: non compatibile.". Il costo di un cantiere non è salvato: si ricalcola dai contenuti al caricamento.
- **Determinismo.** Droni, cantieri e piani si iterano in ordine di id e di coda. Le posizioni `float` del drone rientrano nella regola dell'[ADR 0002](0002-sincronizzazione-ibrida.md): differenze residue rilevate dal checksum e riparate con l'istantanea.
- **Co-op pronto nella forma.** Ogni piano e cantiere ha un proprietario e le celle pianificate sono riservate per tutti i giocatori. Restano fuori più costruttori sullo stesso cantiere e l'"assist" di Mindustry.
- **Client.** Il client legge droni, code e cantieri da `IGameView` (`Builders`, `Sites`, `TryGetSiteAt`, `PlansOf`, `PlanTiles`, `IsPlannedTile`) e reagisce agli eventi `PlanDropped`, `ConstructFinished` e `DeconstructFinished` (`SimHost.EventRaised`). Disegna tutto in un solo livello (`ConstructionLayer`, `DroneVisual`, shader `construct.gdshader`).
- **Costo.** Un blocco nuovo deve passare da `BuildSpec`: id in `BuildSpec.StructureIdFor` e ramo in `Factory.PlaceInstant` (vedi [ARCHITETTURA](../ARCHITETTURA.md#come-aggiungere-un-blocco)).
