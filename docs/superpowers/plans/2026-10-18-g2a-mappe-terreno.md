# G2a — Mappe e terreno: piano di implementazione

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** biomi, pareti che bloccano, acqua bassa e profonda, partite normali generate da seme in 3 misure e mappe della campagna in file, con salvataggi v12 che continuano a leggere i v11.

**Architecture:** il terreno passa a celle compatte (suolo, parete, giacimento: un byte ciascuno) descritte da una tabella in `data/content.json`. Un generatore nuovo (`MapGenerator`) produce mappe a biomi; il generatore di oggi resta, congelato, come "classico" per i salvataggi v11 e per i test. Le regole di piazzamento leggono il terreno in un solo punto (`CanOccupyFootprint`). Le mappe della campagna sono PNG letti da un decodificatore PNG minimo in C# puro. Il client disegna il terreno a pezzi (chunk) preparati una volta.

**Tech Stack:** C# (.NET 8, `System.IO.Compression.ZLibStream` per i PNG), xUnit, Godot 4.4.1 .NET, Python + Pillow solo per strumenti di sviluppo esistenti.

**Spec:** [docs/superpowers/specs/2026-10-18-g2a-mappe-terreno-design.md](../specs/2026-10-18-g2a-mappe-terreno-design.md)

## Global Constraints

- Simulazione deterministica: generazione solo da seme e impostazioni; niente `Random` non seminato, niente ordine di `Dictionary`/`HashSet`, niente orologio, niente cultura nei formati; float solo in espressioni con ordine fisso (stesso risultato su Windows e Linux: preferire interi e virgola fissa per il rumore).
- `src/TIndustry.Shared` senza dipendenze da Godot o dal sistema operativo.
- Biomi: rocce e basalto, sabbia e deserto, neve e ghiaccio, prati e paludi. Suoli (`FloorKind : byte`): `Stone, Basalt, Gravel, Sand, DarkSand, Snow, Ice, Grass, Moss, Mud, Soil, ShallowWater, DeepWater`. Pareti (`WallKind : byte`): `None, StoneWall, SandWall, IceWall, ShrubWall`. Giacimenti (`DepositKind : byte`) come oggi.
- Regole: parete o acqua profonda → non si costruisce (nessun blocco, nastro, estremità di ponte); acqua bassa → si costruisce; il ponte scavalca tutto tra ingresso e uscita; il drone vola sopra tutto. Motivi nuovi in `CommandReasons`: `Wall` = "C'è una parete", `DeepWater` = "Acqua profonda". Fuori mappa (x ≥ Width o y ≥ Height) → `Occupied`.
- Misure delle partite normali: media 1000, grande 1500, enorme 2000 (quadrate). Limite massimo del lato: 2000 (sostituisce `MaxGodotCampaignMapEdge = 1000`).
- Partenza garantita (generatore nuovo): area libera costruibile di raggio 10 attorno al Core; ferro 2×2 in `(core.X-4, core.Y)` e il corridoio `y = core.Y` da `core.X-6` a `core.X-1` libero (i playtest lo usano); rame entro 12 celle; percorso a terra (senza pareti né acqua profonda) dal Core ad almeno un giacimento di ferro, rame e carbone, verificato con riempimento; se manca, il generatore scava un passaggio.
- Prestazioni (Release, questa macchina): generazione 1000² ≤ 1,5 s, 2000² ≤ 6 s; memoria del terreno 3 byte per cella.
- Salvataggi **v12**; i v11 si leggono con il generatore classico; v10 e precedenti restano rifiutati.
- Le API di test restano compatibili: `NewGame.Sandbox(...)` e `NewGame.Campaign(...)` tengono per default il generatore **classico** (i test a coordinate fisse non cambiano); il client chiede esplicitamente il generatore nuovo.
- Testi in italiano, commenti in inglese, commit con `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`, mai dati reali del giocatore, Actions solo a mano.

## Review Focus

1. **Salvataggi v11 dopo l'aggiornamento**: una fabbrica costruita su acqua classica (che non bloccava) deve restare valida. Test: un v11 con un edificio su una cella `Water` classica si carica, l'edificio c'è, `CanPlace` sulle celle classiche d'acqua resta vero (classico: acqua = `ShallowWater`).
2. **Test a coordinate fisse**: con il generatore nuovo come default molti test romperebbero. Test: tutta la suite verde senza toccare le coordinate dei test esistenti (default classico).
3. **Mappe enormi nel client**: 81.000 `DrawRect` per frame a zoom minimo oggi; con 2000² e biomi non deve peggiorare. Test: misura FPS con mappa enorme a zoom minimo nel playtest dedicato (≥ 60 FPS) e memoria del client.
4. **Ponte e acqua profonda**: un fiume largo 3 celle si attraversa solo col ponte; l'uscita su parete o acqua profonda viene saltata. Test in `BridgeTerrainTests`.
5. **Mappe della campagna rovinate o modificate a mano**: colore sconosciuto, misura sbagliata, Core su parete. Test: `CampaignMapTests` con file di prova rotti → errore chiaro in italiano al caricamento, mai un crash a metà partita.

---

### Task 0: base (controllore)

- [ ] Base catture: quella candidata della G1 (o `baseline-sp4` se la G1 non è ancora approvata) — i confronti servono solo a vedere cosa cambia.
- [ ] Riferimento dati reali in `$TEMP/g2a-realdata-before.txt`; build, test, playtest verdi su `main`.

### Task 1: celle nuove, tabella del terreno e regole di piazzamento

**Files:**
- Modify: `src/TIndustry.Shared/Grid/TerrainMap.cs` (tipi e celle), `src/TIndustry.Shared/Factory.cs` (`CanOccupyFootprint`, `CanPlace`, `PlaceMiner`), `src/TIndustry.Shared/Simulation/SimHost.cs` (`PlacedOrOccupied`), `src/TIndustry.Shared/Simulation/Commands.cs` (`CommandReasons`), `src/TIndustry.Shared/Content/FactoryContent.cs` (sezione `terrain`), `data/content.json`, `godot/scripts/world/World.Drawing.cs` (colori dai nuovi suoli, minimo indispensabile)
- Create: `src/TIndustry.Shared/Grid/ClassicTerrainGenerator.cs` (il `Generate` di oggi spostato e congelato), `tests/TIndustry.Shared.Tests/TerrainRulesTests.cs`, `tests/TIndustry.Shared.Tests/ClassicGeneratorTests.cs`
- Modify tests: `TerrainMajorityTests.cs`, `FactoryScenarios.cs`, `SimHostTests.cs` (solo il costruttore delle celle)

**Interfaces:**
- Produces:
  - `public enum FloorKind : byte`, `public enum WallKind : byte`, `public enum DepositKind : byte` (valori elencati nei Global Constraints; `DepositKind` resta con gli stessi nomi e ordine).
  - `public readonly record struct TerrainTile(FloorKind Floor, WallKind Wall, DepositKind Deposit)` con `bool BlocksBuilding => Wall != WallKind.None || Floor == FloorKind.DeepWater`.
  - `TerrainMap`: celle in tre `byte[]` (o un `TerrainTile[]` da 3 byte) con le stesse API di oggi (`Width`, `Height`, `Seed`, indicizzatori, `InBounds`, `FromTiles`, `CountDepositTiles`, `MajorityDeposit`, `ItemIdForDeposit`) più `string Generator` ("classico" | "biomi") e `static TerrainMap Generate(...)` che **delega** a `ClassicTerrainGenerator.Generate` con la stessa firma di oggi.
  - `ClassicTerrainGenerator.Generate(int w, int h, int seed, IReadOnlySet<GridPosition> core, GridPosition starter)`: identico byte per byte all'attuale, con la mappa `Grass→Grass`, `Soil→Soil`, `Stone→Stone`, `Water→ShallowWater`, nessuna parete.
  - `FactoryContent.Terrain`: `IReadOnlyList<FloorDef>` e `IReadOnlyList<WallDef>` con `record FloorDef(string Id, FloorKind Kind, string Biome, string Color, bool Buildable)` e `record WallDef(string Id, WallKind Kind, string Biome, string Color)`; `content.json` → `"terrain": { "floors": [...], "walls": [...] }` (id in kebab-case, colori `#RRGGBB`).
  - `CommandReasons.Wall`, `CommandReasons.DeepWater`; `Factory.CanPlace` sceglie, nell'ordine: `OnCore`, `Wall`, `DeepWater`, `Occupied`.
- [ ] Step 1: test rossi — `ClassicGeneratorIsUnchanged` (impronta FNV delle celle di `Generate(120,120,7,…)` uguale a quella calcolata **prima** della modifica: calcolala sul codice attuale e scrivila nel test), `WallBlocksEveryBlockKind`, `DeepWaterBlocksBeltsButNotBridgeSpan`, `ShallowWaterIsBuildable`, `OutsideMapIsOccupied`, `ReasonsAreWallAndDeepWater`, `TerrainTableCoversEveryKind` (ogni valore degli enum ha una riga in `content.json`).
- [ ] Step 2: implementazione; i test esistenti che costruiscono `TerrainTile` con due argomenti passano al nuovo costruttore.
- [ ] Step 3: client: `World.Drawing` usa il colore della tabella per suolo e parete (pareti più scure), senza ancora i chunk.
- [ ] Step 4: suite completa verde, build 0/0, playtest `ui`/`tutorial` OK (mappa classica: nessuna differenza di gioco); commit; PR "feat(terreno): pareti, acqua profonda e tabella dei suoli (G2a 1/6)".

### Task 2: generatore a biomi

**Files:**
- Create: `src/TIndustry.Shared/Grid/MapGenerator.cs`, `src/TIndustry.Shared/Grid/MapGenSettings.cs`, `src/TIndustry.Shared/Grid/Noise.cs`, `tests/TIndustry.Shared.Tests/MapGeneratorTests.cs`
- Modify: `src/TIndustry.Shared/Simulation/NewGame.cs` (parametro `MapGenSettings? map = null`: null = classico), `src/TIndustry.Shared/Factory.cs` (`MaxMapEdge = 2000`)

**Interfaces:**
- Produces:
  - `public enum MapSize { Media = 1000, Grande = 1500, Enorme = 2000 }`.
  - `public sealed record MapGenSettings(int Width, int Height, int Seed, IReadOnlyList<string> Biomes, int WallDensityPercent = 100, int WaterPercent = 100, int DepositPercent = 100)` con `static MapGenSettings Normal(MapSize size, int seed)` (tutti e 4 i biomi).
  - `public static class MapGenerator { TerrainMap Generate(MapGenSettings s, IReadOnlySet<GridPosition> core, GridPosition starter); }` (`Generator = "biomi"`).
  - `public static class Noise { int Value2D(int x, int y, int seed, int cellSize); int Fractal(int x, int y, int seed, int baseCell, int octaves); }` — valori interi 0..65535, solo aritmetica intera (stessi risultati su ogni piattaforma).
  - `public static ulong TerrainHash(TerrainMap m)` (FNV-1a sulle celle) in `TerrainMap`.
- Algoritmo (scelte che i test non determinano): biomi da due rumori a bassa frequenza (temperatura, umidità: neve = freddo, deserto = caldo e secco, prati = umido, rocce = il resto), bordi tra biomi sfrangiati con un rumore fine; suolo del bioma da un rumore medio (2–3 suoli per bioma); pareti con rumore "a creste" sopra una soglia per bioma (più dense ai margini dei biomi e nelle rocce, meno nei prati); acqua dove l'elevazione è bassa (profonda al centro, anello bassa di 1–3 celle), più frequente in prati e neve, rara nel deserto; giacimenti a gruppi per bioma (ferro e carbone nelle rocce, rame e piombo nel deserto, titanio nella neve, tutto un po' nei prati), mai su parete o acqua profonda. Poi la partenza garantita dei Global Constraints con riempimento a 4 vicini.
- [ ] Step 1: test rossi — `SameSeedSameMap` (hash uguale), `DifferentSeedDifferentMap`, `AllFourBiomesOnMediumMap`, `StartAreaIsFreeAndReachable` (su 50 semi: area libera, ferro 2×2 in `(core.X-4, core.Y)`, corridoio libero, percorso a terra verso ferro, rame e carbone), `NoDepositOnWallOrDeepWater`, `GenerationTimeBudget` (1000² ≤ 1,5 s e 2000² ≤ 6 s in Release; `[Trait("Category","Perf")]`, saltato in Debug), `HashIsStableAcrossRuns` (valore fisso scritto nel test per seme 42 a 256²: protegge dal cambio involontario del generatore).
- [ ] Step 2: implementazione.
- [ ] Step 3: verifiche: suite verde (i test esistenti restano sul classico); commit; PR "feat(terreno): generatore a biomi (G2a 2/6)".

### Task 3: mappe della campagna in file

**Files:**
- Create: `src/TIndustry.Shared/Grid/PngReader.cs` (PNG 8 bit RGB/RGBA, non interlacciato, filtri 0–4, `ZLibStream`), `src/TIndustry.Shared/Grid/CampaignMapFile.cs`, `tools/MapTool/MapTool.csproj` + `Program.cs` (in `tIndustry.sln`), `data/maps/*.png` e `*.json`, `tests/TIndustry.Shared.Tests/PngReaderTests.cs`, `CampaignMapTests.cs`
- Modify: `src/TIndustry.Shared/Campaign/CampaignCatalog.cs` (campo `map`), `data/campaign.json`, `src/TIndustry.Shared/Simulation/NewGame.cs` (Campaign usa la mappa se c'è), test csproj, `godot/TIndustry.Godot.csproj`, `tools/ci/export-godot.sh`, `godot/scripts/world/World.cs` (percorso `maps/`)

**Interfaces:**
- Formato: `data/maps/<id>.png` = suolo e parete (un colore esatto per suolo e per parete, dalla tabella `terrain`; parete sopra suolo = colore della parete, il suolo sotto è quello del bioma più vicino), `data/maps/<id>.giacimenti.png` = stessa misura, trasparente tranne i giacimenti (un colore per minerale, da `ItemVisualColors`), `data/maps/<id>.json` = `{ "core": {"x":…, "y":…}, "biomes": [...], "note": "…" }`.
- `public static class PngReader { (int Width, int Height, byte[] Rgba) Read(Stream s); }` — errori come `InvalidDataException` con testo italiano.
- `public static class CampaignMapFile { TerrainMap Load(string dir, string mapId, FactoryContent content, out GridPosition coreOrigin); IReadOnlyList<string> Validate(string dir, string mapId, FactoryContent content); }` (`Generator = "campagna:<id>"`).
- `CampaignLevelDefinition.Map` (string?, id della mappa); se presente, misura e Core vengono dal file; `mapWidth/mapHeight/seed` restano per compatibilità ma ignorati.
- `tools/MapTool`: `dotnet run --project tools/MapTool -- genera <id> --misura N --seme S --biomi a,b` scrive i tre file dal `MapGenerator`; `valida [id…]` stampa i problemi e termina con 1 se ce ne sono; `anteprima <id>` scrive `.dev/mappe/<id>.png` ingrandita.
- Misure dei livelli (crescono): 01 200, 02 250, 03 300, 04 350, 05 400, 06 500, 07 600, 08 700, 09 850, 10 1000. Biomi: 01–03 prati e rocce; 04–05 + deserto; 06–08 tre biomi; 09–10 tutti (titanio nella neve dal 09). Ogni livello deve avere i giacimenti che servono ai suoi obiettivi (controllo nel `valida`: minerali richiesti dagli obiettivi e dalle ricette sbloccate presenti in quantità ≥ soglia nel raggio 60 dal Core).
- [ ] Step 1: test rossi — `PngReaderDecodesAllFilters` (PNG di prova creati con Pillow e salvati nei test: RGB, RGBA, ogni filtro), `PngReaderRejectsInterlaced`, `CampaignMapLoadsAndMatchesSize`, `UnknownColourIsReportedInItalian`, `CoreOnWallIsReported`, `EveryCampaignLevelHasAValidMap` (tutti i livelli di `campaign.json` passano `Validate`).
- [ ] Step 2: implementazione; genera i 10 livelli con `MapTool`, poi ritocco: Core libero, giacimenti degli obiettivi vicini, nessun giacimento chiuso da pareti. Annota nel rapporto seme e ritocchi per livello.
- [ ] Step 3: anteprime dei 10 livelli in `.dev/mappe/` per la revisione dell'utente.
- [ ] Step 4: verifiche; commit; PR "feat(terreno): mappe della campagna in file (G2a 3/6)".

### Task 4: salvataggi v12

**Files:**
- Modify: `src/TIndustry.Shared/Save/FactorySave.cs`, `src/TIndustry.Shared/Simulation/Snapshot.cs`, `src/TIndustry.Shared/Factory.cs` (Capture/Restore), test dei salvataggi (`SaveVersionTests` e affini)
- Create: `tests/TIndustry.Shared.Tests/SaveMigrationTests.cs`, una fixture v11 reale in `tests/TIndustry.Shared.Tests/fixtures/save-v11.json` (generata dal codice di `main` prima della modifica)

**Interfaces:**
- `CurrentVersion = 12`; nuovi campi `Generator` (string, default "classico" quando assente), `MapSize` (int, 0 = non usato), `MapId` (string?). `Snapshot.Read` e `FactorySaveStore.ReadFile` accettano 11 e 12 (11 → `Generator = "classico"`), rifiutano ≤ 10 come oggi. `Factory.Restore` rigenera con il generatore indicato (classico, biomi con `MapGenSettings.Normal`, oppure il file della campagna).
- [ ] Step 1: test rossi — `V11FixtureLoadsWithClassicTerrain` (stessa impronta del terreno, stesse strutture), `V12RoundTripBiomes`, `V12RoundTripCampaignMap`, `V10StillRejected`, `SaveWritesVersion12`.
- [ ] Step 2: implementazione.
- [ ] Step 3: verifiche; commit; PR "feat(salvataggi): v12 con generatore e mappa (G2a 4/6)".

### Task 5: client — terreno a chunk, Nuova partita, campagna

**Files:**
- Create: `godot/scripts/world/TerrainChunks.cs`, `godot/scripts/ui/panels/NewGamePanel.cs`
- Modify: `godot/scripts/world/World.Drawing.cs` (il terreno esce dal disegno immediato), `World.Session.cs` (nuova partita con misura e seme; campagna con mappa), `World.Panels.cs`, `godot/scripts/ui/panels/HomePanel.cs`, `SavesPanel.cs` (mostra "Biomi · 1500 · seme N" o il nome della mappa), `WorldCamera.cs` (limiti), `godot/scripts/dev/World.Playtest.cs`
**Interfaces:**
- `TerrainChunks : Node2D`: pezzi da 64×64 celle; ogni pezzo è una `Sprite2D` con una `ImageTexture` di 64×64 pixel (un pixel per cella, colore della tabella con leggera variazione da hash della cella; pareti più scure con bordo; acqua profonda più scura della bassa) scalata ×`TileSize`, filtro Nearest; si creano solo i pezzi entro la vista più un margine e si liberano quelli lontani (cache LRU di 256 pezzi); si rigenerano solo a nuova partita/caricamento. I marcatori dei giacimenti restano disegnati come oggi ma il controllo `IsBuildingTile` usa un insieme preparato una volta per frame.
- `NewGamePanel`: misura (Media, Grande, Enorme), seme (campo numerico già compilato con un seme casuale del client, pulsante "Nuovo seme", copia), "Inizia". Il percorso dei playtest (`OnHomeNewGame()`) resta: misura Media, seme 42, generatore a biomi.
- Fantasma: rosso su parete e acqua profonda con il motivo nel suggerimento in basso ("C'è una parete" / "Acqua profonda").
- [ ] Step 1: playtest `ui` e `tutorial` aggiornati al generatore a biomi (seme 42, Media): partenza garantita → stessi passi; catture nuove attese.
- [ ] Step 2: implementazione.
- [ ] Step 3: misura con `TINDUSTRY_PLAYTEST_PERF=1` estesa: mappa Enorme a zoom minimo, FPS medio e minimo, memoria (`OS.GetStaticMemoryUsage`); obiettivo ≥ 60 FPS e < 1,5 GB.
- [ ] Step 4: verifiche; catture in `.dev/g2a-task5/` (Media, Enorme, un livello della campagna, Nuova partita); commit; PR "feat(client): terreno a chunk e Nuova partita (G2a 5/6)".

### Task 6: documenti e chiusura

**Files:**
- Create: `docs/decisioni/0005-mappe-seme-e-file.md`
- Modify: `docs/decisioni.md`, `docs/ARCHITETTURA.md` (terreno, mappe, come aggiungere un suolo o un livello, `tools/MapTool`), `CLAUDE.md` (salvataggi v12; mappe della campagna in `data/maps/`), `godot/README.md`, `README.md`, `CHANGELOG.md`, `tools/dev/mappa_canvas.py` + canvas, spec G2a (link al piano), elimina `FactoryScenarios.SelfTestCampaign` se ancora senza chiamanti
- [ ] Step 1: documenti.
- [ ] Step 2: catture candidate in `.dev/baseline-g2a-candidata/` (non promuovere).
- [ ] Step 3: verifiche complete; controllo dati reali; commit; PR "docs: mappe a seme e in file, ADR 0005 (G2a 6/6)".
