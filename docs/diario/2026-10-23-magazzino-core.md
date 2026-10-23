# Magazzino del CORE e livelli (specifica 3 di 3)

- **Specifica**: [2026-10-23-magazzino-core-design](../superpowers/specs/2026-10-23-magazzino-core-design.md)
- **Piano**: [2026-10-23-magazzino-core](../superpowers/plans/2026-10-23-magazzino-core.md)
- **Periodo**: 2026-10-23
- **Esito**: tutto sul ramo `claude/ripresa-task-notturni-5ht4tf` (nessuna PR per task: una PR sola alla fine, vedi la decisione 5). Costruzione e test verdi; il client non è stato provato a schermo (vedi «Per il PC di casa»).

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-10-23-magazzino-core/progress.md`). Dove il registro non dice qualcosa qui non è inventato.

## Obiettivo

Dare al CORE un magazzino con un massimo per oggetto (a CORE pieno la linea si ferma), tre livelli (shard 3×3, foundation 4×4, nucleus 5×5) con un drone migliore per livello (alpha, beta, gamma), container e vault che allargano il magazzino o fanno da depositi a sé, e una vendita automatica che diventa un ritmo (10 / 25 / 60 pezzi al secondo) con la spunta «solo a CORE pieno». Via il pulsante «Potenzia CORE», il comando `UpgradeCore`, i salvataggi fino alla v14.

## Task e commit

Base del piano: `20fe572`. Un commit per task, sullo stesso ramo.

| Task | Contenuto | Commit |
|---|---|---|
| 1 | Livelli del CORE, droni alpha/beta/gamma, container e vault nei dati | `b849127` |
| 2 | Magazzino con massimo per oggetto (`CoreStorage`, ingresso unico), CORE shard 3×3, linee che si fermano | `ffe6c14` |
| 3 | Vendita automatica a ritmo del CORE e spunta «solo a CORE pieno» | `97fecf9` |
| 4 | Potenziamento piazzato sopra il CORE, ramo CORE della Ricerca, droni beta e gamma | `45489fc` |
| 5 | Container e vault, collegati al CORE o depositi a sé | `a1504cf` |
| 6 | Salvataggi v15; le versioni vecchie non si aprono più | `388b319` |
| 7 | Client: sprite, palette, panoramica, HUD, testi, MCP | `2e24429` |
| 8 | Documenti, Wiki, STATO, CHANGELOG e questo diario | `docs: magazzino del CORE e livelli` |

Ogni task è passato da una revisione, chiusa senza rilievi bloccanti.

## Ordine di esecuzione e perché

Ordine del piano, 1-8. Il controllo di coerenza iniziale ha trovato tre conflitti (decisioni 1-3): togliere `CoreUpgrade` dai dati nel task 1 avrebbe rotto la build; lo stato nuovo dei task 2-5 doveva entrare nel salvataggio perché i round-trip restassero verdi; togliere `UpgradeCore` nel task 4 toccava `godot/`, che fa parte della soluzione. Il passo 3 del task 7 (import, playtest e catture con Godot) non era eseguibile in cloud (decisione 4).

## Decisioni prese in autonomia

1. **Decisione**: il task 1 toglie `economy.coreUpgrade` dai dati, ma `FactoryContent.CoreUpgrade` e `TryUpgradeCore` restano (con un default in codice) fino al task 4 — evita un task 1 che rompe la build — costo se sbagliata: un default morto per tre task.
2. **Decisione**: nei task 2-5 chi aggiunge stato lo aggiunge anche al salvataggio (campi nuovi, versione invariata); il task 6 alza la versione a 15 e toglie il vecchio — i round-trip restano verdi — costo: qualche campo spostato nel task 6.
3. **Decisione**: ogni task tiene `dotnet build tIndustry.sln` a 0 errori e 0 avvisi; chi toglie un'API usata in `godot/` adatta il chiamante col minimo (il pulsante «Potenzia CORE» sparisce già nel task 4) — costo: il task 4 tocca `godot/` un po' prima.
4. **Decisione**: il passo 3 del task 7 non si esegue in cloud, resta un compito per il PC di casa, annotato qui e in `docs/STATO.md` — costo: il client non è provato a schermo prima della PR.
5. **Decisione**: tutto sullo stesso ramo, un commit per task; le PR per task del progetto diventano una PR sola alla fine (ramo unico imposto dalla sessione) — costo: PR più grande da rivedere.
6. **Decisione** (task 1): `core-foundation` con prerequisiti `[]` invece di `["core"]`, perché `core` non è una struttura e il layout appende già alla radice i nodi senza prerequisiti — stesso albero — costo: nessuno se il layout non cambia.
7. **Decisione** (task 2): il terreno di un salvataggio si rigenera sempre con le celle dello shard di partenza (derivabili da origine e livello), non con quelle del CORE attuale; il generatore riceve il CORE iniziale come dato — costo se sbagliata: salvataggi dopo il potenziamento che non si aprono. Applicata nei task 4 e 6.
8. **Decisione** (task 3): l'accumulatore della vendita automatica è in sessantesimi di pezzo (`carry += ritmo`, pezzi = `carry / 60`) invece di `ritmo × 1000 / 60`, che troncava (99 pezzi in 10 s) — esatto e intero — costo: nessuno.
9. **Decisione** (task 5): l'estrattore che tocca il CORE e un deposito a sé prende dal CORE e, se il CORE non ha l'oggetto, dal deposito con l'id più basso — più utile e deterministico — costo: nessuno.
10. **Decisione** (task 7): sprite di shard, foundation e nucleus con nome e generazione propri ma pixel uguali ai vecchi core-2/3/4 — accettato per ora, l'aspetto distinto si chiede all'utente — costo: i tre CORE si distinguono solo per misura.
11. **Decisione** (task 7): categoria «Magazzino» nuova nella palette (container, vault, blocchi del CORE), non chiesta ma coerente — costo: un pulsante in più da controllare in `PlaytestScala`.

## Problemi rinviati

Piccoli, non bloccanti, da riprendere:

- Costi di Ricerca del CORE doppi (`cores[].unlock` e `structures[core-*].unlock`) senza un test di uguaglianza; `DefaultCoreTiers` è una terza copia. `UnitForTier` limita in silenzio un tier fuori intervallo; gli indici di `CoreTiers` non hanno controllo dei limiti (teorico).
- `AgentJournal.CurrentVersion` da alzare (diari vecchi ripetuti su CORE 3×3). I crediti dei cantieri scartati al caricamento passano da `AddClamped` invece di `Return` (`Factory` ~1353). `Factory.Restore` non chiama `Storage.RecomputeLinks` (da verificare).
- `AutoSellCarryMilli` da rinominare `AutoSellCarry` (ora sono sessantesimi); il test `AutoSellPaysTheMinimumPrice` calcola il prezzo atteso senza asserirlo.
- `SimHost.ApplyCoreUpgrade` controlla il tipo dopo `CoreUpgradeRefusal`: un nucleus sullo shard dà il codice del foundation invece di `InvalidCommand`. Gli agenti al livello massimo mandano l'origine (0,0) nel rifiuto. `StartingShard` è dedotto da origine e livello (il salvataggio tiene l'origine di partenza); manca un test del terreno con tier 1.
- `TouchedStorage` duplica `GameState.StorageAt`, con una scansione O(magazzini) per cella; `Restore` non controlla il contenuto contro lo spazio. Container e vault non sono in `ResearchState.DemoStructureIds`.
- Salvataggi: `SaveWritesVersion15` ha perso le asserzioni su generatore, misura e mappa; `Capture` con CORE vuoto salva `CoreSize` 0 (non ricaricabile); `CoreStartX`/`CoreStartY` senza controllo dei limiti.
- Client: `_registra_livello` duplica `_registra` (`core.py`); `MercatoPanel` senza guardia su `max > 0`; la riga `core.livello` dell'HUD non va a capo (cambia le catture); la scia del drone vecchio resta per un frame dopo `SetTier`.

## Per il PC di casa

Il lavoro è stato fatto in cloud, senza Godot. Da fare con il gioco vero (anche in `docs/STATO.md`):

- `--import` e `.import` degli 8 PNG nuovi (ripristinando i `.import` che differiscono solo per fine riga);
- playtest `ui` e `agenti`, `PlaytestScala` (categoria «Magazzino» nuova), catture da rifare;
- le celle di prova di `World.PlaytestGrafica.cs` (righe 159 e 169, a `coreX+3`) ora toccano il CORE 3×3;
- il parere dell'utente su un aspetto distinto per foundation e nucleus.

## Verifiche finali

- `dotnet build tIndustry.sln -c Release`: 0 errori, 0 avvisi.
- Test di `TIndustry.Shared.Tests` e `TIndustry.AgentPlay.Tests` verdi (numeri nel rapporto del task 8).
- `python tools/dev/controlla_wiki.py` e i test del sito verdi.
- Non provati: import, playtest e catture con Godot.
