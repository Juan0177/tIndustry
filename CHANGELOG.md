# Changelog

Tutte le note di versione rilevanti per [tIndustry](https://github.com/Juan0177/tIndustry).
Il formato è ispirato a [Keep a Changelog](https://keepachangelog.com/); il versioning segue SemVer dove ha senso per un prototipo.

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

[0.1.1]: https://github.com/Juan0177/tIndustry/releases/tag/v0.1.1
[0.1.0]: https://github.com/Juan0177/tIndustry/releases/tag/v0.1.0
