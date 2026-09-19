# tIndustry

[![Build publish](https://github.com/Juan0177/tIndustry/actions/workflows/build-publish.yml/badge.svg)](https://github.com/Juan0177/tIndustry/actions/workflows/build-publish.yml)
**Versione 0.1.1** · vedi [CHANGELOG.md](CHANGELOG.md)

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

Fasi **0–6** + Impostazioni/CI. In sintesi:

| Area | In gioco |
| --- | --- |
| Mondo | Mappa **1000×1000**, camera pan/zoom, depositi ferro e rame |
| Produzione | Minatore, **forno** (`smelt-iron`), **assemblatore** (rame + lastra → fili), **generatore** (power stub) |
| Logistica | Nastro base / **veloce**, **incrocio**, **sdoppiatore**, **ponte** (span 2–4) |
| Economia | Wallet (denaro + lastre + fili), prezzi mercato, rimborso 100%, **potenziamento core** (+25% vendite) |
| Progressione | **Ricerca** data-driven: sblocchi a pagamento, persistenti nel save |
| Sessione | Home (Continua se autosave valido / Nuova / Gestione / Impostazioni / Esci), scenari + seed, save JSON **v6** |
| Qualità di vita | Overlay FPS e risorse, tip onboarding, UI in italiano, `--self-test` esteso |
| Grafica | Icone HUD/dock/nastri da pack CC0/CC-BY (vedi [`assets/ATTRIBUTION.md`](assets/ATTRIBUTION.md)); strip risorse con **Δ sessione** |

Non è (ancora) un clone combat di Mindustry, né un idle clicker: il valore sta nel **layout** e nel **reinvestimento**.

---

## Requisiti

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Linux / Windows / macOS (Raylib-cs)

---

## Come avviare

```bash
# build
dotnet build TIndustry.Logistics.csproj

# smoke test (senza finestra): se fallisce, qualcosa è andato storto in sim/save
dotnet run --project TIndustry.Logistics.csproj -- --self-test

# GUI
dotnet run --project TIndustry.Logistics.csproj
```

**Consigli GUI**

1. Dalla home: **Nuova partita** (schermata di caricamento + animazione d’ingresso; la gen 1000² può impiegare 1–2 secondi).
2. Scout vicino al core: ferro starter, rame un po’ più a sud. (**H** / **Home** riporta la camera sul core.)
3. Vendi ore → **Ricerca** (T / icona albero) → sblocca **Forno** → chiudi il loop lastre → finanzia logistica avanzata.
4. **Esc** / icona menu torna alla home; **Continua** riprende l’autosave.

Flag utili: `--smoke-test` (chiude dopo pochi secondi), `--capture` (screenshot in `artifacts/`), `--export-excel [path]`.

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
| **Esc** / icona menu (☰) | Torna alla home |

---

## Home & Impostazioni

**Home**

- **Continua** — carica l’autosave (nascosto se non c’è uno slot valido)
- **Nuova partita** — scenario + seed, mappa 1000×1000
- **Gestione salvataggi** — lista slot, carica, elimina, **Duplica Continua** → `slot-*.json`
- **Impostazioni** — overlay
- **Esci** — chiude il gioco (non la partita: salva prima se ti serve)

**Impostazioni** (anche in-game con **I**)

- **Mostra contatore FPS** → `FPS N` in header (a sinistra delle icone), senza sovrapporre il titolo
- **Mostra inventario risorse** → strip risorse in header (denaro + **Δ sessione** + materiali con icone); **non** nel dock
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
| Formato | JSON versionato (**v6**: edifici, ponti, unlock, economia, generatori, potenza) |
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

## Download build (GitHub Actions)

Su push/PR verso `main` o `cursor/**`, il workflow [`.github/workflows/build-publish.yml`](.github/workflows/build-publish.yml) pubblica artifact self-contained:

| Artifact | Contenuto |
| --- | --- |
| `tIndustry-win-x64` | publish self-contained win-x64 (self-test prima del publish) |
| `tIndustry-linux-x64` | publish self-contained linux-x64 |

**Come scaricarli**

1. Apri il repo su GitHub → tab **Actions**
2. Seleziona un run verde di **Build publish**
3. In fondo alla pagina: **Artifacts** → scarica `tIndustry-win-x64` (o linux)
4. Estrai ed esegui il binario (su Windows: `TIndustry.Logistics.exe` o nome publish)

Il job win-x64 esegue anche `--self-test` prima del publish.

Su tag `v*` (es. `v0.1.0`), il workflow [`.github/workflows/release.yml`](.github/workflows/release.yml) crea una **GitHub Release** con zip win-x64 e linux-x64.

---

## Stack

| Pezzo | Scelta |
| --- | --- |
| Linguaggio | C# / **.NET 10** |
| Rendering | **Raylib-cs** (immediate-mode) |
| Contenuti | seed `data/content.json` → AppData al primo avvio (`~/.local/share/tIndustry/content/`); Excel solo locale/`--export-excel` |
| Progetto | singolo `TIndustry.Logistics` |
| Sim | ~30 Hz step fisso · render 60 FPS |
| Persistenza | JSON (`GameSave`, `GameSettings`) |

Niente Unity/Godot in roadmap early: si itera sul prototipo Raylib finché il loop è chiaro.

---

## Stato & roadmap

| Fatto | Prossimo (orizzonte) |
| --- | --- |
| **v0.1**: Phase 0–6 (camera, save, smelter, research, economia, logistica, power stub, seed, onboarding) | Fuel/cavi potenza, più ricette, bilanciamento più profondo |
| CI publish win/linux + release su tag `v*` | UI più leggibile, performance piena 1000² |
| `miner-advanced` ancora stub | Combat/unità: **non** priorità early |

Criterio di progresso: una sessione deve far sentire *ho trovato il ferro, l’ho portato al forno, ho venduto lastre, ho sbloccato il nastro veloce, ho espanso*. Se manca un pezzo di quella frase, si lavora lì.

---

## Licenza / contributo

Repo in evoluzione attiva. Patch e idea welcome — meglio vertical slice giocabili che feature incomplete. Prima di spingere: `dotnet run -- --self-test`.

**Asset grafici di terze parti**: icone in `assets/icons/` — vedi [`assets/ATTRIBUTION.md`](assets/ATTRIBUTION.md)
(game-icons.net CC BY 3.0 · Kenney CC0).

*Buon layout. Che i nastri non si intasino.*
