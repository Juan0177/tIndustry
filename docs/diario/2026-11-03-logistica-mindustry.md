# Logistica come Mindustry (aspetto 3)

- **Specifica**: [2026-11-03-logistica-mindustry-design](../superpowers/specs/2026-11-03-logistica-mindustry-design.md)
- **Piano**: [2026-11-03-logistica-mindustry](../superpowers/plans/2026-11-03-logistica-mindustry.md)
- **Periodo**: 2026-11-03
- **Esito**: tutto sul ramo `claude/ripresa-task-notturni-5ht4tf`, dentro la PR #294 ancora aperta; chiude le issue #252, #253, #254, #255 e #270. Simulazione e test verdi; il client non è stato provato a schermo (vedi «Per il PC di casa»).

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-11-03-logistica-mindustry/progress.md`). Dove il registro non dice qualcosa qui non è inventato.

## Obiettivo

Incrocio, router, selezionatori ed estrattore si comportano e appaiono come in Mindustry: nessuno ha più una direzione, i filtri si scelgono col clic sul blocco piazzato e i nastri hanno la forma stretta (variante C).

## Task e commit

Base del piano: `9091090`.

| Task | Contenuto | Commit |
|---|---|---|
| 1 | Nastri stretti e sprite dei blocchi logistici (router al posto dello sdoppiatore, selezionatore invertito) | `052c334` |
| 2 | Incrocio come la Junction | `6699cc0`, correzione `d15961f` |
| 3 | Router al posto dello sdoppiatore (id `router`) | `e8cb49a`, correzione `c6a6fd4` |
| 4 | Selezionatore e invertito, comando `SetBlockFilter` | `d1fcc21`, correzione `c9cd83d` |
| 5 | Estrattore come l'Unloader | `0489da3`, correzione `49ce2bb` |
| 6 | Salvataggi alla versione 18 | `0f542d2` |
| 7 | Client: filtri col clic, pannello del filtro | `45b5fa0`, correzione `05b93c6` |
| 8 | Documenti, stato e diario | questo commit |

## Giri di revisione

- **Task 1** e **Task 6**: revisione senza rilievi.
- **Task 2**: minori di fedeltà a Mindustry (niente rimbalzo verso un nastro rivolto all'incrocio, accetta solo con un blocco dall'altra parte, test di smontaggio, minatore, generatore e magazzino, Wiki).
- **Task 3**: le catene di router verso est e sud andavano a un oggetto ogni 9 tick invece di 8; corretto con una passata inversa sui router rifiutati, test esatti, anello, linea trascinata e smontaggio.
- **Task 4**: un solo turno per selezionatore (due ingressi opposti finivano dallo stesso lato) e regola del cambio di turno diversa da Mindustry; corretto con un bit per direzione d'ingresso e il cambio solo quando accettano entrambi i lati.
- **Task 5**: rimbalzo infinito fra magazzini (CORE e deposito, deposito e deposito); corretto con la regola «mai da magazzino a magazzino».
- **Task 7**: la FAQ della Wiki nominava ancora il tasto C; corretta, con il pannello chiuso al cambio di partita e fuori schermo e il pannello del tutorial.

## Decisioni prese in autonomia

Decisione: il Task 1 rinomina sprite e riferimenti del client al router, mentre dati e simulazione restano `splitter` fino al Task 3 — costo: un ponte di un task nel client.
Decisione: i Task 2–5 salvano lo stato nuovo con campi nuovi senza alzare la versione, il Task 6 porta la v18 — costo: campi spostati nel Task 6.
Decisione: playtest del Task 7 solo sul PC di casa — costo: il client non è stato visto a schermo.
Decisione: la suite C# è rimasta rossa (`EveryStructureHasAnIcon`, splitter senza sprite) dal Task 1 al Task 3, che rinomina l'id; il Task 1 non aveva rilanciato i test C# — costo: suite rossa per due task, nessun push su main.
Decisione: un nastro espresso pieno attraverso un incrocio scende a circa 13,8 oggetti/s (6 ogni 26 tick, come Mindustry), anche con l'incrocio automatico — costo: linee espresse più lente agli incroci.
Decisione: la passata di riprova dei router fa muovere un tick prima anche i nastri accanto a un router appena svuotato, necessaria per il ritmo esatto di 8 tick — costo: lieve differenza vicino ai router.
Decisione: l'estrattore non sposta mai da un magazzino (CORE o deposito) a un altro; aggiunto alla specifica — costo: non si può travasare fra depositi con un estrattore (si usa un nastro).
Decisione: il pannello del filtro non conta come pannello modale (`PanelOpen`), altrimenti bloccherebbe i clic sul mondo — costo: possibile sovrapposizione col pannello del tutorial, da controllare nel playtest.

## Problemi rinviati

- La capacità 6 dell'incrocio in `content.json` è duplicata da `JunctionQueueLength`, e `rateItemsPerSecond` non è usato per l'incrocio; `TakesItemsAt` scorre forni e generatori a ogni inserimento.
- La riprova dei router rallenta di un tick un nastro che alimenta un selezionatore.
- Estrattori bloccati senza filtro: ricerca costosa; area degli agenti O(celle × estrattori); ricerca lineare dell'estrattore a ogni frame col pannello aperto.
- Il tasto centrale non copia il filtro del blocco.
- Test dei 5 minuti con un blocco `{ }` superfluo; la partita ripristinata è confrontata solo al minuto 5.
- I salvataggi v17 con splitter e selezionatori perdono oggetti o si caricano come nastri: la v18 li rifiuta comunque.
- Un nastro parallelo al router si disegna come unione; le frecce di `join-left` e `join-right` accanto agli edifici sono da giudicare a schermo.

## Per il PC di casa

- Importare il progetto (`--import`) per i `.import` di `router` e `sorter-inverted` e il `FilterPanel.cs.uid`.
- Lanciare i playtest `ui`, `tutorial` e `agenti` e rifare le catture di riferimento.
- Pannello del filtro con la Scala UI al 150% e al 200%; palette a 3 righe a 1280×720; sovrapposizione col pannello del tutorial.
- A mano: un incrocio fra due linee, una catena di router, un estrattore fra due forni.

## Verifiche finali

Build 0 errori e 0 avvisi, test della simulazione verdi (il test `GenerationTimeBudget` può fallire sotto carico e passa rilanciato da solo), test del sito e controllo della Wiki.
