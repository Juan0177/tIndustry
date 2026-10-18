# G3 — Effetti, ombre e luci: piano di implementazione

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** gli effetti di Mindustry (particelle di lavoro, costruzione e ambiente, ombre, bagliori, notte opzionale con lampade) con impostazioni grafiche per i PC deboli, senza cambiare la simulazione salvo il blocco Lampada e il flag della notte.

**Architecture:** un gestore unico degli effetti nel client (pool di particelle, limite per schermata, solo nel visibile) guidato da definizioni in un file di dati; i visual degli edifici e gli eventi della simulazione chiedono gli effetti per nome. Ombre come sagome scure sotto gli sprite; bagliore con HDR 2D e `WorldEnvironment`; notte con `CanvasModulate` e `PointLight2D` sulle sorgenti. Le impostazioni grafiche stanno in `ClientSettings`.

**Tech Stack:** Godot 4.4.1 .NET (C#, `CPUParticles2D`, `WorldEnvironment`, `PointLight2D`, `CanvasModulate`), Python + Pillow (`tools/art`) per gli sprite delle particelle e della lampada, xUnit per la parte Shared.

**Spec:** [docs/superpowers/specs/2026-10-18-g3-effetti-luci-design.md](../specs/2026-10-18-g3-effetti-luci-design.md)

## Global Constraints

- Gli effetti sono solo aspetto: nessun riferimento agli effetti in `src/TIndustry.Shared`; il determinismo della simulazione non cambia. Uniche aggiunte Shared: blocco `lamp` (Lampada) e flag `Night` (vedi Task 3).
- Particelle con `CPUParticles2D` (stesso comportamento su ogni GPU), riusate da un pool; limite massimo di particelle attive per schermata: Alti 4000, Bassi 1200, Spenti 0. Effetti solo dentro la vista più un margine di 4 celle.
- Impostazioni nuove in `ClientSettings` (con `Clone`, `CopyFrom`, `Normalize`, `ApplyToEngine`, controlli in `SettingsPanel` dentro uno `ScrollContainer`): `Effetti` (`Alti`/`Bassi`/`Spenti`, predefinito `Alti`), `Ombre` (sì), `Bagliore` (sì), `AnimazioniBlocchi` (sì), `LimiteFps` (0 = nessuno, 30, 60, 120, 144), `ScalaRendering` (100, 75, 50: `Window.ContentScaleMode = Viewport` con dimensione ridotta e ingrandimento). I file di impostazioni vecchi restano validi (campi mancanti = predefiniti).
- Playtest: con `TINDUSTRY_PLAYTEST` gli effetti sono **Spenti**, il bagliore spento, la notte spenta e gli orologi fermi, salvo `TINDUSTRY_PLAYTEST_EFFETTI=1` (probe dedicato che misura con effetti Alti). Le catture di base restano stabili.
- Sprite delle particelle e della lampada solo da `tools/art` (ADR 0004): `fx-fumo`, `fx-scintilla`, `fx-polvere`, `fx-vapore`, `fx-fiocco`, `fx-granello`, `fx-foglia`, `fx-lampo` (strisce o singoli 8–16 px), `lamp.png` e parti, `luce.png` (texture radiale per `PointLight2D`, 128×128, generata).
- Definizioni degli effetti in `godot/effetti/effetti.json` (nome → sprite, numero, durata, velocità, colore, dispersione, gravità, livello minimo di qualità): aggiungere un effetto = una riga di dati.
- Prestazioni (probe PERF con `TINDUSTRY_PLAYTEST_EFFETTI=1`, 200 nastri + 100 edifici attivi, effetti Alti): FPS medio ≥ 144 su questa macchina, nodi stabili (il pool non cresce dopo il riscaldamento). Mappa Enorme a zoom minimo con effetti d'ambiente: FPS medio ≥ 144.
- Testi in italiano, commenti in inglese, commit con `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`, mai dati reali del giocatore, Actions solo a mano.

## Review Focus

1. **Particelle che si accumulano**: un forno acceso per ore non deve far crescere i nodi. Test: probe PERF registra il numero di nodi e di particelle attive a 10 s e a 60 s: uguali entro il 5%.
2. **Notte che nasconde il gioco**: con la notte il giocatore deve ancora vedere nastri, oggetti e HUD. Test: cattura notturna con e senza lampade; l'HUD non è mai scurito (sta sul CanvasLayer).
3. **Impostazioni applicate davvero**: cambiare Effetti, Ombre, Bagliore, LimiteFps, ScalaRendering agisce subito senza riavvio. Test: playtest `ui` imposta ciascuna opzione e verifica lo stato del motore (`Engine.MaxFps`, `ContentScaleMode`, visibilità del `WorldEnvironment`, numero di emettitori).
4. **Effetti legati a strutture che spariscono**: smontare un forno mentre fuma non lascia emettitori orfani. Test: dopo lo smontaggio, emettitori legati a quella struttura = 0.
5. **Bagliore troppo forte**: il bloom non deve sbiancare l'HUD o il terreno chiaro (neve). Test: cattura su neve con bagliore; l'HUD fuori dal `WorldEnvironment`.

---

### Task 0: base (controllore)

- [ ] Base catture: `.dev/baseline-g2b-candidata` (se la G2b non è approvata, è comunque il confronto). Riferimento dati reali in `$TEMP/g3-realdata-before.txt`.

### Task 1: impostazioni grafiche e gestore degli effetti

**Files:**
- Modify: `godot/scripts/settings/ClientSettings.cs`, `godot/scripts/ui/panels/SettingsPanel.cs`, `godot/scripts/world/World.cs`, `World.Commands.cs` (pulizia degli effetti in Attach/Detach)
- Create: `godot/scripts/visuals/effects/EffectManager.cs`, `EffectDef.cs`, `godot/effetti/effetti.json`, `tools/art/blocchi/effetti.py` (sprite `fx-*`)
**Interfaces:**
- `public enum EffectsQuality { Spenti, Bassi, Alti }`; `ClientSettings` nuovi campi dei Global Constraints.
- `public partial class EffectManager : Node2D` (figlio di World, z 12): `void Play(string name, Vector2 worldPos, float rotation = 0)`, `int Attach(string name, Node2D owner, Vector2 offset)` (emettitore continuo legato a un nodo, ritorna un id), `void Detach(int id)`, `void Clear()`, `int ActiveParticles`, `int Emitters`; legge `effetti.json` all'avvio; rispetta qualità, limite e vista.
- [ ] Step 1: test (xUnit non vede Godot: la parte pura — parsing e validazione di `effetti.json`, scelta del livello minimo, calcolo del limite — va in una classe statica testabile in `godot/scripts/visuals/effects/EffectRules.cs` collegata al progetto di test come file linkato, oppure in Shared se non dipende da Godot; scegliere e annotare). Test: file valido letto, campo mancante → errore italiano, qualità Spenti → nessun effetto, limite rispettato.
- [ ] Step 2: impostazioni + pannello (con `ScrollContainer`) + applicazione immediata (`Engine.MaxFps`, `ContentScaleMode/Size`, VSync).
- [ ] Step 3: gestore + sprite + definizioni (ancora nessun effetto collegato al gioco tranne uno di prova nel playtest).
- [ ] Step 4: playtest `ui` imposta ogni opzione e verifica lo stato del motore; verifiche; commit; PR "feat(effetti): impostazioni grafiche e gestore degli effetti (G3 1/4)".

### Task 2: effetti di lavoro, costruzione e ambiente

**Files:**
- Modify: `godot/scripts/visuals/buildings/{MinerVisual,SmelterVisual,GeneratorVisual,AssemblerVisual,ExtractorVisual}.cs`, `godot/scripts/visuals/construction/{ConstructionLayer,DroneVisual}.cs`, `godot/scripts/world/World.Visuals.cs` (Core: polvere sulle consegne dal contatore `CoreDeliveredItems`), `World.Commands.cs` (`OnSimEvent`: lampo a fine costruzione, sbriciolamento a fine smontaggio — posizione dal cantiere memorizzata in `ConstructionLayer.Sync`), `godot/effetti/effetti.json`
- Create: `godot/scripts/visuals/effects/AmbientEffects.cs` (neve, granelli, lucciole/foglie per bioma, solo nel visibile; le increspature dell'acqua esistono già nello shader della G2b: con Effetti Spenti il suo tempo si ferma)
- [ ] Step 1: probe `TINDUSTRY_PLAYTEST_EFFETTI=1`: nodi e particelle a 10 s e 60 s (stabili ±5%); emettitori di una struttura smontata = 0.
- [ ] Step 2: collegamenti (emettitori continui accesi solo mentre il blocco lavora: `Efficiency>0`, `IsCrafting`, `IsGenerating`).
- [ ] Step 3: catture in `.dev/g3-task2/` (forno, minatore, generatore, cantiere che finisce, ambiente per bioma) con effetti Alti; verifiche; commit; PR "feat(effetti): lavoro, costruzione e ambiente (G3 2/4)".

### Task 3: ombre, bagliore, notte e Lampada

**Files:**
- Modify: `godot/project.godot` (`rendering/viewport/hdr_2d=true`), `godot/scripts/world/World.cs` (`WorldEnvironment` con glow, sotto il CanvasLayer dell'HUD), visual degli edifici e del Core (ombra: stessa texture scura alfa 0,3 spostata di (6,6) px, sotto lo sprite; spenta con `Ombre=false`), `src/TIndustry.Shared/Construction/GameRules.cs` (`bool Night = false`), salvataggio (campo additivo, nessun cambio di versione: verificare la regola di ADR 0005), `src/TIndustry.Shared/Campaign/CampaignCatalog.cs` (`bool Night = false` ultimo parametro), `data/campaign.json` (notte su 2 livelli scelti: 06 e 10), `godot/scripts/ui/panels/NewGamePanel.cs` (casella "Notte"), `World.Session.cs`, `data/content.json` (struttura `lamp`: 1×1, costo 2 iron-plate + 1 copper-wire, consumo di energia 1, ricerca dopo `power-node`), Shared (lampada come consumatore di energia esistente o nuovo tipo minimo, con test), `tools/art/blocchi/potenza.py` (sprite `lamp`, `luce.png`)
- Create: `godot/scripts/visuals/effects/NightLayer.cs` (`CanvasModulate` scuro + `PointLight2D` su Core, forni accesi, generatori, lampade alimentate, drone; luci solo nel visibile, massimo 128 attive)
- [ ] Step 1: test xUnit: `LampConsumesPowerAndIsBuildable`, `NightFlagRoundTripsInSave`, `CampaignNightFlagLoads`, contenuto valido; probe: ombre spente → nessun nodo ombra; notte → `CanvasModulate` presente e HUD non scurito.
- [ ] Step 2: implementazione.
- [ ] Step 3: catture `.dev/g3-task3/` (giorno con ombre e bagliore, notte con e senza lampade, neve con bagliore); verifiche; commit; PR "feat(effetti): ombre, bagliore, notte e Lampada (G3 3/4)".

### Task 4: documenti e base candidata

- Modify: `docs/decisioni/0004-arte-generata-da-script.md` (effetti e luci), `docs/ARCHITETTURA.md` (come aggiungere un effetto, impostazioni grafiche), `godot/README.md` (impostazioni, notte, Lampada), `README.md`, `CHANGELOG.md`, canvas Obsidian.
- [ ] Catture candidate `.dev/baseline-g3-candidata` (due esecuzioni, 0 differenze), probe PERF con effetti Alti nel rapporto; commit; PR "docs: effetti e luci (G3 4/4)".
