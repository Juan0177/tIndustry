# Changelog

Tutte le note di versione rilevanti per [tIndustry](https://github.com/Juan0177/tIndustry).
Il formato è ispirato a [Keep a Changelog](https://keepachangelog.com/); il versioning segue SemVer dove ha senso per un prototipo.

## [Unreleased]

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

[0.2.2]: https://github.com/Juan0177/tIndustry/releases/tag/v0.2.2
[0.2.1]: https://github.com/Juan0177/tIndustry/releases/tag/v0.2.1
[0.2.0]: https://github.com/Juan0177/tIndustry/releases/tag/v0.2.0
[0.1.1]: https://github.com/Juan0177/tIndustry/releases/tag/v0.1.1
[0.1.0]: https://github.com/Juan0177/tIndustry/releases/tag/v0.1.0
