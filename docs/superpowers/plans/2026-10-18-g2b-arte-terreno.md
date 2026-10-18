# G2b — Arte del terreno: piano di implementazione

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** dare al terreno della G2a l'aspetto di Mindustry con pixel-art generata da `tools/art/`: suoli con varianti, bordi tra suoli, pareti raccordate, acqua animata, giacimenti a puntini, decorazioni; senza perdere fluidità sulle mappe enormi.

**Architecture:** il generatore della G1 produce gli sprite del terreno; il client compone i pezzi di terreno vicini alla telecamera in immagini (16×16 celle, 32 px per cella) con suolo, bordi, pareti, giacimenti e decorazioni; lontano dalla telecamera restano i pezzi a colori piatti di oggi (un pixel per cella). L'acqua si anima con uno shader sopra il pezzo.

**Tech Stack:** Python 3 + Pillow 12.3 (generatore esistente), Godot 4.4.1 .NET (C#, `Image.BlendRect`, `ShaderMaterial`), xUnit.

**Spec:** [docs/superpowers/specs/2026-10-18-g2b-arte-terreno-design.md](../specs/2026-10-18-g2b-arte-terreno-design.md)

## Global Constraints

- Stesse regole della G1 (ADR 0004): arte solo da `python tools/art/genera.py`, 32 px per cella mostrati ×2 Nearest, tavolozza e luce comuni, strisce di animazione a fotogrammi quadrati, nessun asset di terzi, generatore deterministico (controllo `--controlla`, in CI `--controlla --pixel`).
- Nomi degli sprite: `terreno-<id-suolo>-<n>.png` (n = 0..3 varianti, 32×32); `terreno-<id-suolo>-bordo.png` (striscia di 8 pezzi 32×32: N, E, S, O, NE, SE, SO, NO); `parete-<id-parete>.png` (striscia di 16 pezzi 32×32 per la "doppia griglia", indice = bit dei 4 angoli NO=1, NE=2, SE=4, SO=8); `acqua-bassa-anim.png` e `acqua-profonda-anim.png` (4 fotogrammi 32×32 ripetibili); `giacimento-<id-oggetto>-<n>.png` (n = 0..2, 32×32 trasparente); `deco-<bioma>-<n>.png` (2–3 per bioma, 32×32 trasparente). Gli id di suoli e pareti sono quelli della tabella `terrain` di `data/content.json`.
- Variante, decorazione e puntini scelti da un hash della cella (deterministico, stesso aspetto a ogni caricamento); le decorazioni sono solo grafica e non stanno su pareti, acqua o giacimenti (gli edifici le coprono da soli).
- Ordine dei bordi: ogni suolo ha una priorità (campo `edgePriority` nella tabella `terrain`); il suolo a priorità più alta disegna il suo bordo sulle celle vicine a priorità più bassa. L'acqua ha la priorità più bassa.
- Livelli di dettaglio: celle ≥ 16 px a schermo (zoom ≥ 0,25) → pezzi d'arte; sotto → pezzi a colori piatti di oggi. Pezzi d'arte 16×16 celle (512×512 px, 1 MiB), cache LRU di massimo 96 pezzi d'arte; i pezzi a colori restano 64×64 celle come oggi.
- Prestazioni: Enorme a zoom minimo come oggi (≥ 200 FPS medi su questa macchina); a zoom 1 scorrendo, FPS medio ≥ 120 e nessun pezzo composto in più di 4 ms (composizione distribuita: al massimo 4 pezzi nuovi per frame); memoria del processo < 900 MiB su Enorme.
- Simulazione, salvataggi e regole non cambiano.
- Testi in italiano, commenti in inglese, commit con `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`, mai dati reali del giocatore, Actions solo a mano.

## Review Focus

1. **Memoria sulle mappe enormi**: comporre pezzi d'arte a zoom basso esploderebbe la memoria. Test: probe PERF con zoom che va da 0,08 a 2 e ritorno su Enorme: memoria massima < 900 MiB e i pezzi d'arte non superano mai 96.
2. **Scatti durante lo scorrimento**: comporre molti pezzi nello stesso frame blocca il gioco. Test: probe PERF misura il frame più lento mentre si scorre a zoom 1 (≤ 25 ms).
3. **Cuciture tra pezzi**: bordi e pareti al confine tra due pezzi devono combaciare (il pezzo legge le celle vicine fuori dal suo quadrato). Test: test C# della funzione di composizione che confronta un'area 32×32 celle composta in un pezzo con la stessa area composta in quattro pezzi.
4. **Leggibilità del gioco**: il terreno non deve coprire nastri, oggetti ed edifici. Test: catture `ui-belt-into-core` e BLOCCHI su suoli diversi; revisione della tavola.
5. **Giacimenti riconoscibili**: i puntini devono distinguere i 5 minerali a zoom 0,5. Test: cattura dedicata con tutti i minerali vicini e revisione.

---

### Task 0: base (controllore)

- [ ] Base di confronto: `.dev/baseline-g2a-candidata` (non ancora approvata). Riferimento dati reali `$TEMP/g2b-realdata-before.txt`. Build, test e playtest verdi su `main`.

### Task 1: sprite del terreno nel generatore

**Files:**
- Create: `tools/art/blocchi/terreno.py`
- Modify: `tools/art/test_genera.py`, `tools/art/palette.py` (rampe dei suoli, dalle tinte della tabella `terrain`), `data/content.json` (campo `edgePriority` per ogni suolo), `src/TIndustry.Shared/Content/FactoryContent.cs` (lettura e validazione di `edgePriority`), test di contenuto
**Interfaces:**
- Produces: tutti gli sprite dei Global Constraints; `terreno.py` legge gli id da `data/content.json` (nessun elenco ricopiato a mano).
- [ ] Step 1: test rossi: `test_terreno_copertura` (ogni suolo ha 4 varianti e il bordo, ogni parete i 16 pezzi, ogni minerale 3 varianti, ogni bioma ≥ 2 decorazioni), `test_varianti_ripetibili` (ogni variante si affianca a sé stessa e alle altre dello stesso suolo senza cuciture visibili: bordo sinistro e destro con la stessa media di colore entro una soglia), `test_parete_doppia_griglia` (il pezzo 15 è pieno, lo 0 è vuoto, i pezzi a un angolo sono rotazioni coerenti), test xUnit `EveryFloorHasEdgePriority`.
- [ ] Step 2: sprite. Stile: suoli con rumore fine a 2–3 toni e qualche sassolino; bordi frastagliati; pareti con volume, luce in alto a sinistra e ombra in basso a destra; acqua con increspature; puntini dei minerali con riflesso del colore dell'oggetto (lead blu-violetto).
- [ ] Step 3: tavola del terreno `.dev/arte/tavola-terreno.png` (per ogni bioma: una finestra 12×12 celle composta da Python con varianti, bordi, pareti, acqua, giacimenti e decorazioni).
- [ ] Step 4: verifiche (`python -m unittest`, `--controlla`, build, test); commit; PR "feat(arte): sprite del terreno (G2b 1/3)".

### Task 2: terreno disegnato con l'arte nel client

**Files:**
- Create: `godot/scripts/world/TerrainComposer.cs` (composizione pura di un pezzo da celle e sprite; testabile), `godot/shaders/water.gdshader`
- Modify: `godot/scripts/world/TerrainChunks.cs` (due livelli di dettaglio, LRU dei pezzi d'arte, composizione distribuita), `godot/scripts/world/World.Drawing.cs` (marcatori dei giacimenti solo sotto zoom 0,25), `godot/scripts/visuals/Sprites.cs` (accesso alle strisce del terreno), `godot/scripts/dev/World.Playtest.cs` (probe PERF con zoom variabile; cattura dei minerali)
- Create test: `tests/TIndustry.Shared.Tests/` non vede Godot: la logica di scelta (variante, bordo, indice della doppia griglia, decorazione) va in una classe pura in `src/TIndustry.Shared/Grid/TerrainLook.cs` testata con xUnit; il client fa solo i blit.
**Interfaces:**
- `public static class TerrainLook` (Shared): `int Variant(int x, int y, int seed)` (0..3), `int WallMask(TerrainMap m, int x, int y)` (0..15, doppia griglia sugli angoli), `IEnumerable<(FloorKind Floor, int Piece)> EdgesOn(TerrainMap m, int x, int y, IReadOnlyDictionary<FloorKind,int> priority)` in ordine di priorità crescente, `int? Decoration(TerrainMap m, int x, int y, int seed)`.
- `TerrainComposer.Compose(TerrainMap m, int cx, int cy, TerrainSprites s) -> Image` (512×512).
- [ ] Step 1: test xUnit rossi: `VariantIsDeterministicAndSpread`, `WallMaskMatchesCorners`, `EdgesComeFromHigherPriorityNeighboursOnly`, `DecorationNeverOnWallsWaterOrDeposits`, `ChunkSeamsMatch` (con un compositore di prova in C# puro che lavora su indici, non su immagini).
- [ ] Step 2: implementazione; acqua: il pezzo contiene il fondale statico, lo shader `water.gdshader` aggiunge le increspature animate in coordinate del mondo solo dove la maschera dell'acqua (canale alfa di una seconda texture del pezzo) è piena; con `TINDUSTRY_PLAYTEST` il tempo dello shader resta 0.
- [ ] Step 3: probe PERF esteso (zoom 0,08 → 2 → 0,08 su Enorme e scorrimento a zoom 1): FPS medio e minimo, frame più lento, memoria massima, numero massimo di pezzi d'arte.
- [ ] Step 4: catture in `.dev/g2b-task2/` (partenza Media a zoom 1, un punto con tutti i biomi, minerali vicini, Enorme a zoom minimo, campagna livello 1); due esecuzioni dei playtest con 0 differenze tra loro.
- [ ] Step 5: verifiche; commit; PR "feat(client): terreno con l'arte, due livelli di dettaglio (G2b 2/3)".

### Task 3: documenti e base candidata

**Files:**
- Modify: `docs/decisioni/0004-arte-generata-da-script.md` (sezione terreno), `docs/ARCHITETTURA.md` (come aggiungere un suolo con il suo sprite), `README.md` (anteprima con catture vere), `CHANGELOG.md`, `godot/README.md` se serve, `tools/dev/mappa_canvas.py` + canvas, `tools/dev/compare_shots.py` (maschere solo se servono, misurate tra due esecuzioni)
- [ ] Step 1: documenti.
- [ ] Step 2: catture candidate `.dev/baseline-g2b-candidata/{ui,tutorial}` (due esecuzioni, 0 differenze), tavola finale.
- [ ] Step 3: verifiche complete, controllo dati reali; commit; PR "docs: arte del terreno (G2b 3/3)".
