# AgentPlay: server MCP per gli agenti che giocano

Server MCP (stdio) che fa giocare gli agenti AI a tIndustry: vedere lo stato, dare i comandi del giocatore, far scorrere il tempo e rendere riproducibile ogni bug trovato. Specifica: [docs/superpowers/specs/2026-10-18-agenti-giocano-design.md](../../docs/superpowers/specs/2026-10-18-agenti-giocano-design.md).

Claude Code lo avvia da solo grazie a `.mcp.json` alla radice del repository (server `tindustry`, comando `dotnet run --project tools/AgentPlay -c Release`). Libreria: SDK MCP ufficiale per .NET, pacchetto NuGet `ModelContextProtocol` 2.2.0 (versione fissata nel `.csproj`).

## Motori

| Motore | Strumento | Quando |
|---|---|---|
| Simulazione | `apri_simulazione` | Regole, comandi, economia, costruzione, riproduzione dei bug. Veloce, senza grafica, deterministico. |
| Gioco vero | `apri_gioco` | Catture, interfaccia, input. Non ancora disponibile: risponde `MotoreNonDisponibile`. |
| Co-op | `collega_coop` | Segnaposto fino al co-op: risponde `MotoreNonDisponibile`. |

Gli strumenti solo del gioco vero (`esegui`, `riprendi`, `cattura`, `telecamera`, `input`, `ui`, `log`) su una sessione di simulazione rispondono `MotoreNonDisponibile`. Nella simulazione `pausa` non fa nulla: il tempo si muove solo con `avanza` e con il tick di ogni comando.

## Ciclo tipico

1. **Apri**: `apri_simulazione {misura: "Media", seme: 1}` restituisce `sessione` (per esempio `s1`) e `cartella`, la cartella temporanea della sessione. Altre scelte: `generatore` (`biomi-1` o `classico`), `livello` (id della campagna), `salvataggio` (percorso di un file di salvataggio), `costruzioneIstantanea`, `conserva`.
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

I parametri sbagliati non fermano mai il server, che risponde `ParametriNonValidi`. Le coordinate del mondo sono celle `{x, y}`. Le liste lunghe si fermano a 200 voci, con `troncato`.

## Regole

- Mai toccare i dati reali del giocatore (`%LOCALAPPDATA%\tIndustry`, `%APPDATA%\Godot\app_userdata\tIndustry`). Il motore simulazione non li legge e non li scrive. Usa le sue cartelle sotto la cartella temporanea di sistema (`tindustry-agentplay/`). `salvataggio` accetta solo un percorso esplicito, mai uno slot del giocatore.
- stdout è il canale MCP. Gli errori interni completi finiscono su stderr.
- I dati di gioco sono quelli di `data/`, copiati accanto al server quando si compila.

## Prove

```bash
dotnet build tIndustry.sln -c Release -nologo -v q
dotnet test tests/TIndustry.AgentPlay.Tests -c Release --no-build   # avvia il server via stdio
```
