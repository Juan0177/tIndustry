# Giorno e notte

- **Stato**: attuata (2026-10-23; [diario](../../diario/2026-10-23-giorno-notte.md)). Approvata dall'utente il 23 ottobre 2026.
- **Sostituisce**: `GameRules.Night` (notte fissa, solo aspetto) e `night` dei livelli in `data/campaign.json`.
- **Prepara**: l'aspetto 13 «Attacco e difesa» (nemici solo di notte): qui c'è il tempo, non i nemici.

## Obiettivo

Il tempo del mondo diventa un ciclo di giorno e notte, scelto alla creazione del mondo. Di notte arriveranno gli attacchi, quindi il ciclo deve lasciare un giorno lungo per costruire e una notte più corta per difendersi; chi non vuole attacchi gioca una partita «Pacifica», sempre di giorno.

Successo: alla creazione del mondo si sceglie fra ciclo, solo giorno e solo notte; nel ciclo l'HUD dice quanto manca alla notte (o al giorno); in solo notte dice quanto manca al primo attacco; il tutto è deterministico, salvato e uguale per tutti i giocatori.

## Decisioni prese con l'utente

| Tema | Decisione |
|---|---|
| Opzioni | Nella creazione del mondo due spunte che si escludono: **Solo giorno (Pacifica)** e **Solo notte**. Nessuna spunta = **ciclo normale**. |
| Ciclo | **Giorno 10 minuti, notte 4 minuti**, alba e tramonto sfumati di **20 secondi** (dentro i 10 e i 4 minuti). |
| Inizio | Il tempo parte dal **primo blocco piazzato**: prima è giorno e il conto non scorre. La prima notte arriva **10 minuti** dopo. |
| Solo notte | Sempre notte. Contatore fisso di **10 minuti** dal primo blocco piazzato al **primo attacco**. Finché gli attacchi non esistono, a zero il contatore mostra «Attacco!» per qualche secondo e la simulazione emette l'evento `FirstAttackDue`; poi resta notte. |
| Solo giorno | Sempre giorno, nessun contatore, nessun attacco mai (anche quando arriverà l'aspetto 13). |
| Campagna | **Dipende dal livello**: ogni livello sceglie in `campaign.json`. |
| Controlli | Il contatore si ferma con la pausa e accelera con le velocità dev (è in tick). |

## Regole

### Fasi

Il tempo si misura in tick dal primo blocco piazzato (`t = tick − FirstBuildTick`). Nel ciclo (periodo 14 minuti = 50.400 tick):

| Fase | Da | A | Luce |
|---|---|---|---|
| Giorno | 0:00 | 9:40 | piena |
| Tramonto | 9:40 | 10:00 | da piena a notte |
| Notte | 10:00 | 13:40 | notte |
| Alba | 13:40 | 14:00 | da notte a piena |

Poi si ricomincia dal giorno. Prima del primo blocco è giorno pieno. «Notte» per le regole (e per gli attacchi futuri) vuol dire Tramonto finito e Alba non ancora iniziata: da 10:00 a 13:40.

- **Solo giorno**: sempre Giorno, luce piena.
- **Solo notte**: sempre Notte, luce di notte; `FirstAttackDue` una sola volta a `t = 10:00`.

### HUD

Una riga sotto le risorse del CORE:

- prima del primo blocco: «Il tempo parte dal primo blocco»;
- ciclo, di giorno: «Notte fra m:ss»; di notte: «Giorno fra m:ss»;
- solo notte: «Primo attacco fra m:ss», poi «Attacco!» per 5 secondi, poi «Notte»;
- solo giorno: nessuna riga (o «Pacifica»).

### Aspetto

Lo scuro della notte, le luci delle lampade e degli edifici e le lucciole che oggi si accendono con `GameRules.Night` seguono la **luce** della simulazione (0–1000): sfumano nell'alba e nel tramonto invece di scattare.

## Simulazione (`src/TIndustry.Shared`)

- `GameRules`: via `Night`, nuovo `DayNight` (`Cycle`, `AlwaysDay`, `AlwaysNight`), default `Cycle`. I test che non provano il tempo restano indifferenti (il tempo non cambia le regole di gioco finché non ci sono gli attacchi).
- `GameState.FirstBuildTick : long?`: impostato al primo blocco piazzato (cantiere aperto o piazzamento istantaneo; i nastri contano), una volta sola.
- `DayNightClock` (statico, puro, interi): `Phase PhaseAt(GameRules, long? firstBuildTick, long tick)`, `int LightAt(...)` (0 notte, 1000 giorno, lineare nelle sfumature), `long? TicksToNextChange(...)` (notte, giorno o primo attacco), `bool IsNight(...)`.
- Evento `FirstAttackDue` (solo notte, una volta), sistema che lo emette nel tick giusto.
- `IGameView`: `DayNightPhase`, `Light`, `TicksToNextChange`, `DayNight`.
- Agenti: lo stato (`stato`) riporta fase e secondi al prossimo cambio.
- Salvataggi **v16**: `Rules.DayNight` e `FirstBuildTick`; i v15 non si aprono (il gioco non è uscito: stessa regola della v15).

## Campagna (`data/campaign.json`)

`night: true` diventa `dayNight: "notte"`; ogni livello ha `dayNight` fra `"ciclo"`, `"giorno"`, `"notte"`. Proposta finché non rifacciamo la campagna: livelli 1–3 `giorno` (si impara in pace), 6 e 10 `notte` (come oggi), gli altri `ciclo`.

## Client (`godot/`)

- Nuova partita (`NewGamePanel`): due spunte che si escludono, «Solo giorno (Pacifica)» e «Solo notte», con una riga che spiega la scelta («Ciclo: giorno 10 min, notte 4 min»).
- HUD: la riga del tempo; la notte e le luci dalla `Light` della simulazione.
- Testi in `testi.csv` (it, en).

## Test

- `PhaseAt`/`LightAt`/`TicksToNextChange` ai bordi: prima del primo blocco, 9:39, 9:40, 9:50 (luce a metà), 10:00, 13:40, 14:00 (secondo giorno), un secondo giro.
- Solo giorno e solo notte; `FirstAttackDue` una volta sola a 10:00 in solo notte, mai negli altri modi.
- `FirstBuildTick` col primo nastro, col primo edificio, non con un comando rifiutato; non cambia dopo.
- Salvataggio v16 avanti e indietro (con e senza `FirstBuildTick`); v15 rifiutato.
- Campagna: ogni livello ha un `dayNight` valido.
- Determinismo: checksum uguale con il ciclo che attraversa una notte.

## Fuori ambito

Nemici, difese e Game Over (aspetto 13); un tasto dev per saltare al tramonto (si può aggiungere dopo se serve nelle prove).
