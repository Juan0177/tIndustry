# tIndustry

[![Build publish](https://github.com/Juan0177/tIndustry/actions/workflows/build-publish.yml/badge.svg)](https://github.com/Juan0177/tIndustry/actions/workflows/build-publish.yml)
**v0.2.11** · [CHANGELOG](CHANGELOG.md) · [Release](https://github.com/Juan0177/tIndustry/releases/tag/v0.2.11)

```
  miner ──▶ forno ──▶ lastre / piombo / titanio ──▶ CORE / mercato dinamico
    │         ▲  carbone OPPURE corrente (+20%)
  nastro   nodo potenza ◀── generatore (carbone)
  sorter · splitter · ponte · T1/T2/T3 · grafite / silicio
```

**Mindustry** × **Tiny Industry**: logistica a nastro su mappa enorme, economia con portafoglio, ricerca a grafo, vendita al Mercato.

Loop: *scout → estrai → trasporta → trasforma → vendi → sblocca → espandi*.

---

## Download

| Asset | Piattaforma |
| --- | --- |
| [`tIndustry-win-x64.zip`](https://github.com/Juan0177/tIndustry/releases/latest) | Windows x64 (self-contained) |
| [`tIndustry-linux-x64.zip`](https://github.com/Juan0177/tIndustry/releases/latest) | Linux x64 (self-contained) |

Estrai ed esegui (`TIndustry.Logistics.exe` su Windows). Tag `v*` → zip via [release.yml](.github/workflows/release.yml). Push/PR → artifact CI via [build-publish.yml](.github/workflows/build-publish.yml).

---

## Cosa c’è in 0.2.11

| Area | In gioco |
| --- | --- |
| **Mondo** | Mappa **1000×1000**, depositi ferro / rame / carbone / **piombo** / **titanio** |
| **Produzione** | Minatore **T1/T2**, forno multi-ricetta (ferro/piombo/titanio), assemblatore (fili/grafite/silicio), generatore |
| **Logistica** | Nastro **T1/T2/T3**, incrocio, sdoppiatore, **selezionatore**, ponte · I/O belt + adiacenza |
| **Potenza** | **Nodo T1/T2** · forno carbone **o** corrente (**+20%**) · CORE fuori grafo |
| **Economia** | Stock-first · **Mercato dinamico** (prezzi reagiscono allo stock) · upgrade CORE |
| **Grafica** | Glow forno, trivella idle, ombre soft · **atlas icone** (una texture) |
| **Progressione** | Campagna **10 livelli** · tech tree con **zoom + path highlight** · tutorial **14** step |

Non è combat Mindustry né idle clicker: conta il **layout** e il **reinvestimento**.

---

## Da sorgente

```bash
dotnet build TIndustry.Logistics.csproj
dotnet run --project TIndustry.Logistics.csproj -- --self-test   # senza finestra
dotnet run --project TIndustry.Logistics.csproj                  # GUI
```

Richiede [.NET 10 SDK](https://dotnet.microsoft.com/download) · Linux / Windows / macOS (Raylib-cs).

Dopo un `git pull`: `dotnet build` (o `run`) così icone e seed finiscono in `bin/`. I nomi tier si riallineano da seed → AppData a ogni avvio. Etichette/icone stale? Rebuild + riavvio; ultima spiaggia: cancella solo `…/tIndustry/content/` (i `saves/` restano).

| Path | Ruolo |
| --- | --- |
| `src/App/` | Entry, loop, impostazioni |
| `src/Simulation/` | Mondo, nastri, economia, save, potenza, ricerca |
| `src/Content/` | Definizioni + loader JSON/Excel |
| `src/UI/` | Dock / HUD / icone |
| `assets/` · `data/` | Pack grafico + seed `content.json` / `campaign.json` |

Namespace `TIndustry.Logistics` · `.csproj` in root.

Flag: `--smoke-test`, `--capture`, `--export-excel [path]`.

---

## Prima sessione

1. Splash → **Nuova partita** (sandbox + tutorial) oppure **Campagna**.
2. Scout ferro vicino al core (**H** = camera sul core); rame a sud, carbone a est, **piombo a ovest**.
3. Minatore → **nastri uscenti** (o forno a contatto) → stock al CORE → Mercato **1** / **tutti** (prezzi soft con stock alto).
4. **T** Ricerca → Forno → lastre (più profitto delle ore grezze); prova anche piombo → lastre di piombo → silicio.
5. Generatore + **nodi** se i craft stallano; forno gira a carbone **oppure** corrente.
6. Sorter / splitter / Nastro T2–T3 quando il layout si intasa. Overlay amber = uscita, ciano = ingresso.

Default sbloccati: **Nastro T1** + **Minatore T1**.

---

## Controlli

| Input | Azione |
| --- | --- |
| **WASD** / frecce · **Shift+drag** / mmb | Pan |
| **Ctrl+rotella** · **Rotella** / **R** | Zoom · ruota pezzo / nastro |
| **H** / **Home** | Camera sul core |
| **1–8** | Nastro, minatore, forno, rimuovi, assy, incrocio, sdoppiatore, ponte |
| **9** · **Q** / **E** / **Y** | Generatore · Nastro T1 / T2 / T3 |
| **F** | Cicla filtro **selezionatore** |
| Click · drag | Piazza (nastri in drag) |
| **T** · **I** · **U** | Ricerca (Ctrl+rotella zoom, H reset) · Impostazioni · potenzia CORE |
| **Esc** | Chiude toast → home |
| **Backspace** | Salta tutorial |

---

## Home, save, impostazioni

**Home:** Continua · Campagna · Nuova partita · Gestione salvataggi · Impostazioni · Esci.

| Cosa | Dove |
| --- | --- |
| Save | `%LocalAppData%/tIndustry/saves/` · Linux `~/.local/share/tIndustry/saves/` |
| Autosave | `continua.json` · slot `slot-*.json` |
| Settings | `…/tIndustry/settings.json` |
| Content utente | `…/tIndustry/content/content.json` (seed al primo avvio + merge/sync) |

**Impostazioni (I):** scala UI 100–200% · vendita automatica · FPS / strip risorse / overlay CPU·GPU·RAM · VSync · risoluzione (fino 4K) · limite FPS · modalità schermo.

---

## Stack

C# / **.NET 10** · **Raylib-cs** · seed JSON → AppData · sim ~30 Hz / render 60 FPS · un progetto `TIndustry.Logistics`.

Niente Unity/Godot in early roadmap. Icone: [`assets/ATTRIBUTION.md`](assets/ATTRIBUTION.md) (game-icons.net CC BY 3.0 · Kenney CC0).

---

## Roadmap

| Ora (0.2.11) | Dopo |
| --- | --- |
| Polish UX/HUD/copy IT · campagna 10 · atlas · tech-tree · P0 mercato/minerali | Combat / unità · polish lighting avanzato · bilanciamento continuo |

Storia completa: [CHANGELOG.md](CHANGELOG.md).

Criterio: una sessione deve far sentire *ho trovato il ferro, l’ho portato al forno, ho venduto lastre, ho sbloccato il Nastro T2, ho espanso*.

---

## Contributo

Patch welcome — vertical slice giocabili > feature incomplete. Prima di push: `dotnet run --project TIndustry.Logistics.csproj -- --self-test`.

*Buon layout. Che i nastri non si intasino.*
