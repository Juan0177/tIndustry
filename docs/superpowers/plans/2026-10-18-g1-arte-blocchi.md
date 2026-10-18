# G1 — Arte dei blocchi: piano di implementazione

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** sostituire gli sprite di edifici, logistica, Core, drone e oggetti con pixel-art originale a 32 px per cella generata da `tools/art/`, con animazioni di base, senza cambiare la simulazione.

**Architecture:** un generatore Python deterministico (tavolozza + forme + un modulo per famiglia) scrive i PNG in `godot/assets/`; il client li carica da un solo punto (`Sprites`), disegna gli edifici a strati e anima nastri e parti mobili con strisce di fotogrammi.

**Tech Stack:** Python 3 + Pillow (già installato, nessuna dipendenza nuova; test con `unittest` della libreria standard), Godot 4.4.1 .NET (C#), xUnit.

**Spec:** [docs/superpowers/specs/2026-10-18-g1-arte-blocchi-design.md](../specs/2026-10-18-g1-arte-blocchi-design.md)

## Global Constraints

- Pixel-art originale: nessun file o colore copiato da Mindustry; nessun asset di terzi nuovo.
- 32 px per cella del blocco; in gioco ×2 (cella del mondo 64 px), filtro Nearest nel mondo.
- Il generatore è deterministico: due esecuzioni → PNG identici byte per byte (nessun `random` senza seme fisso, nessun timestamp nei PNG: `Image.save(..., optimize=False)` senza metadati variabili).
- `src/TIndustry.Shared` non cambia comportamento; salvataggi restano v11; nessun test di simulazione cambia.
- Sprite solo in `godot/assets/` (piatto), nomi = id di `data/content.json`; parti con suffisso (`<id>-base`, `<id>-drill`, `<id>-glow`, `<id>-rotor`…); animazioni = strisce orizzontali `<id>-anim.png` (fotogrammi della stessa larghezza); tier `core-1…4`, `drone-1…3`; oggetti `<id>.png` (32×32, HUD) e `<id>-belt.png` (10×10, nastri).
- Ogni PNG nuovo ha il suo `.import` (generato con `APPDATA="$(mktemp -d)" godot4 --headless --path godot --import`, poi ripristino dei `.import` che differiscono solo per fine riga, vedi CLAUDE.md).
- Mai toccare i dati reali del giocatore; playtest solo con `tools/dev/playtest.sh`.
- Testi e commit in italiano, commenti nel codice in inglese; commit con `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; nessun trigger push/PR nelle Actions.
- Un ramo per task (`feat/g1-<n>-<nome>`), PR con base `main` (o la PR precedente se non ancora unita); CI a mano dopo il push.

## Review Focus

1. **Doppia tinta**: le icone degli oggetti oggi sono silhouette bianche colorate a runtime (`Modulate`/`ItemPalette.Tint` in World.Visuals, World.Drawing, HudStyle, CorePanel, MercatoPanel, BuildPalette, MindustryBeltVisual). Con icone colorate ogni tinta va tolta, o i colori escono sbagliati. Test: catture del Mercato e della barra con i colori attesi (revisione della tavola) e ricerca nel codice di `ItemPalette.Tint`/`ColorFor` sui percorsi delle icone.
2. **Scale fisse a 64 px**: `MindustryBeltVisual.cs:375,407` (`tileSize / 64f`), `World.Visuals.cs:441` (`ItemScale` per texture da 64). Con 32 px gli sprite uscirebbero a metà misura. Test: ogni scala usa la larghezza reale della texture (`tileSize / tex.GetWidth()`), verificata nelle catture `ui-hud`/`ui-belt-into-core`.
3. **File mancante = crash**: diversi `GD.Load` senza controllo (World.cs:136-143, MinerVisual, SmelterVisual, ExtractorVisual, MindustryBeltVisual:372) → `NullReference` su `.GetWidth()`. Con `Sprites` un id senza sprite dà un segnaposto visibile (magenta) e un `GD.PushWarning`, mai un crash. Test xUnit di copertura: nessun id del contenuto senza sprite.
4. **Mappe di fallback duplicate**: `IconFileFor` in ConstructionLayer, TechTreeCanvas e ContentValidationTests (express→fast, bridge→bridge) e `BuildPalette.cs:33`. Con gli sprite nuovi `conveyor-express` e `conveyor-bridge` hanno i loro file: le mappe spariscono e restano solo in `Sprites`. Test: la copertura usa direttamente l'id.
5. **Contorno dei cantieri**: `construct.gdshader` usa l'alfa (soglia 0,1) per il contorno dei cantieri; sprite con angoli trasparenti danno un contorno sagomato (voluto), ma parti a strati vanno composte in **un** PNG di anteprima per piani e cantieri (`<id>.png` = icona composta). Test: cattura di un cantiere a metà (playtest `ui` già lo mostra) e revisione della tavola.

---

### Task 0: base e area di lavoro (controllore, nessun codice)

- [ ] Base degli screenshot: `$TEMP/baseline-sp4/{ui,tutorial}` (approvata il 2026-10-18).
- [ ] Riferimento dei dati reali: elenco con date dei file in `%LOCALAPPDATA%\tIndustry` e `%APPDATA%\Godot\app_userdata\tIndustry` in `$TEMP/g1-realdata-before.txt`.
- [ ] Build, test e playtest verdi su `main`.

### Task 1: generatore, tavolozza, forme e punto unico per le texture

**Files:**
- Create: `tools/art/palette.py`, `tools/art/forme.py`, `tools/art/genera.py`, `tools/art/tavola.py`, `tools/art/blocchi/__init__.py`, `tools/art/test_genera.py`, `tools/art/README.md`
- Create: `godot/scripts/visuals/Sprites.cs` (+ `.uid` generato da Godot)
- Modify: `godot/scripts/visuals/construction/ConstructionLayer.cs` (TextureFor → Sprites), `godot/scripts/ui/panels/TechTreeCanvas.cs` (ResolveIcon → Sprites), `godot/scripts/ui/hud/BuildPalette.cs` (TexPath → id), `godot/scripts/ui/hud/HudStyle.cs` (ItemTexture → Sprites), `tests/TIndustry.Shared.Tests/ContentValidationTests.cs`

**Interfaces:**
- Produces (Python):
  - `palette.py`: costanti `OUT` (contorno) e rampe `METALLO`, `FULIGGINE`, `RAME`, `FERRO`, `PIOMBO`, `TITANIO`, `CARBONE`, `GRAFITE`, `SILICIO`, `BAGLIORE`, `ACCENTO`, `ENERGIA` (liste di tuple RGB da scuro a chiaro, 4–6 toni). Si parte dai colori dei campioni `.dev/pixelart-campioni/genera.py` (stile approvato) e da `ItemVisualColors.cs` per le tinte degli oggetti.
  - `forme.py`: `nuova(w, h) -> Image`, `pixel(im, x, y, c)`, `rett(im, x0, y0, x1, y1, c)`, `pannello(im, x0, y0, x1, y1, rampa, contorno=True)`, `bullone(im, x, y, rampa)`, `tubo(im, x0, y0, x1, y1, rampa)`, `griglia(im, x0, y0, x1, y1, rampa, passo)`, `ingranaggio(im, cx, cy, r, denti, rampa, angolo)`, `cerchio(im, cx, cy, r, rampa, pieno=True)`, `bagliore(im, cx, cy, r, rampa)`, `ombra(im, alfa)`, `striscia(fotogrammi: list[Image]) -> Image`, `componi(*strati) -> Image`. Luce da in alto a sinistra in tutte.
  - `blocchi/__init__.py`: registro `SPRITE: dict[str, Callable[[], Image]]` riempito dai moduli con un decoratore `@sprite("<nome file senza .png>")`.
  - `genera.py`: `main(out_dir="godot/assets", tavola_dir=".dev/arte") -> list[str]` (nomi scritti, ordinati); salva ogni PNG con `im.save(path, format="PNG", optimize=False)` dopo `im.info.clear()`; scrive la tavola con `tavola.py`. CLI: `python tools/art/genera.py [--solo NOME…] [--controlla]`; `--controlla` non scrive in `godot/assets` ma confronta i byte e termina con codice 1 se qualcosa differisce.
  - `tavola.py`: `tavola(nomi, vecchi_dir, nuovi_dir) -> Image`: per ogni sprite vecchio accanto a nuovo, a 1× e 2×, su fondo chiaro e scuro; salvata in `.dev/arte/tavola-<data>.png` e `.dev/arte/tavola.png`.
  - `test_genera.py` (`python -m unittest tools/art/test_genera.py`): `test_deterministico` (due generazioni in cartelle temporanee → byte identici), `test_misure` (ogni sprite di blocco ha lato = 32 × celle del blocco; strisce `-anim` con larghezza multipla dell'altezza o del fotogramma dichiarato), `test_copertura` (ogni id di `structures` e di `market` in `data/content.json`, più `money`, `core-1…4`, `drone-1…3`, ha uno sprite registrato).
- Produces (C#): `public static class Sprites` in `TIndustry.Godot`:
  - `Texture2D For(string id)` — `res://assets/{id}.png`, cache, segnaposto magenta 32×32 + `GD.PushWarning` se manca.
  - `Texture2D? TryFor(string id)` — null se manca (per l'HUD che oggi nasconde l'icona).
  - `Texture2D Item(string itemId)` = `For(itemId)`; `Texture2D BeltItem(string itemId)` = `For(itemId + "-belt")` con ripiego su `Item`.
  - `Texture2D[] Frames(string id)` — taglia `{id}-anim.png` in fotogrammi quadrati (lato = altezza) con `AtlasTexture`; cache.
  - `float ScaleToCells(Texture2D tex, int cells, int tileSize)` = `cells * tileSize / (float)tex.GetWidth()`.
- Consumes: niente dai task precedenti.

- [ ] Step 1: test Python rossi (`test_deterministico`, `test_misure`) prima di `genera.py`: `python -m unittest tools/art/test_genera.py` → errore di import; poi uno sprite di prova registrato solo nel test.
- [ ] Step 2: `palette.py`, `forme.py`, registro, `genera.py`, `tavola.py`. `test_copertura` si salta (`unittest.skipUnless`) finché non c'è `TINDUSTRY_ARTE_COMPLETA=1`: i task 2–5 aggiungono gli sprite famiglia per famiglia e il task 6 lo rende obbligatorio. Gli sprite non ancora rifatti restano i PNG di oggi in `godot/assets/`; `genera.py` scrive solo quelli registrati.
- [ ] Step 3: test xUnit `ContentValidationTests`: elimina `IconFileFor`; `EveryStructureHasAnIcon` usa `{id}.png` direttamente e diventa rosso per `conveyor-express` e `conveyor-bridge` → in questo task si aggiungono i due PNG copiando gli esistenti (`conveyor-fast.png`, `bridge.png`) con il nome giusto, così il test torna verde e i task 2 li ridisegnano.
- [ ] Step 4: `Sprites.cs`; ConstructionLayer, TechTreeCanvas, BuildPalette, HudStyle passano da `Sprites` (spariscono le tre mappe di ripiego e `BuildPalette.cs:33`).
- [ ] Step 5: verifiche: `python -m unittest tools/art/test_genera.py` OK; `python tools/art/genera.py --controlla` OK; build 0/0; test verdi; playtest `ui` e `tutorial` OK; catture identiche alla base (in questo task nessuno sprite visibile cambia).
- [ ] Step 6: `tools/art/README.md` (come si lancia, come si aggiunge uno sprite); commit; PR "feat(arte): generatore e punto unico per le texture (G1 1/6)".

### Task 2: logistica

**Files:**
- Create: `tools/art/blocchi/logistica.py`
- Modify: `godot/scripts/visuals/belts/ScrollingBeltStrip.cs`, `BeltCornerTile.cs`, `MindustryBeltVisual.cs`, `BridgeSpanVisual.cs`; `godot/shaders/belt_scroll.gdshader` e `belt_corner_v2.gdshader` (eliminati se non più usati); asset PNG generati
**Interfaces:**
- Consumes: `forme.*`, `palette.*`, `@sprite`, `Sprites.For/Frames/ScaleToCells`.
- Produces (sprite): per `conveyor-basic`, `conveyor-fast`, `conveyor-express`: `<id>.png` (icona 32×32, freccia nel senso Est), `<id>-anim.png` (4 fotogrammi 32×32 del nastro dritto verso Est che scorre di 8 px per fotogramma, ciclo continuo), `<id>-corner-anim.png` (4 fotogrammi della curva entra-da-Ovest-esce-a-Sud). Tre tier riconoscibili (base: rullo grigio; veloce: bordi rame; express: bordi titanio/accento). `junction.png`, `splitter.png`, `sorter.png` (centro vuoto di 14×14 px per il glifo del filtro), `conveyor-bridge.png` (estremità, Est), `conveyor-bridge-span.png` (16 px di campata ripetibile, Est).
- [ ] Step 1: aggiungi a `test_genera.py` `test_strisce_cicliche` (il fotogramma 4 scorso di 8 px coincide col fotogramma 1).
- [ ] Step 2: sprite in `logistica.py`.
- [ ] Step 3: client: nastri dritti come una `Sprite2D` per cella con la striscia (fotogramma scelto da un orologio condiviso: `frame = (int)(scroll * 4) % 4`, con `scroll` che avanza a `RateItemsPerSecond` celle/s come oggi), ruotati per direzione; curve con la loro striscia, stessa rotazione e `FlipH` di `BeltCornerTile.ResolveTransform`; scale con `Sprites.ScaleToCells`. `MindustryBeltVisual.cs:375,407` usano la larghezza reale. Campata del ponte: `Sprite2D` con `RegionEnabled` e texture in modalità ripetizione (`TextureRepeat = Enabled`) lunga quanto la campata, spessore 0,70 cella come oggi; le frecce che si accendono restano procedurali sopra.
- [ ] Step 4: prestazioni: un nodo per cella di nastro. Misura con 200 nastri (playtest esistente con 184 piani del QA come riferimento: FPS medio ≥ 150 a 1280×720) — se peggio, un `MultiMeshInstance2D` per tier; annota la misura nel rapporto.
- [ ] Step 5: verifiche (build, test, `python -m unittest`, `genera.py --controlla`, playtest `ui`/`tutorial` OK, tavola in `.dev/arte/`); commit; PR "feat(arte): logistica (G1 2/6)".

### Task 3: produzione e potenza

**Files:**
- Create: `tools/art/blocchi/produzione.py`, `tools/art/blocchi/potenza.py`
- Modify: `godot/scripts/visuals/buildings/{MinerVisual,SmelterVisual,AssemblerVisual,ExtractorVisual,GeneratorVisual,BuildingPad}.cs`
- Delete: `godot/assets/miner_drill_sheet.png` (+ `.import`), `miner-pad*.png`, `gear-*.png` se non più usati (verifica con grep di tutto il repo, test compresi)
**Interfaces:**
- Produces (sprite, 64×64 per i 2×2, 32×32 per l'1×1):
  - `miner`, `miner-advanced`: `<id>-base.png`, `<id>-drill-anim.png` (8 fotogrammi della trivella che gira — non un ventilatore: punta conica/elicoidale vista dall'alto con denti, più corta del lato), `<id>.png` (composto: base + fotogramma 1); avanzato: accenti rossi e lampada `miner-advanced-lamp.png` accesa con energia.
  - `smelter`: `smelter-base.png`, `smelter-glow-anim.png` (4 fotogrammi di bocca incandescente), `smelter.png` composto.
  - `assembler`: `assembler-base.png`, `assembler-arm.png` (braccio che si muove avanti e indietro, posizione dal codice come oggi), `assembler.png` composto.
  - `extractor`: `extractor.png` con centro vuoto Ø 8 px per il punto del filtro (oggi `DotWell` r=0,13 cella).
  - `generator`: `generator-base.png`, `generator-rotor-anim.png` (4 fotogrammi), `generator.png` composto.
  - `power-node` (1×1), `power-node-t2` (2×2): `<id>.png`.
  - Basi diverse tra blocchi (forma del telaio, bulloni, colori): criterio di revisione della spec.
- [ ] Step 1: `test_misure` copre i nuovi nomi; revisione della tavola come verifica visiva.
- [ ] Step 2: sprite.
- [ ] Step 3: visual a strati: base `Sprite2D`, parte animata `Sprite2D` con `Sprites.Frames` (fotogramma dall'orologio del visual, velocità piena se il blocco lavora — `miner.Efficiency>0`, `IsCrafting`, `IsGenerating` come oggi — ferma altrimenti), bagliore sopra con `Modulate` che pulsa (riusa il `_pulse` di SmelterVisual). Via i `Polygon2D`/`Line2D` che disegnavano i blocchi (restano solo il punto del filtro dell'estrattore e il bordo del minatore se ancora utile). Nessun cambio di dimensioni o di ZIndex.
- [ ] Step 4: verifiche come task 2, più una cattura manuale con tutti i blocchi accesi e spenti (profilo temporaneo, scenario playtest esistente esteso o seed manuale in `.dev/g1-task3/`).
- [ ] Step 5: commit; PR "feat(arte): produzione e potenza (G1 3/6)".

### Task 4: Core e drone

**Files:**
- Create: `tools/art/blocchi/core.py`, `tools/art/blocchi/drone.py`
- Modify: `godot/scripts/world/World.Visuals.cs` (AddCoreVisual), `godot/scripts/world/World.cs:143`, `godot/scripts/visuals/construction/DroneVisual.cs`, `godot/scripts/ui/hud/CorePanel.cs:66`, `TechTreeCanvas` (root `core` → `core-1`)
**Interfaces:**
- Produces: `core-1.png` 64×64 (2×2), `core-2.png` 96×96, `core-3.png` 128×128, `core-4.png` 160×160, ognuno con `core-<n>-light-anim.png` (4 fotogrammi, luce lenta sul nucleo); `core.png` = copia di `core-1` (icona per HUD e ricerca). `drone-1.png` (alpha), `drone-2.png` (beta), `drone-3.png` (gamma): vista dall'alto con il muso verso Est, 24×24 / 28×28 / 32×32 px, sagome diverse (alpha a freccia, beta con due motori, gamma più grande con quattro ali).
- [ ] Step 1: `test_misure` e copertura per i tier.
- [ ] Step 2: sprite.
- [ ] Step 3: Core in partita con `core-1` + luce animata; il contorno ciano resta. DroneVisual disegna `drone-1` ruotato secondo la direzione (oggi calcolata per i poligoni), con la scia e il raggio procedurali che restano; ombra del drone (lo stesso sprite scuro, alfa 0,35, spostato di (6, 6) px). Tier 2–4 e beta/gamma: solo nella tavola.
- [ ] Step 4: verifiche come task 2; maschere di `compare_shots.py` del drone ricontrollate (il drone ora è uno sprite: le maschere servono ancora per l'ondeggiamento).
- [ ] Step 5: commit; PR "feat(arte): Core in 4 tier e droni (G1 4/6)".

### Task 5: oggetti e HUD

**Files:**
- Create: `tools/art/blocchi/oggetti.py`
- Modify: `godot/scripts/world/World.Visuals.cs` (SyncItemSprites, ResolveItemTexture, ItemScale), `World.Drawing.cs` (marcatori dei giacimenti), `godot/scripts/ui/hud/{HudStyle,CorePanel,BuildPalette}.cs`, `godot/scripts/ui/panels/MercatoPanel.cs`, `godot/scripts/visuals/belts/MindustryBeltVisual.cs` (glifo del selettore), `godot/scripts/visuals/items/{ItemPalette,ItemSilhouette}.cs`, `godot/assets/ATTRIBUTION.md`
**Interfaces:**
- Produces: per i 12 oggetti (`iron-ore`, `copper-ore`, `coal`, `lead-ore`, `titanium-ore`, `iron-plate`, `lead-plate`, `titanium-plate`, `copper-wire`, `graphite`, `silicon`, `money`): `<id>.png` 32×32 e `<id>-belt.png` 10×10, colorati (niente tinta a runtime), con contorno scuro; minerali = pietre con riflessi del colore, lastre = piastre in prospettiva, filo = bobina, grafite = barre, silicio = cristalli, monete = pila.
- [ ] Step 1: test xUnit rosso `BeltItemTexturesExistForEveryMarketItem` (ogni oggetto del mercato e `money` hanno `<id>.png` e `<id>-belt.png`).
- [ ] Step 2: sprite.
- [ ] Step 3: nastri: un solo `Sprite2D` per oggetto con `Sprites.BeltItem(id)` a scala ×2 (≈ 20 px), niente contorno calcolato (`ItemSilhouette` eliminato se non più usato), niente tinta; via la mappa di `ResolveItemTexture` (ogni oggetto ha il suo sprite: corregge il ripiego su `iron-ore` per lastre, grafite e silicio). HUD, Mercato, barra, selettore, chip dei costi: `Sprites.Item(id)` senza `Modulate`/`Tint`; le icone sotto 32 px restano col filtro lineare dell'HUD, quelle a 32 px o più passano a Nearest. Giacimenti: il marcatore usa lo sprite del minerale senza tinta (il terreno vero è G2).
- [ ] Step 4: `ATTRIBUTION.md` ridotto alle icone di `godot/assets/ui/` ancora di game-icons; via `research.png` della radice se non usato (grep).
- [ ] Step 5: verifiche come task 2; commit; PR "feat(arte): oggetti e HUD (G1 5/6)".

### Task 6: pulizia, documenti e base candidata

**Files:**
- Delete: `tools/gen_structure_blocks.py`, asset orfani (verifica con grep)
- Create: `docs/decisioni/0004-arte-generata-da-script.md`
- Modify: `docs/decisioni.md`, `docs/ARCHITETTURA.md` ("come aggiungere lo sprite di un blocco"), `CLAUDE.md` (regola: sprite solo da `tools/art/genera.py`), `godot/README.md`, `README.md` (anteprima con catture vere, niente emoji), `CHANGELOG.md`, `tools/dev/compare_shots.py` (maschere), `tools/dev/mappa_canvas.py` + `docs/mappa-progetto.canvas`, `docs/superpowers/specs/2026-10-18-g1-arte-blocchi-design.md` (link al piano)
- [ ] Step 1: `test_copertura` diventa sempre attivo (via lo `skipUnless`) e passa.
- [ ] Step 2: documenti e ADR (perché arte da script, regole di tavolozza e luce, come rigenerare, tier e nomi).
- [ ] Step 3: catture candidate: `tools/dev/playtest.sh ui .dev/baseline-g1-candidata/ui` e `tutorial`; maschere ricalibrate confrontando due esecuzioni identiche (rumore solo dove serve); tavola finale `.dev/arte/tavola.png`. **Non** promuovere la base: la promuove il controllore dopo l'ok dell'utente.
- [ ] Step 4: verifiche complete; controllo dati reali con `$TEMP/g1-realdata-before.txt`; commit; PR "docs: arte generata da script e ADR 0004 (G1 6/6)".
