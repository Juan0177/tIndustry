# Il drone sei tu (aspetto 11)

- **Specifica**: [2026-11-03-drone-giocatore-design](../superpowers/specs/2026-11-03-drone-giocatore-design.md)
- **Piano**: [2026-11-03-drone-giocatore](../superpowers/plans/2026-11-03-drone-giocatore.md)
- **Periodo**: 2026-11-03
- **Esito**: tutto sul ramo `claude/ripresa-task-notturni-5ht4tf`. Simulazione e test verdi; il client non è stato provato a schermo (vedi «Per il PC di casa»).

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-11-03-drone-giocatore/progress.md`). Dove il registro non dice qualcosa qui non è inventato.

## Obiettivo

Si gioca come in Mindustry: il giocatore è il suo drone. Lo muove con WASD, la visuale lo segue, il drone costruisce e smonta solo entro il suo raggio, scava a mano i giacimenti e porta i pezzi al CORE. Niente più strumenti: il clic seleziona o configura, il destro smonta.

## Task e commit

Base del piano: `bf35cca`.

| Task | Contenuto | Commit |
|---|---|---|
| 1 | Il drone si muove con l'ingresso (`SetDroneInput`) e costruisce solo nel raggio | `368a50f` |
| 2 | Scavo a mano e scarico nel CORE (`SetMining`, `HandMiningSystem`) | `9d9c5eb`, correzione `e65dbcc` |
| 3 | Salvataggi v19 e comandi `muovi` e `scava` degli agenti | `cfd69f5` |
| 4 | Client: WASD, comandi come Mindustry, niente strumenti | `2d74ba1` |
| 5 | Client: piani fuori raggio, scavo a mano, stiva | `0732adf`, correzione `baec14e` |
| 6 | Documenti, stato e diario | questo commit |

## Giri di revisione

- **Task 1**, **Task 3** e **Task 4**: revisione senza rilievi.
- **Task 2** (un giro, 5 rilievi corretti): mancava il rifiuto a stiva piena; lo scavo fuori raggio restava in pausa invece di annullarsi; lo scavo sotto un edificio era permesso; niente scavo mentre il drone costruisce; clamp della stiva e nota sulla distanza nella specifica.
- **Task 5** (un giro, 5 rilievi corretti): il rilascio dell'ingresso prima del salvataggio faceva girare un tick anche in pausa e mancava su Menu ed Esci; corretto rilasciando al caricamento. Inoltre ponte degli agenti senza tick fra le fette, laser fuori raggio, flag riusati, sonda nel playtest `ui`.

## Decisioni prese in autonomia

Decisione: il Task 1 adatta test e scenari degli agenti al drone che non vola da solo (movimento col comando o drone messo vicino), i playtest Godot si adattano nel Task 4 — costo: playtest non affidabili fra il Task 1 e il Task 4.
Decisione: i Task 1–2 salvano lo stato nuovo con campi nuovi senza alzare la versione, il Task 3 porta la v19 — costo: campi spostati nel Task 3.
Decisione: playtest del client solo sul PC di casa — costo: il client non è stato visto a schermo.
Decisione: il comando `muovi` degli agenti arriva nel Task 3; fino ad allora un agente non costruisce lontano dal CORE — costo: agenti limitati per due task.
Decisione: scavo come Mindustry — annullato fuori raggio, sotto un edificio o senza giacimento; in pausa mentre il drone costruisce; rifiuto a stiva piena — costo: nessuno.
Decisione: la distanza dello scarico si misura dal centro del drone al bordo più vicino del CORE (scritto nella specifica) — costo: col CORE più grande lo scarico arriva un po' più lontano.
Decisione: M resta il Mercato (la «mappa grande» della specifica non esiste); la rotella ruota il blocco scelto se orientabile, altrimenti zoom; il clic su un blocco mostra le informazioni già presenti al passaggio del mouse; `Q` usa il tasto fisico — specifica allineata in questo commit — costo: nessuno.
Decisione: un ingresso del drone salvato si rilascia al caricamento (se nessun tasto è premuto), non al salvataggio — costo: un salvataggio contiene ancora l'ingresso tenuto (innocuo per il client; un agente che apre quel salvataggio e manda `muovi` nello stesso frame perde una volta la tenuta).
Decisione: lo scarico a mano non conta in `CoreDeliveredItems` (che è dei nastri) — costo: gli obiettivi di consegna non contano i pezzi portati a mano.

## Problemi rinviati

- `FirstInRange` scorre tutta la coda a ogni tick senza cantiere.
- Coda che oscilla con un piano bloccato nel raggio e piani lontani (mandare in fondo solo se un altro piano è nel raggio); `PlanCentre` morto in `Factory.cs`.
- Nel gioco vero `muovi {cella}` può girare fino a 36000 tick in un frame: serve un limite più basso per il motore di gioco; le fette di `muovi` superano `maxTick` e fermano il drone un tick per fetta.
- Il playtest `PlaytestFlyDrone` usa `GD.PrintErr` (non fa fallire); `FreeLook` non finisce con `muovi {cella}`.
- Il testo `blocco.dettaglio` («destro = elimina») da aggiornare; salvataggio automatico con un tasto tenuto (rilasciare prima); clic tenuto con pannello aperto (innocuo).

## Per il PC di casa

- Importare il progetto (`--import`) e scrivere a mano `World.PlaytestDrone.cs.uid`.
- Lanciare i playtest `ui`, `tutorial` e `agenti` e `ui` con `TINDUSTRY_PLAYTEST_EFFETTI=1`; rifare le catture di riferimento; controllare i passi nuovi (drone, piano fuori raggio, clic tenuto).
- Scheda del tutorial alla Scala UI al 150%.
- A mano: una partita intera, `Q` su AZERTY, `Esc` verso la Home tenendo premuto `W`.

## Carry-over

L'icona `cat-strumenti`, rimasta orfana dopo la palette senza strumenti, è tolta da `tools/art` (sorgente, test, README) e dal repository (PNG e `.import`).

## Verifiche finali

Build 0 errori e 0 avvisi, test della simulazione, degli agenti, dell'arte e del sito verdi (`GenerationTimeBudget` può fallire sotto carico e passa rilanciato da solo), controllo della Wiki.
