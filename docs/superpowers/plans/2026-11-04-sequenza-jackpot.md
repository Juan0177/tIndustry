# Sequenza del tentativo di jackpot — piano di implementazione

> **Per gli agenti:** SOTTO-SKILL RICHIESTA: superpowers:subagent-driven-development (consigliata) o superpowers:executing-plans, task per task. I passi usano le caselle (`- [ ]`).

**Obiettivo:** un tentativo di jackpot fatto con lo schermo di borsa fa partire una sequenza di 12 s nel mondo (zoom, bot dello schermo, valigetta, «calcolo…», LED, rulli, coriandoli e «JACKPOT»), saltabile tranne col jackpot vinto, senza cambiare la simulazione.

**Architettura:** la logica (passi, tempi, pausa, salto, chiusura) è una classe pura `JackpotSequence` in `src/TIndustry.Shared` (come `TechTreeLayout`: nessuna dipendenza da Godot, provata dai test xUnit). Il client ha un nodo `JackpotSequenceView` che la legge a ogni frame e muove camera, sprite ed effetti; `World` blocca i comandi e nasconde l'HUD quando la sequenza lo chiede.

**Tecnologie:** C# .NET 8 (`src/TIndustry.Shared`, xUnit), client Godot 4.4 .NET, sprite da `tools/art/genera.py`.

**Specifica:** [`docs/superpowers/specs/2026-11-04-sequenza-jackpot-design.md`](../specs/2026-11-04-sequenza-jackpot-design.md)

## Vincoli globali

- Parte **solo** su un `SaleResolved` con `Attempt == true` del giocatore locale (schermo attivo, picco, ≥ 400 pezzi).
- Passi e durate (secondi di tempo reale): Zoom 1, CambioCanale 1, CorsaAlCore 2, Ritorno 2, Calcolo 2, Led 2, Risultato 2; totale **12 s**.
- Salto (clic o Esc): nei passi 1–6 → passo 7 per intero; nel passo 7 → fine. **Ignorato col jackpot vinto** in tutti i passi.
- Pausa del giocatore (Spazio) e perdita del fuoco fermano la sequenza; chiusura immediata con Home, caricamento, partita nuova, schermo smontato.
- Durante la sequenza: HUD nascosto, comandi del mondo bloccati (resta solo il salto); la simulazione va a velocità normale.
- L'esito resta sullo schermo nel mondo 6 s **dalla fine** della sequenza.
- Simulazione, salvataggi e agenti (`vendi`) invariati.
- Regole di sempre (CLAUDE.md): testi da `testi.csv` it+en, stili dal tema e colori da `UiColors` nelle tre varianti, sprite solo da `genera.py`, commenti in inglese, commit in italiano con `Co-Authored-By:`, STATO + mappa + barre.

## Punti da sorvegliare in revisione

1. **Comandi che sfuggono al blocco**: tasti dei pannelli (T, M, G, I…), rotella, tasto centrale, Q, F5/F6 durante la sequenza non devono agire sul mondo; Esc salta, non apre la Home — prove nel Task 3.
2. **Ritorno della visuale**: dopo la sequenza (anche saltata o chiusa) la camera torna al drone con lo zoom di prima, senza scatti — Task 3.
3. **Velocità dev ×20 e pausa**: la sequenza dura 12 s di tempo reale a ogni velocità; in pausa si ferma — test in Task 1, prova in Task 3.
4. **Tentativo dell'agente durante una sequenza**: quella in corso si chiude e la nuova parte; il ponte aspetta la fine — Task 4.
5. **Schermo smontato o partita caricata a metà**: niente riferimenti a nodi liberati, HUD e comandi tornano normali — Task 3.

---

### Task 1: Logica della sequenza

**File:** nuovo `src/TIndustry.Shared/Economy/JackpotSequence.cs`; test nuovi `tests/TIndustry.Shared.Tests/JackpotSequenceTests.cs`

**Interfacce prodotte:**
- `enum JackpotStep { Zoom, CambioCanale, CorsaAlCore, Ritorno, Calcolo, Led, Risultato, Finita }`
- `sealed class JackpotSequence` costruita con `(SaleResolved sale)`; `Advance(double realSeconds)` (ignorato in pausa); `bool Paused { get; set; }`; `bool TrySkip()` (false col jackpot vinto o se finita); `void Close()`; `JackpotStep Step`; `double StepProgress` (0–1); `double Elapsed`; `bool HidesHud`, `bool BlocksWorldInput` (veri finché non è `Finita`); `bool Finished`; `SaleResolved Sale`; costanti delle durate (`StepSeconds(JackpotStep)`, `TotalSeconds = 12`).
- [ ] **Passo 1: test che falliscono**: durate dei sette passi e totale 12; `Advance` porta da un passo al successivo esattamente ai confini (0,999 → Zoom, 1,0 → CambioCanale); un `Advance` grande (es. 30 s) arriva a `Finita` senza saltare l'ordine; `Paused` ferma `Elapsed`; `TrySkip` in un passo 1–6 → `Risultato` con progresso 0; in `Risultato` → `Finita`; col jackpot vinto `TrySkip` è false e il passo non cambia; `Close` → `Finita` subito; `HidesHud`/`BlocksWorldInput` falsi a `Finita`; un `Advance` negativo o NaN non fa danni.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** build 0/0, test verdi.
- [ ] **Passo 5: commit** `feat(mercato): logica della sequenza del tentativo di jackpot`.

### Task 2: Sprite del bot dello schermo, valigetta e coriandoli

**File:** `tools/art/blocchi/borsa.py` (o un modulo nuovo accanto), `tools/art/test_genera.py`, PNG nuovi in `godot/assets/` (bot in 2.5D: fermo, volo a 2 fotogrammi, sorpreso, con valigetta, lancio; valigetta; coriandoli in pochi pezzetti), `godot/assets/ATTRIBUTION.md` solo se serve
- [ ] **Passo 1:** test in `test_genera.py` (misure dei fogli, numero di fotogrammi, sfondo trasparente, bot riconoscibile come lo schermo: stessi colori della cornice).
- [ ] **Passo 2:** implementazione, `python tools/art/genera.py`, `--controlla --pixel` pulito, test verdi.
- [ ] **Passo 3: commit** `feat(arte): bot dello schermo di borsa, valigetta e coriandoli`.

### Task 3: Client — la sequenza nel mondo

**File:** nuovo `godot/scripts/world/JackpotSequenceView.cs` (+ `.uid` scritto a mano), `World.cs`/`World.Commands.cs` (avvio al `SaleResolved` con `Attempt`, al posto dell'avviso del jackpot per i tentativi), `World.Input.cs` (blocco dei comandi, clic/Esc come salto), `WorldCamera.cs` (zoom sullo schermo e ritorno al drone), `MarketScreenVisual.cs` (grafico al rallentatore, «calcolo…», LED che lampeggiano, esito 6 s dalla fine), HUD (nascosto), `UiColors.cs`/`UiTheme.cs` (scritta JACKPOT, coriandoli), `godot/lingue/testi.csv`
**Interfacce consumate:** `JackpotSequence` del Task 1; sprite del Task 2; `IGameView.MarketScreen`, `SaleResolved`.
- [ ] **Passo 1:** client: avvio, passi, rulli dell'incasso (cifre che girano e si fermano sul valore vero), coriandoli e «JACKPOT» col jackpot, suggerimento «clic o Esc per saltare» (assente col jackpot vinto), pausa con Spazio e perdita del fuoco, chiusura con Home, caricamento, partita nuova, schermo smontato; HUD e comandi normali dopo.
- [ ] **Passo 2:** build 0/0, `TestiTests` verdi, `genera.py --controlla --pixel` pulito.
- [ ] **Passo 3 (PC di casa):** a mano: tentativo perso saltato, tentativo perso intero, jackpot vinto (salto ignorato), ×20, pausa, Home a metà, i tre colori.
- [ ] **Passo 4: commit** `feat(client): sequenza del tentativo di jackpot nel mondo`.

### Task 4: Agenti e playtest

**File:** `godot/scripts/dev/AgentBridge.cs` (aspetta la fine della sequenza prima del comando successivo; un tentativo nuovo chiude quella in corso), playtest `ui` in `godot/scripts/dev/` (passo nuovo: tentativo preparato con schermo attivo e picco, cattura a metà e sul risultato, salto di un tentativo perso), `tools/AgentPlay/README.md` (una riga: nel gioco vero il «Vendi tutto» di un tentativo aspetta la sequenza)
- [ ] **Passo 1:** ponte e playtest; build 0/0; test AgentPlay verdi.
- [ ] **Passo 2 (PC di casa):** playtest `ui`, `tutorial`, `agenti` con catture nuove.
- [ ] **Passo 3: commit** `feat(dev): la sequenza del jackpot nel ponte degli agenti e nel playtest`.

### Task 5: Documenti, stato e diario

**File:** `docs/wiki/Mercato-come-una-borsa.md` (la sequenza), `godot/README.md` (comandi durante la sequenza), `docs/ARCHITETTURA.md` (`JackpotSequence` e la vista), `CHANGELOG.md`, `docs/STATO.md` (aspetto 6: 2b approvata e attuata; controlli del PC di casa), mappa, barre, diario `docs/diario/2026-11-04-sequenza-jackpot.md`, specifica «attuata», commento in #268.
- [ ] **Passo 1:** testi coi numeri dei vincoli.
- [ ] **Passo 2:** `python tools/sito/stato.py`, `python tools/dev/mappa_canvas.py`, `python tools/dev/controlla_wiki.py`, test del sito.
- [ ] **Passo 3: commit** `docs: sequenza del tentativo di jackpot`.
