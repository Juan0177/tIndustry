# Arte generata

Come nascono sprite, icone e carattere di tIndustry: da uno script Python, mai disegnati a mano in un editor.

## La regola

Tutti gli sprite di edifici, nastri, Core, drone, oggetti, terreno ed effetti, le icone dell'interfaccia, il logo e il carattere pixel escono da **`python tools/art/genera.py`** (Python e Pillow, nessun asset di terzi, nessun file di Mindustry). **I PNG in `godot/assets/` non si ritoccano mai a mano**: si cambia lo script e si rigenera. La scelta e le sue ragioni sono nell'[ADR 0004](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0004-arte-generata-da-script.md).

Perché uno script: l'arte è ripetibile, si rivede come codice, un cambio di tavolozza si propaga a tutto e due esecuzioni danno gli stessi byte.

## Comandi

Dalla radice del repository:

```bash
python tools/art/genera.py                      # scrive gli sprite e il carattere in godot/assets/, le tavole in .dev/arte/
python tools/art/genera.py --solo miner         # solo alcuni sprite (più nomi separati da spazio)
python tools/art/genera.py --controlla          # non scrive: esce con 1 se un PNG in godot/assets/ non è aggiornato
python tools/art/genera.py --controlla --pixel  # come sopra, confrontando misura e pixel invece dei byte
python -m unittest tools/art/test_genera.py     # determinismo, misure, tavolozza, copertura di content.json
```

La tavola `.dev/arte/tavola.png` mette ogni sprite vecchio accanto al nuovo per la revisione a occhio; per il terreno c'è `.dev/arte/tavola-terreno.png`, per l'interfaccia `.dev/arte/tavola-ui.png`.

## Come è organizzato

| File | Cosa contiene |
|---|---|
| `tools/art/palette.py` | Tutti i colori, in rampe da scuro a chiaro; luce sempre da in alto a sinistra, contorno scuro |
| `tools/art/forme.py` | Mattoncini comuni: pannelli, bulloni, tubi, griglie, ingranaggi, strisce di fotogrammi |
| `tools/art/blocchi/` | Un modulo per famiglia (`produzione`, `logistica`, `potenza`, `core`, `drone`, `oggetti`, `terreno`, `effetti`, `interfaccia`); ogni funzione registrata con `@sprite("<nome>")` è un PNG |
| `tools/art/carattere.py` | Glifi del carattere dell'interfaccia, esportati come BMFont |
| `tools/art/tavola.py` | Tavole di confronto e regola di composizione del terreno |
| `tools/art/fixture_terreno.py` | Fixture che tiene allineate la regola del terreno in Python e quella in C# (`TerrainLook`) |

## Convenzioni principali

- **32 px per cella**, mostrati ×2 nel gioco con filtro Nearest.
- **Nome del file = id** di `data/content.json`; le parti hanno un suffisso (`miner-base`, `miner-drill`…), le animazioni sono strisce `<id>-anim.png` di fotogrammi quadrati; tier `core-1…4` e `drone-1…3`.
- **Oggetti**: `<id>.png` 32×32 per HUD e pannelli, `<id>-belt.png` 10×10 per i nastri.
- **Solo colori di `palette.py`.**
- Nel client ogni texture passa da `Sprites` (`godot/scripts/visuals/Sprites.cs`): uno sprite mancante diventa un riquadro magenta con un avviso, mai un crash.

## Aggiungere uno sprite

1. Nel modulo della sua famiglia in `tools/art/blocchi/` scrivi una funzione che restituisce un'immagine RGBA e registrala con `@sprite("<id>")`. Un blocco nuovo va anche nella tabella `CELLE` di `test_genera.py`.
2. `python tools/art/genera.py --solo <id>` e guarda la tavola.
3. `python -m unittest tools/art/test_genera.py`.
4. Importa i PNG nuovi in Godot per avere i loro `.import` (comando in [Compilare e avviare](Compilare-e-avviare)) e fai il commit di PNG e `.import` insieme.

Esempi e regole di dettaglio (terreno, interfaccia, nastri) sono in [tools/art/README.md](https://github.com/Juan0177/tIndustry/blob/main/tools/art/README.md).

## Versione di Pillow e CI

I byte dei PNG dipendono dal codificatore di Pillow: gli sprite del repository sono scritti con **Pillow 12.3**. Con un'altra versione `--controlla` può segnalare differenze a pixel uguali: in quel caso si rigenera e si controlla la tavola prima del commit. I workflow di build e di release installano Pillow 12.3.0 ed eseguono i test e `genera.py --controlla --pixel` (sul runner Linux si confrontano misura e pixel, non i byte). Un test controlla anche che ogni PNG nella radice di `godot/assets/` esca dal generatore.

Un asset libero di terzi, se mai servisse, va citato in `godot/assets/ATTRIBUTION.md`.

## Per approfondire

- [tools/art/README.md](https://github.com/Juan0177/tIndustry/blob/main/tools/art/README.md)
- [ADR 0004 — Arte generata da script](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0004-arte-generata-da-script.md)
- [docs/ARCHITETTURA.md, Grafica e sprite](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md#grafica-e-sprite)
- [Contenuti e dati](Contenuti-e-dati) · [Interfaccia e lingue](Interfaccia-e-lingue) · [Mappe e terreno](Mappe-e-terreno)
