# Generatore degli sprite

Gli sprite di edifici, logistica, Core, drone e oggetti, le icone e il carattere dell'interfaccia sono pixel-art originale disegnata da questo script (G1, spec in `docs/superpowers/specs/2026-10-18-g1-arte-blocchi-design.md`). Solo Python e Pillow; nessun asset di terzi.

## Comandi

Dalla radice del repo:

```bash
python tools/art/genera.py                  # scrive gli sprite registrati e il carattere in godot/assets/, le tavole in .dev/arte/
python tools/art/genera.py --solo miner     # solo alcuni sprite (più nomi separati da spazio)
python tools/art/genera.py --controlla      # non scrive: esce con 1 se un PNG in godot/assets/ non è aggiornato (byte)
python tools/art/genera.py --controlla --pixel  # idem confrontando misura e pixel (CI, altri sistemi)
python -m unittest tools/art/test_genera.py # determinismo, misure, tavolozza, copertura di content.json
python tools/art/proposte_nastri_stretti.py # tavola delle proposte di nastri stretti in .dev/arte/nastri-stretti.png
python -m unittest tools/art/test_proposte_nastri_stretti.py
```

**Proposte di nastri stretti** (`proposte_nastri_stretti.py`): solo una tavola per scegliere, non entra nel registro di `genera.py` e non scrive in `godot/assets/`. Tre varianti col battistrada largo quanto lo sprite `*-belt` più grande (A binari sottili di 2 px, B canale incassato fra bordi scuri, C proporzioni di Mindustry con frecce piccole), per i quattro tier: dritto pieno di oggetti a 0,4 di cella, curva, T e unione laterale, a 1× e 2× (la scala del gioco) su fondo chiaro e scuro, accanto al nastro di oggi coi suoi sprite. Usa `TIER` e la piegatura della curva di `blocchi/logistica.py`. Il 2026-10-20 l'utente ha scelto la **C «Mindustry»**: un piano a parte la porta negli sprite del gioco (issue #252).

Dopo aver scritto PNG nuovi: `APPDATA="$(mktemp -d)" godot4 --headless --path godot --import`, poi ripristina i `.import` che cambiano solo per fine riga (vedi `CLAUDE.md`).

**In CI** i workflow manuali e di release lanciano i test e `--controlla --pixel` con Pillow 12.3.0 (su Linux il codificatore PNG scrive byte diversi, quindi lì si confrontano misura e pixel); un test controlla che ogni PNG nella radice di `godot/assets/` sia uno sprite registrato.

**Versione di Pillow.** I byte dei PNG dipendono dal codificatore di Pillow (zlib): gli sprite del repo sono stati scritti con **Pillow 12.3**. `--controlla` confronta i byte, quindi con un'altra versione di Pillow può segnalare differenze anche se i pixel sono uguali; in quel caso rigenera con `genera.py` e controlla nella tavola che non sia cambiato nulla a occhio prima di fare il commit.

`genera.py` scrive solo gli sprite registrati: gli altri PNG di `godot/assets/` restano come sono. La tavola `.dev/arte/tavola.png` (e una copia datata) mette ogni sprite vecchio accanto al nuovo, a 1× e 2×, su fondo chiaro e scuro: serve per la revisione a occhio. Quando scrive sprite del terreno, `genera.py` compone anche `.dev/arte/tavola-terreno.png`: una finestra di 12×12 celle per bioma con varianti, bordi, pareti, acqua, giacimenti e decorazioni, disegnate come le disegnerà il client.

**Interfaccia (G4).** Le icone `ui-<nome>.png` (32×32) e `ui-<nome>-16.png` (16×16) vanno in `godot/assets/ui/` (nel registro hanno il nome `ui/ui-<nome>`): una sagoma bianca disegnata a caratteri su una griglia 14×14, con un contorno scuro di 1 px tutto attorno; la 32 px è la stessa sagoma ×2. Le icone (`blocchi/interfaccia.py`, tabella `ICONE_UI` dei test) sono le categorie della palette (`cat-strumenti`, `cat-logistica`, `cat-produzione`, `cat-potenza`), `menu`, `ricerca`, `mercato`, `campagna`, `lucchetto`, `energia`, `impostazioni`, `salvataggi` e `lingua`. Nello stesso modulo (tabella `ALTRI_UI`) ci sono le quattro caselle di spunta del tema `ui-spunta-si`, `-no`, `-si-spenta`, `-no-spenta` (20×20, colori `UI_*` di `palette.py`, che `test_spunte_come_ui_colors` confronta con `UiColors` del client) e il logo della Home `ui-logo` (54×16: "tINDUSTRY" nel carattere del gioco a 1×, rampa dell'accento, su un nastro; il client lo mostra ×5). Il carattere pixel (`carattere.py`, ASCII stampabile più `àèéìíòóùú ÀÈÉÌÒÙ « » … · € ×` e i segni usati dai testi del gioco `→ ← ↑ ↓ ↔ ↖ — – ✓ ° ≥`) esce come BMFont in due misure, `ui/carattere-testo` (glifi ×2) e `ui/carattere-titoli` (×3), ciascuna con `.fnt` (testo, fine riga LF) e `.png`; lo scrive solo un'esecuzione completa (senza `--solo`) e `--controlla` lo confronta. La tavola `.dev/arte/tavola-ui.png` mostra frasi di prova in italiano e in inglese a ×2 e ×3, tutto l'insieme dei glifi, una riga di riferimento in un carattere OFL (Aileron, incluso in Pillow) e le icone a 16, 32 e 32×2 su un pannello dell'interfaccia. Il test `test_carattere_copre_i_testi` raccoglie ogni carattere dei testi del gioco (valori di `data/*.json`, le colonne italiana e inglese di `godot/lingue/testi.csv` e i letterali stringa C# di `godot/scripts` e `src/TIndustry.Shared`, commenti esclusi) e fallisce elencando quelli senza glifo in `carattere-testo.fnt`, con il file dove compaiono: un testo nuovo con un segno nuovo chiede prima il glifo qui. Il client usa solo le misure native dei due file (16 per il testo, 24 e 48 per i titoli), così ogni pixel del carattere resta intero (ADR 0006).

## File

| File | Cosa contiene |
|---|---|
| `palette.py` | Tutti i colori: contorno `OUT` e rampe da scuro a chiaro (4–6 toni) per metalli, materiali, bagliore, accento, energia. |
| `forme.py` | Mattoncini comuni: pannello in rilievo, bullone, tubo, griglia, ingranaggio, cerchio, bagliore, ombra, striscia di fotogrammi, composizione a strati. Luce sempre da in alto a sinistra. |
| `blocchi/` | Un modulo per famiglia (`produzione`, `logistica`, `potenza`, `core`, `drone`, `oggetti`, `terreno`, `effetti`, `interfaccia`); ogni modulo del pacchetto è importato da solo. `terreno` legge suoli, pareti e biomi da `data/content.json` (vedi la sua docstring per nomi e misure). |
| `carattere.py` | Glifi del carattere dell'interfaccia (un carattere per pixel) ed esportazione BMFont. |
| `genera.py` | Comando principale. |
| `tavola.py` | Tavola di confronto vecchio/nuovo, tavola del terreno e tavola dell'interfaccia; `pezzi_mappa` è la composizione di riferimento del terreno. |
| `fixture_terreno.py` | Scrive la fixture dei test C# (`tests/TIndustry.Shared.Tests/Fixtures/terrain-look-24.json`): mappa fissa 24×24 e pezzi di `pezzi_mappa`; `--controlla` dice se va rigenerata. |
| `test_genera.py` | Test con `unittest`. |

## Regole

- **32 px per cella** del blocco (in gioco ×2, filtro Nearest). Oggetti: `<id>.png` 32×32 (HUD) e `<id>-belt.png` 10×10 (nastri).
- **Nomi = id** di `data/content.json`. Parti con suffisso (`miner-base`, `miner-drill`, `smelter-glow`…); animazioni come strisce orizzontali `<id>-anim.png` di fotogrammi sempre quadrati (lato = altezza: `Sprites.Frames` taglia quadrati). Tier: `core-1…4`, `drone-1…3`; parti disegnate sopra un drone, nello stesso quadrato: `drone-1-elmetto` (tabella `PARTI_DRONE` di `test_genera.py`).
- **Nastri a un pixel per fotogramma**: le strisce `conveyor-*-anim` (dritto, curva, T, unioni laterali) hanno 32 fotogrammi per cella (`PASSO = 1` in `blocchi/logistica.py`); il battistrada di ogni fotogramma è il precedente spostato di 1 px nel verso del flusso, così il nastro scorre liscio accanto agli oggetti che si muovono a ogni tick. Il client sceglie il fotogramma dal numero di fotogrammi della striscia.
- **Tier dei nastri** (`TIER` in `blocchi/logistica.py`: rampa dei binari, rampa delle frecce, bordo interno): base grigio, veloce rame, espresso titanio col bordo d'accento, rinforzato (`conveyor-armored`) acciaio con le piastre rivettate (`RINFORZATI`) e bordo e frecce rossi. Ogni tier ha tutte le strisce (dritto, curva, T, unioni); `test_rinforzato_diverso_dal_veloce` controlla che i binari del rinforzato non abbiano un pixel in comune con quelli del veloce.
- **Deterministico**: niente `random` senza seme fisso, niente date o metadati nei PNG. Due esecuzioni danno gli stessi byte.
- **Mai a mano**: i PNG generati non si ritoccano con un editor; si cambia lo script e si rigenera. Le scelte (perché da script, tavolozza, luce, tier) sono in `docs/decisioni/0004-arte-generata-da-script.md`.
- Si usano solo i colori di `palette.py`.

## Aggiungere uno sprite

1. Nel modulo della sua famiglia in `blocchi/` (crealo se manca) scrivi una funzione che restituisce un'immagine RGBA e registrala:

   ```python
   from blocchi import sprite
   import forme
   from palette import METALLO

   @sprite("junction")
   def junction():
       im = forme.nuova(32, 32)
       forme.pannello(im, 0, 0, 31, 31, METALLO)
       return im
   ```

   Per una striscia di fotogrammi: `@sprite("miner-drill-anim")` e `return forme.striscia(fotogrammi)`, con fotogrammi quadrati. Un nome nuovo deve corrispondere a un oggetto, a un drone, a un blocco della tabella `CELLE` di `test_genera.py` a uno sprite del terreno, a una particella `fx-*` (quadrato o striscia di varianti quadrate da 8-16 px, usate dal gestore degli effetti con `godot/effetti/effetti.json`) o a un'icona dell'interfaccia `ui/ui-*` (tabella `ICONE_UI`, o `ALTRI_UI` per le caselle di spunta e il logo), altrimenti il test delle misure fallisce.
2. `python tools/art/genera.py --solo junction`, guarda `.dev/arte/tavola.png`.
3. Importa in Godot (sopra) e lancia i test.

Nel client la texture si prende sempre da `Sprites` (`godot/scripts/visuals/Sprites.cs`): `Sprites.For(id)`, `Sprites.TryFor(id)`, `Sprites.Item(id)`, `Sprites.BeltItem(id)`, `Sprites.Frames(id)`. Un id nuovo non richiede di toccare altri file del client.

## Terreno: come si compone una mappa

Gli sprite del terreno (`blocchi/terreno.py`) sono pezzi da comporre. La regola qui sotto è quella di `tavola.pezzi_mappa` (l'elenco dei pezzi nell'ordine di disegno; `tavola.componi` li incolla), riportata identica nel client da `TerrainLook.Plan` (`src/TIndustry.Shared/Grid/TerrainLook.cs`): il test C# `TerrainLookTests.MatchesPythonReference` confronta i due elenchi su una mappa fissa (fixture di `fixture_terreno.py`; dopo aver cambiato la regola, `python tools/art/fixture_terreno.py`); le coordinate sono quelle delle celle della mappa, con `y` che cresce verso Sud. Tutte le scelte "a caso" usano `hash_cella(x, y, sale)` (hash intero a 32 bit di `terreno.py`, da riportare identico in C# con aritmetica `uint` senza controllo di overflow).

Per ogni cella, in quest'ordine:

1. **Suolo.** `terreno-<suolo>-<n>` con `n = hash_cella(x, y, 1) % 4` (`SALE_VARIANTE`), anche per l'acqua: le sue varianti sono il fondale fermo. Nel client le increspature le aggiunge lo shader `godot/shaders/water.gdshader` in coordinate del mondo (niente motivo ripetuto a ogni cella) dove il fondale resta scoperto; le strisce `acqua-bassa-anim` e `acqua-profonda-anim` non entrano nella composizione.
2. **Bordi** (`pezzi_bordo`). Per gli 8 vicini nell'ordine N, E, S, O, NE, SE, SO, NO (pezzi 0..7 della striscia `terreno-<vicino>-bordo`): un lato si disegna se il suolo del vicino ha `edgePriority` più alta di quello della cella; un angolo si disegna se il vicino diagonale ha priorità più alta **e nessuno dei due lati accanto** (N ed E per NE, e così via) ha lo stesso suolo, perché lì le frange dei lati coprono già l'angolo. I pezzi si disegnano dalla priorità più bassa alla più alta e, a pari suolo, nell'ordine della striscia. Le frange non hanno contorno: due pezzi dello stesso suolo che si sovrappongono non lasciano segni. Ai bordi della mappa il vicino mancante vale come la cella stessa (nessun bordo).
3. **Giacimento.** Se la cella ha un giacimento: `giacimento-<oggetto>-<n>` con `n = hash_cella(x, y, 4) % 3` (`SALE_GIACIMENTO`); l'oggetto è `TerrainMap.ItemIdForDeposit`.
4. **Decorazione.** Solo su terra, senza parete né giacimento: se `hash_cella(x, y, 2) % 100 < DENSITA_DECO[bioma]` (`SALE_DECO`, bioma del suolo), `deco-<bioma>-<k>` con `k = hash_cella(x, y, 3) % 3` (`SALE_DECO_TIPO`). Gli edifici la coprono da soli.

Poi le **pareti**, a doppia griglia: un pezzo per ogni angolo di cella (anche sui bordi della mappa), centrato sull'angolo, cioè spostato di mezza cella in alto a sinistra rispetto alla cella `(x, y)` di cui l'angolo è lo spigolo in alto a sinistra. L'indice del pezzo nella striscia `parete-<parete>` è la somma dei bit delle quattro celle attorno all'angolo che hanno quella parete: NO = cella `(x-1, y-1)` → 1, NE = `(x, y-1)` → 2, SE = `(x, y)` → 4, SO = `(x-1, y)` → 8; il pezzo 0 non si disegna. Fuori dalla mappa non ci sono pareti. Pareti diverse (biomi diversi) si disegnano una dopo l'altra, in ordine di id, con lo stesso calcolo. I pezzi combaciano da soli: ogni lato di un pezzo dipende solo dai due angoli che ha in comune col vicino (test).
