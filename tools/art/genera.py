"""Generate the game sprites into godot/assets/ and the review sheet into .dev/arte/.

    python tools/art/genera.py                 # every registered sprite + review sheets
    python tools/art/genera.py --solo miner    # only some sprites
    python tools/art/genera.py --controlla     # write nothing; exit 1 if godot/assets differs

The review sheets are .dev/arte/tavola.png (old next to new), .dev/arte/tavola-terreno.png when
terrain sprites are written (one composed window per biome) and .dev/arte/tavola-ui.png when the
interface icons or the font are written (font samples and icons on a panel of the interface).
A full run (no --solo) also writes the interface font (carattere.py) into godot/assets/ui/.
Only the sprites registered in blocchi/ are written; every other PNG in
godot/assets/ is left alone. Output is deterministic: same code, same bytes.
"""
import argparse
import datetime
import os
import shutil
import sys
import tempfile

ART_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ART_DIR, "..", ".."))
if ART_DIR not in sys.path:
    sys.path.insert(0, ART_DIR)

from PIL import Image  # noqa: E402

import carattere  # noqa: E402
from blocchi import SPRITE  # noqa: E402
from blocchi.terreno import NOMI as TERRENO  # noqa: E402
from tavola import tavola, tavola_terreno, tavola_ui  # noqa: E402


def _assoluto(path):
    return path if os.path.isabs(path) else os.path.join(REPO, path)


def _nomi(solo):
    if not solo:
        return sorted(SPRITE)
    ignoti = sorted(set(solo) - set(SPRITE))
    if ignoti:
        raise SystemExit(f"sprite non registrati: {', '.join(ignoti)}")
    return sorted(set(solo))


def salva(im, path):
    """Save without variable metadata so the bytes only depend on the pixels."""
    im.info.clear()
    im.save(path, format="PNG", optimize=False)


def _scrivi(nomi, cartella, font=False):
    """Write the sprites (names may hold a subfolder, as "ui/ui-menu") and, with `font`, the font files."""
    for nome in nomi:
        path = os.path.join(cartella, nome + ".png")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        salva(SPRITE[nome](), path)
    if font:
        for rel, dati in carattere.file_carattere().items():
            path = os.path.join(cartella, rel)
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, "wb") as f:
                f.write(dati)


def main(out_dir="godot/assets", tavola_dir=".dev/arte", solo=None):
    """Write the sprites and the review sheet; return the names written, sorted."""
    out_dir, tavola_dir = _assoluto(out_dir), _assoluto(tavola_dir)
    nomi = _nomi(solo)
    with tempfile.TemporaryDirectory() as vecchi:
        for nome in nomi:
            path = os.path.join(out_dir, nome + ".png")
            if os.path.exists(path):
                os.makedirs(os.path.dirname(os.path.join(vecchi, nome)), exist_ok=True)
                shutil.copyfile(path, os.path.join(vecchi, nome + ".png"))
        _scrivi(nomi, out_dir, font=not solo)
        if nomi:
            os.makedirs(tavola_dir, exist_ok=True)
            sheet = tavola(nomi, vecchi, out_dir)
            oggi = datetime.date.today().isoformat()
            salva(sheet, os.path.join(tavola_dir, f"tavola-{oggi}.png"))
            salva(sheet, os.path.join(tavola_dir, "tavola.png"))
        if set(nomi) & set(TERRENO):
            salva(tavola_terreno(lambda nome: SPRITE[nome]()), os.path.join(tavola_dir, "tavola-terreno.png"))
        if not solo or any(n.startswith("ui/") for n in nomi):
            os.makedirs(tavola_dir, exist_ok=True)
            salva(tavola_ui(lambda nome: SPRITE[nome]()), os.path.join(tavola_dir, "tavola-ui.png"))
    return nomi


def _pixel(path):
    with Image.open(path) as im:
        return im.size, im.convert("RGBA").tobytes()


def _file_attesi(solo):
    """Files a run writes into godot/assets, relative paths: the sprites, plus the font on a full run."""
    return [n + ".png" for n in _nomi(solo)] + ([] if solo else sorted(carattere.file_carattere()))


def controlla(out_dir="godot/assets", solo=None, pixel=False):
    """Files (relative paths) in out_dir that are missing or differ from a fresh generation: same bytes,
    or with `pixel` PNGs of the same size and RGBA pixels (the PNG encoder may differ between platforms
    or Pillow builds); the font descriptors (.fnt) are always compared byte by byte."""
    out_dir = _assoluto(out_dir)
    diversi = []
    with tempfile.TemporaryDirectory() as nuovi:
        _scrivi(_nomi(solo), nuovi, font=not solo)
        for rel in _file_attesi(solo):
            attuale = os.path.join(out_dir, rel)
            if not os.path.exists(attuale):
                diversi.append(rel)
                continue
            nuovo = os.path.join(nuovi, rel)
            if pixel and rel.endswith(".png"):
                if _pixel(attuale) != _pixel(nuovo):
                    diversi.append(rel)
                continue
            with open(attuale, "rb") as a, open(nuovo, "rb") as b:
                if a.read() != b.read():
                    diversi.append(rel)
    return diversi


def _cli(argv):
    parser = argparse.ArgumentParser(description="Genera gli sprite di tIndustry.")
    parser.add_argument("--solo", nargs="+", metavar="NOME", help="genera solo questi sprite")
    parser.add_argument("--controlla", action="store_true",
                        help="non scrive niente; esce con 1 se godot/assets non è aggiornata")
    parser.add_argument("--pixel", action="store_true",
                        help="con --controlla confronta misura e pixel invece dei byte (CI, altri sistemi)")
    args = parser.parse_args(argv)
    if args.controlla:
        diversi = controlla(solo=args.solo, pixel=args.pixel)
        for rel in diversi:
            print(f"diverso o mancante: {rel}")
        print(f"{len(_file_attesi(args.solo)) - len(diversi)} file aggiornati, {len(diversi)} da rigenerare")
        return 1 if diversi else 0
    nomi = main(solo=args.solo)
    print(f"{len(nomi)} sprite scritti in godot/assets"
          + ("" if args.solo else " con il carattere in godot/assets/ui")
          + (", tavole in .dev/arte/" if nomi else ""))
    return 0


if __name__ == "__main__":
    sys.exit(_cli(sys.argv[1:]))
