# tIndustry

[![Build publish](https://github.com/Juan0177/tIndustry/actions/workflows/build-publish.yml/badge.svg)](https://github.com/Juan0177/tIndustry/actions/workflows/build-publish.yml)
**v0.3.4** · [Download](https://github.com/Juan0177/tIndustry/releases/latest) · [CHANGELOG](CHANGELOG.md) · Godot 4.4 .NET

Gioco di logistica industriale: **Mindustry** × **Tiny Industry**.
Estrai minerali, portali al CORE su nastri, fondili in lastre e fili, vendili al Mercato e reinvesti in Ricerca per sbloccare nuovi blocchi.

```
  minatore ──▶ nastro ──▶ forno ──▶ lastre / fili ──▶ CORE ──▶ Mercato
                            ▲                                    │
              carbone oppure corrente                            ▼
                 generatore ──▶ nodi                    $ ──▶ Ricerca ──▶ nuovi blocchi
```

*Esplora → estrai → trasporta → trasforma → vendi → sblocca → espandi.*

---

## Gioca

Scarica l'ultima [Release](https://github.com/Juan0177/tIndustry/releases/latest), estrai lo zip ed esegui:

| Sistema | File | Eseguibile |
| --- | --- | --- |
| Windows | `tIndustry-win-x64.zip` | `tIndustry.exe` |
| Linux | `tIndustry-linux-x64.zip` | `tIndustry.x86_64` |

Gli zip `tIndustry-godot-*-x64.zip` hanno lo stesso contenuto (nomi storici).

### Prima partita

1. Dalla Home: **Continua**, **Campagna** o **Nuova partita**.
2. Piazza un **Minatore** (tasto `2`) su un minerale e collegalo al CORE con i **nastri** (`1`, `R` per ruotare).
3. Apri il **Mercato** (`M`) e vendi quello che arriva al CORE.
4. Con i fondi apri la **Ricerca** (`T`) e sblocca forno, nastri veloci, energia…
5. La **Campagna** (`G`) ti guida con obiettivi; il tutorial avanza da solo mentre giochi.

Controlli completi, salvataggi, Ricerca e Mercato: [`godot/README.md`](godot/README.md).

---

## Cosa c'è nel gioco

| Area | Contenuto |
| --- | --- |
| **Mondo** | Mappa 1000×1000 generata da seed, CORE al centro, minerali visibili (ferro, rame, piombo, carbone, titanio…) |
| **Produzione** | Minatore T1/T2, estrattore, forno multi-ricetta (carbone o corrente), assemblatore |
| **Logistica** | Nastro T1/T2/T3, incrocio, sdoppiatore, selezionatore con filtro, ponte (2–4 caselle) |
| **Energia** | Generatore, nodi T1/T2 con buffer di capacità |
| **Economia** | Fondi, costi e rimborsi di piazzamento, Mercato a prezzo dinamico, vendita automatica, potenziamento del CORE |
| **Ricerca** | Albero stile Mindustry dal CORE, a rami: Logistica, Produzione, Energia |
| **Campagna** | 10 livelli, da *Primi passi* a *Espansione T2*, con tutorial non bloccante |
| **Salvataggi** | Continua (autosave), slot, Salva come; formato versione 9 |

Combattimento e unità sono fuori scope per ora.

---

## Sviluppo

Requisiti: [.NET 8 SDK](https://dotnet.microsoft.com/download) e [Godot 4.4 .NET (mono)](https://godotengine.org/download) come comando `godot4`.

### Avvio rapido

```bash
tools/dev/dev.sh                 # Linux/macOS (bash)
dev.cmd                          # Windows, dalla radice del repo (usa Git Bash)
```

Controlla gli strumenti, compila, importa gli asset se serve e avvia il gioco. Opzioni (uguali per entrambi):

| Opzione | Effetto |
| --- | --- |
| `--profilo-temp` | profilo dati temporaneo: non tocca impostazioni e salvataggi reali (**consigliato per le prove**) |
| `--nuova` | salta la Home e apre una nuova partita |
| `--editor` | apre l'editor Godot |
| `--test` | esegue i test prima di avviare |
| `--playtest ui\|tutorial` | playtest automatico con screenshot in `.dev/` |
| `--solo-build` | compila e basta |
| `--help` | tutte le opzioni |

### Passo per passo

```bash
dotnet build tIndustry.sln -c Release -nologo -v q              # 0 errori, 0 avvisi
dotnet test tests/TIndustry.Shared.Tests -c Release --no-build
godot4 --path godot                                             # legge i contenuti da data/
TINDUSTRY_FRESH=1 godot4 --path godot                           # ignora il salvataggio Continua
```

### Test e playtest

- **Test** (`tests/TIndustry.Shared.Tests`, xUnit): nastri, craft, energia, salvataggi, Ricerca, Mercato, Campagna, determinismo della simulazione e validazione di `data/`. Usano sempre una cartella temporanea.
- **Playtest** (`tools/dev/playtest.sh ui|tutorial <cartella>`): giocano UI e tutorial con input simulati in un profilo dati isolato; un PNG per passo, esito `PLAYTEST OK`.
- **Confronto screenshot**: `python tools/dev/compare_shots.py <baseline> <nuovi>`.

Per provare senza toccare i tuoi dati, imposta `TINDUSTRY_DATA_DIR` su una cartella temporanea (gli script lo fanno da soli).

### Struttura

| Percorso | Ruolo |
| --- | --- |
| `godot/` | Client di gioco: mondo, HUD, pannelli, grafica |
| `src/TIndustry.Shared/` | Simulazione indipendente da Godot: griglia, nastri, edifici, energia, economia, ricerca, campagna, salvataggi |
| `data/` | Unica fonte dei contenuti: `content.json`, `campaign.json` |
| `tests/` | Test xUnit della simulazione |
| `tools/` | `dev/` avvio, playtest e screenshot · `ci/` export |
| `docs/` | [Architettura](docs/ARCHITETTURA.md), [decisioni](docs/decisioni/), specifiche e piani |

La simulazione gira a tick fissi di 1/60 s, in modo deterministico. Per orientarti nel codice (ciclo di frame, dati, salvataggi, come aggiungere un blocco) parti da [`docs/ARCHITETTURA.md`](docs/ARCHITETTURA.md); per lavorare con agenti, [`CLAUDE.md`](CLAUDE.md).

### Release

I workflow GitHub Actions non partono su push o PR:

- [`release.yml`](.github/workflows/release.yml): su tag `v*` o avvio manuale — test ed export Godot, zip nella Release.
- [`build-publish.yml`](.github/workflows/build-publish.yml): solo manuale — build e test.

Export in locale: `tools/ci/setup-godot.sh` (una tantum), poi `tools/ci/export-godot.sh` → `dist/tIndustry-*-x64.zip`.

Il vecchio client Raylib è stato rimosso dopo la 0.3.4: il codice resta nel tag `v0.3.4`, gli zip nelle Release fino alla v0.3.4.

---

## Crediti

Icone da [game-icons.net](https://game-icons.net) (CC BY 3.0) e [Kenney](https://kenney.nl) (CC0): dettagli in [`godot/assets/ATTRIBUTION.md`](godot/assets/ATTRIBUTION.md).

Storia delle versioni: [CHANGELOG.md](CHANGELOG.md).

*Buon layout. Che i nastri non si intasino.*
