# tIndustry

[![Build publish](https://github.com/Juan0177/tIndustry/actions/workflows/build-publish.yml/badge.svg)](https://github.com/Juan0177/tIndustry/actions/workflows/build-publish.yml)
**Godot 4 .NET — path ufficiale** · Raylib **legacy** (v0.2.11) · [CHANGELOG](CHANGELOG.md)

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
| `src/TIndustry.Shared/` | Sim/data condivisa (nastri, craft, power, save, campagna) |
| `data/` | Seed `content.json` / `campaign.json` |

Combat / unità: **ancora fuori scope**.

---

## Download (legacy Raylib)

Le release zip [`tIndustry-*-x64.zip`](https://github.com/Juan0177/tIndustry/releases/latest) pubblicano ancora il client **Raylib** (`TIndustry.Logistics`, v0.2.11). Uso: manutenzione / confronto; **non** è più il path di sviluppo primario.

Tag `v*` → [release.yml](.github/workflows/release.yml). Push/PR → [build-publish.yml](.github/workflows/build-publish.yml) (Godot/Shared + publish Raylib).

---

## Da sorgente

### Godot (consigliato)

```bash
dotnet build src/TIndustry.Shared/TIndustry.Shared.csproj
dotnet build godot/TIndustry.Godot.csproj
godot4 --path godot
```

### Raylib (legacy)

```bash
dotnet build TIndustry.Logistics.csproj
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

Richiede [.NET 10 SDK](https://dotnet.microsoft.com/download) · Raylib-cs.

| Path | Ruolo |
| --- | --- |
| `src/App/` · `src/Simulation/` · `src/UI/` · `src/Content/` | Client Raylib legacy |
| `TIndustry.Logistics.csproj` | Entry Raylib (namespace `TIndustry.Logistics`) |

---

## Cosa c’è (Godot)

| Area | In gioco |
| --- | --- |
| **Mondo** | Slice giocabile, depositi da seed campagna/sandbox |
| **Produzione** | Miner T1/T2, forno, assemblatore, estrattore, generatore |
| **Logistica** | Nastro T1/T2, junction, splitter, sorter, ponte |
| **Potenza** | Generatore + nodi T1/T2 · craft boostato se alimentato |
| **Economia** | Stock Core · Mercato · vendita automatica opzionale |
| **Progressione** | Home splash · Campagna · Ricerca (grafo) · unlock placeables |
| **UI** | Palette angolo Mindustry · info slot · tech tree a icone |

Art block 64×64 allineata mappa + palette; polish grafico in corso (opzionale).

---

## Prima sessione (Godot)

1. Splash → **Continua** / **Campagna** / **Nuova partita**.
2. Palette basso-destra: **St / Lo / Pr / Po**; **Esc** = cursore.
3. Minatore → nastri → Core; **M** Mercato per vendere; **T** Ricerca per sblocchi.
4. **G** Campagna per obiettivi; **F5/F9** salva/carica continua.

Default sbloccati: nastro T1 + minatore T1 (come nel seed Shared).

---

## Stack

| | Ufficiale | Legacy |
| --- | --- | --- |
| Client | **Godot 4.4 .NET** (`godot/`) | Raylib-cs (`TIndustry.Logistics`) |
| Sim | `TIndustry.Shared` (.NET 8) | `src/Simulation` (.NET 10) |
| Contenuti | `data/*.json` | stesso seed + AppData Raylib |

Icone: [`assets/ATTRIBUTION.md`](assets/ATTRIBUTION.md) (game-icons.net CC BY 3.0 · Kenney CC0).

---

## Roadmap

| Ora | Dopo |
| --- | --- |
| Godot ufficiale · art polish opzionale · bilanciamento | Combat / unità (deferred) · eventuale ritiro Raylib |

Storia: [CHANGELOG.md](CHANGELOG.md).

---

*Buon layout. Che i nastri non si intasino.*
