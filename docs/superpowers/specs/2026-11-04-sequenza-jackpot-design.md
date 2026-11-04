# Sequenza del tentativo di jackpot — specifica 2b

- **Stato**: attuata (2026-11-04, ramo claude/ripresa-task-notturni-5ht4tf; [diario](../../diario/2026-11-04-sequenza-jackpot.md)). Approvata dall'utente il 4 novembre 2026 ([piano](../plans/2026-11-04-sequenza-jackpot.md)). Brainstorming con l'utente del 4 novembre 2026; issue [#268](https://github.com/Juan0177/tIndustry/issues/268). Aspetto 6 «Mercato ed economia» di `docs/STATO.md`.
- **Le specifiche del Mercato**: 1 [Mercato come una borsa](2026-10-22-mercato-borsa-design.md), 2a [Schermo di borsa](2026-11-04-schermo-borsa-design.md), 3 [Magazzino del CORE](2026-10-23-magazzino-core-design.md), tutte attuate; **2b** questa.
- **Sostituisce**: l'avviso del jackpot di oggi (il toast con l'incasso) per i tentativi fatti con lo schermo; l'esito sullo schermo della 2a resta.

## Obiettivo

Il «Vendi tutto» che può vincere il jackpot diventa un momento da guardare: la visuale si avvicina allo schermo di borsa, il bot dello schermo corre al CORE con la valigetta, lo schermo calcola, i LED lampeggiano e l'incasso gira su rulli; col jackpot coriandoli e una grande scritta «JACKPOT». La fabbrica intanto continua a lavorare.

Successo: un tentativo di jackpot fa partire la sequenza di circa 12 secondi e finisce sull'esito giusto; un tentativo perso si può saltare, uno vinto no; la simulazione e gli agenti non cambiano.

## Decisioni prese con l'utente

| Tema | Decisione |
|---|---|
| Quando | **Solo nei tentativi di jackpot**: «Vendi tutto» con lo schermo attivo, nel picco, con almeno 400 pezzi (`SaleResolved.Attempt`). Gli altri «Vendi tutto» mostrano solo l'esito sullo schermo, come nella 2a. |
| Durata e salto | **Circa 12 s.** Un clic o Esc salta al risultato, **tranne quando il jackpot è vinto**: allora il salto non funziona (che possa far intuire il jackpot è accettato: chi non ha fretta non salta). |
| Scena | **Nel mondo, con zoom** sullo schermo vero accanto al CORE; l'HUD sparisce; la fabbrica intorno continua a lavorare. |
| Bot | **Un bot dello schermo**, solo grafica: esce dallo schermo, va al CORE e torna. Il drone del giocatore resta dov'è. |
| Durante | **Solo guardare**: comandi del mondo bloccati (WASD, clic, palette, tasti dei pannelli), resta solo il salto. La simulazione va a velocità normale; Spazio mette in pausa anche la sequenza, che riparte da dove era. |

## La sequenza

Tempo reale del client (non tick della simulazione), così le velocità di sviluppo non la accorciano; si ferma con la pausa del giocatore.

| Passo | Durata | Cosa si vede |
|---|---|---|
| 1. Zoom | 1 s | L'HUD sparisce, la visuale va sullo schermo con uno zoom più vicino |
| 2. Cambio canale | 1 s | Il bot cambia canale sull'oggetto venduto ed è sorpreso («!» sopra la testa) |
| 3. Corsa al CORE | 2 s | Il bot vola al bordo del CORE più vicino e riempie la valigetta; sopra compare il numero dei pezzi |
| 4. Ritorno | 2 s | Il bot torna di fretta; sullo schermo il grafico va avanti al rallentatore e si ferma sul prezzo del clic |
| 5. Lancio e «calcolo…» | 2 s | Il bot lancia la valigetta nello schermo; sullo schermo «calcolo…» con i puntini che crescono |
| 6. LED | 2 s | La striscia dei LED lampeggia a turno blu, viola e giallo, poi si ferma sul colore dell'esito |
| 7. Risultato | 2 s | L'incasso gira su rulli e si ferma sulla cifra vera (come il prezzo del Mercato nel racconto del sito, `tools/sito/racconto.js`). Jackpot: coriandoli, luci gialle negli angoli della cornice e scritta «JACKPOT» grande in mezzo allo schermo del gioco |

Totale 12 s. Poi la visuale torna sul drone del giocatore (con l'inseguimento morbido di sempre) e l'HUD riappare. L'esito resta sullo schermo nel mondo per i 6 secondi della 2a, contati dalla fine della sequenza.

### Salto e interruzioni

- Clic o Esc durante i passi 1–6: si salta al passo 7, mostrato per intero (2 s). Durante il passo 7: si chiude subito. Con il jackpot vinto il salto è ignorato in tutti i passi.
- La sequenza si chiude subito, senza animazione, se si apre la Home, si carica una partita, si inizia una partita nuova o lo schermo viene smontato; l'esito resta comunque nella riga di stato e, se lo schermo c'è, sul vetro.
- Perdita del fuoco della finestra: la sequenza va in pausa come con Spazio e riparte al ritorno.
- Un secondo tentativo non può partire durante la sequenza (i comandi sono bloccati); se ne arrivasse uno da un agente, la sequenza in corso finisce e quella nuova parte da capo.

## Architettura

- **Logica senza Godot**: classe nuova `JackpotSequence` in `src/TIndustry.Shared` (come `TechTreeLayout`: nessuna dipendenza da Godot, provata dai test xUnit) con passi, durate, avanzamento col tempo reale, pausa, salto e regola del jackpot non saltabile. Riceve l'esito (`SaleResolved` + posizione dello schermo e del CORE) e dà a ogni istante: passo, avanzamento 0–1 del passo, se l'HUD è nascosto, se i comandi sono bloccati.
- **Client Godot**: un nodo `JackpotSequenceView` che legge `JackpotSequence` e muove camera (zoom e ritorno), bot, valigetta, numeri, LED, rulli, coriandoli e scritta; `World` blocca i comandi del mondo e nasconde l'HUD quando la sequenza lo chiede.
- **Simulazione**: nessun cambiamento. Il risultato è già deciso al clic (`SaleResolved`).

## Grafica e testi

- Sprite da `tools/art/genera.py`: bot dello schermo in 2.5D (fermo, in volo, sorpreso, con la valigetta, lancio), valigetta, coriandoli (pezzetti nei colori dei LED). Animazioni a fotogrammi come gli altri blocchi.
- Colori da `UiColors` (LED, coriandoli) nelle tre varianti; la scritta «JACKPOT» con una variante del tema se serve.
- Testi in `testi.csv` (it + en): «calcolo…», «JACKPOT», il numero dei pezzi, l'incasso, il suggerimento «clic o Esc per saltare».

## Agenti e playtest

- Gli agenti non vedono la sequenza (vive solo nel client); `vendi` risponde come oggi.
- Nel gioco vero comandato da un agente (`apri_gioco`) la sequenza parte come per il giocatore; il ponte aspetta che finisca prima del comando successivo, così le catture sono stabili.
- Playtest `ui`: un passo nuovo prepara un tentativo (schermo attivo, picco, pezzi), cattura a metà sequenza e sul risultato, prova il salto di un tentativo perso; servono catture di riferimento nuove sul PC di casa.

## Test

- `JackpotSequence`: durate dei passi e totale 12 s; avanzamento con il tempo; pausa e ripresa; salto ai passi 1–6 → passo 7; salto al passo 7 → fine; salto ignorato col jackpot vinto; chiusura immediata; HUD nascosto e comandi bloccati solo durante la sequenza.
- Client: build 0/0, `TestiTests` verdi, `genera.py --controlla --pixel` pulito; prove a mano e playtest sul PC di casa.

## Fuori ambito

Suoni (il gioco non ne ha ancora); la campagna rifatta (aspetto 8); una sequenza per i «Vendi tutto» che non sono tentativi di jackpot.
