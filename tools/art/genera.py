"""Generate the game sprites into godot/assets/ and the review sheet into .dev/arte/.

    python tools/art/genera.py                 # every registered sprite + review sheet
    python tools/art/genera.py --solo miner    # only some sprites
    python tools/art/genera.py --controlla     # write nothing; exit 1 if godot/assets differs

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

from blocchi import SPRITE  # noqa: E402
from tavola import tavola  # noqa: E402


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


def _scrivi(nomi, cartella):
    os.makedirs(cartella, exist_ok=True)
    for nome in nomi:
        salva(SPRITE[nome](), os.path.join(cartella, nome + ".png"))


def main(out_dir="godot/assets", tavola_dir=".dev/arte", solo=None):
    """Write the sprites and the review sheet; return the names written, sorted."""
    out_dir, tavola_dir = _assoluto(out_dir), _assoluto(tavola_dir)
    nomi = _nomi(solo)
    with tempfile.TemporaryDirectory() as vecchi:
        for nome in nomi:
            path = os.path.join(out_dir, nome + ".png")
            if os.path.exists(path):
                shutil.copyfile(path, os.path.join(vecchi, nome + ".png"))
        _scrivi(nomi, out_dir)
        if nomi:
            os.makedirs(tavola_dir, exist_ok=True)
            sheet = tavola(nomi, vecchi, out_dir)
            oggi = datetime.date.today().isoformat()
            salva(sheet, os.path.join(tavola_dir, f"tavola-{oggi}.png"))
            salva(sheet, os.path.join(tavola_dir, "tavola.png"))
    return nomi


def controlla(out_dir="godot/assets", solo=None):
    """Names whose PNG in out_dir is missing or differs from a fresh generation."""
    out_dir = _assoluto(out_dir)
    nomi = _nomi(solo)
    diversi = []
    with tempfile.TemporaryDirectory() as nuovi:
        _scrivi(nomi, nuovi)
        for nome in nomi:
            attuale = os.path.join(out_dir, nome + ".png")
            if not os.path.exists(attuale):
                diversi.append(nome)
                continue
            with open(attuale, "rb") as a, open(os.path.join(nuovi, nome + ".png"), "rb") as b:
                if a.read() != b.read():
                    diversi.append(nome)
    return diversi


def _cli(argv):
    parser = argparse.ArgumentParser(description="Genera gli sprite di tIndustry.")
    parser.add_argument("--solo", nargs="+", metavar="NOME", help="genera solo questi sprite")
    parser.add_argument("--controlla", action="store_true",
                        help="non scrive niente; esce con 1 se godot/assets non è aggiornata")
    args = parser.parse_args(argv)
    if args.controlla:
        diversi = controlla(solo=args.solo)
        for nome in diversi:
            print(f"diverso o mancante: {nome}.png")
        print(f"{len(_nomi(args.solo)) - len(diversi)} sprite aggiornati, {len(diversi)} da rigenerare")
        return 1 if diversi else 0
    nomi = main(solo=args.solo)
    print(f"{len(nomi)} sprite scritti in godot/assets"
          + (", tavola in .dev/arte/tavola.png" if nomi else ""))
    return 0


if __name__ == "__main__":
    sys.exit(_cli(sys.argv[1:]))
