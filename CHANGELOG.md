# Changelog

Tutte le note di versione rilevanti per [tIndustry](https://github.com/Juan0177/tIndustry).
Il formato è ispirato a [Keep a Changelog](https://keepachangelog.com/); il versioning segue SemVer dove ha senso per un prototipo.

## [Unreleased]

## [0.3.3] — 2026-09-26

Godot QoL + mondo grande: gestione salvataggi completa, overlay risorse sistema, mappe **1000×1000** seed-based. Zip ufficiali restano **export Godot**.

### Aggiunto / migliorato

- **Godot**: Gestione salvataggi full — autosave Continua (Esc/Home/Esci), Salva come, seed in lista, ↑↓, Conferma elimina ([#125](https://github.com/Juan0177/tIndustry/pull/125))
- **Godot/Shared**: overlay **risorse sistema** (FPS · CPU · RAM · GPU · tempo processo) da Impostazioni ([#126](https://github.com/Juan0177/tIndustry/pull/126))
- **Godot/Shared**: mondo **1000×1000** seed-based (campagna + sandbox), clamp 1000, draw con **viewport culling** ([#127](https://github.com/Juan0177/tIndustry/pull/127))

### Come giocare (download)

1. Scarica [`tIndustry-win-x64.zip`](https://github.com/Juan0177/tIndustry/releases/latest) (o linux)
2. Estrai → avvia `tIndustry.exe` (Windows) / `tIndustry.x86_64` (Linux)

Da sorgente: `godot4 --path godot` (vedi README).

## [0.3.2] — 2026-09-26

Parity patch: Godot recupera i gap Raylib (P0–P1) — costi, multi-ricetta, power, tutorial, crumbs mid-game. Zip ufficiali restano **export Godot** (come 0.3.1).

### Aggiunto / migliorato

- **Godot/Shared**: costi di piazzamento + rimborso come Raylib (`FactorySlice` spende `BuildCost` / rimborsa su remove); sandbox Nuova partita parte con lastre/fili; toast IT se stock insufficiente ([#114](https://github.com/Juan0177/tIndustry/pull/114))
- **Godot/Shared**: forno/assemblatore **multi-ricetta** (auto-pick `smelt-*` / `craft-*` come Raylib) — piombo, titanio, grafite, silicio craftabili ([#115](https://github.com/Juan0177/tIndustry/pull/115))
- **Godot**: palette/place **Nastro T3** (`conveyor-express`) + **Nodo potenza T2** (`power-node-t2`) ([#116](https://github.com/Juan0177/tIndustry/pull/116))
- **Godot/Shared**: **CORE upgrade** (+25% vendite) da `economy.coreUpgrade`, chip HUD, save v5 ([#117](https://github.com/Juan0177/tIndustry/pull/117))
- **Godot/Shared**: forno **carbone OR corrente** (Raylib): buffer fuel + burn, craft bloccato senza; **+20%** se alimentato; save v6 ([#118](https://github.com/Juan0177/tIndustry/pull/118))
- **Godot/Shared**: **power buffer/capacity** (Raylib thin): core+gen ricarica, spend on craft, soft brownout → forno torna al carbone; HUD `pot. buf/cap`; save v7 ([#119](https://github.com/Juan0177/tIndustry/pull/119))
- **Godot**: **tutorial first-run** (8 step IT, Avanti/Salta, persist) + **Impostazioni** (VSync, scala UI, FPS overlay, rivedi tutorial) da Home / tasto I ([#120](https://github.com/Juan0177/tIndustry/pull/120))
- **Shared**: **I/O adiacente** edificio↔edificio/core (Raylib): miner/forno transferiscono senza nastro se i footprint si toccano ([#121](https://github.com/Juan0177/tIndustry/pull/121))
- **Godot/Shared**: **map size da campagna** (`level.MapWidth/Height`, core centrato, clamp 128× per draw Godot); save v8 seed/WxH ([#122](https://github.com/Juan0177/tIndustry/pull/122))
- **Godot**: **Gestione salvataggi** (lista slot, Carica/Elimina/Duplica Continua) da Home ([#123](https://github.com/Juan0177/tIndustry/pull/123))

### Come giocare (download)

1. Scarica [`tIndustry-win-x64.zip`](https://github.com/Juan0177/tIndustry/releases/latest) (o linux)
2. Estrai → avvia `tIndustry.exe` (Windows) / `tIndustry.x86_64` (Linux)

Da sorgente: `godot4 --path godot` (vedi README).

## [0.3.1] — 2026-09-26

Patch release: i download ufficiali della Release sono **export Godot**, non più Raylib.

### Cambiato

- **Release assets**: `tIndustry-win-x64.zip` / `tIndustry-linux-x64.zip` = client **Godot** (`tIndustry.exe` / `tIndustry.x86_64`)
- Alias `tIndustry-godot-*-x64.zip` (stesso contenuto)
- Raylib solo come `tIndustry-raylib-legacy-*-x64.zip`
- CI `release.yml`: install Godot 4.4.1 mono + templates → export Windows/Linux; Raylib in job separato
- `godot/export_presets.cfg`, `godot/TIndustry.Godot.sln`, seed in `godot/data/`, script `tools/ci/setup-godot.sh` + `export-godot.sh`

### Come giocare (download)

1. Scarica [`tIndustry-win-x64.zip`](https://github.com/Juan0177/tIndustry/releases/latest) (o linux)
2. Estrai → avvia `tIndustry.exe` (Windows) / `tIndustry.x86_64` (Linux)

Da sorgente: `godot4 --path godot` (vedi README).

## [0.3.0] — 2026-09-25

**Godot 4 .NET diventa il client ufficiale.** Raylib resta legacy (zip release ancora disponibili). Port slice completo: fabbrica, UI Mindustry, campagna, art, logistica.

### Cambiato

- **Godot 4 .NET = path ufficiale** per build & play ([#111](https://github.com/Juan0177/tIndustry/pull/111))
- Client Raylib (`TIndustry.Logistics`) demoted a **legacy** (manutenzione opzionale)
- CI: job **Godot + Shared**; publish zip Raylib invariato (etichettato legacy)

### Godot — gameplay & sistemi

- Factory slice: nastri (corner gallery), craft (forno/assy), junction/splitter, power stubs ([#66](https://github.com/Juan0177/tIndustry/pull/66)–[#87](https://github.com/Juan0177/tIndustry/pull/87))
- **Save/load** continua + slot ([#88](https://github.com/Juan0177/tIndustry/pull/88))
- **Sorter + ponte** (decision 14 thin span) ([#89](https://github.com/Juan0177/tIndustry/pull/89))
- **Ricerca** unlock + tech-tree grafo ([#91](https://github.com/Juan0177/tIndustry/pull/91)–[#92](https://github.com/Juan0177/tIndustry/pull/92))
- **Mercato** sell + `$` HUD ([#93](https://github.com/Juan0177/tIndustry/pull/93)); **auto-sell** al Core ([#99](https://github.com/Juan0177/tIndustry/pull/99))
- **Campagna** select + obiettivi HUD ([#94](https://github.com/Juan0177/tIndustry/pull/94))
- **Home splash** Continua / Campagna / Nuova partita ([#98](https://github.com/Juan0177/tIndustry/pull/98))
- **T2** nastro/miner, estrattore, power-node ([#100](https://github.com/Juan0177/tIndustry/pull/100))
- **Map seed** terrain da campagna/sandbox ([#101](https://github.com/Juan0177/tIndustry/pull/101))

### Godot — UI Mindustry

- Palette angolo + categorie St/Lo/Pr/Po · info strip · tech tree icon-only ([#95](https://github.com/Juan0177/tIndustry/pull/95)–[#97](https://github.com/Juan0177/tIndustry/pull/97))
- Cursor-default toolbar ([#90](https://github.com/Juan0177/tIndustry/pull/90))

### Godot — art

- Block sprites 64×64 coerenti mappa + palette ([#102](https://github.com/Juan0177/tIndustry/pull/102))
- Nastro T2 single chevron ([#103](https://github.com/Juan0177/tIndustry/pull/103))
- Logistics redesign splitter/sorter/bridge ([#104](https://github.com/Juan0177/tIndustry/pull/104)–[#105](https://github.com/Juan0177/tIndustry/pull/105))
- Miner gear animato ([#106](https://github.com/Juan0177/tIndustry/pull/106)–[#109](https://github.com/Juan0177/tIndustry/pull/109))
- Estrattore / forno / assemblatore / generatore / core redesign ([#110](https://github.com/Juan0177/tIndustry/pull/110))

### Limitazioni

- Combat / unità ancora deferred
- Zip GitHub Release = client **Raylib legacy**; per Godot: `godot4 --path godot` (vedi README)
- Art polish opzionale in corso

### Come giocare (ufficiale — Godot)

```bash
dotnet build src/TIndustry.Shared/TIndustry.Shared.csproj
dotnet build godot/TIndustry.Godot.csproj
godot4 --path godot
```

Richiede Godot **4.4+** .NET. Dettagli: [`godot/README.md`](godot/README.md).

### Legacy Raylib

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

O scarica gli zip dalla Release (`tIndustry-win-x64.zip` / `tIndustry-linux-x64.zip`).

## [0.2.11] — 2026-09-21

Polish completo sopra **0.2.10**: UX/copy IT, feedback, HUD, ghost placement — niente feature P2.

### Aggiunto / migliorato

- **Copy IT**: tutorial Potenza, dock Sdop/Selez/Pot., Ingresso/Uscita, magazzino (no “stock”), toast potenza/raggio, Menu principale, Ricerca, segnaposto
- **Font HUD**: atlas con — → … ’ ✓ Δ (niente più `?` ripetuti); atlas 160×scale + Bilinear per testo meno granuloso; spacing più stretto
- **Overflow**: truncate toast/home/ricerca/campagne/save/header; CORE upgrade sempre testo compatto (niente icone sovrapposte); Mercato righe dinamiche
- **Short labels**: Pb/Ti lastre → `Pb Ls` / `Ti Ls` nella strip
- **Feedback**: toast errore in ambra; refuse place nastri/logistica; salvataggio Esc con “Partita salvata.”; elimina slot a doppia conferma
- **HUD**: strip risorse overflow (+N, priorità stock); Fabbrica Pot. + tooltip legenda M/F/A/N/G/P; Home centrata; save manager responsive
- **Ghost**: silhouette anche se invalido; cerchio raggio nodi T1/T2
- **Campagna**: obiettivo hit-test (no click-through); label Magazzino; vittoria “Menu principale”
- **Docs**: README allineato a 0.2.11

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.10] — 2026-09-21

P1 gap close sopra **0.2.9**: campagna estesa, atlas icone, polish tech-tree UX.

### Aggiunto / migliorato

- **Campagna 10 livelli**: dopo Economia Foundry → Corrente (gen+nodo) → Piombo → Logistica (sorter) → Titanio/silicio → Espansione T2; bilanciamento starter/obiettivi mid-game
- **AppData campagna**: merge id seed mancanti + sync name/obiettivi/`unlocksNext` (come `content.json`)
- **Tech-tree UX**: Ctrl+rotella zoom (verso cursore), path highlight su prerequisiti/dipendenti, H/Home reset pan/zoom
- **Atlas icone**: `GameIcons` packa ~40 PNG in una sola texture (UV draw); API `Draw`/`TryDraw` invariata
- **Docs**: README allineato a 0.2.10

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.9] — 2026-09-21

P0 gap close sopra **0.2.8**: mercato dinamico, più minerali/ricette, polish animazioni/lighting.

### Aggiunto / migliorato

- **Mercato dinamico**: prezzi reagiscono allo stock (curva supply / softStock); bonus CORE applicato sul prezzo dinamico; Mercato mostra fino a 6 voci (stockate in alto)
- **Minerali**: depositi **piombo** + **titanio** (noise + starter/scout); item grezzi + lastre
- **Ricette**: forno multi-ricetta (`smelt-iron` / `smelt-lead` / `smelt-titanium`); assemblatore (`craft-copper-wire` / `craft-graphite` / `craft-silicon`)
- **Grafica**: glow forno gated su fuel/power + bloom; trivella idle rock a 0%; ombre soft a 2 layer
- **Docs**: README allineato a 0.2.9

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.8] — 2026-09-21

Release di contenuto sopra **0.2.7**: mid-game Phase 6, I/O, tech tree, sorter, potenza a nodi, forno carbone-o-corrente, icona minatore **trivella**.

### Aggiunto / migliorato

- **I/O belt-uscente** + trasferimento **edificio adiacente** (miner|forno, forno|assy, →CORE); overlay amber/ciano; tutorial **14** passi ([#38](https://github.com/Juan0177/tIndustry/pull/38))
- **Tech tree a grafo**: nodi/archi da `prerequisites`; hit-test unlock + sync prereq AppData ([#40](https://github.com/Juan0177/tIndustry/pull/40), [#47](https://github.com/Juan0177/tIndustry/pull/47))
- **Selezionatore (sorter)**: filtro item (F), routing Mindustry ([#39](https://github.com/Juan0177/tIndustry/pull/39))
- **Grafica mondo (Raylib)**: silhouettes TI-inspired, terrain micro-variazione ([#42](https://github.com/Juan0177/tIndustry/pull/42))
- **Mid-game T2/T3**: Minatore T2, Nastro T3, fuel generatore (carbone) ([#41](https://github.com/Juan0177/tIndustry/pull/41))
- **Icone**: ore rock+tint ([#44](https://github.com/Juan0177/tIndustry/pull/44)); minatore = **trivella / auger** Lorc `drill` (non trapano a pistola); T2 riusa lo stesso PNG
- **AppData**: merge id seed mancanti (no crash) ([#45](https://github.com/Juan0177/tIndustry/pull/45)); sync `displayName` tier ([#46](https://github.com/Juan0177/tIndustry/pull/46))
- **Overlay versione** semi-trasparente in play HUD ([#48](https://github.com/Juan0177/tIndustry/pull/48))
- **Nodi potenza** T1/T2 (linee dirette + auto-link); cavi tile superseduti; grafo esclude CORE ([#49](https://github.com/Juan0177/tIndustry/pull/49)–[#51](https://github.com/Juan0177/tIndustry/pull/51))
- **Forno = carbone OR corrente** (+20% craft se alimentato da rete) ([#52](https://github.com/Juan0177/tIndustry/pull/52))
- Naming UI a **codici tier** (Minatore T1/T2, Nastro T1/T2/T3)
- **Docs**: README **revamp** per 0.2.8 (pitch mid-game, download first, controlli/power aggiornati)

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.7] — 2026-09-19

Patch UX sopra **0.2.6**: bottone upgrade **CORE** mostra costo completo (×N lastre + $), come il footer dock. ([#35](https://github.com/Juan0177/tIndustry/pull/35))

### Aggiunto / migliorato

- **Fabbrica / CORE**: label Peak-style `CORE · ×N [icona lastre] · +$cost`; toast di afford cita `×N lastre` (non più "+ lastre" vago) ([#35](https://github.com/Juan0177/tIndustry/pull/35))
- **Docs**: README allineato a 0.2.7

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.6] — 2026-09-19

Patch UX sopra **0.2.5** (Campagna già inclusa): polish Mercato/dock, ricette Input/Output nel footer, fix conteggi Fabbrica e upgrade CORE. ([#31](https://github.com/Juan0177/tIndustry/pull/31), [#32](https://github.com/Juan0177/tIndustry/pull/32), [#33](https://github.com/Juan0177/tIndustry/pull/33))

### Aggiunto / migliorato

- **Mercato / dock**: padding e accent polish; seeding capture per still HUD ([#31](https://github.com/Juan0177/tIndustry/pull/31))
- **Dock footer**: riga **Input / Output** (icone ×N + durata ricetta) quando tieni Forno/Assemblatore; sotto resta il costo build ([#32](https://github.com/Juan0177/tIndustry/pull/32))
- **Fabbrica HUD**: conteggi `M/F/A/N/G` restano sopra **CORE** a scala UI di default; click CORE con feedback afford / già potenziato ([#33](https://github.com/Juan0177/tIndustry/pull/33))
- **Docs**: README allineato a 0.2.6

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.5] — 2026-09-19

Patch sopra **0.2.4**: modalità **Campagna** a livelli + fix clip Mercato e costi build a icone nel dock. ([#28](https://github.com/Juan0177/tIndustry/pull/28), [#29](https://github.com/Juan0177/tIndustry/pull/29))

### Aggiunto / migliorato

- **Campagna**: Home → **Campagna** → selezione livelli data-driven (`data/campaign.json`); obiettivi (earn/stock/unlock/sell); HUD **OBIETTIVO**; progressione in AppData `campaignProgress.json`; 5 livelli starter ([#28](https://github.com/Juan0177/tIndustry/pull/28))
- **Mercato**: bottoni `1` / `tutti` inset e scale-safe — etichette non più tagliate dal bordo giallo ([#29](https://github.com/Juan0177/tIndustry/pull/29))
- **Dock footer**: costi build Peak-style (`Nome · ×N [icona] · +$cost`); **Rimuovi** con label chiaro senza costi finti ([#29](https://github.com/Juan0177/tIndustry/pull/29))
- **Docs**: README allineato a 0.2.5

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.4] — 2026-09-19

Patch UX sopra **0.2.3**: pannello Mercato dedicato, icone più chiare, tutorial stock-first a 6 step, HUD scale-safe. ([#26](https://github.com/Juan0177/tIndustry/pull/26))

### Aggiunto / migliorato

- **Mercato**: pannello dedicato (stock + vendi 1/tutti + prezzi + auto-sell), separato dalla strip **Fabbrica** (tips / CORE)
- **Icone**: item/dock più leggibili con etichette IT brevi; icona vendita UI
- **Tutorial**: **6 step** produce → stock → spendi/vendi → ricerca (prima 5)
- **UI scale**: Mercato soft-scale + scroll Impostazioni — layout valido a **100–200%** senza overlap HUD
- **Docs**: README allineato a 0.2.4

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.3] — 2026-09-19

Patch economia sopra **0.2.2**: core stock-first, vendita esplicita Mercato, auto-sell opzionale. ([#24](https://github.com/Juan0177/tIndustry/pull/24))

### Changed

- **Economia stock-first**: item al core → magazzino (wallet); vendita esplicita dal Mercato (`1` / `tutti`) o toggle **Vendita automatica** (OFF di default, persistito in `settings.json`) ([#24](https://github.com/Juan0177/tIndustry/pull/24))
- **Docs**: README allineato a 0.2.3

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.2] — 2026-09-19

Patch logistica + tutorial sopra **0.2.1**: sdoppiatore T-fork, miner round-robin, tutorial ripartibile. ([#21](https://github.com/Juan0177/tIndustry/pull/21), [#22](https://github.com/Juan0177/tIndustry/pull/22))

### Aggiunto / migliorato

- **Sdoppiatore**: flusso lungo il facing + alternanza sinistra/destra al handoff; drag-connect non ruota più lo splitter (come gli incroci) ([#21](https://github.com/Juan0177/tIndustry/pull/21))
- **Miner**: eject **round-robin** sui nastri adiacenti che accettano — più uscite ricevono ore nel tempo ([#21](https://github.com/Juan0177/tIndustry/pull/21))
- **Tutorial**: **Nuova partita → Conferma** riparte sempre da Tutorial 1/5; **Impostazioni → Rivedi tutorial** azzera `tutorialCompleted`; Salta/Fine restano per la run corrente ([#22](https://github.com/Juan0177/tIndustry/pull/22))
- **Docs**: README allineato a 0.2.2

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.1] — 2026-09-19

Patch QoL / playability sopra **0.2.0**: nastri leggibili, UI scale, tutorial, miner multi-lato, toast, layout `src/`. ([#15](https://github.com/Juan0177/tIndustry/pull/15), [#16](https://github.com/Juan0177/tIndustry/pull/16), [#17](https://github.com/Juan0177/tIndustry/pull/17), [#18](https://github.com/Juan0177/tIndustry/pull/18), [#19](https://github.com/Juan0177/tIndustry/pull/19); splash ~8s già in 0.2.0 via [#13](https://github.com/Juan0177/tIndustry/pull/13))

### Aggiunto / migliorato

- **Nastri**: chip item saturi + icona sopra gli edifici; viaggio lungo `RoutedExit` — i minerali in transito si vedono di nuovo ([#16](https://github.com/Juan0177/tIndustry/pull/16))
- **UI scale**: Impostazioni **100% / 125% / 150% / 200%** (default **125%**), atlas DejaVu riscalato; layout Impostazioni senza overlap ON/OFF ([#16](https://github.com/Juan0177/tIndustry/pull/16), [#19](https://github.com/Juan0177/tIndustry/pull/19))
- **Tutorial**: banner IT a 5 step alla prima Nuova partita; **Salta** / Backspace; `tutorialCompleted` nel save ([#16](https://github.com/Juan0177/tIndustry/pull/16))
- **Piazzamento**: edifici su qualsiasi terra libera (non acqua); miner off-deposito a **0%**; click su Forno bloccato non ruba più lo strumento ([#16](https://github.com/Juan0177/tIndustry/pull/16))
- **Risorse sistema**: overlay **CPU · GPU · RAM** (FPS/CPU/RAM; GPU via `nvidia-smi` se c’è); strip inventario sempre a parte ([#16](https://github.com/Juan0177/tIndustry/pull/16))
- **Miner**: eject su **tutti e quattro** i lati adiacenti (qualsiasi nastro che accetta); freccia facing rimossa ([#17](https://github.com/Juan0177/tIndustry/pull/17))
- **Direzione nastro**: chevron unidirezionali allineati al facing (niente hash bidirezionali) ([#17](https://github.com/Juan0177/tIndustry/pull/17))
- **HUD**: FPS unico (angolo XOR overlay sistema); overlay sistema solo in play; font più nitido a scale alte ([#17](https://github.com/Juan0177/tIndustry/pull/17))
- **Status toast**: auto-clear ~3.5s con fade; **Esc** lo chiude ([#18](https://github.com/Juan0177/tIndustry/pull/18))
- **Repo**: sorgenti sotto `src/{App,Simulation,Content,UI}/`; `.csproj` in root ([#19](https://github.com/Juan0177/tIndustry/pull/19))
- **Docs**: README allineato a 0.2.1 ([#15](https://github.com/Juan0177/tIndustry/pull/15) + refresh release)

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.0] — 2026-09-19

Minor sopra **0.1.1**: first-launch AppData, icon pack HUD, splash + ottimizzazioni hot-path, tooling MCP. ([#9](https://github.com/Juan0177/tIndustry/pull/9), [#10](https://github.com/Juan0177/tIndustry/pull/10), [#11](https://github.com/Juan0177/tIndustry/pull/11), [#12](https://github.com/Juan0177/tIndustry/pull/12), [#13](https://github.com/Juan0177/tIndustry/pull/13); polish UI [#8](https://github.com/Juan0177/tIndustry/pull/8))

### Aggiunto / migliorato

- **Contenuti first-launch**: niente Excel nel publish; seed `content.json` → AppData al primo avvio (`~/.local/share/tIndustry/content/`) ([#9](https://github.com/Juan0177/tIndustry/pull/9))
- **Home**: **Continua** nascosto se non c’è autosave valido ([#9](https://github.com/Juan0177/tIndustry/pull/9))
- **Dock**: rimossa categoria **Strumenti**; **Rimuovi** in Produzione; tooltip hover IT (nome + hint) ([#9](https://github.com/Juan0177/tIndustry/pull/9))
- **Grafica**: icon pack HUD/dock/nastri (game-icons.net CC BY 3.0 + Kenney CC0); pannelli più nitidi; label **Δ sessione** ([#10](https://github.com/Juan0177/tIndustry/pull/10))
- **Splash**: schermata avvio con `assets/thumbnail.png`, brand tINDUSTRY, dismiss click/tasto o auto (~8s) ([#12](https://github.com/Juan0177/tIndustry/pull/12), [#13](https://github.com/Juan0177/tIndustry/pull/13))
- **Performance**: culling terreno, cache dock/home, intake nastro O(footprint), bridge O(1), meno alloc per frame ([#12](https://github.com/Juan0177/tIndustry/pull/12))
- **Tooling**: `.cursor/mcp.json`, raccomandazioni C# VS Code, bootstrap `dnx` in `install.sh` ([#11](https://github.com/Juan0177/tIndustry/pull/11))
- **UI polish**: loading, icone dock, fix Tools/miner su rotazione ([#8](https://github.com/Juan0177/tIndustry/pull/8))

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.1.1] — 2026-09-19

Patch UI/QoL dopo 0.1.0: dock Mindustry, controlli camera, core centrato, Impostazioni display estese. ([#7](https://github.com/Juan0177/tIndustry/pull/7))

### Aggiunto / migliorato

- **Dock Mindustry** (basso-destra): colonna categorie (Produzione / Logistica / Potenza / Strumenti / Inventario) + griglia piazzabili/item con bordo di selezione giallo/arancio
- **Header snello** + strip risorse (se overlay attivo); pannello Mercato compatto invece della barra piena a destra
- **Camera**: zoom solo con **Ctrl+rotella**; **rotella** sola ruota la direzione di piazzamento; **niente edge pan** (restano WASD / drag centrale / Shift+drag)
- **Core al centro** della mappa 1000×1000; la camera parte sul core
- **Impostazioni**: preset **2K** (2560×1440) e **4K** (3840×2160), **Auto risoluzione**, **VSync** on/off, **limite FPS** (30→600) + **Illimitato** — persistiti in `settings.json`

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.1.0] — 2026-09-19

Prima release pubblica-ish: fasi **0–6** sul prototipo C# / Raylib (`.NET 10`), più Impostazioni e CI publish.

### Aggiunto

- **Mondo**: mappa **1000×1000**, camera pan/zoom, culling viewport, depositi ferro e rame
- **Home / sessione**: Continua, Nuova partita, Gestione salvataggi, Impostazioni, Esci
- **Nuova partita**: selezione scenario (Classico / Espanso / Arcipelago) + seed numerico
- **Produzione**: minatore, **forno** (`smelt-iron`), **assemblatore** (rame + lastra → fili)
- **Logistica**: nastro base / **veloce**, **incrocio**, **sdoppiatore**, **ponte** (span 2–4)
- **Economia**: wallet (denaro + lastre + fili), prezzi mercato content-driven, rimborso rimozione 100%, **potenziamento core** (+25% vendite)
- **Ricerca**: sblocchi strutture data-driven, a pagamento, persistenti nel save
- **Power (stub)**: pool globale (baseline core + **Generatore**); forni/assemblatori consumano potenza (brownout = craft in stall)
- **QoL**: overlay FPS/risorse (Impostazioni), onboarding tip contestuali, UI in italiano, `--self-test` esteso
- **Persistenza**: save JSON versionato (**v6**: edifici, ponti, unlock, economia, generatori, PowerBuffer)
- **CI**: publish self-contained `win-x64` / `linux-x64` su push/PR; release job su tag `v*`

### Limitazioni note

- Niente **fuel / coal grid** né cavi di potenza (solo pool globale)
- Niente **combat**, unità, fog of war
- `miner-advanced` resta **stub** (nel content/ricerca, non giocabile)
- Bilanciamento ancora grezzo; poche ricette mid-game
- Slot nominati solo via **Duplica Continua** (niente “Salva come…” in-game)
- Grafica procedurale (niente sprite atlas)

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

[0.3.3]: https://github.com/Juan0177/tIndustry/releases/tag/v0.3.3
[0.3.2]: https://github.com/Juan0177/tIndustry/releases/tag/v0.3.2
[0.3.1]: https://github.com/Juan0177/tIndustry/releases/tag/v0.3.1
[0.3.0]: https://github.com/Juan0177/tIndustry/releases/tag/v0.3.0
[0.2.2]: https://github.com/Juan0177/tIndustry/releases/tag/v0.2.2
[0.2.1]: https://github.com/Juan0177/tIndustry/releases/tag/v0.2.1
[0.2.0]: https://github.com/Juan0177/tIndustry/releases/tag/v0.2.0
[0.1.1]: https://github.com/Juan0177/tIndustry/releases/tag/v0.1.1
[0.1.0]: https://github.com/Juan0177/tIndustry/releases/tag/v0.1.0
