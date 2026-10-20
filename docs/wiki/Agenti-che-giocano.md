# Agenti che giocano

Il server MCP `tindustry` permette agli agenti AI (Claude Code e i suoi sotto-agenti) di giocare a tIndustry per cercare bug e fare catture, con gli stessi comandi del giocatore.

## Perché

I playtest sono copioni scritti a mano e trovano solo i bug che chi li ha scritti si aspettava. Un agente che **gioca davvero** vede lo stato, dà comandi, usa l'interfaccia, fa scorrere il tempo, cattura lo schermo e rende riproducibile ogni bug trovato. Decisione e motivazioni: [ADR 0008](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0008-agenti-che-giocano.md).

## Come parte

Il server è in C# (`tools/AgentPlay`, SDK `ModelContextProtocol`) e riusa `TIndustry.Shared`. Claude Code lo avvia da solo grazie a `.mcp.json` alla radice del repository, che lancia `python tools/AgentPlay/avvia.py`. Il lanciatore compila il server in una cartella temporanea per impronta dei sorgenti (mai nei `bin/` del repository) e lo avvia via stdio.

La prima partenza dopo un cambio dei sorgenti compila ed è più lenta; si può preparare prima:

```bash
python tools/AgentPlay/avvia.py --prepara
```

Servono `python` e `dotnet` nel PATH. Per il gioco vero servono anche Godot e il progetto importato e compilato (vedi [Compilare e avviare](Compilare-e-avviare)). Se il server non trova `godot4`, imposta la variabile d'ambiente utente `TINDUSTRY_GODOT` sull'eseguibile di Godot: `.mcp.json` la passa al server. Non scrivere percorsi della tua macchina in `.mcp.json`.

## Tre motori, gli stessi strumenti

| Motore | Strumento | Quando |
|---|---|---|
| Simulazione | `apri_simulazione` | Regole, comandi, economia, costruzione, riproduzione dei bug. Senza grafica, veloce, deterministica |
| Gioco vero | `apri_gioco` | Catture, interfaccia, input. Apre Godot in una finestra 1280×720 |
| Co-op | `collega_coop` | Segnaposto: risponde `MotoreNonDisponibile` |

Strumenti principali (nomi in italiano):

| Gruppo | Strumenti |
|---|---|
| Sessioni | `apri_simulazione`, `apri_gioco`, `sessioni`, `chiudi` |
| Guardare | `stato`, `area`, `trova`, `catalogo`, `eventi` |
| Agire | `piazza`, `smonta`, `ruota`, `sblocca`, `vendi`, `potenzia_core` |
| Tempo | `avanza` (tick esatti), e nel gioco vero `esegui`, `pausa`, `riprendi` |
| Solo gioco vero | `cattura`, `telecamera`, `input`, `ui`, `log` |
| Riprodurre | `diario`, `rigioca` |

Ogni risposta è `{ok: true, ...}` oppure `{ok: false, codice, messaggio, mancano?}`, con i codici del gioco (`RefusalCode`, `SaveLoadError`) o del canale. `diario` salva i comandi con il loro tick; `rigioca` li riesegue su una simulazione nuova e restituisce l'impronta della partita, così un bug trovato nel gioco vero si riproduce senza grafica e diventa un test xUnit.

## Sicurezza e dati del giocatore

- Il gioco vero gira **sempre** in un profilo temporaneo creato dal server sotto la cartella temporanea di sistema: `TINDUSTRY_DATA_DIR`, `APPDATA` e il log puntano lì. Non va aggirato: niente `TINDUSTRY_AGENT_PORT` impostata a mano su un profilo vero.
- Un salvataggio dato all'agente si copia nel profilo; i file sotto i dati veri (`%LOCALAPPDATA%\tIndustry`, `%APPDATA%\Godot\app_userdata\tIndustry`) sono rifiutati. Per usare un salvataggio vero, copialo prima in una cartella temporanea.
- Il canale di controllo dentro il gioco (`godot/scripts/dev/AgentBridge.cs`) parte solo in una build di sviluppo, con porta, segreto e profilo temporaneo, e ascolta solo su `127.0.0.1`: nel gioco esportato non esiste.

## Skill gioca-e-testa

La skill di progetto [`gioca-e-testa`](https://github.com/Juan0177/tIndustry/blob/main/.claude/skills/gioca-e-testa/SKILL.md) dà agli agenti due ricette pronte: **trovare bug in un'area del gioco** (scegli il motore, guarda, agisci come un giocatore, fai scorrere il tempo, controlla, salva il diario e rigiocalo, riporta) e **fare le catture di una schermata** (apri il gioco con lingua, scala e colori, prepara la scena, inquadra, cattura, confronta con una base).

## Prove

```bash
dotnet test tests/TIndustry.AgentPlay.Tests -c Release --no-build   # avvia il server via stdio
tools/dev/playtest.sh agenti /tmp/shots-agenti                      # prova il canale del gioco senza il server
```

I test del gioco vero (`GameEngineTests`) aprono Godot e si saltano, con il motivo, se Godot o il progetto importato mancano.

## Per approfondire

- [tools/AgentPlay/README.md](https://github.com/Juan0177/tIndustry/blob/main/tools/AgentPlay/README.md): guida completa a strumenti, parametri e risposte
- [ADR 0008 — Agenti che giocano](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0008-agenti-che-giocano.md)
- [docs/ARCHITETTURA.md, Agenti che giocano](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md#agenti-che-giocano)
- [Test e playtest](Test-e-playtest) · [Architettura](Architettura) · [Compilare e avviare](Compilare-e-avviare)
