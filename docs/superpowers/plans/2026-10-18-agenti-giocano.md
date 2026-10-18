# Agenti che giocano — piano di implementazione

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** strumenti MCP con cui un agente AI apre una partita (simulazione senza grafica o gioco vero), la guarda, la gioca con comandi e input, gestisce il tempo, fa catture e rende riproducibili i bug.

**Architecture:** la logica comune (protocollo, metodi di gioco su `SimHost`/`IGameView`, diario) sta in `src/TIndustry.Shared/Agent/`, così la usano sia il server MCP sia il client Godot. `tools/AgentPlay` è il server MCP via stdio: motore simulazione nello stesso processo, motore gioco tramite un canale JSON a righe su TCP locale verso il nodo `AgentBridge` del client (solo build di sviluppo con `TINDUSTRY_AGENT_PORT`). Il motore co-op è un segnaposto.

**Tech Stack:** .NET 8, `TIndustry.Shared`, libreria MCP ufficiale per .NET (pacchetto NuGet `ModelContextProtocol`, ultima versione stabile al momento del Task 2, fissata nel csproj), System.Text.Json, Godot 4.4.1 .NET, xUnit.

**Spec:** [docs/superpowers/specs/2026-10-18-agenti-giocano-design.md](../specs/2026-10-18-agenti-giocano-design.md)

## Global Constraints

- `TIndustry.Shared` resta senza dipendenze da Godot o dal sistema operativo; niente rete nel codice Shared (il trasporto sta in `tools/AgentPlay` e in `godot/`).
- Il canale del gioco esiste solo con `OS.IsDebugBuild()` **e** `TINDUSTRY_AGENT_PORT` impostata; ascolta solo su `127.0.0.1`. Nella versione esportata non si attiva mai.
- Profili sempre temporanei (come `tools/dev/playtest.sh`: `TINDUSTRY_DATA_DIR`, `APPDATA` sul profilo, `--log-file`); mai `%LOCALAPPDATA%\tIndustry` né `%APPDATA%\Godot\app_userdata\tIndustry`.
- Ogni azione che cambia la partita passa da `SimHost.Submit` (stessi comandi del giocatore); l'input simulato passa da `Input.ParseInputEvent`.
- Risposte compatte: `{ok:true, ...}` oppure `{ok:false, codice, messaggio}`; codici del gioco (`RefusalCode`, `SaveLoadError`) e codici del canale (`SessioneSconosciuta`, `MotoreNonDisponibile`, `Timeout`, `ParametriNonValidi`, `GiocoTerminato`); `area` al massimo 64×64 celle; liste oltre 200 voci troncate con `troncato: N`.
- Nomi degli strumenti MCP in italiano, snake_case, come nella specifica: `apri_simulazione`, `apri_gioco`, `collega_coop`, `sessioni`, `chiudi`, `stato`, `area`, `trova`, `eventi`, `piazza`, `smonta`, `ruota`, `sblocca`, `vendi`, `potenzia_core`, `avanza`, `esegui`, `pausa`, `riprendi`, `cattura`, `telecamera`, `input`, `ui`, `log`, `diario`, `rigioca`.
- Testi in italiano (descrizioni degli strumenti comprese), commenti in inglese, commit con `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`, GitHub Actions solo a mano.

## Review Focus

1. **Gioco che si blocca o muore a metà** (Godot crasha, finestra chiusa a mano): lo strumento successivo risponde `GiocoTerminato` con le ultime righe del log, entro il timeout, senza appendere l'agente. Test nel Task 4.
2. **Richieste mentre la simulazione avanza** nel gioco vero: nessuna lettura di stato a metà tick (le richieste si eseguono tra un frame e l'altro). Test nel Task 3 (raffica di `stato` durante `esegui`).
3. **Coordinate fuori mappa o area troppo grande**: `area` 1000×1000 o celle negative → `ParametriNonValidi`, mai un'eccezione che chiude il server. Test nel Task 1.
4. **Profilo temporaneo che resta o dati reali toccati**: dopo `chiudi` la cartella del profilo non esiste (salvo `conserva`); nessun percorso sotto i dati reali. Test nel Task 4.
5. **Diario non riproducibile** (ordine dei comandi nello stesso tick, input del gioco vero): `rigioca` di un diario della simulazione dà lo stesso `Checksum`; per il gioco vero il diario registra solo i comandi di gioco con il tick, non gli input grezzi. Test nel Task 1.

---

### Task 1: nucleo comune degli agenti in Shared e motore simulazione

**Files:**
- Create: `src/TIndustry.Shared/Agent/AgentProtocol.cs` (richiesta e risposta, codici del canale), `src/TIndustry.Shared/Agent/AgentGame.cs` (metodi di gioco su un `SimHost`), `src/TIndustry.Shared/Agent/AgentJournal.cs` (diario)
- Test: `tests/TIndustry.Shared.Tests/AgentGameTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record AgentRequest(long Id, string Method, JsonElement Params)` e `public sealed record AgentResponse(long Id, bool Ok, JsonElement? Result, string? Code, string? Message)`; `AgentProtocol.ReadLine(string) / WriteLine(AgentResponse)` (JSON a righe, `JsonSerializerOptions` con nomi camelCase).
  - `public sealed class AgentGame` (costruita su `SimHost` + `FactoryContent`): `JsonElement Call(string method, JsonElement @params)` per i metodi comuni `stato`, `area`, `trova`, `eventi`, `piazza`, `smonta`, `ruota`, `sblocca`, `vendi`, `potenzia_core`, `avanza`, `diario`; lancia `AgentException(code, message)` per errori del canale.
  - `public sealed class AgentJournal`: voci `(long Tick, string Method, JsonElement Params)`; `Save(path)`, `Load(path)`; solo i metodi che cambiano la partita o il tempo.
  - `AgentGame.Replay(AgentJournal, SimHost)`.
- [ ] Step 1: test (prima, devono fallire): `StatoReportsCoreAndMoney` (Sandbox con `GameRules(InstantBuild: true)`: soldi, risorse del Core, energia, conteggi); `PiazzaUsesSimHostAndReportsRefusalCode` (miner su parete → `ok:false`, `codice:"Wall"`); `PiazzaLineDragsLikeThePlayer` (linea di nastri da A a B → celle e direzioni come il trascinamento del client di oggi); `AreaIsCompactAndBounded` (righe di testo per cella; 65×65 → `ParametriNonValidi`; fuori mappa → `ParametriNonValidi`); `TrovaReturnsNearestDeposits`; `EventiReturnsOnlyNewEvents`; `AvanzaRunsExactTicks` (tick prima/dopo); `ReplayGivesSameChecksum` (diario di 20 azioni rigiocato su una partita nuova → stesso `SimHost.Checksum()`).
- [ ] Step 2: implementazione. `area`: una riga per riga di mappa, un carattere per suolo/parete/acqua e un simbolo per giacimento e struttura, più una legenda nella risposta; `piazza` per i nastri accetta `{da, a, direzione?, tier}` e costruisce la stessa sequenza di celle del client (`DragCells` 4-connesso, spostato in Shared come funzione pura e usato anche dal client).
- [ ] Step 3: `dotnet build` 0 avvisi, `dotnet test` tutti verdi; commit; PR "feat(agenti): nucleo comune e motore simulazione (1/5)".

### Task 2: server MCP `tools/AgentPlay` sul motore simulazione

**Files:**
- Create: `tools/AgentPlay/AgentPlay.csproj` (net8.0, riferimento a `TIndustry.Shared`, pacchetto `ModelContextProtocol` a versione fissa), `Program.cs` (host stdio), `Sessions.cs` (gestore delle sessioni e degli id), `Tools.cs` (strumenti MCP), `tools/AgentPlay/README.md`, `.mcp.json` alla radice; `tIndustry.sln` (progetto aggiunto); `tests/TIndustry.AgentPlay.Tests/` (nuovo progetto xUnit)
**Interfaces:**
- Consumes: `AgentGame`, `AgentJournal`, `AgentProtocol` del Task 1.
- Produces: `interface IAgentEngine { JsonElement Call(string method, JsonElement p); void Close(); string Kind { get; } }`; `SimEngine : IAgentEngine` (su `AgentGame`); `Sessions.Open(IAgentEngine) -> string id`, `Sessions.Get(id)`, `Sessions.Close(id)`; strumenti MCP con descrizioni italiane brevi (una frase: cosa fa e quando usarlo).
- [ ] Step 1: test d'integrazione `ServerListsToolsAndPlaysASimulation`: avvia `AgentPlay` come processo con stdio, chiede la lista degli strumenti (tutti i nomi dei Global Constraints presenti), `apri_simulazione` (Media, seme 1), `stato`, `piazza` un minatore, `avanza 120`, `stato` (risorsa cresciuta o piano costruito), `chiudi`; poi `stato` sulla sessione chiusa → `SessioneSconosciuta`. Test `CoopIsAPlaceholder` (`collega_coop` → `MotoreNonDisponibile`). Test `BadParamsNeverKillTheServer` (parametri sbagliati su ogni strumento → `ParametriNonValidi`, il server risponde ancora).
- [ ] Step 2: implementazione; gli strumenti solo-gioco su una sessione di simulazione → `MotoreNonDisponibile` con messaggio "solo nel gioco vero".
- [ ] Step 3: `.mcp.json` con `{"mcpServers": {"tindustry": {"command": "dotnet", "args": ["run", "--project", "tools/AgentPlay", "-c", "Release"]}}}`; README con ciclo tipico e regole; build, test; commit; PR "feat(agenti): server MCP sul motore simulazione (2/5)".

### Task 3: canale `AgentBridge` nel client Godot

**Files:**
- Create: `godot/scripts/dev/AgentBridge.cs` (+ `.uid`)
- Modify: `godot/scripts/world/World.cs` (crea il ponte solo se `OS.IsDebugBuild()` e `TINDUSTRY_AGENT_PORT`), `godot/scripts/dev/World.Playtest*.cs` (nuovo playtest `agenti`), `tools/dev/playtest.sh` (modalità `agenti`)
**Interfaces:**
- Consumes: `AgentProtocol`, `AgentGame` del Task 1.
- Produces: protocollo del canale sul `TcpServer` di Godot: i metodi comuni delegano ad `AgentGame` sul `SimHost` del mondo; i metodi solo-gioco: `cattura {percorso?}` → `{percorso}` (PNG della finestra), `telecamera {x?, y?, zoom?}` → posizione attuale, `input {tipo: clic|trascina|tasto|rotella, schermo?:{x,y}, cella?:{x,y}, verso?, tasto?, pulsante?}`, `ui {}` → controlli visibili `{nome, tipo, testo, rett}`, `ui {clic: nome}`, `log {righe}`, `esegui {secondi}`, `pausa`, `riprendi` (riusa `PlaytestHeld`).
- [ ] Step 1: playtest `agenti` (scritto prima, fallisce senza ponte): avvia il gioco con `TINDUSTRY_AGENT_PORT`, un client TCP dentro il playtest stesso manda `stato`, `piazza` (minatore), `avanza 60`, `input clic` su un pulsante dell'HUD trovato con `ui`, `cattura` → file esistente 1280×720; raffica di 50 `stato` durante `esegui 2` senza errori (Review Focus 2); chiusura pulita (nessun "Leaked unsafe reference").
- [ ] Step 2: implementazione; le richieste si accodano dal thread di rete e si eseguono in `_Process` del ponte; `cella` → schermo con la trasformazione del canvas (stessa di `ScreenToCell` al contrario).
- [ ] Step 3: test che in un build non di debug il ponte non si crea (funzione pura `AgentBridge.ShouldStart(isDebug, envPort)` collegata ai test come `StartupDisplay`); build-solutions, playtest `ui`, `tutorial`, `agenti` OK; commit; PR "feat(agenti): canale di controllo nel gioco (3/5)".

### Task 4: motore gioco vero e co-op segnaposto nel server

**Files:**
- Create: `tools/AgentPlay/GameEngine.cs`, `tools/AgentPlay/GameLauncher.cs`
- Modify: `tools/AgentPlay/Tools.cs`, `tests/TIndustry.AgentPlay.Tests/`
**Interfaces:**
- Consumes: `IAgentEngine` (Task 2), protocollo del canale (Task 3).
- Produces: `GameEngine : IAgentEngine`; `GameLauncher.Start(GameOptions{Lingua, Scala, Colori, Livello?, Seme?, Misura?, Conserva}) -> (Process, int port, string profileDir)` (porta libera scelta dal server, profilo temporaneo, `APPDATA` sul profilo, `--log-file`, `--resolution 1280x720`, comando `godot4 --path godot`); timeout di avvio 60 s e di richiesta 30 s.
- [ ] Step 1: test (marcati per girare solo se `godot4` è nel PATH, altrimenti ignorati con motivo): `GameSessionTakesAShot` (`apri_gioco`, `cattura` → PNG 1280×720, `chiudi` → profilo cancellato); `KilledGameIsReported` (uccide il processo, poi `stato` → `GiocoTerminato` con log entro 30 s, Review Focus 1); `ProfileNeverUnderRealData` (il profilo sta sotto `%TEMP%` e non sotto i dati reali, Review Focus 4); `KeepLeavesTheProfile` (`conserva:true`).
- [ ] Step 2: implementazione; `diario` del gioco registra i comandi di gioco con il tick restituito dal ponte (Review Focus 5); `rigioca` di un diario del gioco su una sessione di simulazione è permesso.
- [ ] Step 3: build, test (con Godot installato: tutti verdi); commit; PR "feat(agenti): motore gioco vero (4/5)".

### Task 5: documenti, skill e primo uso reale

**Files:**
- Create: `docs/decisioni/0008-agenti-che-giocano.md`, `.claude/skills/gioca-e-testa/SKILL.md` (due ricette: "trova bug in un'area del gioco", "fai le catture di una schermata")
- Modify: `docs/decisioni.md`, `docs/ARCHITETTURA.md` (motori, canale, sicurezza), `CLAUDE.md` (sezione "Agenti che giocano" che rimanda a `tools/AgentPlay/README.md`), `CHANGELOG.md`, `tools/dev/mappa_canvas.py` (+ canvas rigenerato)
- [ ] Step 1: documenti e skill; verifica che lo skill sia leggibile e che `.mcp.json` funzioni da una sessione nuova di Claude Code (elenco degli strumenti `tindustry`).
- [ ] Step 2: commit; PR "docs: agenti che giocano, ADR 0008 (5/5)".
- [ ] Step 3 (controllore, dopo l'unione): un sotto-agente usa gli strumenti per una partita breve nel gioco vero, fa le catture di HUD, palette e nastri, prova l'unione a T e l'incrocio automatico, e riporta i bug con il diario per riprodurli.
