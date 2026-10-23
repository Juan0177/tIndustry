# Giorno e notte (aspetto 13, parte 1)

- **Specifica**: [2026-10-23-giorno-notte-design](../superpowers/specs/2026-10-23-giorno-notte-design.md)
- **Piano**: [2026-10-23-giorno-notte](../superpowers/plans/2026-10-23-giorno-notte.md)
- **Periodo**: 2026-10-23
- **Esito**: tutto sul ramo `claude/ripresa-task-notturni-5ht4tf` (una PR sola alla fine). Costruzione e test verdi; il client non è stato provato a schermo (vedi «Per il PC di casa»).

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-10-23-giorno-notte/progress.md`). Dove il registro non dice qualcosa qui non è inventato.

## Obiettivo

Sostituire la notte fissa con un tempo del mondo: ciclo di giorno e notte, solo giorno (Pacifica) o solo notte, scelto alla creazione del mondo e, in campagna, per livello. Qui c'è il tempo, non i nemici: gli attacchi veri sono il resto dell'aspetto 13.

## Task e commit

Base del piano: `b0d3780`.

| Task | Contenuto | Commit |
|---|---|---|
| 1 | Simulazione: `DayNightClock`, `GameRules.DayNight`, `FirstBuildTick`, evento `FirstAttackDue`, vista `IGameView`, stato per gli agenti | `eb158c7` |
| 2 | Campagna (`dayNight` per livello) e salvataggi v16 | `bb19309` |
| 3 | Client: Nuova partita, riga del tempo nell'HUD, luce, lampade, playtest | `8d868d4`, correzione `656ba64` |
| 4 | Documenti, STATO, mappa, barre, CHANGELOG e questo diario | `docs: giorno e notte` |

Numeri: periodo 50.400 tick; giorno 0–34.800, tramonto 34.800–36.000, notte 36.000–49.200, alba 49.200–50.400; luce intera da 0 (notte) a 1000 (giorno), lineare nelle sfumature di 1.200 tick; in solo notte il primo attacco a 36.000 tick dal primo blocco.

## Giri di revisione

- Task 1 e 2: revisione senza rilievi bloccanti, solo piccole cose rinviate (sotto).
- Task 3: un giro di correzione. Rilievo importante: le catture dei playtest erano instabili per la riga del tempo nel ciclo. Corretto (`656ba64`): playtest sempre in solo giorno, finestra «Attacco!» dal tick esatto dell'attacco, accessori inutili tolti.

## Decisioni prese in autonomia

1. **Decisione**: il Task 1 adatta col minimo i chiamanti di `GameRules.Night` (salvataggio: `Night` ↔ `AlwaysNight` finché il Task 2 non porta la v16; client: notte se `DayNight == AlwaysNight` finché il Task 3 non usa la luce; `NewGame.Campaign`: `level.Night` → `AlwaysNight`) — così la build resta verde a ogni task — costo: codice ponte per due task.
2. **Decisione**: i playtest del Task 3 si fanno sul PC di casa, non in cloud (niente Godot) — costo: il client non è visto a schermo prima della PR.
3. **Decisione**: in solo notte è notte (luce 0) anche prima del primo blocco; il contatore del primo attacco parte dal primo blocco — è il «sempre notte» della specifica — costo: nessuno.
4. **Decisione**: nel salvataggio `DayNight` resta col nome inglese dell'enum (`Cycle`, `AlwaysDay`, `AlwaysNight`), mentre campagna e diario dell'agente usano `ciclo`, `giorno`, `notte` — i salvataggi non si leggono a mano — costo: due vocabolari per la stessa cosa.
5. **Decisione**: i playtest `ui`, `tutorial` e `agenti` (anche i livelli della campagna) giocano sempre in solo giorno; solo le sonde del tempo impostano le proprie regole — le catture restano stabili — costo: i playtest generali non vedono mai la notte.

## Problemi rinviati

- `FirstBuildTick` e `FirstAttackRaised` fuori da salvataggio, istantanea e checksum (segnalato dalla revisione del Task 1): ripreso nel Task 2, che li ha messi nella v16.
- `AgentJournal.CurrentVersion` da alzare a 3 per la chiave `giornoNotte`: ripreso nel Task 2.
- `World.PlaytestLuci.cs` forzava `AlwaysDay` nel `finally` invece di ripristinare le regole: ripreso nel Task 3.
- Riga vuota mancante in `Factory.cs` (vicino alla riga 503): rinviato, solo estetica.
- `RestoreDayNight` più contorto del necessario; nomi italiani doppi in `AgentRulesSetup` e `CampaignCatalog.DayNightNames`; `Assert.Contains` con elemento nullabile: rinviati, nessun effetto sul gioco.
- Nessun test unitario di `WorldTimeText` (sta nell'assembly di Godot): rinviato.

## Per il PC di casa

Da fare dove c'è Godot (anche in [STATO](../STATO.md)):

- playtest `ui`, `tutorial` e `agenti`, anche in inglese (`TINDUSTRY_PLAYTEST_LINGUA=en`) e con la Scala UI diversa dal 100%;
- la sonda delle luci con `TINDUSTRY_PLAYTEST_EFFETTI=1`;
- a mano: la riga del tempo si ferma con la pausa; il tramonto a velocità dev ×20; le Lampade si accendono sotto metà luce; in solo notte «Primo attacco fra» → «Attacco!» → «Notte»;
- larghezza di «Il tempo parte dal primo blocco» con la Scala UI al 100% e al 200%.

## Verifiche finali

Build della soluzione a 0 errori e 0 avvisi, test della simulazione verdi, test del sito e controllo della Wiki (risultati nel report del Task 4). Il client non è stato lanciato.
