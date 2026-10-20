# Sito "fabbrica" — ripartenza da zero

- **Data**: 2026-10-19
- **Per chi**: la sessione che riprende il lavoro sul sito di GitHub Pages.
- **Stato**: da progettare. Il lavoro del 18 ottobre non si riprende: si rifà tutto partendo da `main` di oggi.

## Cosa c'è da fare

Riprogettare il sito pubblicato su GitHub Pages (`tools/sito/`) come una **fabbrica di tIndustry**. La vetrina è una pagina lunga in cui un nastro porta gli oggetti dall'alto fino al Core in fondo, dove c'è il bottone di download. Il lavoro comprende anche una pagina sullo sviluppo del gioco e la pagina degli asset nella veste nuova.

Si comincia con la skill `superpowers:brainstorming` insieme all'utente, poi specifica nuova, piano e implementazione. **Non eseguire il piano del 18 ottobre**: è superato (vedi sotto).

## Da dove partire

| File | Cosa contiene | Come usarlo |
|---|---|---|
| [2026-10-18-sito-fabbrica-design.md](2026-10-18-sito-fabbrica-design.md) | La specifica approvata il 18 ottobre: stile industriale con effetto CRT e strisce da cantiere, logo come titolo, hero a tutto schermo, nastri con curve, ponte, incrocio e selezionatore, catena `iron-ore` → forno → `iron-plate` → assemblatore → `copper-wire` → Core, drone che costruisce i bottoni, potenza, sfondi dei quattro biomi, pagina `sviluppo/` | **Punto di partenza del brainstorming**: sono le idee che l'utente aveva già approvato. Vanno riconfermate una per una, non date per buone. |
| [../plans/2026-10-18-sito-fabbrica.md](../plans/2026-10-18-sito-fabbrica.md) | Il piano in 8 task scritto per quella specifica | **Solo come riferimento.** Le parti sui nastri sono sbagliate per il gioco di oggi. |

I due file sono stati ricostruiti dalla cronologia della sessione del 18. Il ramo locale `sito/fabbrica`, con i Task 1–4 fatti allora, non è mai stato pubblicato su GitHub: **ignoralo**, non serve recuperarlo.

## Cosa è cambiato nel gioco dal 18 ottobre

Il piano vecchio dava per scontate cose che oggi non valgono più:

- **Nastri a 32 fotogrammi per cella**, uno per pixel (`PASSO = 1` in `tools/art/blocchi/logistica.py`), non più 8. La velocità di un fotogramma è `rateItemsPerSecond × 32` fotogrammi al secondo (celle al secondo × fotogrammi per cella). Il client calcola il fotogramma come `(int)(Scroll * count) % count`, dove `count` è il numero di fotogrammi della striscia (`MindustryBeltVisual.cs`). Va letto dallo sprite, non scritto a mano.
- **Pezzi nuovi dei nastri**, per tutti e tre i tier: `-t-anim` (unione a T), `-join-left/right/both-anim` (unioni laterali), `-join-building-left/right/both-anim` (edifici ai lati), più `junction-base` e `junction-bordo` (incrocio ridisegnato). Servono per i rami laterali della vetrina (carbone al forno, rame all'assemblatore) e per l'incrocio.
- **Nastro rinforzato `conveyor-armored`** previsto (Task 10 del piano [nastri come Mindustry](../plans/2026-10-19-nastri-mindustry.md)), non ancora in `main`. Il sito deve prendere i tier dei nastri da `data/content.json`, così quando arriva compare da solo.
- Altri blocchi e grafiche nuovi sono possibili: l'elenco vero è sempre il registro di `tools/art` e `godot/assets/`, mai una lista scritta nella specifica.

## Trappole note

- **Errore già online.** `tools/sito/composti.py` (`_nastro`) calcola ancora `fps = VELOCITA_NASTRO[tier] * 8` e `giro = 8 / fps`. Con 32 fotogrammi i nastri della pagina asset pubblicata scorrono 4 volte più lenti del gioco e ogni giro mostra un quarto della striscia. Va corretto all'inizio, anche in una PR piccola a parte, con un test che legga il numero di fotogrammi dallo sprite.
- **`TINDUSTRY_PLAYTEST_BASI` non esiste.** La specifica vecchia lo cita come fonte di catture; esiste solo `TINDUSTRY_PLAYTEST_BLOCCHI` (`godot/scripts/dev/World.Playtest.cs`). Anche `docs/img/sito/` non c'è in `main`: le catture per il sito fatte dalla sessione «Catture di gioco per il sito» sono rimaste non committate sul PC dell'utente. Se servono catture nuove, si fanno con i playtest (`tools/dev/playtest.sh`, profilo temporaneo) o con il server MCP `tindustry` (skill `gioca-e-testa`), mai con partite e salvataggi reali.
- **I numeri delle ADR non sono consecutivi**: in `docs/decisioni/` ci sono 0001–0006 e 0008, la 0007 manca. Il sito deve leggere i file che ci sono, senza aspettarsi una sequenza.
- **Il diario cresce**: le voci di `docs/diario/` si leggono dall'indice `docs/diario/README.md`, non con date o conteggi fissi nei test.
- **Niente Godot nel cloud.** Una sessione nel cloud ha Python ma non `dotnet` né `godot4`: il sito si costruisce e si prova (`python tools/sito/costruisci.py`, `python -m unittest tools/sito/test_costruisci.py`), le catture del gioco no.

## Regole del progetto da rispettare

Quelle di `CLAUDE.md`. Quelle che contano di più per il sito:

- Si parla con l'utente in italiano; testi del sito, documenti, commit e PR in italiano; commenti nel codice in inglese.
- Tutto il sito è generato dal repository (`python tools/sito/costruisci.py`), mai scritto a mano, e senza caricamenti esterni.
- Sprite solo da `python tools/art/genera.py`: il sito li legge da `godot/assets/` e non ne disegna di suoi.
- GitHub Actions solo a mano: il sito si pubblica col workflow Pages lanciato a mano da `main`. Mai trigger `push` o `pull_request`.
- Una PR per pezzo di lavoro, commit con la riga `Co-Authored-By:` del modello, corpo della PR che finisce con `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
- A fine piano, il diario in `docs/diario/` (formato in `docs/diario/README.md`).

## Ordine consigliato

1. Correggere la velocità dei nastri in `composti.py` (trappola sopra), con PR a parte.
2. Brainstorming con l'utente partendo dalla specifica del 18: cosa tenere, cosa cambiare, cosa aggiungere (pezzi nuovi dei nastri, blocchi nuovi).
3. Specifica nuova in `docs/superpowers/specs/`, approvata dall'utente; poi piano in `docs/superpowers/plans/` con la skill `superpowers:writing-plans`.
4. Implementazione task per task, una PR per task.

## Come avviare la sessione

Avvia la sessione sul ramo `claude/ripresa-task-notturni-5ht4tf`, che contiene questo documento e i due file del 18 (oppure su `main`, dopo averlo unito). Primo messaggio:

> Leggi `docs/superpowers/specs/2026-10-19-sito-fabbrica-ripartenza.md` e segui l'ordine che indica: prima la correzione dei nastri in `composti.py`, poi il brainstorming con me sul sito «fabbrica».
