# 0008 — Agenti che giocano: MCP e canale di controllo

**Stato:** accettata, 2026-10-19

## Contesto

I playtest (`tools/dev/playtest.sh ui|tutorial`) sono copioni scritti a mano: provano sempre gli stessi passi e trovano solo i bug che chi li ha scritti si aspettava. Per le catture e per cercare bug servivano agenti AI (Claude Code e i suoi sotto-agenti) capaci di **giocare davvero**: vedere lo stato, dare i comandi del giocatore, usare l'interfaccia, far scorrere il tempo, fare catture e rendere riproducibile ogni bug trovato. Il tutto senza mai toccare i dati veri del giocatore e senza aprire nel gioco pubblicato una porta di controllo.

Specifica: [2026-10-18-agenti-giocano-design](../superpowers/specs/2026-10-18-agenti-giocano-design.md). Piano: [2026-10-18-agenti-giocano](../superpowers/plans/2026-10-18-agenti-giocano.md). Guida d'uso: [tools/AgentPlay/README.md](../../tools/AgentPlay/README.md).

## Opzioni

1. **Server MCP in C# (`tools/AgentPlay`) che riusa `TIndustry.Shared` e parla col gioco su un canale locale** (scelta). Claude Code avvia da solo un server MCP via stdio; la simulazione gira nello stesso processo, il gioco vero si comanda con un ponte dentro il client.
2. **Server MCP in Python o TypeScript.** SDK più diffusi, ma la simulazione andrebbe chiamata da fuori o riscritta: niente motore senza grafica, niente `rigioca` con la stessa impronta, due linguaggi per la stessa logica (contro l'[ADR 0001](0001-server-csharp.md)).
3. **Playtest più ricchi senza MCP.** Nessun lavoro nuovo sul protocollo, ma l'agente non vede nulla mentre gioca: scrive un copione, aspetta, legge le catture. Non esplora.

## Decisione

**Un server MCP in C# (`tools/AgentPlay`, SDK ufficiale `ModelContextProtocol` 2.2.0) espone gli stessi strumenti su tre motori; il gioco vero si comanda con il nodo `AgentBridge`, che esiste solo nelle build di sviluppo, solo con un profilo temporaneo e solo su `127.0.0.1` con un segreto.**

- **Perché C#.** Il server riusa `TIndustry.Shared` senza copie: la stessa `SimHost`, gli stessi comandi del giocatore, gli stessi codici di rifiuto e la stessa impronta della partita. La dipendenza MCP vale solo per lo strumento, non per il gioco né per `TIndustry.Shared`, che resta senza rete e senza sistema operativo. `.mcp.json` alla radice avvia `python tools/AgentPlay/avvia.py`, che compila il server in `%TEMP%/tindustry-agentplay-bin/<impronta dei sorgenti>` (mai nei `bin/` del repository, così `dotnet build` funziona col server acceso) e lo lancia con stdin e stdout intatti: stdout è il canale MCP, tutto il resto va su stderr.
- **Tre motori, gli stessi strumenti.** Nomi in italiano, snake_case (`apri_simulazione`, `stato`, `area`, `trova`, `piazza`, `avanza`, `cattura`, `diario`, `rigioca`…).
  - **Simulazione** (`apri_simulazione`): `TIndustry.Shared` nello stesso processo, senza grafica, deterministica. Per regole, comandi, economia, costruzione e riproduzione dei bug.
  - **Gioco vero** (`apri_gioco`): il server avvia `godot4 --path godot` con finestra 1280×720, lingua, scala e colori a scelta, e si collega al suo canale. Per catture, interfaccia e input. Gli strumenti solo del gioco (`esegui`, `riprendi`, `cattura`, `telecamera`, `input`, `ui`, `log`) su una simulazione rispondono `MotoreNonDisponibile`.
  - **Co-op** (`collega_coop`): segnaposto che risponde `MotoreNonDisponibile` fino al sotto-progetto 5; il protocollo del canale è lo stesso che userà il server.
- **Il codice comune sta in Shared.** `src/TIndustry.Shared/Agent/` ha `AgentGame` (stato, area, trova, eventi e comandi su una `SimHost`, usato sia dal server sia dal ponte nel client), `AgentJournal` (diario) e `AgentProtocol` (righe, codici, opzioni JSON). Le partite dell'agente si costruiscono con `AgentGameSetup.CreateHost`, la stessa funzione che usano il client (sandbox di `apri_gioco`) e `rigioca`: un test controlla che dia la stessa partita di *Nuova partita*.
- **Protocollo del canale.** JSON a righe su TCP: una richiesta `{id, metodo, parametri}` per riga, una risposta `{id, ok, risultato}` oppure `{id, ok: false, codice, messaggio}`. La prima riga del client è `{"token": "..."}`; la risposta di benvenuto (id 0) porta `{canale: "tindustry"}`, e il server rifiuta un benvenuto diverso (un altro programma sulla porta). Le richieste vanno eseguite sul thread principale di Godot, tra un frame e l'altro; quelle che cambiano la partita passano da `SimHost.Submit` come quelle del giocatore, gli input simulati da `Input.ParseInputEvent` come quelli veri. Le risposte sono compatte: `area` al massimo 64×64 celle, liste oltre 200 voci troncate con `troncato`. I codici sono quelli del gioco (`RefusalCode`, `SaveLoadError`) più quelli del canale: `SessioneSconosciuta`, `MotoreNonDisponibile`, `ParametriNonValidi`, `Timeout`, `GiocoTerminato`, `ErroreInterno`, e dal ponte `Occupato` (più di 1024 richieste in coda: connessione chiusa) e `NonCliccabile` (`ui {clic}` su un controllo fuori dalla finestra o nascosto).
- **Sicurezza.**
  - Il ponte parte solo se valgono insieme: `OS.IsDebugBuild()`, `TINDUSTRY_AGENT_PORT` valida, `TINDUSTRY_AGENT_TOKEN` non vuoto e `TINDUSTRY_DATA_DIR` assoluto fuori da `%LOCALAPPDATA%\tIndustry` e da `%APPDATA%\Godot\app_userdata\tIndustry` (`AgentBridge.ShouldStart`, con test). Nel gioco esportato non si attiva mai; con il profilo vero del giocatore neanche in sviluppo.
  - Ascolta solo su `127.0.0.1`. Un solo client autenticato alla volta. Il segreto si confronta in tempo costante; un segreto sbagliato, una riga che sembra HTTP (`GET /…`, `Host: …`: una pagina web che prova a parlare alla porta) o una riga oltre 64 KiB chiudono la connessione; chi non manda il segreto entro 5 s viene scollegato.
  - Il server crea il profilo sotto `%TEMP%/tindustry-agentplay/gioco-*` e controlla, prima di creare qualsiasi cartella, che non cada nei dati veri. Godot riceve `TINDUSTRY_DATA_DIR` e `APPDATA` sul profilo e `--log-file <profilo>/godot.log`, come i playtest; le variabili dei playtest (`TINDUSTRY_PLAYTEST`, `TINDUSTRY_FRESH`, `TINDUSTRY_SEED_CORE_LINE`) si tolgono. Un `salvataggio` dato all'agente si copia nel profilo e si legge solo per copiarlo; i file sotto i dati veri sono rifiutati.
  - `cattura` scrive solo sotto `<profilo>/catture`: nome relativo `.png`, niente percorsi assoluti né `..`.
  - Il gioco non sopravvive al server: su Windows è in un job object con `KILL_ON_JOB_CLOSE`, chiuso dal sistema quando il server muore; e il ponte, con `TINDUSTRY_AGENT_ESCI_SENZA_CLIENT=1` (messa solo dal lanciatore), chiude il gioco quando cade la connessione autenticata. `chiudi` fa cadere la connessione, aspetta l'uscita al massimo 5 s (niente attesa se il canale non si era mai aperto), poi chiude il job e cancella profilo e diari, salvo `conserva: true`.
- **Tempo.** Nella simulazione il tempo si muove solo con i comandi: ogni comando esegue il proprio tick e risponde con l'esito (`tickPerComando: true`, registrato nell'intestazione del diario), `avanza N` esegue N tick esatti, `pausa` non fa nulla. Nel gioco vero il tempo scorre da solo: un comando risponde dopo il tick del frame che lo applica, senza tick in più (`tickPerComando: false` nel ponte). Il diario del gioco si scrive comunque con `tickPerComando: true`: nel gioco un comando si applica al tick successivo per mano del frame, nella riproduzione per mano del suo tick e l'intervallo seguente è più corto di uno, quindi la linea del tempo è la stessa; `pausa` e `riprendi` lo fermano e lo fanno ripartire, `avanza` esegue tick esatti e lascia il gioco fermo (catture stabili), `esegui {secondi}` lo lascia scorrere. Tempi limite: 60 s per l'avvio, 30 s per richiesta (`esegui`: 30 s più i suoi secondi). Se il gioco esce o va in crash la risposta è `GiocoTerminato` con le ultime 30 righe del log e la sessione si chiude da sola.
- **Diario e `rigioca`.** Ogni sessione registra i comandi (`piazza`, `smonta`, `ruota`, `sblocca`, `vendi`, `potenzia_core`, `avanza`) con il tick; `diario {nome}` li salva con un'intestazione `partita` (motore, misura, seme, generatore, livello o istantanea del salvataggio, regole, `tickPerComando`). `rigioca` riesegue il diario su una simulazione nuova e restituisce l'impronta della partita: un bug trovato nel gioco vero si riproduce senza grafica e si trasforma in un test xUnit. **Limiti:** gli input simulati (`input`, `ui {clic}`) e quello che fa una persona alla finestra non sono nel diario e non si rigiocano; `esegui` e `riprendi` non sono voci (conta solo il numero di tick trascorsi tra due comandi). Se un bug dipende dall'interfaccia, il diario dà lo stato di partenza e i passi d'input vanno descritti a parte.
- **Dove sta Godot.** Il server cerca `TINDUSTRY_GODOT`, poi `godot4(.exe|.cmd|.bat)` nel PATH. Claude Code su Windows avvia il server con il PATH di Windows, che può non avere le cartelle aggiunte solo da Git Bash (per esempio `~/bin`): `.mcp.json` passa al server la variabile d'ambiente dell'utente (`"TINDUSTRY_GODOT": "${TINDUSTRY_GODOT:-}"`), e senza Godot `apri_gioco` risponde `MotoreNonDisponibile` con le istruzioni esatte. Nessun percorso di una macchina sta nel repository. Con un `.cmd` i percorsi di progetto e profilo non possono contenere `& | ^ % ! < > "`: in quel caso serve l'`.exe`.

### Scelte prese con l'utente

- **Tre motori con gli stessi strumenti**, scelti dall'agente secondo il compito; co-op come segnaposto fino al sotto-progetto 5.
- **Nel gioco vero comandi e input**: comandi di gioco e input simulato (clic, trascinamenti, tasti, rotella, controlli dell'interfaccia per nome).
- **Tecnologia libera purché facile per le AI**: server MCP in C#.

### Scelte prese durante l'attuazione

- **Un tick per comando nella simulazione**, così la risposta porta l'esito; nel gioco nessun tick in più.
- **`ruota` gira il pennello dell'agente**: la simulazione non ha ancora la rotazione sul posto (arriva col piano dei nastri alla Mindustry).
- **Lanciatore in Python** (`avvia.py`, Python serve già per `tools/art`) che compila in una cartella temporanea per impronta: `dotnet run` nel repository bloccava i `bin/` e scriveva su stdout.
- **Segreto e rifiuto delle righe HTTP** dopo la revisione: `127.0.0.1` da solo non ferma una pagina web aperta nel browser.
- **Job object e uscita senza lanciatore**: un server ucciso non lascia finestre di Godot aperte.
- **Il diario di una sessione di gioco sta nella sua cartella `diari`**, cancellata da `chiudi` salvo `conserva`: si rigioca o si copia prima di chiudere.

## Conseguenze

- **Un comando nuovo del giocatore** va anche in `AgentGame` (e quindi nel diario) se gli agenti devono usarlo; un `RefusalCode` nuovo arriva agli agenti da solo.
- **Un'opzione nuova del client** usata da `apri_gioco` (variante di colori, lingua, limiti della scala) va anche in `GameLauncher`; `GameOptionsMatchTheClient` fallisce finché non è allineata.
- **I test del gioco vero** (`GameEngineTests`) aprono finestre e girano solo con Godot e il progetto compilato; in CI si saltano con il motivo. Il playtest `agenti` prova il ponte senza il server.
- **Rimandi**: un figlio del wrapper `.cmd` nato nei microsecondi prima dell'ingresso nel job resta fuori (lo chiude comunque l'uscita senza lanciatore); una richiesta partita proprio mentre il gioco chiude il canale può aspettare il suo tempo limite prima di `GiocoTerminato`; quando i Core partiranno in un punto casuale (biomi-2) l'intestazione del diario dovrà portare la posizione del Core.
