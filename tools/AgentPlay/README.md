# AgentPlay: server MCP per gli agenti che giocano

Server MCP (stdio) che fa giocare gli agenti AI a tIndustry: vedere lo stato, dare i comandi del giocatore, far scorrere il tempo e rendere riproducibile ogni bug trovato. Specifica: [docs/superpowers/specs/2026-10-18-agenti-giocano-design.md](../../docs/superpowers/specs/2026-10-18-agenti-giocano-design.md).

Libreria: SDK MCP ufficiale per .NET, pacchetto NuGet `ModelContextProtocol` 2.2.0 (versione fissata nel `.csproj`).

## Avvio

Claude Code lo avvia da solo grazie a `.mcp.json` alla radice del repository: server `tindustry`, comando `python tools/AgentPlay/avvia.py`. Servono `python` e `dotnet` nel PATH.

Il lanciatore `avvia.py` fa tre cose:

1. Calcola un'impronta dei sorgenti: codice di AgentPlay e di `TIndustry.Shared`, `data/content.json`, `data/campaign.json` e `data/maps/`.
2. Compila AgentPlay in `%TEMP%/tindustry-agentplay-bin/<impronta>`. Se la cartella esiste già, la riusa.
3. Avvia la dll compilata con stdin e stdout passati così come sono.

Così il server non usa mai i `bin/` e `obj/` del repository, e `dotnet build tIndustry.sln -c Release` funziona anche mentre il server gira. Tutto l'output della compilazione va su stderr, perché stdout è il canale MCP. Le compilazioni di sorgenti vecchi, non usate da una settimana, si cancellano da sole.

La **prima partenza** dopo un cambio dei sorgenti compila, quindi è più lenta: con una restore NuGet a freddo servono anche decine di secondi. Le partenze successive con gli stessi sorgenti durano meno di un secondo. Per non far scadere l'attesa di Claude Code, prepara la compilazione prima:

```bash
python tools/AgentPlay/avvia.py --prepara
```

## Motori

| Motore | Strumento | Quando |
|---|---|---|
| Simulazione | `apri_simulazione` | Regole, comandi, economia, costruzione, riproduzione dei bug. Veloce, senza grafica, deterministico. |
| Gioco vero | `apri_gioco` | Catture, interfaccia, input. Apre Godot con finestra (vedi sotto): più lento, il tempo scorre da solo. |
| Co-op | `collega_coop` | Segnaposto fino al co-op: risponde `MotoreNonDisponibile`. |

Gli strumenti solo del gioco vero (`esegui`, `riprendi`, `cattura`, `telecamera`, `input`, `ui`, `log`) su una sessione di simulazione rispondono `MotoreNonDisponibile`. Nella simulazione `pausa` non fa nulla: il tempo si muove solo con `avanza` e con il tick di ogni comando.

## Gioco vero

`apri_gioco {misura?, seme?, generatore?, livello?, salvataggio?, lingua?, scala?, colori?, conserva?}` avvia `godot4 --path godot` (oppure l'eseguibile in `TINDUSTRY_GODOT`) come i playtest, ma senza modalità playtest:

- profilo temporaneo nuovo sotto `%TEMP%/tindustry-agentplay/gioco-*`, mai nei dati veri (controllato prima di creare qualsiasi cartella): `TINDUSTRY_DATA_DIR` e `APPDATA` puntano al profilo, `--log-file <profilo>/godot.log`, finestra `--resolution 1280x720`;
- `lingua` (`it` o `en`), `scala` (0.75-2 a passi di 0.05) e `colori` (`Normale`, `Daltonici`, `AltoContrasto`) vanno nel `settings.json` del profilo, con il tutorial già fatto; un test tiene questi elenchi allineati al codice del client;
- il canale degli agenti su una porta libera con un segreto nuovo a ogni sessione; poi la partita: un sandbox `misura`/`seme`/`generatore` (`biomi-1` o `classico`; predefiniti Media, 1 e biomi-1, fatto come lo rifà `rigioca`), il livello `livello`, oppure il salvataggio `salvataggio`;
- `salvataggio` è il percorso di un file: il server lo **copia** in `<profilo>/godot-saves/agente.json` e il gioco carica la copia; il file dato si legge solo per copiarlo. I file sotto i dati veri del giocatore sono rifiutati.

Risponde `{sessione, motore: "gioco", cartella, diari, processo}`: `cartella` è il profilo, `diari` la cartella dei diari, `processo` il pid avviato. Servono `godot4` nel PATH del server (altrimenti imposta `TINDUSTRY_GODOT`, per esempio nell'`env` di `.mcp.json`) e il progetto importato e compilato (`godot4 --import`, `--build-solutions`, vedi CLAUDE.md). Se `godot4` è uno script `.cmd`/`.bat`, i percorsi del progetto e del profilo non possono contenere `& | ^ % ! < > "` (cmd.exe spezzerebbe la riga): in quel caso imposta `TINDUSTRY_GODOT` sull'eseguibile `.exe`. Nel gioco vero:

- il tempo scorre in tempo reale; `pausa` e `riprendi` lo fermano e lo fanno ripartire (anche durante un `esegui`: vale l'ultima scelta), `avanza` esegue tick esatti e lascia il gioco fermo, `esegui {secondi}` lo lascia scorrere;
- le richieste a una sessione possono andare in parallelo; un comando risponde dopo il tick del gioco che lo applica;
- `cattura {percorso?}` scrive solo sotto `<profilo>/catture` (`percorso` è un nome relativo `.png`) e restituisce il percorso assoluto;
- `input {tipo, schermo | cella, verso?, tasto?, pulsante?}`: con `trascina` il punto finale è `verso`, come `{x, y}` o `"x,y"`, nello stesso spazio dell'inizio; con `rotella` `verso` è `su` o `giu`;
- `ui {clic?}` può rispondere `NonCliccabile` (controllo fuori dalla finestra o nascosto dal contenitore); il canale risponde `Occupato` se riceve troppe richieste in coda;
- `log {righe?, tutte?}` legge `<profilo>/godot.log`: avvisi ed errori, o tutte le righe con `tutte: true`;
- tempi limite: 60 s per l'avvio (fino al canale pronto e alla partita iniziata; se il gioco esce o scrive che la porta è occupata, la risposta arriva subito), 30 s per richiesta (`esegui`: 30 s più i suoi secondi);
- se il gioco esce, va in crash o chiude il canale, la chiamata risponde `GiocoTerminato` con le ultime 30 righe del log e la sessione si chiude da sola (profilo cancellato salvo `conserva`): le chiamate seguenti rispondono `SessioneSconosciuta`;
- `chiudi` chiude subito il canale (una richiesta in attesa finisce con `SessioneSconosciuta`), il gioco esce da solo quando il suo lanciatore se ne va; dopo 5 s ciò che resta viene terminato. Poi cancella il profilo e i diari, salvo `conserva: true`;
- il gioco non sopravvive al server: esce quando la connessione del lanciatore cade (`TINDUSTRY_AGENT_ESCI_SENZA_CLIENT=1`, messa solo dal lanciatore) e, su Windows, è in un job object che il sistema chiude quando il server muore.

`diario {nome}` sul gioco vero salva i comandi (`piazza`, `smonta`, `ruota`, `sblocca`, `vendi`, `potenzia_core`, `avanza`) con il tick del gioco e un'intestazione `partita` con `motore: "gioco"` e le scelte di apertura (per un salvataggio, la sua istantanea), nella cartella `diari` della sessione, che `chiudi` cancella salvo `conserva`: rigioca o copia il diario prima di chiudere. `rigioca` lo riesegue su una sessione di simulazione. Limiti: gli input simulati (`input`, `ui {clic}`) e quello che fa una persona alla finestra non sono nel diario, quindi non si rigiocano; il tempo tra due comandi si rigioca a tick esatti, ma `esegui` e `riprendi` non sono voci del diario (contano solo i tick trascorsi).

## Ciclo tipico

1. **Apri**: `apri_simulazione {misura: "Media", seme: 1}` restituisce `sessione` (per esempio `s3f9a-1`: il prefisso cambia a ogni avvio del server, così un id vecchio risponde `SessioneSconosciuta`) e `cartella`, la cartella temporanea della sessione. Altre scelte: `generatore` (`biomi-1` o `classico`), `livello` (id della campagna), `salvataggio` (percorso di un file di salvataggio; con le regole salvate, quindi senza `costruzioneIstantanea`), `costruzioneIstantanea`, `conserva`. `misura` è il nome Media, Grande o Enorme oppure un numero di celle per lato (12-2000).
2. **Guarda**:
   - `stato` dice tick, soldi, risorse, energia, ricerca, obiettivi, edifici, coda di costruzione, cella del Core (`core.cella`), misura della mappa e ultimi rifiuti;
   - `area {da, a}` mostra al massimo 64×64 celle in righe di simboli, con legenda;
   - `trova {cosa, cella}` cerca i giacimenti o le strutture più vicini;
   - `eventi` dice cosa è successo dall'ultima chiamata.
3. **Agisci**: `piazza`, `smonta`, `ruota`, `sblocca`, `vendi`, `potenzia_core`. Sono gli stessi comandi del giocatore, passano da `SimHost`. Ogni comando esegue il proprio tick e risponde con l'esito.
4. **Avanza**: `avanza {tick}` esegue un numero esatto di tick (60 al secondo, al massimo 36000 per chiamata).
5. **Riproduci**: `diario {nome}` salva le azioni con il loro tick e restituisce il `percorso` del file. `rigioca {percorso}` apre una sessione nuova con lo stesso stato e restituisce `impronta`, il checksum della partita.
6. **Chiudi**: `chiudi {sessione}` cancella la cartella temporanea, diari compresi. Fa eccezione la sessione aperta con `conserva: true`. Prima di chiudere, rigioca il diario o copialo altrove.

## Risposte

Ogni strumento risponde con un testo JSON: `{ok: true, ...}` oppure `{ok: false, codice, messaggio}`. L'agente legge `codice`, che è uno di questi:

- un codice del gioco: un `RefusalCode`, per esempio `Wall` o `InsufficientResources`, oppure un `SaveLoadError`;
- un codice del canale: `SessioneSconosciuta`, `MotoreNonDisponibile`, `ParametriNonValidi`, `Timeout`, `GiocoTerminato` o `ErroreInterno`.

I parametri sbagliati non fermano mai il server, che risponde `ParametriNonValidi`. Il messaggio nomina il parametro: `manca 'sessione'`, `'tick' ha il tipo sbagliato: atteso numero intero`, oppure `'direzzione' non è un parametro: quelli validi sono ...`. Le coordinate del mondo sono celle `{x, y}`. Le liste lunghe si fermano a 200 voci, con `troncato`.

## Regole

- Mai toccare i dati reali del giocatore (`%LOCALAPPDATA%\tIndustry`, `%APPDATA%\Godot\app_userdata\tIndustry`). Il motore simulazione non li legge e non li scrive. Usa le sue cartelle sotto la cartella temporanea di sistema (`tindustry-agentplay/`). `salvataggio` accetta solo un percorso esplicito, e rifiuta i file sotto le due cartelle dei dati veri: prima copia il salvataggio in una cartella temporanea.
- Le cartelle delle sessioni lasciate da un server ucciso si cancellano al successivo avvio, dopo 24 ore. Fanno eccezione quelle aperte con `conserva: true`, che hanno il file `.conserva`.
- stdout è il canale MCP. Gli errori interni completi finiscono su stderr.
- I dati di gioco sono quelli di `data/`, copiati accanto al server quando si compila.

## Prove

```bash
dotnet build tIndustry.sln -c Release -nologo -v q
dotnet test tests/TIndustry.AgentPlay.Tests -c Release --no-build   # avvia il server via stdio
```

I test del gioco vero (`GameEngineTests`) aprono Godot con finestra: girano solo se `godot4` è nel PATH (o c'è `TINDUSTRY_GODOT`), altrimenti si saltano con il motivo, come nella CI.
