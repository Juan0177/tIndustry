# tIndustry

[![Build publish](https://github.com/Juan0177/tIndustry/actions/workflows/build-publish.yml/badge.svg)](https://github.com/Juan0177/tIndustry/actions/workflows/build-publish.yml)
**Versione 0.2.2** · vedi [CHANGELOG.md](CHANGELOG.md)

```
  ▢──▢──▢──▢── CORE ──$──
  │  miner → forno → lastre → mercato
  rame ──┼── assemblatore → fili
```

**Mindustry** ti ha insegnato a far scorrere gli item. **Tiny Industry** ti ha insegnato a far tornare i conti.  
**tIndustry** è dove i due si incontrano: logistica a nastro su mappa enorme, economia con portafoglio, ricerca e vendite al core.

Loop tipico: *scouting → estrazione → trasporto → trasformazione → vendita → sblocchi → fabbrica più grande*.

---

## Cosa c’è già (su `main`)

**v0.2.2** — tutto 0.2.1 più fix logistica e tutorial ripartibile. In sintesi:

| Area | In gioco |
| --- | --- |
| Mondo | Mappa **1000×1000**, core al centro, camera pan/zoom (Ctrl+rotella), depositi ferro e rame |
| Produzione | Minatore (output **4 lati**, eject **round-robin**), **forno**, **assemblatore**, **generatore** (power stub); edifici su terra libera (miner off-deposito = **0%**) |
| Logistica | Nastro base / **veloce**, **incrocio**, **sdoppiatore** (T-fork L/R), **ponte**; chip item saturi; chevron = facing |
| Economia | Wallet (denaro + lastre + fili), prezzi mercato, rimborso 100%, **potenziamento core** (+25% vendite) |
| Progressione | **Ricerca** data-driven; **tutorial** IT 5 step — riparte su **Nuova partita** / **Rivedi tutorial** (**Salta** / Fine) |
| Sessione | **Splash** brand (~8s / click) → Home (**Continua** solo con autosave), scenari + seed, save JSON **v6** |
| Dock | Stile Mindustry (Produzione / Logistica / Potenza / Inventario); **Rimuovi** in Produzione; tooltip IT |
| Qualità di vita | UI scale **100–200%**; overlay **CPU · GPU · RAM**; FPS unico; status toast auto-clear; tip onboarding; `--self-test` |
| Grafica | Icone HUD/dock/nastri CC0/CC-BY ([`assets/ATTRIBUTION.md`](assets/ATTRIBUTION.md)); strip risorse + **Δ sessione** |
| Contenuti | Seed `content.json` → AppData al primo avvio; niente Excel nel publish |
| Codice | Sorgenti in `src/{App,Simulation,Content,UI}/`; `.csproj` in root |

Non è (ancora) un clone combat di Mindustry, né un idle clicker: il valore sta nel **layout** e nel **reinvestimento**.

---

## Requisiti

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Linux / Windows / macOS (Raylib-cs)

---

## Come avviare

```bash
# build (csproj in root; sorgenti in src/)
dotnet build TIndustry.Logistics.csproj

# smoke test (senza finestra): se fallisce, qualcosa è andato storto in sim/save
dotnet run --project TIndustry.Logistics.csproj -- --self-test

# GUI
dotnet run --project TIndustry.Logistics.csproj
```

**Layout sorgenti**

| Cartella | Contenuto |
| --- | --- |
| `src/App/` | Entry (`Program`), loop gioco, impostazioni, monitor sistema |
| `src/Simulation/` | Mondo, nastri, economia, save, camera, ricerca |
| `src/Content/` | Definizioni dati, loaders JSON/Excel |
| `src/UI/` | Tema HUD/dock, icone |
| `assets/` · `data/` | Pack grafico e seed `content.json` (invariati) |

Namespace: `TIndustry.Logistics` (invariato). Il `.csproj` resta in root così CI e `dotnet run --project TIndustry.Logistics.csproj` non cambiano.

**Consigli GUI**

1. All’avvio: **splash** (thumbnail + brand) — click/tasto per continuare, oppure attendi (~8s). Poi dalla home: **Nuova partita** (loading + animazione; gen 1000² ≈ 1–2 s).
2. **Nuova partita**: segue il **tutorial** a banner (camera → miner → nastri → vendi → ricerca); riparte a ogni Conferma (o **Impostazioni → Rivedi tutorial**). **Salta** / Fine se preferisci senza.
3. Scout vicino al core: ferro starter, rame un po’ più a sud. (**H** / **Home** riporta la camera sul core.)
4. Piazza il miner: butta ore su **ogni** nastro adiacente. Vendi → **Ricerca** (T) → **Forno** → lastre → logistica avanzata.
5. **Esc** chiude prima il toast di status (se c’è), poi torna alla home; **Continua** solo con autosave valido.

Flag utili: `--smoke-test` / `--capture` (saltano lo splash; smoke chiude dopo pochi secondi), `--export-excel [path]`.

---

## Controlli

| Input | Azione |
| --- | --- |
| **WASD** / frecce | Pan |
| **Shift + drag** / tasto centrale | Pan (drag) |
| **Rotella** | Ruota pezzo / direzione nastro |
| **Ctrl + rotella** | Zoom |
| **H** / **Home** | Riporta la camera sul **core** |
| **R** | Ruota pezzo / direzione |
| **1–8** | Tool: nastro, minatore, forno, rimuovi, assemblatore, incrocio, sdoppiatore, ponte |
| **9** | Generatore (se sbloccato) |
| **Q** / **E** | Nastro base / nastro veloce (se sbloccato) |
| Click sinistro | Piazza (nastri: drag) |
| **T** / icona albero | Ricerca |
| **I** / icona ingranaggio | Impostazioni |
| **U** | Potenzia core (se puoi) |
| **Esc** / icona menu (☰) | Chiude toast → torna alla home |
| **Backspace** | Salta tutorial (se attivo) |

---

## Home & Impostazioni

**Home**

- **Continua** — carica l’autosave (nascosto se non c’è uno slot valido)
- **Nuova partita** — scenario + seed, mappa 1000×1000 (+ tutorial a ogni Conferma; **Rivedi tutorial** in Impostazioni)
- **Gestione salvataggi** — lista slot, carica, elimina, **Duplica Continua** → `slot-*.json`
- **Impostazioni** — overlay
- **Esci** — chiude il gioco (non la partita: salva prima se ti serve)

**Impostazioni** (anche in-game con **I**)

- **Scala UI**: **100% / 125% / 150% / 200%** (default **125%**) — dock, font, pannelli
- **Mostra contatore FPS** → `FPS N` in angolo (se overlay sistema OFF); con overlay ON il FPS sta lì
- **Mostra inventario risorse** → strip denaro + **Δ sessione** + materiali (sempre a parte dal dock)
- **Mostra risorse sistema (CPU · GPU · RAM)** → overlay play-only (GPU via `nvidia-smi` se disponibile)
- Hover su **Δ sessione** → tooltip: variazione patrimonio netto dall'inizio partita
- **VSync** ON/OFF (con VSync attivo il frame pacing segue il refresh; la preferenza limite FPS resta salvata)
- **Risoluzione**: preset fino a **2K** / **4K**, più **Auto risoluzione** (monitor corrente)
- **Limite FPS**: 30 → 600, poi **Illimitato**
- **Modalità schermo**: Finestra / Senza bordi / Schermo intero → **Applica**

Persistenza: `%LocalAppData%/tIndustry/settings.json` (Linux: `~/.local/share/tIndustry/settings.json`).

---

## Salvataggi, mappa, ricerca

| Cosa | Dove / come |
| --- | --- |
| Cartella save | `%LocalAppData%/tIndustry/saves/` |
| Autosave | `continua.json` |
| Slot nominati | `slot-*.json` via **Duplica Continua** |
| Formato | JSON versionato (**v6**: edifici, ponti, unlock, economia, generatori, potenza, `tutorialCompleted`) |
| Mappa | **1000×1000** tile; draw culling sulla viewport |
| Ricerca | **T** / **RICERCA**: seleziona struttura → verifica costi → **Conferma sblocco** (spende, non rimborsa) |

**Hint progressione (ordine sensato)**

1. Minatore + nastri → vendi **ferro grezzo** ($8)  
2. Sblocca **Forno** → vendi **lastre** ($30; meglio di 2× ore)  
3. **Potenzia core** se vuoi +25% sulle vendite  
4. Sblocca logistica (veloce / incrocio / sdoppiatore / ponte)  
5. Rame + **Assemblatore** → **fili** (prezzo mercato aggiornato) e sblocchi più cari  
6. Se i forni stallano: **RICERCA** → **Generatore** → **9** / **GEN.**

Default sbloccati: nastro base e minatore. Il resto paga il pedaggio della ricerca.

---

## Download

**Release consigliata:** [v0.2.2](https://github.com/Juan0177/tIndustry/releases/tag/v0.2.2) · [latest](https://github.com/Juan0177/tIndustry/releases/latest)

| Asset | Piattaforma |
| --- | --- |
| `tIndustry-win-x64.zip` | Windows x64 (self-contained) |
| `tIndustry-linux-x64.zip` | Linux x64 (self-contained) |

Estrai ed esegui il binario (su Windows: `TIndustry.Logistics.exe`).

Su ogni tag `v*`, [`.github/workflows/release.yml`](.github/workflows/release.yml) pubblica questi zip. Su push/PR verso `main` o `cursor/**`, [`.github/workflows/build-publish.yml`](.github/workflows/build-publish.yml) espone anche artifact CI (`tIndustry-win-x64` / `tIndustry-linux-x64`) dalla tab **Actions**.

---

## Stack

| Pezzo | Scelta |
| --- | --- |
| Linguaggio | C# / **.NET 10** |
| Rendering | **Raylib-cs** (immediate-mode) |
| Contenuti | seed `data/content.json` → AppData al primo avvio (`%LocalAppData%/tIndustry/content/` · Linux `~/.local/share/tIndustry/content/`); Excel solo locale/`--export-excel` |
| Progetto | singolo `TIndustry.Logistics` (sorgenti sotto `src/App` · `Simulation` · `Content` · `UI`) |
| Sim | ~30 Hz step fisso · render 60 FPS |
| Persistenza | JSON (`GameSave`, `GameSettings`) |

Niente Unity/Godot in roadmap early: si itera sul prototipo Raylib finché il loop è chiaro.

---

## Stato & roadmap

| Fatto | Prossimo (orizzonte) |
| --- | --- |
| **v0.2.2**: splitter T-fork, miner round-robin, tutorial ripartibile | Fuel/cavi potenza, più ricette, bilanciamento più profondo |
| **v0.2.1**: playability (nastri, UI scale, tutorial, miner 4-lati, toast) + `src/` | Ulteriore polish UI / performance mappa piena |
| **v0.2.0**: splash, icon pack, first-launch AppData, hot-path | Combat/unità: **non** priorità early |
| CI publish win/linux + release su tag `v*` | |
| `miner-advanced` ancora stub | |

Dettaglio versioni: [CHANGELOG.md](CHANGELOG.md) · note [v0.2.2](https://github.com/Juan0177/tIndustry/releases/tag/v0.2.2).

Criterio di progresso: una sessione deve far sentire *ho trovato il ferro, l’ho portato al forno, ho venduto lastre, ho sbloccato il nastro veloce, ho espanso*. Se manca un pezzo di quella frase, si lavora lì.

---

## Licenza / contributo

Repo in evoluzione attiva. Patch e idea welcome — meglio vertical slice giocabili che feature incomplete. Prima di spingere: `dotnet run -- --self-test`.

**Asset grafici di terze parti**: icone in `assets/icons/` — vedi [`assets/ATTRIBUTION.md`](assets/ATTRIBUTION.md)
(game-icons.net CC BY 3.0 · Kenney CC0).

*Buon layout. Che i nastri non si intasino.*
