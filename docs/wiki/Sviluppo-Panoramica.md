# Panoramica dello sviluppo

Cos'è tIndustry dal punto di vista del codice, com'è organizzato il repository e dove trovare i documenti di riferimento.

## Il progetto in breve

tIndustry è un gioco di logistica industriale ispirato a Mindustry e Tiny Industry, scritto in **Godot 4.4 .NET** (C#, .NET 8). Il codice è diviso in due metà:

- **la simulazione** (`src/TIndustry.Shared/`): C# puro, senza dipendenze da Godot né dal sistema operativo. Gira a tick fissi di 1/60 s, è guidata da comandi ed è deterministica, così un giorno potrà girare identica su un server e su più client (co-op);
- **il client** (`godot/`): disegna la partita, raccoglie l'input e lo trasforma in comandi per la simulazione.

Contenuti di gioco, sprite e mappe non si scrivono a mano nel client: i contenuti stanno in `data/`, gli sprite escono da uno script Python, le mappe della campagna da uno strumento dedicato.

## Mappa delle cartelle

| Percorso | Cosa contiene |
|---|---|
| `src/TIndustry.Shared/` | Simulazione: griglia e nastri, edifici, costruzione, energia, economia, ricerca, campagna, salvataggi, codice comune per gli agenti |
| `godot/` | Client: scena principale, script del mondo e dell'interfaccia, shader, sprite in `assets/`, testi in `lingue/testi.csv`, effetti in `effetti/` |
| `data/` | Unica fonte dei contenuti: `content.json`, `campaign.json`, `maps/` (mappe della campagna) |
| `tests/TIndustry.Shared.Tests/` | Test xUnit della simulazione (scenari, validazione dei contenuti, salvataggi, determinismo) |
| `tests/TIndustry.AgentPlay.Tests/` | Test del server MCP degli agenti |
| `tools/dev/` | Avvio all-in-one (`dev.sh`), playtest, confronto degli screenshot, mappa Obsidian del progetto |
| `tools/art/` | Generatore degli sprite, delle icone e del carattere |
| `tools/MapTool/` | Mappe della campagna: genera, valida, anteprima |
| `tools/AgentPlay/` | Server MCP che fa giocare gli agenti AI |
| `tools/sito/` | Sito su GitHub Pages |
| `tools/ci/` | Script di installazione di Godot ed export per la CI |
| `docs/` | Architettura, decisioni, specifiche e piani, diario di lavoro |

La mappa completa, file per file, è in cima a [ARCHITETTURA.md](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md#mappa-delle-cartelle).

## Dove guardare

| Documento | A cosa serve |
|---|---|
| [docs/ARCHITETTURA.md](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md) | Il punto di partenza: ciclo di frame, simulazione, dati, salvataggi, interfaccia, ricette «come aggiungere…» |
| [docs/decisioni.md](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni.md) | Indice delle decisioni: ADR e scelte di prodotto |
| [docs/decisioni/](https://github.com/Juan0177/tIndustry/tree/main/docs/decisioni) | Le decisioni architetturali (ADR 0001–0006 e 0008) |
| [docs/superpowers/](https://github.com/Juan0177/tIndustry/tree/main/docs/superpowers) | Specifiche (`specs/`) e piani di lavoro (`plans/`) |
| [docs/diario/](https://github.com/Juan0177/tIndustry/blob/main/docs/diario/README.md) | Diario di lavoro: un file per piano completato |
| [CHANGELOG.md](https://github.com/Juan0177/tIndustry/blob/main/CHANGELOG.md) | Storia delle versioni e lavori non ancora rilasciati |
| [godot/README.md](https://github.com/Juan0177/tIndustry/blob/main/godot/README.md) | Controlli e funzioni del gioco, prove automatiche |
| [CLAUDE.md](https://github.com/Juan0177/tIndustry/blob/main/CLAUDE.md) | Regole e comandi per gli agenti AI che lavorano sul repository |

## Le decisioni architetturali

| ADR | Tema |
|---|---|
| [0001](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0001-server-csharp.md) | Il server multiplayer sarà in C# e riuserà la simulazione |
| [0002](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0002-sincronizzazione-ibrida.md) | Sincronizzazione ibrida: comandi ordinati dall'host, checksum, istantanea |
| [0003](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0003-costruzione-come-mindustry.md) | Costruzione come Mindustry: piani, drone, cantieri |
| [0004](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0004-arte-generata-da-script.md) | Arte generata da script |
| [0005](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0005-mappe-seme-e-file.md) | Mappe generate da seme e prestabilite in file |
| [0006](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0006-interfaccia-lingue-carattere.md) | Interfaccia, lingue e carattere |
| [0008](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0008-agenti-che-giocano.md) | Agenti che giocano: MCP e canale di controllo |

## Per approfondire

- [docs/ARCHITETTURA.md](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md)
- [README.md, sezione Sviluppo](https://github.com/Juan0177/tIndustry/blob/main/README.md#sviluppo)
- [Compilare e avviare](Compilare-e-avviare) · [Architettura](Architettura) · [Come contribuire](Come-contribuire)
