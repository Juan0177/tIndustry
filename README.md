# tIndustry

[![Build publish](https://github.com/Juan0177/tIndustry/actions/workflows/build-publish.yml/badge.svg)](https://github.com/Juan0177/tIndustry/actions/workflows/build-publish.yml)
**v0.3.4** · [CHANGELOG](CHANGELOG.md) · [Release](https://github.com/Juan0177/tIndustry/releases/tag/v0.3.4) · **Godot 4 .NET**

```
  miner ──▶ forno ──▶ lastre / fili ──▶ CORE / mercato
    │         ▲  carbone OPPURE corrente
  nastro   nodo potenza ◀── generatore
  sorter · splitter · ponte · T1/T2 · campagna · ricerca
```

**Mindustry** × **Tiny Industry**: logistica a nastro, economia con portafoglio, ricerca a grafo, vendita al Mercato — in **Godot 4 (.NET)**.

Loop: *scout → estrai → trasporta → trasforma → vendi → sblocca → espandi*.

---

## Gioca (ufficiale) — Godot 4 .NET

Richiede [Godot **4.4+** .NET](https://godotengine.org/download) e [.NET 8 SDK](https://dotnet.microsoft.com/download) (per il restore C# del progetto).

```bash
godot4 --path godot
TINDUSTRY_FRESH=1 godot4 --path godot   # ignora salvataggio continua
```

Dettagli controlli, save, ricerca, mercato, campagna: [`godot/README.md`](godot/README.md).

| Path | Ruolo |
| --- | --- |
| `godot/` | Client ufficiale (HUD Mindustry, mondo, UI) |
| `src/TIndustry.Shared/` | Sim/data (nastri, craft, power, save, campagna) |
| `tests/` | Test xUnit della sim |
| `data/` | Seed `content.json` / `campaign.json` |

Combat / unità: **ancora fuori scope**.

---

## Download (ufficiale — Godot)

| Asset | Contenuto |
| --- | --- |
| [`tIndustry-win-x64.zip`](https://github.com/Juan0177/tIndustry/releases/latest) | **Godot** — estrai ed esegui `tIndustry.exe` |
| [`tIndustry-linux-x64.zip`](https://github.com/Juan0177/tIndustry/releases/latest) | **Godot** — estrai ed esegui `tIndustry.x86_64` |

Alias espliciti (stesso contenuto): `tIndustry-godot-win-x64.zip` / `tIndustry-godot-linux-x64.zip`.

Il vecchio client Raylib è stato rimosso dopo la 0.3.4: i suoi zip restano sulle Release ≤ v0.3.4 e il codice nel tag `v0.3.4`.

Workflow GitHub Actions **non automatici su push/PR**: tag `v*` o avvio manuale → [release.yml](.github/workflows/release.yml) (test + export Godot); build + test su richiesta → [build-publish.yml](.github/workflows/build-publish.yml) (solo manuale).

---

## Cosa c’è in 0.3.4

| Area | In gioco (Godot) |
| --- | --- |
| **Client** | Godot 4 .NET |
| **Mondo** | **1000×1000** seed · CORE centro · H→core · bordi/clamp · minerali visibili |
| **Produzione** | Miner T1/T2 · forno multi-ricetta · assemblatore · estrattore · generatore |
| **Logistica** | Nastro T1/T2/T3 · junction · splitter · sorter · ponte · I/O adiacente |
| **Potenza** | Generatore + nodi T1/T2 · buffer/capacity · fuel-or-power forno |
| **Economia** | Costi/rimborsi place · CORE upgrade · Mercato · auto-sell |
| **Progressione** | Home · Campagna · Ricerca · tutorial · Gestione salvataggi (autosave/Salva come) |
| **UI** | Palette Mindustry · Impostazioni · overlay risorse · item tint Raylib |
| **Art** | Block 64×64 · terrain Raylib-like · ore icons tintati · gears miner |

---

## Da sorgente

```bash
dotnet build tIndustry.sln
dotnet test tests/TIndustry.Shared.Tests
godot4 --path godot
```

`tests/TIndustry.Shared.Tests` (xUnit) esegue gli scenari della sim — nastri, craft, potenza, save, ricerca, mercato, campagna — su una cartella salvataggi temporanea. Girano nel workflow di release e su richiesta (build-publish manuale).

---

## Prima sessione (Godot)

1. Splash → **Continua** / **Campagna** / **Nuova partita**.
2. Palette basso-destra: **St / Lo / Pr / Po**; **Esc** = cursore.
3. Minatore → nastri → Core; **M** Mercato per vendere; **T** Ricerca per sblocchi.
4. **G** Campagna per obiettivi; **F5/F9** salva/carica continua.

Default sbloccati: nastro T1 + minatore T1 (come nel seed Shared).

---

## Stack

| | |
| --- | --- |
| Client | **Godot 4.4 .NET** (`godot/`) |
| Sim | `TIndustry.Shared` (.NET 8) |
| Test | `tests/TIndustry.Shared.Tests` (xUnit) |
| Contenuti | `data/*.json` |

Icone: [`assets/ATTRIBUTION.md`](assets/ATTRIBUTION.md) (game-icons.net CC BY 3.0 · Kenney CC0).

---

## Roadmap

| Ora (0.3.4) | Dopo |
| --- | --- |
| Godot · 1000² · saves full · overlay risorse | Combat / unità (deferred) · P2.4 polish |

Storia: [CHANGELOG.md](CHANGELOG.md).

---

*Buon layout. Che i nastri non si intasino.*
