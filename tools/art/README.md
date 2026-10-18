# Generatore degli sprite

Gli sprite di edifici, logistica, Core, drone e oggetti sono pixel-art originale disegnata da questo script (G1, spec in `docs/superpowers/specs/2026-10-18-g1-arte-blocchi-design.md`). Solo Python e Pillow; nessun asset di terzi.

## Comandi

Dalla radice del repo:

```bash
python tools/art/genera.py                  # scrive gli sprite registrati in godot/assets/ e la tavola in .dev/arte/
python tools/art/genera.py --solo miner     # solo alcuni sprite (più nomi separati da spazio)
python tools/art/genera.py --controlla      # non scrive: esce con 1 se un PNG in godot/assets/ non è aggiornato
python -m unittest tools/art/test_genera.py # determinismo, misure, tavolozza, copertura di content.json
```

Dopo aver scritto PNG nuovi: `APPDATA="$(mktemp -d)" godot4 --headless --path godot --import`, poi ripristina i `.import` che cambiano solo per fine riga (vedi `CLAUDE.md`).

**Versione di Pillow.** I byte dei PNG dipendono dal codificatore di Pillow (zlib): gli sprite del repo sono stati scritti con **Pillow 12.3**. `--controlla` confronta i byte, quindi con un'altra versione di Pillow può segnalare differenze anche se i pixel sono uguali; in quel caso rigenera con `genera.py` e controlla nella tavola che non sia cambiato nulla a occhio prima di fare il commit.

`genera.py` scrive solo gli sprite registrati: gli altri PNG di `godot/assets/` restano come sono. La tavola `.dev/arte/tavola.png` (e una copia datata) mette ogni sprite vecchio accanto al nuovo, a 1× e 2×, su fondo chiaro e scuro: serve per la revisione a occhio.

## File

| File | Cosa contiene |
|---|---|
| `palette.py` | Tutti i colori: contorno `OUT` e rampe da scuro a chiaro (4–6 toni) per metalli, materiali, bagliore, accento, energia. |
| `forme.py` | Mattoncini comuni: pannello in rilievo, bullone, tubo, griglia, ingranaggio, cerchio, bagliore, ombra, striscia di fotogrammi, composizione a strati. Luce sempre da in alto a sinistra. |
| `blocchi/` | Un modulo per famiglia (`produzione`, `logistica`, `potenza`, `core`, `drone`, `oggetti`); ogni modulo del pacchetto è importato da solo. |
| `genera.py` | Comando principale. |
| `tavola.py` | Tavola di confronto vecchio/nuovo. |
| `test_genera.py` | Test con `unittest`. |

## Regole

- **32 px per cella** del blocco (in gioco ×2, filtro Nearest). Oggetti: `<id>.png` 32×32 (HUD) e `<id>-belt.png` 10×10 (nastri).
- **Nomi = id** di `data/content.json`. Parti con suffisso (`miner-base`, `miner-drill`, `smelter-glow`…); animazioni come strisce orizzontali `<id>-anim.png` di fotogrammi sempre quadrati (lato = altezza: `Sprites.Frames` taglia quadrati). Tier: `core-1…4`, `drone-1…3`.
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

   Per una striscia di fotogrammi: `@sprite("miner-drill-anim")` e `return forme.striscia(fotogrammi)`, con fotogrammi quadrati. Un nome nuovo deve corrispondere a un oggetto, a un drone o a un blocco della tabella `CELLE` di `test_genera.py`, altrimenti il test delle misure fallisce.
2. `python tools/art/genera.py --solo junction`, guarda `.dev/arte/tavola.png`.
3. Importa in Godot (sopra) e lancia i test.

Nel client la texture si prende sempre da `Sprites` (`godot/scripts/visuals/Sprites.cs`): `Sprites.For(id)`, `Sprites.TryFor(id)`, `Sprites.Item(id)`, `Sprites.BeltItem(id)`, `Sprites.Frames(id)`. Un id nuovo non richiede di toccare altri file del client.
