# Test e playtest

Quali prove automatiche ci sono, come scriverne di nuove per la simulazione e come far giocare il client da solo per controllare l'interfaccia.

## Test automatici

```bash
dotnet build tIndustry.sln -c Release -nologo -v q
dotnet test tests/TIndustry.Shared.Tests -c Release --no-build      # simulazione
dotnet test tests/TIndustry.AgentPlay.Tests -c Release --no-build   # server MCP degli agenti
python -m unittest tools/art/test_genera.py                         # generatore degli sprite
python -m unittest discover -s tools/sito -p "test_*.py"            # sito
node --test tools/sito/test_sito.mjs tools/sito/test_racconto.mjs   # script del sito
```

`tests/TIndustry.Shared.Tests` (xUnit) copre nastri, produzione, energia, costruzione, salvataggi, Ricerca, Mercato, campagna, determinismo e validazione di `data/`. Alcuni test fanno da guardia alle regole del progetto:

| Test | Cosa protegge |
|---|---|
| `DeterminismTests`, `SnapshotTests`, `SystemOrderTests` | Regole di determinismo e ordine dei sistemi |
| `ContentValidationTests` | Coerenza di `data/content.json` e `campaign.json` |
| `TestiTests` | Testi solo da chiavi, italiano e inglese allineati |
| `ClassicGeneratorTests`, `MapGeneratorTests` | Impronte dei generatori di terreno pubblicati |
| `EveryCampaignLevelHasAValidMap` | Mappe della campagna valide |
| `TerrainLookTests` | Regola del terreno uguale in C# e in Python |

I test usano sempre cartelle temporanee, mai i dati del giocatore.

## Scrivere un test della simulazione

- **Costruzione istantanea.** Un test che non prova la costruzione usa `GameRules(InstantBuild: true)`, per esempio `NewGame.Sandbox(..., rules: new GameRules(InstantBuild: true))`: così i comandi passati a `SimHost` posano il blocco nel tick del comando invece di creare un piano per il drone. I metodi `Factory.TryPlace…` piazzano sempre subito.
- **Terreno.** `NewGame.Sandbox(...)` e `NewGame.Campaign(...)` usano per default il generatore **classico**, congelato, a coordinate fisse. Il generatore a biomi si chiede con `map: MapGenSettings.Normal(misura, seme)`, la mappa vera di un livello con `mapsDirectory:`.
- **Tempo.** `SimHost.AdvanceTicks(n)` esegue esattamente `n` tick, senza orologio.

## Playtest del client

I playtest avviano il gioco vero e lo fanno giocare da un copione con input simulati:

```bash
tools/dev/playtest.sh ui /tmp/shots-ui              # anche: tutorial, agenti
tools/dev/dev.sh --playtest ui                       # stessa cosa, screenshot in .dev/playtest-ui
```

- Ogni esecuzione usa un **profilo dati temporaneo** (`TINDUSTRY_DATA_DIR` e, su Windows, `APPDATA`), quindi non tocca impostazioni e salvataggi reali.
- Salva un PNG per passo, `log.txt` e `godot.log` nella cartella indicata.
- Esce con 0 solo se il log contiene `PLAYTEST OK`, nessuna riga `ERROR` e nessun segno di chiusura sporca del motore.
- Gira sempre in una finestra 1280×720 con la Scala UI al 100%, con effetti spenti e orologi fermi, così le catture si possono confrontare.

| Playtest | Cosa prova |
|---|---|
| `ui` | HUD, palette, pannelli, nastri e costruzione, lingue, varianti di colore, Scala UI, Home |
| `tutorial` | Il tutorial dall'inizio alla fine |
| `agenti` | Il canale di controllo degli agenti (`AgentBridge`), senza il server MCP |

Variabili utili:

| Variabile | Effetto |
|---|---|
| `TINDUSTRY_PLAYTEST_LINGUA=en` | Gioca il playtest intero in inglese (predefinito `it`) |
| `TINDUSTRY_PLAYTEST_SCALA=<percento>` | Gioca il playtest intero a quella Scala UI |
| `TINDUSTRY_PLAYTEST_PERF=1` | Nel playtest `ui` misura FPS e nodi con molti nastri ed edifici e sulla mappa Enorme |
| `TINDUSTRY_PLAYTEST_EFFETTI=1` | Accende effetti, ombre, bagliore e notte e misura le prestazioni |
| `TINDUSTRY_PLAYTEST_BLOCCHI=1` | Cattura ogni blocco di produzione e potenza spento e acceso |
| `TINDUSTRY_CAPTURE_DIR` | Dove vanno gli screenshot lanciando Godot a mano (predefinito `godot/artifacts/`) |

Lanciando Godot a mano, `TINDUSTRY_PLAYTEST=ui|tutorial|agenti` richiede `TINDUSTRY_DATA_DIR`.

## Confronto degli screenshot

```bash
python tools/dev/compare_shots.py <baseline> <nuovi>
```

Confronta i PNG con lo stesso nome ed esce con 1 se differiscono, indicando il rettangolo della differenza. Ignora il contatore degli FPS e poche zone piccole legate ai tempi (elencate nello script). Una base di catture si fa con tre esecuzioni per playtest e lingua che il confronto deve trovare identiche.

## Per approfondire

- [docs/ARCHITETTURA.md, Test e playtest](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md#test-e-playtest)
- [godot/README.md, Prove automatiche](https://github.com/Juan0177/tIndustry/blob/main/godot/README.md#prove-automatiche)
- [tools/dev/playtest.sh](https://github.com/Juan0177/tIndustry/blob/main/tools/dev/playtest.sh) · [tools/dev/compare_shots.py](https://github.com/Juan0177/tIndustry/blob/main/tools/dev/compare_shots.py)
- [Compilare e avviare](Compilare-e-avviare) · [Agenti che giocano](Agenti-che-giocano) · [Architettura](Architettura)
