# Agenti che giocano: strumenti MCP e canale di controllo

- **Stato**: da approvare (progetto approvato a voce il 2026-10-18)
- **Decisioni collegate**: [ADR 0001 — Server C#](../../decisioni/0001-server-csharp.md), [ADR 0002 — Sincronizzazione ibrida](../../decisioni/0002-sincronizzazione-ibrida.md) (comandi via `SimHost`)

## Obiettivo

Permettere agli agenti AI (Claude Code e i suoi sotto-agenti) di **giocare davvero** a tIndustry, per fare catture e trovare bug meglio dei playtest scritti a mano: vedere lo stato, dare comandi, usare l'interfaccia, far scorrere il tempo, fare catture, e rendere riproducibile ogni bug trovato.

## Decisioni prese con l'utente

| Tema | Decisione |
|---|---|
| Motori | Lo stesso insieme di strumenti con tre motori, scelti dall'agente secondo il compito: **solo simulazione** (senza grafica), **gioco vero** (client Godot), **co-op** (in futuro, SP5). |
| Azioni nel gioco vero | **Comandi e input**: comandi di gioco (piazza, smonta, sblocca…) e input simulato (clic, trascinamenti, tasti, controlli dell'interfaccia per nome). |
| Tecnologia | Libera, purché facile da usare per le AI: scelto **un server MCP in C#** (`tools/AgentPlay`), che riusa la simulazione. |

## Progetto

### Pezzi

- **`tools/AgentPlay`**: server MCP .NET via stdio, avviato da Claude Code. Registrato in `.mcp.json` alla radice del repository (nome `tindustry`, comando `dotnet run --project tools/AgentPlay`). Usa la libreria MCP ufficiale per .NET (versione fissata nel piano). La dipendenza vale solo per lo strumento, non per il gioco né per `TIndustry.Shared`.
- **Sessione**: ogni partita aperta dall'agente ha un id; più sessioni possono stare aperte insieme. Ogni sessione ha un profilo temporaneo suo, cancellato alla chiusura salvo `conserva: true`.
- **Motore simulazione**: `TIndustry.Shared` nello stesso processo (partite con `NewGame`, campagna da `data/`, caricamento di un salvataggio), senza grafica.
- **Motore gioco vero**: il server avvia Godot (`godot4 --path godot`, finestra 1280×720, profilo temporaneo come i playtest, lingua e scala a scelta) con `TINDUSTRY_AGENT_PORT` e vi si collega. Lato Godot un nodo **`AgentBridge`** esiste solo nelle build di sviluppo (`OS.IsDebugBuild()`) e solo con la variabile impostata. Ascolta solo su `127.0.0.1` ed esegue le richieste sul thread principale, tra un frame e l'altro.
- **Motore co-op**: segnaposto che risponde "non ancora disponibile" fino all'SP5; il protocollo è già pensato per collegarsi a un server.

### Strumenti

| Gruppo | Strumenti | Motori |
|---|---|---|
| Sessioni | `apri_simulazione` (misura, seme, generatore, livello di campagna o salvataggio), `apri_gioco` (gli stessi più lingua, scala, colori), `collega_coop` (segnaposto), `sessioni`, `chiudi` | — |
| Vedere | `stato` (soldi, risorse del Core, energia, ricerca, obiettivi, conteggi degli edifici, coda di costruzione, ultimi rifiuti con codice), `area` (celle di una zona, al massimo 64×64: suolo, parete, giacimento, edificio, direzione e forma dei nastri, in righe compatte), `trova` (minerali o edifici per tipo, più vicini a una cella), `eventi` (cosa è successo dall'ultima chiamata) | tutti |
| Agire con i comandi | `piazza` (un blocco, una linea di nastri o un trascinamento con la stessa logica del giocatore), `smonta`, `ruota`, `sblocca`, `vendi`, `potenzia_core`: tutti tramite `SimHost`, con esito e codice di rifiuto | tutti |
| Tempo | `avanza` (N tick esatti), `esegui` (S secondi in tempo reale, solo gioco vero), `pausa`, `riprendi` | tutti |
| Solo gioco vero | `cattura` (PNG su file, restituisce il percorso), `telecamera` (posizione, zoom), `input` (clic, trascinamento, tasto, rotellina su coordinate dello schermo o su una cella del mondo), `ui` (controlli visibili con nome, testo e rettangolo; clic per nome), `log` (righe di avviso ed errore di Godot) | gioco |
| Riproduzione | `diario` (azioni della sessione con il tick, su file), `rigioca` (riesegue un diario su una sessione nuova) | tutti |

### Protocollo del canale

JSON a righe su TCP locale: una richiesta `{id, metodo, parametri}` per riga, una risposta `{id, ok, risultato | codice, messaggio}` per riga. Lo stesso formato servirà per il co-op. Le richieste che cambiano il gioco passano da `SimHost` come quelle del giocatore. Gli input simulati passano dalla stessa via dell'input reale di Godot (`Input.ParseInputEvent`), così si prova l'interfaccia vera.

### Tempo e determinismo

- **Simulazione**: parte in pausa; `avanza N` esegue N tick esatti. A parità di seme, numero di partenza e comandi il risultato è identico.
- **Gioco vero**: di default il tempo scorre. `pausa` e `avanza` congelano la simulazione come fanno già i playtest (`PlaytestHeld`), per avere catture stabili.
- **Diario**: ogni sessione registra comandi e input con il tick. `rigioca` riesegue il diario su una sessione nuova, così un bug trovato da un agente è riproducibile e si può trasformare in un test xUnit o in un passo di playtest.

### Risposte pensate per le AI

- Brevi e strutturate, con `ok` oppure `codice` e `messaggio`. I codici sono quelli del gioco (`RefusalCode`, `SaveLoadError`); i testi nella lingua scelta.
- Coordinate in celle per il mondo e in pixel per lo schermo, sempre dette nel nome del campo.
- `area` limitata a 64×64 celle per chiamata; liste lunghe troncate con un conteggio, per non riempire il contesto.
- Le catture restituiscono il percorso del file: l'agente lo apre con il proprio strumento per le immagini.

### Errori e sicurezza

- Se il gioco non parte o non risponde entro un tempo limite, la sessione si chiude con un messaggio chiaro e le ultime righe del log. Un crash di Godot viene riportato allo stesso modo.
- Il canale non esiste nella versione esportata (controllato da un test). Ascolta solo su `127.0.0.1`.
- Sempre profili temporanei: mai `%LOCALAPPDATA%\tIndustry` né `%APPDATA%\Godot\app_userdata\tIndustry`. Godot si avvia come nei playtest, con `APPDATA` sul profilo.

### Come lo usano gli agenti

- `tools/AgentPlay/README.md`: guida breve, con quando usare la simulazione e quando il gioco vero, il ciclo tipico (apri, guarda, agisci, avanza, cattura, chiudi) e le regole di sicurezza.
- Una sezione in `CLAUDE.md` che rimanda alla guida.
- Uno skill di progetto **gioca-e-testa** con due ricette: "trova bug in un'area del gioco" e "fai le catture di una schermata".

## Verifica

- xUnit sul motore simulazione: apri, comandi, avanza, stato, `rigioca` che dà lo stesso stato (impronta della partita) del diario originale.
- Test d'integrazione che avvia il server MCP via stdio, elenca gli strumenti e chiama i principali sul motore simulazione.
- Playtest nuovo: il gioco col canale attivo riceve comandi, input e una cattura dal protocollo e si chiude pulito.
- Controllo che nella versione esportata `AgentBridge` non si attivi.
- **Primo uso reale**: un agente gioca una partita breve con questi strumenti, fa le catture di HUD, palette e nastri, prova l'unione a T e l'incrocio automatico, e riporta i bug trovati.

## Documenti

Nuovo **ADR 0008 — Agenti che giocano: MCP e canale di controllo**. Aggiornati `docs/ARCHITETTURA.md` (motori, canale, sicurezza), `CLAUDE.md`, `CHANGELOG.md` e la mappa Obsidian.

## Fuori ambito

- Collegamento a una partita co-op vera (arriva con l'SP5; resta il segnaposto).
- Registrazioni video.
- Agenti che giocano da soli per ore senza una richiesta.
