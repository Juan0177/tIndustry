# Asset attribution

## Icone e carattere

Edifici, logistica, Core, droni e oggetti (`conveyor-*`, `junction`, `splitter`, `sorter`, `miner*`, `smelter*`, `assembler*`, `extractor`, `generator*`, `power-node*`, `core-*`, `drone-*`, le 12 icone degli oggetti `<id>.png` e i loro sprite per i nastri `<id>-belt.png`) e terreno (`terreno-*`, `parete-*`, `giacimento-*`, `deco-*`, `acqua-*-anim`) sono pixel-art originale generata da `tools/art/genera.py`: nessuna attribuzione.

Anche le icone dell'interfaccia `ui/ui-*.png` (16 e 32 px) e il carattere pixel `ui/carattere-testo.fnt`/`.png` e `ui/carattere-titoli.fnt`/`.png` sono originali, generati da `tools/art/genera.py` (`blocchi/interfaccia.py`, `carattere.py`): nessuna attribuzione.

Nel gioco non restano asset di terzi. Un asset libero aggiunto in futuro (per esempio un carattere OFL) va elencato qui con autore, licenza e fonte.

## Layout

Gli sprite stanno in `godot/assets/` (cartella piatta, unica copia); quelli dell'interfaccia (icone `ui-*` e carattere) in `godot/assets/ui/`.
