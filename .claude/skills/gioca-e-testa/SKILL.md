---
name: gioca-e-testa
description: Gioca a tIndustry con gli strumenti MCP del server tindustry (mcp__tindustry__*) per cercare bug in un'area del gioco o fare le catture di una schermata. Usalo quando ti chiedono di provare, giocare, testare, riprodurre un bug o catturare HUD, palette, nastri o pannelli nel gioco vero o nella simulazione.
---

# Gioca e testa

Strumenti: server MCP `tindustry` (`.mcp.json`), guida completa in `tools/AgentPlay/README.md`, decisione in `docs/decisioni/0008-agenti-che-giocano.md`.

Regole fisse:

- Ogni risposta è `{ok: true, ...}` oppure `{ok: false, codice, messaggio, mancano?}`: leggi sempre `codice` (`RefusalCode` del gioco o codice del canale); `mancano` dice soldi, oggetti o prerequisiti che mancano. `GiocoTerminato` vuol dire che il gioco è uscito (la sessione si chiude da sola); con `PartitaCambiata` la sessione resta viva.
- Il gioco vero gira solo nel profilo temporaneo che crea il server. Mai dati veri del giocatore: un salvataggio da `%LOCALAPPDATA%\tIndustry` va prima copiato in una cartella temporanea.
- Celle del mondo `{x, y}`, punti dello schermo in pixel. Parti dalla cella del Core (`stato` → `core.cella`).
- Chiudi sempre le sessioni (`chiudi`); prima salva e rigioca il diario se serve.
- Se `apri_gioco` risponde `MotoreNonDisponibile`, il messaggio dice cosa fare (progetto da importare, `TINDUSTRY_GODOT`): riferiscilo, non aggirarlo. `godot4 --headless --import` a volte esce con il codice 139 (segfault alla chiusura): l'importazione è comunque valida.

## Ricetta 1: trova bug in un'area del gioco

1. **Scegli il motore.** Regole, economia, costruzione, nastri: `apri_simulazione {misura: 64, seme: 1}` (veloce, deterministica; `costruzioneIstantanea: true` se la costruzione non c'entra, `livello` per la campagna). Interfaccia, input, disegno: `apri_gioco {misura: 64, seme: 1}`. Nel gioco vero metti subito `pausa`.
2. **Guarda.** `stato`; `catalogo` per gli id, i costi e cosa è sbloccato; `trova {cosa: "Iron", cella: <core>}` per i giacimenti o un id di struttura; `area {da, a}` (al massimo 64×64) attorno alla zona da provare: legenda e forme dei nastri (solo curve e T) dicono cosa c'è in ogni cella, `oggetti: true` aggiunge gli oggetti sui nastri.
3. **Agisci come un giocatore.** `piazza {tipo, cella, direzione}` per un blocco, `piazza {tier, da, a}` per una linea di nastri (la stessa funzione del trascinamento, incrocio automatico compreso), `smonta`, `ruota`, `sblocca`, `vendi`, `potenzia_core`. Prova anche i casi limite: bordi della mappa, pareti, acqua profonda, Core, blocchi sovrapposti, soldi finiti, strutture bloccate.
4. **Fai scorrere il tempo.** `avanza {tick}` (60 al secondo) dopo ogni gruppo di azioni; nel gioco vero `esegui {secondi}` se serve il tempo reale.
5. **Controlla.** `eventi` dopo ogni passo (rifiuti, piani scartati, costruzioni finite con cella e struttura; nel gioco vero anche `partitaCaricata` e `inputReale`: qualcuno ha cambiato la partita o usato la finestra), `stato` (risorse, edifici, coda, ultimi rifiuti), di nuovo `area`. Un bug è: un rifiuto con il codice sbagliato o mancante, risorse che non tornano, oggetti fermi o persi sui nastri, una forma di nastro sbagliata, un'eccezione. Nel gioco vero guarda anche `log` e fai una `cattura` (apri il PNG con il tuo strumento per le immagini).
6. **Rendi riproducibile.** `diario {nome: "bug-<breve>"}` dà il `percorso`; `rigioca {percorso}` su una simulazione nuova rifà lo stesso stato fino al tick in cui hai salvato il diario: la sua `impronta` deve essere quella di `stato`. Nel gioco vero `input` e clic di `ui` sono nel diario come non rigiocabili e `rigioca` li elenca in `avvisi`: scrivi a parte quei passi.
7. **Riporta** per ogni bug: motore e apertura (misura, seme, livello), i passi, l'atteso e l'ottenuto con il `codice`, il percorso del diario (riaperto con `conserva: true` se deve restare) e le catture. Poi `chiudi`.

## Ricetta 2: fai le catture di una schermata

1. **Apri il gioco vero** con l'aspetto voluto: `apri_gioco {misura: 64, seme: 1, lingua: "it"|"en", scala: 1, colori: "Normale"|"Daltonici"|"AltoContrasto", conserva: true}`. `conserva` tiene il profilo con le catture dopo `chiudi`.
2. **Prepara la scena** con i comandi: `piazza` gli edifici e i nastri da mostrare, poi `avanza {tick: 600}` perché il drone costruisca (o apri con `livello`/`salvataggio` per una scena già pronta). `avanza` lascia il gioco fermo: le catture restano stabili.
3. **Inquadra.** `telecamera {cella, zoom}` sulla cella da mostrare (zoom 2 per i dettagli, 1 per una zona).
4. **Trova i controlli.** `ui` elenca nome, tipo, testo, suggerimento e rettangolo dei controlli che un clic raggiunge, palette compresa; `ui {clic: "Ricerca"}` (nome, testo o suggerimento) apre pannelli e palette (`NonCliccabile` se è tagliato, "ambiguo" se indica più controlli). Esc in partita apre la Home e Esc la richiude (`Riprendi`): non esce dal gioco. Per il resto `input {tipo: "clic"|"trascina"|"tasto"|"rotella", cella | schermo, ...}`, per esempio `input {tipo: "tasto", tasto: "Escape"}`. Dopo un input `avanza {tick: 2}` perché il frame si aggiorni.
5. **Cattura.** Con un blocco in mano il cursore lascia il suo fantasma rosso sull'ultima cella: prima della cattura `input {tipo: "tasto", tasto: "Escape"}` e `avanza {tick: 2}`. Poi `cattura {percorso: "<nome>.png"}` (nome relativo, finisce in `<profilo>/catture`) e apri il PNG per controllarlo: testi tagliati, sovrapposizioni, colori, segni.
6. **Confronta** con una base, se c'è: `python tools/dev/compare_shots.py <base> <cartella catture>`. Ripeti con un'altra `lingua`, `scala` o `colori` aprendo una sessione nuova.
7. Copia le catture dove servono (per una PR: ramo orfano `pr-media`), poi `chiudi`.
