# AgentPlay: server MCP per gli agenti che giocano

Server MCP (stdio) che fa giocare gli agenti AI a tIndustry: vedere lo stato, dare i comandi del giocatore, far scorrere il tempo e rendere riproducibile ogni bug trovato. Specifica: [docs/superpowers/specs/2026-10-18-agenti-giocano-design.md](../../docs/superpowers/specs/2026-10-18-agenti-giocano-design.md).

Decisione: [ADR 0008](../../docs/decisioni/0008-agenti-che-giocano.md). Ricette pronte (cercare bug, fare catture): skill [`gioca-e-testa`](../../.claude/skills/gioca-e-testa/SKILL.md).

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

Risponde `{sessione, motore: "gioco", cartella, diari, processo}`: `cartella` è il profilo, `diari` la cartella dei diari, `processo` il pid avviato. Servono `godot4` nel PATH del server e il progetto importato e compilato (`godot4 --import`, `--build-solutions`, vedi CLAUDE.md; senza, `apri_gioco` risponde subito `MotoreNonDisponibile` con i comandi da eseguire). Claude Code avvia il server con il PATH di Windows, che non ha le cartelle aggiunte solo da Git Bash (per esempio `~/bin`): in quel caso imposta la variabile d'ambiente utente `TINDUSTRY_GODOT` sull'eseguibile di Godot 4.4 mono e riavvia Claude Code. `.mcp.json` la passa al server con `"TINDUSTRY_GODOT": "${TINDUSTRY_GODOT:-}"`, quindi nel repository non c'è il percorso di nessuna macchina; se la scrivi direttamente in `.mcp.json`, non farne il commit. Se `godot4` è uno script `.cmd`/`.bat`, i percorsi del progetto e del profilo non possono contenere `& | ^ % ! < > "` (cmd.exe spezzerebbe la riga): in quel caso imposta `TINDUSTRY_GODOT` sull'eseguibile `.exe`. Nel gioco vero:

- il tempo scorre in tempo reale; `pausa` e `riprendi` lo fermano e lo fanno ripartire (anche durante un `esegui`: vale l'ultima scelta), `avanza` esegue tick esatti e lascia il gioco fermo, `esegui {secondi}` lo lascia scorrere per al massimo 120 s (oltre: `ParametriNonValidi`);
- `stato.direzione`, `ruota` e i `piazza` senza `direzione` usano la direzione del giocatore, quella che mostra l'HUD e che gira il tasto R (anche con `input tasto R`): nel gioco vero c'è una sola direzione;
- le richieste a una sessione possono andare in parallelo; un comando risponde dopo il tick del gioco che lo applica;
- `cattura {percorso?}` scrive solo sotto `<profilo>/catture` (`percorso` è un nome relativo `.png`) e restituisce il percorso assoluto;
- `input {tipo, schermo | cella, verso?, tasto?, pulsante?}`: con `trascina` il punto finale è `verso`, come `{x, y}` o `"x,y"`, nello stesso spazio dell'inizio; con `rotella` `verso` è `su` o `giu`;
- `ui {}` elenca i controlli che un clic raggiunge: non quelli trasparenti (un toast già sparito), sotto un pannello che prende il mouse (l'HUD sotto la Home) o sotto una finestra popup aperta, o doppi; compaiono anche le caselle della palette (`CategoriaLogistics`, `BloccoBelt`…) con il loro suggerimento (`Nastro T1 [1]`). I pulsanti principali hanno nomi stabili (`MenuRow/Ricerca`, `PotenziaCore`, `Toast`, nella Home `Riprendi`, `NuovaPartita`, `Esci`…). `ui {clic}` accetta il nome intero, la sua ultima parte, il testo visibile o il suggerimento (senza il tasto: `Ricerca`, `Nastro T1`); se ne indica più di uno risponde `ParametriNonValidi` con "ambiguo" e i nomi. Può rispondere `NonCliccabile` (controllo fuori dalla finestra o tagliato dal contenitore); il canale risponde `Occupato` se riceve troppe richieste in coda;
- `log {righe?, tutte?}` legge `<profilo>/godot.log`: avvisi ed errori, o tutte le righe con `tutte: true`;
- tempi limite: 60 s per l'avvio (fino al canale pronto e alla partita iniziata; se il gioco esce o scrive che la porta è occupata, la risposta arriva subito), 30 s per richiesta (`esegui`: 30 s più i suoi secondi, al massimo 120); una richiesta oltre 64 KiB (un parametro lunghissimo) è rifiutata con `ParametriNonValidi` prima di arrivare al gioco;
- se il gioco esce, va in crash o chiude il canale, la chiamata risponde `GiocoTerminato` con le ultime 30 righe del log e la sessione si chiude da sola (profilo cancellato salvo `conserva`): le chiamate seguenti rispondono `SessioneSconosciuta`. La sessione invece resta viva con `PartitaCambiata` (la partita è cambiata prima dell'esito di un comando: il comando è perso) e con `MotoreNonDisponibile` "Nessuna partita in corso";
- `eventi` segnala anche `partitaCaricata` (`tickPrima`, `tick`, `indietro` se il tick è tornato indietro) quando la partita cambia, per l'agente o per una persona alla finestra, e `inputReale` (una volta fino al prossimo `eventi`) quando una persona usa tastiera o mouse;
- `chiudi` chiude subito il canale (una richiesta in attesa finisce con `SessioneSconosciuta`), il gioco esce da solo quando il suo lanciatore se ne va; dopo 5 s ciò che resta viene terminato. Poi cancella il profilo e i diari, salvo `conserva: true`;
- il gioco non sopravvive al server: esce quando la connessione del lanciatore cade (`TINDUSTRY_AGENT_ESCI_SENZA_CLIENT=1`, messa solo dal lanciatore) e, su Windows, è in un job object che il sistema chiude quando il server muore.

`diario {nome}` sul gioco vero salva i comandi (`piazza`, `smonta`, `ruota`, `sblocca`, `vendi`, `potenzia_core`, `avanza`) con il tick del gioco, il tick finale `fine` e un'intestazione `partita` con `motore: "gioco"`, nella cartella `diari` della sessione, che `chiudi` cancella salvo `conserva`: rigioca o copia il diario prima di chiudere. `rigioca` lo riesegue su una sessione di simulazione fino a `fine`, così anche il tempo dopo l'ultimo comando (`esegui`, tempo reale) si rigioca. Il diario segue la partita: il ponte lo fa ripartire quando la partita cambia (nuova partita, livello, salvataggio caricato, anche con Continua o Nuova partita fatti da una persona nella finestra) e dopo un comando scaduto. L'intestazione è quella di apertura per la partita di `apri_gioco` (sandbox `misura`/`seme`/`generatore` o `livello`), altrimenti l'istantanea (`partenza`) della partita com'è in quel momento, come per `salvataggio`. Se l'istantanea non si può fare, `diario` risponde `rigiocabile: false` e `rigioca` rifiuta il diario. `input` e `ui {clic}` sono nel diario con `rigiocabile: false`: `rigioca` li salta e lo scrive in `avvisi`, insieme agli avvisi del diario (una persona alla finestra, un comando scaduto).

## Ciclo tipico

1. **Apri**: `apri_simulazione {misura: "Media", seme: 1}` restituisce `sessione` (per esempio `s3f9a-1`: il prefisso cambia a ogni avvio del server, così un id vecchio risponde `SessioneSconosciuta`) e `cartella`, la cartella temporanea della sessione. Altre scelte: `generatore` (`biomi-1` o `classico`), `livello` (id della campagna), `salvataggio` (percorso di un file di salvataggio; con le regole salvate, quindi senza `costruzioneIstantanea`), `costruzioneIstantanea`, `conserva`. `misura` è il nome Media, Grande o Enorme oppure un numero di celle per lato (12-2000).
2. **Guarda**:
   - `stato` dice tick, soldi, risorse, energia, `ricerca` (le strutture già sbloccate), edifici, coda di costruzione, cella del Core (`core.cella`), misura della mappa, direzione, ultimi rifiuti e `impronta`, il checksum della partita; `obiettivi` e `livelloCompletato` ci sono solo nei livelli della campagna;
   - `area {da, a, oggetti?}` mostra al massimo 64×64 celle in righe di simboli, con legenda e `forme`: solo le curve e le T dei nastri (il dritto è il caso normale), con `ingresso`, il verso di marcia dell'oggetto che entra (una curva che riceve da ovest ha `ingresso: "est"`). Con `oggetti: true` anche `oggetti`: le celle con oggetti sui nastri, quanti (`n`) e di che tipo (`tipi`);
   - `trova {cosa, cella}` cerca i giacimenti o le strutture più vicini;
   - `catalogo` elenca le strutture: id, nome, tipo, costo di costruzione, costo di sblocco, prerequisiti, `sbloccato`;
   - `eventi` dice cosa è successo dall'ultima chiamata (`costruito` e `smontato` con `cella` e `struttura`).
3. **Agisci**: `piazza`, `smonta`, `ruota`, `sblocca`, `vendi`, `potenzia_core`. Sono gli stessi comandi del giocatore, passano da `SimHost`. Ogni comando esegue il proprio tick e risponde con l'esito. Una linea di `piazza {da, a}` fa come il trascinamento del giocatore, incrocio automatico compreso: la decisione è la stessa funzione del client (`BeltDrag`); una cella diventata incrocio ha `incrocio: true`, un incrocio non ancora sbloccato è rifiutato con `CrossingNeedsJunction`.
4. **Avanza**: `avanza {tick}` esegue un numero esatto di tick (60 al secondo, al massimo 36000 per chiamata).
5. **Riproduci**: `diario {nome}` salva le azioni con il loro tick e restituisce il `percorso` del file. `rigioca {percorso}` apre una sessione nuova con lo stesso stato e restituisce `impronta` (confrontala con quella di `stato`) e, se serve, `avvisi`. Nel percorso vanno bene anche le barre `/`.

**Tick.** Il `tick` di una voce del diario e di un rifiuto (in `stato.rifiuti` e negli `eventi`) è sempre il tick della partita quando è arrivato il comando; il comando si applica nel tick dopo, quello che la risposta del comando riporta in `tick`.
6. **Chiudi**: `chiudi {sessione}` cancella la cartella temporanea, diari compresi. Fa eccezione la sessione aperta con `conserva: true`. Prima di chiudere, rigioca il diario o copialo altrove.

## Risposte

Ogni strumento risponde con un testo JSON: `{ok: true, ...}` oppure `{ok: false, codice, messaggio, mancano?}`. `mancano` dice cosa manca a un comando rifiutato, quando si sa (`InsufficientResources`, `Locked`, `UnlockUnaffordable`): `soldi`, gli oggetti per id e `prerequisiti`; c'è anche nei `rifiuti` di `piazza`, di `stato` e degli `eventi`. L'agente legge `codice`, che è uno di questi:

- un codice del gioco: un `RefusalCode`, per esempio `Wall` o `InsufficientResources`, oppure un `SaveLoadError`;
- un codice del canale: `SessioneSconosciuta`, `MotoreNonDisponibile`, `ParametriNonValidi`, `Timeout`, `GiocoTerminato` (il gioco è uscito: la sessione si chiude da sola), `PartitaCambiata` (la sessione resta viva) o `ErroreInterno`.

I parametri sbagliati non fermano mai il server, che risponde `ParametriNonValidi`. Il messaggio nomina il parametro: `manca 'sessione'`, `'tick' ha il tipo sbagliato: atteso numero intero`, oppure `'direzzione' non è un parametro: quelli validi sono ...`. Le coordinate del mondo sono celle `{x, y}`. Le liste lunghe si fermano a 200 voci, con `troncato`.

## Regole

- Mai toccare i dati reali del giocatore (`%LOCALAPPDATA%\tIndustry`, `%APPDATA%\Godot\app_userdata\tIndustry`). Il motore simulazione non li legge e non li scrive. Usa le sue cartelle sotto la cartella temporanea di sistema (`tindustry-agentplay/`). `salvataggio` accetta solo un percorso esplicito, e rifiuta i file sotto le due cartelle dei dati veri: prima copia il salvataggio in una cartella temporanea.
- Le cartelle delle sessioni lasciate da un server ucciso si cancellano al successivo avvio, dopo 24 ore. Fanno eccezione quelle aperte con `conserva: true`, che hanno il file `.conserva`, e quelle ancora vive: il server che le usa tiene aperto il file-lucchetto `.in-uso` (esclusivo, cancellato alla chiusura), e la pulizia di un altro server le salta, per vecchia che sembri la data della cartella.
- stdout è il canale MCP. Gli errori interni completi finiscono su stderr.
- I dati di gioco sono quelli di `data/`, copiati accanto al server quando si compila.

## Prove

```bash
dotnet build tIndustry.sln -c Release -nologo -v q
dotnet test tests/TIndustry.AgentPlay.Tests -c Release --no-build   # avvia il server via stdio
```

I test del gioco vero (`GameEngineTests`) aprono Godot con finestra: girano solo se `godot4` è nel PATH (o c'è `TINDUSTRY_GODOT`), altrimenti si saltano con il motivo, come nella CI.

L'importazione headless (`godot4 --headless --path godot --import`) a volte esce con il codice 139 (segmentation fault in `Godot.DisposablesTracker.OnGodotShuttingDown`) **dopo** aver finito: l'importazione è comunque valida e la compilazione dopo funziona. Uno script che controlla il codice d'uscita non deve prenderlo per un errore: guarda invece che ci siano `godot/lingue/testi.it.translation` e i file in `godot/.godot/imported/`.
