# Sequenza del tentativo di jackpot (specifica 2b)

- **Specifica**: [2026-11-04-sequenza-jackpot-design](../superpowers/specs/2026-11-04-sequenza-jackpot-design.md)
- **Piano**: [2026-11-04-sequenza-jackpot](../superpowers/plans/2026-11-04-sequenza-jackpot.md)
- **Periodo**: 2026-11-04
- **Esito**: tutto sul ramo `claude/ripresa-task-notturni-5ht4tf`, issue [#268](https://github.com/Juan0177/tIndustry/issues/268). Logica, sprite, agenti e test verdi; il client e il playtest sono compilati ma non provati a schermo (vedi «Per il PC di casa»).

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-11-04-sequenza-jackpot/progress.md`) e dai rapporti dei task. Dove il registro non dice qualcosa qui non è inventato.

## Obiettivo

Il «Vendi tutto» che tenta il jackpot con lo schermo di borsa attivo diventa un momento da guardare: sequenza di 12 secondi nel mondo, in 7 passi (zoom senza HUD, cambio canale, corsa del bot al CORE con la valigetta, ritorno, lancio e «calcolo…», LED, risultato con incasso a rulli; col jackpot coriandoli e «JACKPOT»). Si salta con un clic o Esc, tranne col jackpot vinto; Spazio la mette in pausa. La simulazione non cambia.

## Task e commit

Base del piano: `10676e6`.

| Task | Contenuto | Commit |
|---|---|---|
| 1 | `JackpotSequence` in Shared: passi, durate, avanzamento col tempo reale, pausa, salto, chiusura | `e32d074` |
| 2 | Arte: bot dello schermo (5 animazioni), valigetta e coriandoli da `genera.py` | `111ce0c` |
| 3 | Client: `JackpotSequenceView`, camera con `BeginFocus`/`EndFocus`, vetro e LED guidati dal passo, blocco dell'input, testi, variante `ScrittaJackpot` | `1858a82`, correzioni `87967a2` |
| 4 | Agenti e playtest: attesa del ponte (`sequenza`, `sequenzaInCorso`), passo `sequenza-jackpot` del playtest `ui` | `907b714` |
| 5 | Documenti, stato e diario | commit `docs: sequenza del tentativo di jackpot` |

## Giri di revisione

- **Task 1, Task 2, Task 4**: revisione senza rilievi da correggere (solo minori, sotto).
- **Task 3** (un giro, 7 rilievi corretti): l'importante era che bot e valigetta finivano sotto gli edifici (ZIndex 10): ora `JackpotSequenceZ = ConstructionZ + 1`. Inoltre `MarketPrices.UnitIncome` pubblico al posto della formula duplicata, «In pausa» davanti all'overlay e «JACKPOT» nascosto in pausa, adattamento del testo al vetro una volta sola (`FitCache`), il salto ignorato nei primi 0,2 s (test nuovo), tolta la divisione per `Engine.TimeScale`.
- **Task 5**: la sua revisione si fonde nella revisione finale (sotto).

## Decisioni prese in autonomia

Decisione: `JackpotSequence` sta in `src/TIndustry.Shared/Economy` (come `TechTreeLayout` in Research), senza dipendenze da Godot e provata da xUnit — costo: un pezzo di presentazione nella libreria condivisa.
Decisione: i Task 3 e 4 si fanno in cloud senza Godot (build 0/0, `TestiTests`); prove a mano e playtest sul PC di casa — costo: errori visivi trovati solo a casa.
Decisione: la revisione del Task 5 (documenti) si fonde nella revisione finale — costo: un controllo in meno sui documenti.
Decisione (Task 2): i coriandoli nel client si colorano col pezzo bianco tinto (`Modulate`) con i colori `UiColors` dei LED, così seguono le tre varianti — costo: i pezzi colorati del foglio restano solo per la variante Normale.
Decisione (Task 3): con un pannello aperto (Ricerca, campagna, impostazioni, salvataggi) all'arrivo del tentativo la sequenza non parte e resta il toast; un pannello aperto durante la sequenza la chiude — costo: due comportamenti da controllare a casa.
Decisione (Task 3): nei playtest la sequenza parte solo con `World.PlaytestJackpotSequence`, e la perdita del fuoco non la ferma nei playtest né col ponte degli agenti (le loro finestre spesso non hanno il fuoco) — costo: la pausa per il fuoco non è provata dai playtest.
Decisione (Task 3, dopo la revisione): si ignora il salto nei primi 0,2 s del passo 1 (doppio clic su «Vendi tutto») — costo: un salto un po' ritardato.
Decisione (Task 3): il Mercato si chiude all'avvio e non si riapre alla fine, così si vedono i 6 s dell'esito sul vetro — costo: un clic in più per riaprirlo.
Decisione (Task 4): durante la sequenza il ponte tiene le risposte dei comandi e fa passare solo `input`; attesa massima 20 s reali (`AgentProtocol.GameJackpotSequenceWaitSeconds`), `vendi` +40 s — costo: ogni richiesta al gioco vero ha 20 s di margine in più.
Decisione (Task 4): `GameState.ForPlaytestForceJackpotRoll(bool?)` pubblico, sopra il gancio interno dei test, per forzare l'estrazione nel passo del playtest — costo: un metodo pubblico che non deve mai usare il gioco.
Decisione (Task 4): il passo `sequenza-jackpot` è l'ultimo del playtest `ui` (sul primo livello della campagna), così nessuna cattura di prima cambia — costo: tre catture nuove da approvare.

## Problemi rinviati

- `JackpotSequence`: `Starts` scritto a mano accanto a `Durations` (calcolarlo come somma); test mancanti: salto da Zoom, jackpot vinto che ignora il salto in Risultato, salto in pausa (Task 1).
- Arte: bocca del bot sorpreso che sembra un sorriso, corpo non inclinato in volo, coriandoli piccoli da guardare a ×2 nel client (Task 2).
- Client: «In pausa» portato davanti solo all'avvio; il pannello del tutorial forse visibile sopra la sequenza; popup che ricevono l'input prima di `World._Input`; `World.Commands.cs` aggiunge i campi del Task 4 alla vista del Task 3 (Task 3).
- Agenti: ordine delle risposte nel fotogramma in cui finisce l'attesa; un avviso di livello completato potrebbe finire nella cattura `ui-jackpot-dopo`; +40 s di attesa su ogni `vendi`; il secondo clic del playtest è controllato dopo un solo fotogramma (Task 4).
- Tre frasi su «Staccato dal CORE» (Economia-e-Ricerca, `godot/README.md`, CHANGELOG) ammorbidite qui, come parcheggiato dal piano precedente (Task 5).

## Per il PC di casa

- Importare il progetto (`--import`) per i `.import` dei PNG nuovi (`market-bot-idle-anim`, `market-bot-fly-anim`, `market-bot-surprised-anim`, `market-bot-carry-anim`, `market-bot-throw-anim`, `market-briefcase`, `market-confetti-anim`) e per i `.uid` nuovi di `JackpotSequenceView.cs` (`uid://cjq5xtr2bw4ke`) e `World.PlaytestJackpot.cs` (`uid://b7jkq3pw2xnfa`), scritti a mano: controllare che Godot li accetti; `--build-solutions`; ripristinare i `.import` che cambiano solo per fine riga. Nessun avviso «Sprites: manca …market-bot…».
- `python tools/art/genera.py --controlla` byte per byte: in cloud vale solo `--pixel` (Pillow diverso). Se segnala i PNG di questo piano, rigenerarli con `python tools/art/genera.py --solo` seguito dai nomi.
- Playtest `ui` (PLAYTEST OK, 0 ERROR, riga `PLAYTEST step ok: sequenza-jackpot`), `tutorial` e `agenti` (non devono cambiare). `compare_shots.py`: le catture vecchie restano uguali; da guardare e aggiungere ai riferimenti `ui-jackpot-calcolo`, `ui-jackpot-risultato` e `ui-jackpot-dopo`. In `ui-jackpot-dopo` può comparire un avviso di livello completato (il passo vende 500 piastre sul primo livello): se il passo fallisce o la sequenza si chiude, spostare la vendita su un oggetto che non completa il livello o salvare e ripristinare la sessione.
- Il clic al centro arriva davvero a `TrySkip` (nessun overlay che prende il mouse); doppio clic su «Vendi tutto» che non salta.
- Le 10 prove a mano del Task 3:
  1. importazione e avvio senza errori;
  2. tentativo perso e saltato: clic nei passi 1–6 porta al risultato (2 s), un secondo clic chiude; HUD, comandi e camera tornano normali;
  3. tentativo perso e intero: circa 12 s, zoom morbido, bot che esce, «!», volo al CORE con il numero dei pezzi, ritorno, lancio, «calcolo…», LED, rulli che si fermano sull'incasso giusto, camera che torna sul drone con lo zoom di prima, esito sul vetro per 6 s dalla fine;
  4. jackpot vinto: clic ed Esc ignorati, niente suggerimento, coriandoli, luci gialle negli angoli, «JACKPOT» grande;
  5. ×20 (F4 con `TINDUSTRY_DEV=1`): sempre circa 12 s reali;
  6. pausa con Spazio (ferma bot, rulli e coriandoli, mostra «In pausa») e con Alt+Tab;
  7. Home a metà (Esc salta, non apre la Home), F9/F7, caricamento dalla Home dopo la fine, partita nuova, schermo smontato durante: chiusura immediata senza nodi residui;
  8. le tre varianti di colore: LED, coriandoli e «JACKPOT» nei colori della variante;
  9. il CORE sopra, sotto, a destra e a sinistra dello schermo: bot mai sopra il vetro a riposo, volo visibile, tutto inquadrato; Scala UI al 150% per suggerimento e scritta;
  10. FPS come prima.
- Con gli agenti (`apri_gioco`): schermo a contatto col CORE, un oggetto in picco con almeno 400 pezzi, `vendi {tutto: true}` risponde dopo circa 12 s con `sequenza: true`; una `cattura` mandata intanto risponde dopo la fine; un `input` clic in parallelo salta.

## Verifiche dei task

Ultimo stato riportato dai task: build Release 0 errori e 0 avvisi (progetto Godot compreso); `TIndustry.Shared.Tests` 1213 superati e 2 saltati (`JackpotSequenceTests` e `TestiTests` verdi); `TIndustry.AgentPlay.Tests` 29 superati e 10 saltati; `test_genera.py` 50 OK; `genera.py --controlla --pixel`: 267 file aggiornati, 0 da rigenerare. Nessun playtest (manca Godot in cloud).

## Revisione finale

Pronta da unire (0 critici, 0 importanti, 5 minori). Verificati: partenza solo coi tentativi veri del giocatore locale con lo schermo, comandi bloccati in `World._Input` (Esc salta, non apre la Home), visuale e HUD rimessi a posto da ogni uscita, nessun nodo appeso, tempo reale, ponte degli agenti sempre con un limite di 20 s, aggancio del playtest che non tocca le partite normali, la modifica al test del sito legittima, documenti uguali a specifica e codice. Corretti in `80dadbb`: la frase su `sequenzaInCorso` nel README degli agenti, «se un pannello si apre» al posto di «se apri un pannello», `WorldCamera.ResetFocus()` chiamato da `FocusCameraOnCore` (caricamento subito dopo la sequenza), `Starts` calcolato da `Durations` e tre test in più (salto da Zoom dopo la guardia, jackpot vinto in Risultato, salto in pausa). Ri-revisione: tutto a posto.

Resta (deciso, da guardare a casa): lo zoom della sequenza è fisso a 2, quindi chi era già più vicino vede la visuale allontanarsi un poco.
