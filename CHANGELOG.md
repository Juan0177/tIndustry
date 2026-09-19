# Changelog

Tutte le note di versione rilevanti per [tIndustry](https://github.com/Juan0177/tIndustry).
Il formato è ispirato a [Keep a Changelog](https://keepachangelog.com/); il versioning segue SemVer dove ha senso per un prototipo.

## [Unreleased]

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

[0.2.0]: https://github.com/Juan0177/tIndustry/releases/tag/v0.2.0
[0.1.1]: https://github.com/Juan0177/tIndustry/releases/tag/v0.1.1
[0.1.0]: https://github.com/Juan0177/tIndustry/releases/tag/v0.1.0
