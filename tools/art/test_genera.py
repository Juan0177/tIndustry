"""Tests for the sprite generator (stdlib unittest only).

Run from the repo root:  python -m unittest tools/art/test_genera.py
"""
import csv
import io
import json
import math
import os
import sys
import tempfile
import unittest

from PIL import Image, ImageChops

ART_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ART_DIR, "..", ".."))
if ART_DIR not in sys.path:
    sys.path.insert(0, ART_DIR)

import carattere  # noqa: E402
import forme  # noqa: E402
import genera  # noqa: E402
from blocchi import SPRITE, sprite  # noqa: E402
from palette import ACCENTO, BAGLIORE, CELLA, METALLO, NASTRO, NUCLEO, OUT, RAME, RAMPE  # noqa: E402

T = 32  # pixels per block cell
BELT_ITEM = 10  # side of the small item sprite drawn on belts
NASTRI = ("conveyor-basic", "conveyor-fast", "conveyor-express", "conveyor-armored")  # belt tiers with scrolling strips

# Block footprints in cells, kept by hand: update it together with Factory.FootprintSize
# (src/TIndustry.Shared/Factory.cs) when a block changes size. The bridge is two 1x1 ends.
CELLE = {
    "miner": 2, "miner-advanced": 2, "smelter": 2, "assembler": 2, "generator": 2,
    "power-node-t2": 2,
    "extractor": 1, "power-node": 1, "lamp": 1, "conveyor-basic": 1, "conveyor-fast": 1,
    "conveyor-express": 1, "conveyor-armored": 1, "junction": 1, "splitter": 1, "sorter": 1, "conveyor-bridge": 1,
    "core-1": 2, "core-2": 3, "core-3": 4, "core-4": 5,
}
CORE_TIER = 4
CORE_LUCE_FOTOGRAMMI = 4
# Drone tiers seen from above, nose to the East: square sprites of this side (alpha, beta, gamma).
DRONI = {"drone-1": 24, "drone-2": 28, "drone-3": 32}
PARTI_DRONE = ("elmetto",)  # drawn on top of a drone, same square: "<drone>-<part>"
# Terrain (G2b): floor variants, deposit variants, dual-grid wall pieces (corner bits NO=1, NE=2,
# SE=4, SO=8), edge pieces in strip order, and the colour step allowed across a variant seam.
VARIANTI = 4
GIACIMENTI = 3
NO, NE, SE, SO = 1, 2, 4, 8
BORDI = ("N", "E", "S", "O", "NE", "SE", "SO", "NO")
SOGLIA_CUCITURA = 8.0
# Particle sprites (G3): side range in px, most variants in a strip, and the names the client expects.
FX_LATO = (8, 16)
FX_VARIANTI = 8
FX = ("fx-fumo", "fx-scintilla", "fx-polvere", "fx-vapore", "fx-fiocco", "fx-granello", "fx-foglia", "fx-lampo",
      "fx-lucciola")
# Light texture of the night (G3 3/4): one square, PointLight2D scales it.
LUCE = ("luce", 128)
# Interface icons (G4): "ui/ui-<nome>" 32x32 and "ui/ui-<nome>-16" 16x16, for these names.
ICONE_UI = ("cat-strumenti", "cat-logistica", "cat-produzione", "cat-potenza", "menu", "ricerca", "mercato",
            "campagna", "lucchetto", "energia", "impostazioni", "salvataggi", "lingua")
# Other interface sprites (G4 5/6): the theme's check boxes and the Home logo, name -> size.
ALTRI_UI = {"ui/ui-spunta-si": (20, 20), "ui/ui-spunta-no": (20, 20), "ui/ui-spunta-si-spenta": (20, 20),
            "ui/ui-spunta-no-spenta": (20, 20), "ui/ui-logo": (54, 16)}
# Interface font (G4): required glyphs and the exported files in godot/assets/ui/.
INSIEME_CARATTERE = "".join(chr(c) for c in range(32, 127)) + "àèéìíòóùúÀÈÉÌÒÙ«»…·€×→←↑↓↔↖—–✓°≥▲▼"
FONT_UI = ("carattere-testo", "carattere-titoli")
BS = chr(92)  # backslash, for the C# sources built in the tests
CONTROLLO = (chr(9), chr(10), chr(13))  # tab and line breaks: layout, not glyphs


def _letterali_cs(sorgente):
    """Characters inside the string and char literals of a C# source, comments skipped.

    Handles regular ("..." with backslash escapes, unicode escapes decoded), verbatim (@"..." with ""),
    interpolated ($"...", $@"...", the holes are kept: they are code, ASCII in practice) and raw
    (triple-quoted) literals, and char literals ('x').
    """
    out = []
    i, n = 0, len(sorgente)
    while i < n:
        c = sorgente[i]
        if sorgente.startswith("//", i):
            i = sorgente.find(chr(10), i)
            i = n if i < 0 else i
        elif sorgente.startswith("/*", i):
            j = sorgente.find("*/", i + 2)
            i = n if j < 0 else j + 2
        elif c in "$@" or c == '"':
            j = i
            while j < n and sorgente[j] in "$@":
                j += 1
            if j >= n or sorgente[j] != '"':
                i = j + 1 if j == i else j
                continue
            prefisso = sorgente[i:j]
            if sorgente.startswith('"""', j):
                k = j
                while k < n and sorgente[k] == '"':
                    k += 1
                chiusura = sorgente[j:k]
                fine = sorgente.find(chiusura, k)
                fine = n if fine < 0 else fine
                out.append(sorgente[k:fine])
                i = fine + len(chiusura)
            elif "@" in prefisso:
                k = j + 1
                while k < n:
                    if sorgente[k] == '"':
                        if sorgente.startswith('""', k):
                            out.append('"')
                            k += 2
                            continue
                        break
                    out.append(sorgente[k])
                    k += 1
                i = k + 1
            else:
                k = j + 1
                while k < n and sorgente[k] != '"' and sorgente[k] != chr(10):
                    if sorgente[k] == "\\":
                        if sorgente[k + 1] == "u":
                            out.append(chr(int(sorgente[k + 2:k + 6], 16)))
                            k += 6
                            continue
                        k += 2
                        continue
                    out.append(sorgente[k])
                    k += 1
                i = k + 1
        elif c == "'":
            k = i + 1
            if k < n and sorgente[k] == "\\":
                k += 2
            else:
                out.append(sorgente[k] if k < n else "")
                k += 1
            j = sorgente.find("'", k)
            i = n if j < 0 else j + 1
        else:
            i += 1
    return "".join(out)


def _stringhe_json(valore):
    """Every string value (not the keys) of a parsed JSON document, joined."""
    if isinstance(valore, str):
        return valore
    if isinstance(valore, dict):
        return "".join(_stringhe_json(v) for v in valore.values())
    if isinstance(valore, list):
        return "".join(_stringhe_json(v) for v in valore)
    return ""


def _testi_del_gioco():
    """
    {character: first file using it} over data/*.json, both columns of godot/lingue/testi.csv (Italian and
    English) and the C# literals of the client and Shared.
    """
    usi = {}
    with open(os.path.join(REPO, "godot", "lingue", "testi.csv"), encoding="utf-8", newline="") as fh:
        for riga in list(csv.reader(fh))[1:]:
            for ch in "".join(riga[1:]):
                usi.setdefault(ch, "godot/lingue/testi.csv")
    cartella = os.path.join(REPO, "data")
    for f in sorted(os.listdir(cartella)):
        if f.endswith(".json"):
            with open(os.path.join(cartella, f), encoding="utf-8") as fh:
                for ch in _stringhe_json(json.load(fh)):
                    usi.setdefault(ch, f"data/{f}")
    for radice in (os.path.join(REPO, "godot", "scripts"), os.path.join(REPO, "src", "TIndustry.Shared")):
        for cart, sotto, file in os.walk(radice):
            sotto[:] = sorted(d for d in sotto if d not in ("bin", "obj"))
            for f in sorted(file):
                if f.endswith(".cs"):
                    path = os.path.join(cart, f)
                    with open(path, encoding="utf-8-sig") as fh:
                        for ch in _letterali_cs(fh.read()):
                            usi.setdefault(ch, os.path.relpath(path, REPO).replace(os.sep, "/"))
    return usi


def _content():
    with open(os.path.join(REPO, "data", "content.json"), encoding="utf-8") as f:
        return json.load(f)


def _item_ids():
    return {m["itemId"] for m in _content()["market"]} | {"money"}


def fotogrammi_di(striscia):
    """Square frames of an "-anim" strip (side = strip height)."""
    h = striscia.height
    return [striscia.crop((i * h, 0, (i + 1) * h, h)) for i in range(striscia.width // h)]


def _righe(im, y0, y1):
    """Pixels of rows y0..y1 (inclusive)."""
    return im.crop((0, y0, im.width, y1 + 1)).tobytes()


def _minerali():
    """Ore item ids (one deposit kind each), from the item sprite table."""
    from blocchi.oggetti import OGGETTI
    return sorted(i for i, (tipo, _, _) in OGGETTI.items() if tipo == "minerale")


def _biomi_terra():
    """Land biomes: those with a wall (water floors are shared by every biome)."""
    return sorted({w["biome"] for w in _content()["terrain"]["walls"]})


def _misure_terreno():
    """Terrain sprite name -> expected size (G2b global constraints)."""
    terrain = _content()["terrain"]
    attese = {"acqua-bassa-anim": (4 * T, T), "acqua-profonda-anim": (4 * T, T)}
    for f in terrain["floors"]:
        attese |= {f"terreno-{f['id']}-{n}": (T, T) for n in range(VARIANTI)}
        attese[f"terreno-{f['id']}-bordo"] = (8 * T, T)
    attese |= {f"parete-{w['id']}": (16 * T, T) for w in terrain["walls"]}
    attese |= {f"giacimento-{m}-{n}": (T, T) for m in _minerali() for n in range(GIACIMENTI)}
    return attese


def errore_misura(nome, im):
    """Size problem of one generated sprite, or None when it is fine.

    Every name must be an item, a drone tier, a block (or block part) of CELLE or a terrain
    sprite: an unknown name is an error, never a silent pass."""
    w, h = im.size
    terreno = _misure_terreno()
    if nome in terreno:
        return None if (w, h) == terreno[nome] else f"{nome}: {w}x{h}, atteso {terreno[nome][0]}x{terreno[nome][1]}"
    if nome.startswith("deco-") and nome[len("deco-"):].rsplit("-", 1)[0] in _biomi_terra():
        return None if (w, h) == (T, T) else f"{nome}: {w}x{h}, atteso {T}x{T}"
    if nome in ALTRI_UI:
        return None if (w, h) == ALTRI_UI[nome] else f"{nome}: {w}x{h}, atteso {ALTRI_UI[nome][0]}x{ALTRI_UI[nome][1]}"
    if nome.startswith("ui/"):
        piccola = nome.endswith("-16")
        icona = nome[len("ui/ui-"): -len("-16") if piccola else None]
        if not nome.startswith("ui/ui-") or icona not in ICONE_UI:
            return f"{nome}: icona dell'interfaccia sconosciuta"
        lato = 16 if piccola else 32
        return None if (w, h) == (lato, lato) else f"{nome}: {w}x{h}, atteso {lato}x{lato}"
    if nome == LUCE[0]:
        return None if (w, h) == (LUCE[1], LUCE[1]) else f"{nome}: {w}x{h}, atteso {LUCE[1]}x{LUCE[1]}"
    if nome.startswith("fx-"):
        # Particle (G3): one square or a strip of square variants, side 8-16 px.
        ok = FX_LATO[0] <= h <= FX_LATO[1] and w % h == 0 and 1 <= w // h <= FX_VARIANTI
        return None if ok else f"{nome}: {w}x{h}, atteso un quadrato o una striscia di quadrati da 8-16 px"
    anim = nome.endswith("-anim")
    base = nome[: -len("-anim")] if anim else nome
    if anim and (h <= 0 or w % h != 0):
        # Sprites.Frames cuts strips into squares: frame side = strip height.
        return f"{nome}: larghezza {w} non multipla dell'altezza {h} (fotogrammi quadrati)"
    if base.endswith("-belt") and base[: -len("-belt")] in _item_ids():
        return None if (w, h) == (BELT_ITEM, BELT_ITEM) else f"{nome}: {w}x{h}, atteso {BELT_ITEM}x{BELT_ITEM}"
    if base in _item_ids():
        return None if (w, h) == (T, T) else f"{nome}: {w}x{h}, atteso {T}x{T}"
    drone = next((d for d in DRONI if base == d or base in (f"{d}-{p}" for p in PARTI_DRONE)), None)
    if drone:
        lato = DRONI[drone]
        return None if (w, h) == (lato, lato) else f"{nome}: {w}x{h}, atteso {lato}x{lato}"
    # Block or block part: "<id>" or "<id>-<parte>" with a known footprint.
    block = next((b for b in sorted(CELLE, key=len, reverse=True)
                  if base == b or base.startswith(b + "-")), None)
    if block is None:
        return f"{nome}: nome sconosciuto (né oggetto, né drone, né blocco di CELLE)"
    side = T * CELLE[block]
    if base.endswith("-span"):
        # Repeatable span texture (bridge): tiles a cell evenly, never taller than the block.
        ok = w > 0 and side % w == 0 and 0 < h <= side
        return None if ok else f"{nome}: {w}x{h}, attesa larghezza divisore di {side} e altezza <= {side}"
    frame_w = h if anim else w
    if h != side or frame_w != side:
        return f"{nome}: fotogramma {frame_w}x{h}, atteso {side}x{side}"
    return None


# Test-only sprites: exercise every shape and an animation strip.
def _prova():
    im = forme.nuova(2 * T, 2 * T)
    forme.pannello(im, 0, 0, 2 * T - 1, 2 * T - 1, METALLO)
    forme.griglia(im, 6, 6, 25, 25, METALLO, 3)
    forme.tubo(im, 30, 4, 60, 9, RAME)
    forme.ingranaggio(im, 44, 44, 12, 8, METALLO, 0.3)
    forme.cerchio(im, 16, 46, 7, RAME)
    forme.cerchio(im, 16, 46, 3, ACCENTO, pieno=False)
    forme.bagliore(im, 44, 20, 6, BAGLIORE)
    forme.bullone(im, 3, 3, METALLO)
    forme.rett(im, 30, 30, 33, 33, ACCENTO[2])
    forme.pixel(im, 1, 62, BAGLIORE[4])
    return forme.componi(forme.ombra(im, 90), im)


def _prova_anim():
    fotogrammi = []
    for k in range(4):
        im = forme.nuova(T, T)
        forme.ingranaggio(im, 16, 16, 12, 6, METALLO, k * 0.26)
        fotogrammi.append(im)
    return forme.striscia(fotogrammi)


TEST_SPRITES = {"miner-prova": _prova, "conveyor-basic-prova-anim": _prova_anim}


class TestGenera(unittest.TestCase):
    def setUp(self):
        self._saved = dict(SPRITE)
        for nome, fn in TEST_SPRITES.items():
            sprite(nome)(fn)

    def tearDown(self):
        SPRITE.clear()
        SPRITE.update(self._saved)

    def _genera(self, cartella):
        out = os.path.join(cartella, "assets")
        os.makedirs(out)
        nomi = genera.main(out_dir=out, tavola_dir=os.path.join(cartella, "arte"))
        return out, nomi

    def test_deterministico(self):
        with tempfile.TemporaryDirectory() as a, tempfile.TemporaryDirectory() as b:
            out_a, nomi_a = self._genera(a)
            out_b, nomi_b = self._genera(b)
            self.assertEqual(nomi_a, nomi_b)
            self.assertEqual(nomi_a, sorted(SPRITE))
            for nome in nomi_a:
                with open(os.path.join(out_a, nome + ".png"), "rb") as fa, \
                        open(os.path.join(out_b, nome + ".png"), "rb") as fb:
                    self.assertEqual(fa.read(), fb.read(), nome)
            for rel in carattere.file_carattere():
                with open(os.path.join(out_a, rel), "rb") as fa, open(os.path.join(out_b, rel), "rb") as fb:
                    self.assertEqual(fa.read(), fb.read(), rel)
            self.assertTrue(os.path.exists(os.path.join(a, "arte", "tavola-ui.png")))

    def test_misure(self):
        errori = [e for nome in sorted(SPRITE) if (e := errore_misura(nome, SPRITE[nome]()))]
        self.assertEqual([], errori)

    def test_misure_rileva_errori(self):
        self.assertIsNotNone(errore_misura("miner", forme.nuova(T, T)))
        self.assertIsNotNone(errore_misura("iron-ore-belt", forme.nuova(T, T)))
        self.assertIsNotNone(errore_misura("smelter-anim", forme.nuova(5 * T, 2 * T + 1)))
        self.assertIsNone(errore_misura("smelter-anim", forme.nuova(6 * T, 2 * T)))
        self.assertIsNotNone(errore_misura("core-3", forme.nuova(3 * T, 3 * T)))
        self.assertIsNotNone(errore_misura("core-4-light-anim", forme.nuova(4 * 4 * T, 4 * T)))
        self.assertIsNone(errore_misura("core-4-light-anim", forme.nuova(4 * 5 * T, 5 * T)))
        self.assertIsNotNone(errore_misura("drone-2", forme.nuova(24, 24)))
        self.assertIsNone(errore_misura("drone-2", forme.nuova(28, 28)))
        self.assertIsNotNone(errore_misura("drone-2-anim", forme.nuova(3 * 28 + 1, 28)))
        self.assertIsNotNone(errore_misura("core-9", forme.nuova(T, T)))
        self.assertIsNotNone(errore_misura("sconosciuto", forme.nuova(T, T)))
        self.assertIsNotNone(errore_misura("parete-stone-wall", forme.nuova(T, T)))
        self.assertIsNone(errore_misura("parete-stone-wall", forme.nuova(16 * T, T)))
        self.assertIsNotNone(errore_misura("terreno-sand-bordo", forme.nuova(4 * T, T)))
        self.assertIsNotNone(errore_misura("terreno-lava-0", forme.nuova(T, T)))
        self.assertIsNotNone(errore_misura("deco-vulcano-0", forme.nuova(T, T)))
        self.assertIsNone(errore_misura("fx-fumo", forme.nuova(36, 12)))
        self.assertIsNone(errore_misura("fx-lampo", forme.nuova(16, 16)))
        self.assertIsNotNone(errore_misura("fx-fumo", forme.nuova(30, 12)))
        self.assertIsNotNone(errore_misura("fx-fumo", forme.nuova(T, T)))
        self.assertIsNotNone(errore_misura("fx-fumo", forme.nuova(6, 6)))
        self.assertIsNone(errore_misura("ui/ui-menu", forme.nuova(32, 32)))
        self.assertIsNone(errore_misura("ui/ui-menu-16", forme.nuova(16, 16)))
        self.assertIsNotNone(errore_misura("ui/ui-menu", forme.nuova(16, 16)))
        self.assertIsNotNone(errore_misura("ui/ui-menu-16", forme.nuova(32, 32)))
        self.assertIsNotNone(errore_misura("ui/ui-sconosciuta", forme.nuova(32, 32)))
        self.assertIsNotNone(errore_misura("ui/menu", forme.nuova(32, 32)))

    def test_copertura_tier(self):
        # The four Core tiers with their light strip and the three drones.
        attesi = {f"core-{n}" for n in range(1, CORE_TIER + 1)}
        attesi |= {f"core-{n}-light-anim" for n in range(1, CORE_TIER + 1)} | set(DRONI)
        self.assertEqual([], sorted(attesi - set(SPRITE)))

    def test_core(self):
        lampade = []
        for n in range(1, CORE_TIER + 1):
            with self.subTest(tier=n):
                core = SPRITE[f"core-{n}"]()
                fotogrammi = fotogrammi_di(SPRITE[f"core-{n}-light-anim"]())
                self.assertEqual(CORE_LUCE_FOTOGRAMMI, len(fotogrammi))
                # The core body is opaque all over its footprint (it stands on its plate).
                self.assertEqual(255, core.getpixel((core.width // 2, 2))[3])
                for f in fotogrammi:
                    # Light frames are opaque discs over the nucleus: stacked on the icon they replace
                    # it, they never add to it (no double glow).
                    self.assertEqual({0, 255}, set(f.getchannel("A").tobytes()))
                    sopra = forme.componi(core, f)
                    self.assertEqual(sopra.getbbox(), core.getbbox())
                self.assertEqual(core.tobytes(), forme.componi(core, fotogrammi[0]).tobytes())
                self.assertNotEqual(fotogrammi[0].tobytes(), fotogrammi[2].tobytes(), "la luce non cambia")
                px = core.tobytes()
                lampade.append(sum(1 for i in range(0, len(px), 4) if tuple(px[i:i + 3]) == NUCLEO[-1]))
        # Each tier is grander: more lit cyan highlights than the one below.
        self.assertEqual(sorted(lampade), lampade)
        self.assertEqual(len(set(lampade)), len(lampade))

    def test_droni_muso_a_est(self):
        # The half is drawn nose-up: its first row must end on the East edge, not the West one.
        from blocchi import drone
        im = drone.disegna(4, "O. .. .. ..").getchannel("A")
        self.assertEqual([255, 255], [im.getpixel((3, 0)), im.getpixel((3, 3))])
        self.assertEqual(0, max(im.getpixel((0, y)) for y in range(4)))
        with self.assertRaises(ValueError):
            drone.disegna(6, "O. ..")

    def test_droni(self):
        sagome = []
        for nome, lato in DRONI.items():
            with self.subTest(drone=nome):
                im = SPRITE[nome]()
                alfa = im.getchannel("A")
                # Mirror-symmetric across the flight axis (West-East) and centred on it both ways,
                # so the client turns it about its middle.
                self.assertEqual(alfa.tobytes(), alfa.transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes())
                x0, y0, x1, y1 = alfa.getbbox()
                self.assertLessEqual(abs(x0 + x1 - lato), 1, f"{nome}: non centrato lungo il volo")
                # A glowing team cell sits on the axis in the middle.
                self.assertIn(im.getpixel((lato // 2, lato // 2))[:3], CELLA[1:], f"{nome}: cella non al centro")
                self.assertEqual((0, 0, 0, 0), im.getpixel((0, 0)))
                sagome.append(alfa.resize((32, 32)).tobytes())
        self.assertEqual(len(set(sagome)), len(sagome))

    def test_elmetto(self):
        # The helmet of drone-1: same square, symmetric across the flight axis, army green with an outline,
        # sitting on the hull (its middle is opaque, its corners empty).
        from palette import MUSCHIO
        im = SPRITE["drone-1-elmetto"]()
        self.assertEqual((24, 24), im.size)
        alfa = im.getchannel("A")
        self.assertEqual(alfa.tobytes(), alfa.transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes())
        colori = {px[:3] for px in im.getdata() if px[3]}
        self.assertLessEqual(colori, set(MUSCHIO) | {OUT})
        self.assertGreaterEqual(len(colori & set(MUSCHIO)), 4)
        self.assertEqual(255, im.getpixel((12, 12))[3])
        self.assertEqual(0, im.getpixel((0, 0))[3])
        self.assertIsNotNone(errore_misura("drone-2-elmetto", forme.nuova(24, 24)))
        self.assertIsNotNone(errore_misura("drone-1-cappello", forme.nuova(24, 24)))

    def test_tavolozza(self):
        for nome, rampa in RAMPE.items():
            self.assertTrue(4 <= len(rampa) <= 6, nome)
            luma = [0.299 * r + 0.587 * g + 0.114 * b for r, g, b in rampa]
            self.assertEqual(sorted(luma), luma, f"{nome}: non va da scuro a chiaro")

    def test_strisce_cicliche(self):
        # Straight belt strips: 32 frames, the tread (rows 5-26) of each one is the previous one
        # moved 1 px along the flow (East), so frame 32 moved by 1 px is frame 1 again and the
        # loop has no seam; the rails (rows 0-4 and 27-31) stay still.
        for nastro in NASTRI:
            with self.subTest(nastro=nastro):
                self.assertIn(nastro + "-anim", SPRITE)
                fotogrammi = fotogrammi_di(SPRITE[nastro + "-anim"]())
                self.assertEqual(32, len(fotogrammi))
                for i, f in enumerate(fotogrammi):
                    dopo = fotogrammi[(i + 1) % len(fotogrammi)]
                    self.assertEqual(_righe(ImageChops.offset(f, 1, 0), 5, 26), _righe(dopo, 5, 26),
                                     f"{nastro}: fotogramma {i + 1} scorso di 1 px != fotogramma {(i + 1) % 32 + 1}")
                    for y0, y1 in ((0, 4), (27, 31)):
                        self.assertEqual(_righe(f, y0, y1), _righe(fotogrammi[0], y0, y1), f"{nastro}: binari mossi")

    def test_rinforzato_diverso_dal_veloce(self):
        # The armored belt reads apart from the fast one at a glance: inside the outline its rails (rows 1-3,
        # 28-30) share no pixel with the fast belt's, and they carry rivets (more tones).
        rinforzato, veloce = SPRITE["conveyor-armored"](), SPRITE["conveyor-fast"]()
        binari = [(x, y) for x in range(T) for y in (1, 2, 3, 28, 29, 30)]
        self.assertEqual(0, sum(rinforzato.getpixel(p) == veloce.getpixel(p) for p in binari))
        colori = {rinforzato.getpixel(p) for p in binari}
        self.assertGreater(len(colori), len({veloce.getpixel(p) for p in binari}))

    def test_curve_raccordate(self):
        # Corner (in from the West edge, out from the South edge): its West column is column 0
        # of the straight frame and its South row is column 31 of it (row across the belt =
        # 31 - x there), frame by frame, so corner and straights join without a seam.
        for nastro in NASTRI:
            with self.subTest(nastro=nastro):
                dritti = fotogrammi_di(SPRITE[nastro + "-anim"]())
                curve = fotogrammi_di(SPRITE[nastro + "-corner-anim"]())
                self.assertEqual(32, len(dritti))
                self.assertEqual(len(dritti), len(curve))
                for k, (d, c) in enumerate(zip(dritti, curve)):
                    ovest = [c.getpixel((0, y)) for y in range(T)]
                    self.assertEqual([d.getpixel((0, y)) for y in range(T)], ovest, f"{nastro} {k + 1}: lato Ovest")
                    sud = [c.getpixel((x, T - 1)) for x in range(T)]
                    self.assertEqual([d.getpixel((T - 1, T - 1 - x)) for x in range(T)], sud, f"{nastro} {k + 1}: lato Sud")

    def test_unioni_a_t_raccordate(self):
        # T merge (in from the North and South edges, out from the East edge) composed from two
        # corners, frame by frame: east of the back rail every pixel inside a corner's bend (row
        # across the belt >= 6 from its pivot, i.e. its tread inside the groove) is that corner's pixel: the bottom half the corner
        # turned to come in from the South, the top half its mirror. So the East edge joins the
        # straight belt (bottom half: column 31 of the straight frame), the input edges join the
        # side belts like a corner, and the West edge is a rail (no input behind).
        tread = {c[:3] for c in NASTRO}
        for nastro in NASTRI:
            with self.subTest(nastro=nastro):
                dritti = fotogrammi_di(SPRITE[nastro + "-anim"]())
                curve = fotogrammi_di(SPRITE[nastro + "-corner-anim"]())
                unioni = fotogrammi_di(SPRITE[nastro + "-t-anim"]())
                self.assertEqual(len(curve), len(unioni))
                for k, (d, c, u) in enumerate(zip(dritti, curve, unioni)):
                    self.assertEqual((T, T), u.size)
                    da_sud = c.transpose(Image.Transpose.ROTATE_90)
                    da_nord = da_sud.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
                    for y in range(T):
                        for x in range(5, T):
                            a = T - 1 - y if y >= T // 2 else y
                            if math.hypot(a, T - 1 - x) <= T - 6.5:
                                atteso = (da_sud if y >= T // 2 else da_nord).getpixel((x, y))
                                self.assertEqual(atteso, u.getpixel((x, y)), f"{nastro} {k + 1}: {x},{y}")
                    self.assertEqual([d.getpixel((T - 1, y)) for y in range(T // 2, T)],
                                     [u.getpixel((T - 1, y)) for y in range(T // 2, T)], f"{nastro} {k + 1}: Est")
                    ovest = {u.getpixel((x, y))[:3] for x in range(5) for y in range(T)}
                    self.assertFalse(ovest & tread, f"{nastro}: Ovest aperto")

    def test_unioni_laterali(self):
        # Fed from behind and by belts on both sides (BeltShape.JoinBoth): the T opened behind. Its West
        # columns 0-4 are the straight frame's (the belt behind runs in), the rest is the T's.
        for nastro in NASTRI:
            with self.subTest(nastro=nastro):
                dritti = fotogrammi_di(SPRITE[nastro + "-anim"]())
                unioni = fotogrammi_di(SPRITE[nastro + "-t-anim"]())
                lati = fotogrammi_di(SPRITE[f"{nastro}-join-both-anim"]())
                self.assertEqual(len(dritti), len(lati))
                for k, (d, u, j) in enumerate(zip(dritti, unioni, lati)):
                    for y in range(T):
                        for x in range(T):
                            atteso = (u if x >= 5 else d).getpixel((x, y))
                            self.assertEqual(atteso, j.getpixel((x, y)), f"{nastro} both {k + 1}: {x},{y}")

    def test_unioni_da_edificio(self):
        # Side joins with one fed side (belt or building) or with a building on either side are the junction
        # composition ("dot block") in the tier's colours, but a belt: the West and East edge columns (0-4, 27-31) of the East-West tread are the straight frame's
        # (ribs, no chevron: rows 6 / 25), so they scroll with it; a side without a building is the straight
        # frame's rail and groove, corners included.
        for nastro in NASTRI:
            with self.subTest(nastro=nastro):
                dritti = fotogrammi_di(SPRITE[nastro + "-anim"]())
                unioni = [("left", True, False), ("right", False, True)]
                if nastro != "conveyor-armored":  # never blends with a building: no building-both strip
                    unioni.append(("building-both", True, True))
                else:
                    self.assertNotIn(f"{nastro}-join-building-both-anim", SPRITE)
                for nome, nord, sud in unioni:
                    lati = fotogrammi_di(SPRITE[f"{nastro}-join-{nome}-anim"]())
                    self.assertEqual(len(dritti), len(lati))
                    for k, (d, j) in enumerate(zip(dritti, lati)):
                        for y in range(T):
                            chiuso = (y <= 5 and not nord) or (y >= T - 6 and not sud)
                            for x in range(T):
                                if chiuso:
                                    atteso = d.getpixel((x, y))
                                elif (x < 5 or x > T - 6) and 5 <= y <= T - 6:
                                    atteso = d.getpixel((x, y if y in (5, T - 6) else 6 if y < T // 2 else T - 7))
                                else:
                                    continue
                                self.assertEqual(atteso, j.getpixel((x, y)), f"{nastro} edificio {nome} {k + 1}: {x},{y}")
                        # The hub of the junction marks the merge.
                        self.assertEqual(SPRITE["junction-base"]().getpixel((15, 15)), j.getpixel((15, 15)))

    def test_basamento_sotto_i_blocchi(self):
        # Blocks that feed or take from belts stand on a rectangular plate: their footprint is opaque
        # everywhere (no ground in the cut corners), with the dark outline on the edge.
        for nome in ("miner", "miner-base", "miner-advanced", "miner-advanced-base", "assembler",
                     "assembler-base", "smelter", "extractor", "generator", "generator-base",
                     *(f"core-{n}" for n in range(1, CORE_TIER + 1))):
            with self.subTest(sprite=nome):
                im = SPRITE[nome]()
                self.assertEqual({255}, set(im.getchannel("A").tobytes()))
                self.assertEqual(OUT, im.getpixel((0, 0))[:3])

    def test_incrocio_a_pezzi(self):
        # The icon is the open base; the North rail piece is the straight belt's top rail (rows 0-4)
        # over the groove, transparent below, so on a side without connection the rail stays.
        self.assertEqual(SPRITE["junction-base"]().tobytes(), SPRITE["junction"]().tobytes())
        bordo = SPRITE["junction-bordo"]()
        dritto = fotogrammi_di(SPRITE["conveyor-basic-anim"]())[0]
        self.assertEqual((T, T), bordo.size)
        self.assertEqual(_righe(dritto, 0, 5), _righe(bordo, 0, 5))
        self.assertIsNone(bordo.crop((0, 6, T, T)).getbbox())

    def test_incrocio_senza_frecce(self):
        # Junction: two belts crossing. The treads (rows and columns 5-26) reach every edge and hold
        # only tread colours outside the small centre hub (no arrow glyphs); the corners are rails.
        im = SPRITE["junction"]()
        self.assertEqual((T, T), im.size)
        tread = {c[:3] for c in NASTRO}
        c = (T - 1) / 2
        for y in range(T):
            for x in range(T):
                colore = im.getpixel((x, y))
                self.assertEqual(255, colore[3], f"buco su {x},{y}")
                if abs(x - c) + abs(y - c) <= 4:
                    continue  # hub
                in_tread = 5 <= x <= 26 or 5 <= y <= 26
                self.assertEqual(in_tread, colore[:3] in tread, f"{x},{y}")

    def test_parti_produzione_e_potenza(self):
        # Layered blocks: base, animated part (frame count), composed icon = base + frame 1.
        strati = {
            "miner": ("miner-drill-anim", 8), "miner-advanced": ("miner-advanced-drill-anim", 8),
            "smelter": ("smelter-glow-anim", 4), "generator": ("generator-rotor-anim", 4),
        }
        for blocco, (anim, n) in strati.items():
            with self.subTest(blocco=blocco):
                fotogrammi = fotogrammi_di(SPRITE[anim]())
                self.assertEqual(n, len(fotogrammi))
                icona = forme.componi(SPRITE[blocco + "-base"](), fotogrammi[0])
                self.assertEqual(icona.tobytes(), SPRITE[blocco]().tobytes())
        for nome in ("miner-advanced-lamp", "assembler-base", "assembler-arm", "assembler",
                     "extractor", "power-node", "power-node-t2", "lamp-base", "lamp-glow"):
            self.assertIn(nome, SPRITE)
        # Lamp icon = base + lit dome; the dome alone is transparent outside the glass.
        lampada = forme.componi(SPRITE["lamp-base"](), SPRITE["lamp-glow"]())
        self.assertEqual(lampada.tobytes(), SPRITE["lamp"]().tobytes())
        self.assertEqual(0, SPRITE["lamp-glow"]().getpixel((0, 0))[3])
        # The drill bit is clearly smaller than its frame (not a fan filling the block).
        for anim in ("miner-drill-anim", "miner-advanced-drill-anim"):
            x0, y0, x1, y1 = fotogrammi_di(SPRITE[anim]())[0].getbbox()
            self.assertLessEqual(max(x1 - x0, y1 - y0), T, anim)
        # Extractor: empty dark well, 8 px across, in the centre for the filter dot: one colour
        # over the whole disc and exactly 8 px of it across the middle row and column.
        ex = SPRITE["extractor"]()
        pozzo = NASTRO[0] + (255,)
        disco = {ex.getpixel((x, y)) for y in range(T) for x in range(T)
                 if (x + 0.5 - T / 2) ** 2 + (y + 0.5 - T / 2) ** 2 <= 16}
        self.assertEqual({pozzo}, disco)
        self.assertEqual(8, sum(1 for x in range(T) if ex.getpixel((x, T // 2)) == pozzo))
        self.assertEqual(8, sum(1 for y in range(T) if ex.getpixel((T // 2, y)) == pozzo))

    def test_oggetti(self):
        # Every market item and money: HUD icon and belt sprite, opaque pixels only (no tint at
        # runtime, so no half-transparent wash), a closed outline and a look of its own.
        from blocchi.oggetti import BORDO_CHIARO, OGGETTI
        from palette import OUT
        self.assertEqual(_item_ids(), set(OGGETTI))
        for misura in ("", "-belt"):
            viste = []
            for item_id, (_, _, contorno) in OGGETTI.items():
                if misura and item_id in BORDO_CHIARO:
                    contorno = OUT  # dark outline, light rim inside (checked below)
                with self.subTest(sprite=item_id + misura):
                    im = SPRITE[item_id + misura]()
                    self.assertEqual({0, 255}, set(im.getchannel("A").tobytes()))
                    for y in range(im.height):
                        for x in range(im.width):
                            px = im.getpixel((x, y))
                            vicini = [im.getpixel((x + ax, y + ay))[3]
                                      if 0 <= x + ax < im.width and 0 <= y + ay < im.height else 0
                                      for ax, ay in ((1, 0), (-1, 0), (0, 1), (0, -1))]
                            if px[3] and 0 in vicini:
                                self.assertEqual(contorno, px[:3], f"{item_id}{misura}: ({x},{y}) fuori dal contorno")
                    viste.append(im.tobytes())
            self.assertEqual(len(set(viste)), len(viste))
        # Contrast on belts: a light rim right inside the dark outline.
        for item_id, colore in BORDO_CHIARO.items():
            with self.subTest(bordo=item_id):
                im = SPRITE[item_id + "-belt"]()
                bordo = [im.getpixel((x, y))[:3] for y in range(1, im.height - 1) for x in range(1, im.width - 1)
                         if im.getpixel((x, y))[3] and im.getpixel((x, y))[:3] != OUT
                         and OUT in [im.getpixel((x + ax, y + ay))[:3] for ax, ay in ((1, 0), (-1, 0), (0, 1), (0, -1))]]
                self.assertTrue(bordo)
                self.assertEqual({colore}, set(bordo))

    def test_copertura(self):
        # Every structure and item of content.json, every Core tier and every drone has a sprite.
        c = _content()
        attesi = {s["id"] for s in c["structures"]} | _item_ids()
        attesi |= {f"core-{n}" for n in range(1, CORE_TIER + 1)} | set(DRONI)
        reali = set(SPRITE) - set(TEST_SPRITES)
        self.assertEqual([], sorted(attesi - reali))

    def test_effetti(self):
        # Every particle sprite the client expects exists and none of its variants is empty.
        self.assertEqual([], sorted(set(FX) - set(SPRITE)))
        for nome in FX:
            im = SPRITE[nome]()
            for f in fotogrammi_di(im):
                self.assertIsNotNone(f.getbbox(), f"{nome}: variante vuota")

    def test_luce(self):
        # Night light: white, opaque-ish in the centre, fading to fully transparent at the edge and corners.
        im = SPRITE[LUCE[0]]()
        c = LUCE[1] // 2
        self.assertEqual((LUCE[1], LUCE[1]), im.size)
        self.assertEqual({(255, 255, 255)}, {im.getpixel((x, c))[:3] for x in range(LUCE[1]) if im.getpixel((x, c))[3]})
        self.assertGreater(im.getpixel((c, c))[3], 240)
        for x, y in ((0, 0), (0, c), (c, 0), (LUCE[1] - 1, LUCE[1] - 1)):
            self.assertEqual(0, im.getpixel((x, y))[3])
        alfa = [im.getpixel((x, c))[3] for x in range(c, LUCE[1])]
        self.assertEqual(sorted(alfa, reverse=True), alfa)

    def test_terreno_copertura(self):
        # Every floor has its variants and its edge strip, every wall its 16 dual-grid pieces,
        # every ore its deposit variants, every land biome at least 2 decorations, both waters
        # their animation; every floor and wall has its own ramp in palette.py.
        from palette import PARETI, SUOLI
        terrain = _content()["terrain"]
        attesi = set(_misure_terreno())
        self.assertEqual([], sorted(attesi - set(SPRITE)))
        for bioma in _biomi_terra():
            with self.subTest(bioma=bioma):
                self.assertGreaterEqual(sum(1 for n in SPRITE if n.startswith(f"deco-{bioma}-")), 2)
        self.assertEqual(sorted(f["id"] for f in terrain["floors"]), sorted(SUOLI))
        self.assertEqual(sorted(w["id"] for w in terrain["walls"]), sorted(PARETI))
        # Deposits and decorations are drawn over the floor: transparent around, the floor shows.
        for nome in sorted(n for n in SPRITE if n.startswith(("giacimento-", "deco-"))):
            with self.subTest(sprite=nome):
                alfa = SPRITE[nome]().getchannel("A").tobytes()
                pieni = sum(1 for a in alfa if a)
                self.assertGreater(pieni, 0)
                self.assertLessEqual(pieni, len(alfa) // 4, f"{nome}: copre troppo il suolo")

    def test_varianti_ripetibili(self):
        # A variant tiles with itself and with every other variant of its floor. Across each seam
        # (right edge next to left edge, bottom next to top): the two outer rings hold only the
        # noise tones 1-3 (no pebble, blade or glint cut by the edge), facing pixels differ at most
        # by the noise span, and the mean colour stays within a step.
        from palette import SUOLI

        def media(px):
            return [sum(p[i] for p in px) / len(px) for i in range(3)]

        def lati(im):
            rgb = im.convert("RGB")
            return {"destra": [rgb.getpixel((T - 1, y)) for y in range(T)],
                    "sinistra": [rgb.getpixel((0, y)) for y in range(T)],
                    "sotto": [rgb.getpixel((x, T - 1)) for x in range(T)],
                    "sopra": [rgb.getpixel((x, 0)) for x in range(T)]}

        def controlla_cucitura(nome, a, b, ampiezza):
            for lato_a, lato_b in (("destra", "sinistra"), ("sotto", "sopra")):
                pa, pb = lati(a)[lato_a], lati(b)[lato_b]
                scarto = max(abs(x - y) for x, y in zip(media(pa), media(pb)))
                self.assertLessEqual(scarto, SOGLIA_CUCITURA, f"{nome}: cucitura visibile ({scarto:.1f})")
                salto = max(abs(u - v) for p, q in zip(pa, pb) for u, v in zip(p, q))
                self.assertLessEqual(salto, ampiezza, f"{nome}: salto di colore sulla cucitura ({salto})")

        for f in _content()["terrain"]["floors"]:
            rampa = SUOLI[f["id"]]
            rumore = {tuple(rampa[k]) for k in (1, 2, 3)}
            ampiezza = max(abs(u - v) for u, v in zip(rampa[1], rampa[3]))
            varianti = [SPRITE[f"terreno-{f['id']}-{n}"]() for n in range(VARIANTI)]
            for n, a in enumerate(varianti):
                self.assertEqual({255}, set(a.getchannel("A").tobytes()), f"{f['id']}: suolo non opaco")
                anello = {a.getpixel((x, y))[:3] for y in range(T) for x in range(T)
                          if min(x, y, T - 1 - x, T - 1 - y) < 2}
                self.assertEqual(set(), anello - rumore, f"{f['id']}-{n}: dettagli tagliati dal bordo")
            self.assertEqual(VARIANTI, len({v.tobytes() for v in varianti}), f"{f['id']}: varianti uguali")
            for i, a in enumerate(varianti):
                for j, b in enumerate(varianti):
                    controlla_cucitura(f"{f['id']} {i}|{j}", a, b, ampiezza)
        # Water frames tile the same way and loop (frame 4 is followed by frame 1).
        from blocchi.terreno import ACQUA
        for suolo, nome in ACQUA.items():
            fotogrammi = fotogrammi_di(SPRITE[nome]())
            self.assertEqual(4, len(fotogrammi))
            self.assertEqual(4, len({fr.tobytes() for fr in fotogrammi}), f"{nome}: fotogrammi uguali")
            rampa = SUOLI[suolo]
            for fr in fotogrammi:
                controlla_cucitura(nome, fr, fr, max(abs(u - v) for u, v in zip(rampa[1], rampa[-1])))

    def test_parete_doppia_griglia(self):
        # 16 pieces indexed by the wall corners (NO=1, NE=2, SE=4, SO=8): 15 is full, 0 is empty,
        # a piece rotated a quarter turn clockwise is the piece of the rotated corners (opaque
        # pixels: the light stays top-left), and each edge strip (colours included) only depends
        # on its two corners, so neighbouring pieces always join.
        def ruota(i):  # corners after a clockwise quarter turn: NO->NE->SE->SO->NO
            return ((i << 1) | (i >> 3)) & 15

        def opachi(im):
            return Image.eval(im.getchannel("A"), lambda a: 255 if a == 255 else 0).tobytes()

        lati = {  # edge -> (box, its two corners)
            "N": ((0, 0, T, 1), NO | NE), "E": ((T - 1, 0, T, T), NE | SE),
            "S": ((0, T - 1, T, T), SO | SE), "O": ((0, 0, 1, T), NO | SO),
        }
        for w in _content()["terrain"]["walls"]:
            with self.subTest(parete=w["id"]):
                pezzi = fotogrammi_di(SPRITE[f"parete-{w['id']}"]())
                self.assertEqual(16, len(pezzi))
                self.assertEqual({255}, set(pezzi[15].getchannel("A").tobytes()))
                self.assertEqual({0}, set(pezzi[0].getchannel("A").tobytes()))
                for i in range(1, 15):
                    girato = pezzi[i].transpose(Image.Transpose.ROTATE_270)
                    self.assertEqual(opachi(girato), opachi(pezzi[ruota(i)]), f"pezzo {i} ruotato != pezzo {ruota(i)}")
                for lato, (box, angoli) in lati.items():
                    for k in (0, angoli & -angoli, angoli & (angoli - 1), angoli):
                        viste = {pezzi[i].crop(box).tobytes() for i in range(16) if i & angoli == k}
                        self.assertEqual(1, len(viste), f"lato {lato}: dipende da angoli lontani")

    def test_parete_margine_esatto(self):
        # The wall look reads the footprint up to MARGINE_PARETE px around the piece: there it must
        # not depend on the cells beyond the four corners (unknown to the piece).
        from blocchi.terreno import MARGINE_PARETE, _pieno
        m = MARGINE_PARETE
        for angoli in range(16):
            diversi = [(x, y) for y in range(-m, T + m) for x in range(-m, T + m)
                       if _pieno(angoli, x, y, esterni=False) != _pieno(angoli, x, y, esterni=True)]
            self.assertEqual([], diversi, f"pezzo {angoli}: dipende da celle fuori dal pezzo")

    def test_regola_dei_bordi(self):
        # Corner pieces only where no side neighbour already brings the same floor; lowest
        # priority first; a lower or equal neighbour draws nothing.
        from blocchi.terreno import PRIORITA, pezzi_bordo
        ordine = sorted(PRIORITA, key=PRIORITA.get)
        basso, altro, alto = ordine[2], ordine[-2], ordine[-1]
        vicini = dict.fromkeys(BORDI, basso)
        self.assertEqual([], pezzi_bordo(basso, vicini))
        self.assertEqual([(alto, BORDI.index("NE"))], pezzi_bordo(basso, vicini | {"NE": alto}))
        self.assertEqual([(alto, BORDI.index("N"))], pezzi_bordo(basso, vicini | {"N": alto, "NE": alto}))
        self.assertEqual([(alto, BORDI.index("N")), (alto, BORDI.index("E"))],
                         pezzi_bordo(basso, vicini | {"N": alto, "E": alto, "NE": alto}))
        self.assertEqual([(altro, BORDI.index("N")), (alto, BORDI.index("NE"))],
                         pezzi_bordo(basso, vicini | {"N": altro, "NE": alto}))
        self.assertEqual([], pezzi_bordo(alto, vicini | {"N": altro}))

    def test_finestra_composta_senza_segni(self):
        # Composed window: a low cell under a higher floor on N, E and NE (two side fringes meeting
        # in a corner). Every pixel of the cell is its own variant or the higher floor's texture
        # at that spot: no dark lip or seam where the fringes meet.
        from blocchi import terreno as tr
        from tavola import componi
        ordine = sorted(tr.PRIORITA, key=tr.PRIORITA.get)
        basso, alto = ordine[2], ordine[-1]
        b, a = (basso, None, None), (alto, None, None)
        im = componi([[b, a, a], [b, b, a], [b, b, b]], lambda nome: SPRITE[nome]())
        cella = im.crop((T, T, 2 * T, 2 * T))
        proprio = SPRITE[f"terreno-{basso}-{tr.hash_cella(1, 1, tr.SALE_VARIANTE) % VARIANTI}"]()
        trama = SPRITE[f"terreno-{alto}-0"]()
        estranei = [(x, y) for y in range(T) for x in range(T)
                    if cella.getpixel((x, y)) not in (proprio.getpixel((x, y)), trama.getpixel((x, y)))]
        self.assertEqual([], estranei)
        coperti = sum(1 for y in range(T) for x in range(T)
                      if cella.getpixel((x, y)) == trama.getpixel((x, y)) != proprio.getpixel((x, y)))
        self.assertGreater(coperti, 2 * T)  # the fringes are there

    def test_stili_terreno(self):
        # Every terrain id of content.json has its art parameters, and the ore deposits are
        # exactly the items of TerrainMap.ItemIdForDeposit (DepositKind) in the C# simulation.
        import re
        from blocchi import terreno as tr
        c = _content()["terrain"]
        self.assertEqual(sorted(f["id"] for f in c["floors"]), sorted(tr.STILI))
        self.assertEqual(sorted(w["id"] for w in c["walls"]), sorted(tr.STILI_PARETI))
        self.assertEqual(_biomi_terra(), sorted(tr.DENSITA_DECO))
        self.assertEqual(["acqua-bassa-anim", "acqua-profonda-anim"], sorted(tr.ACQUA.values()))
        with open(os.path.join(REPO, "src", "TIndustry.Shared", "Grid", "TerrainMap.cs"), encoding="utf-8") as f:
            sorgente = f.read()
        corpo = re.search(r"enum DepositKind\s*:\s*byte\s*\{([^}]*)\}", sorgente).group(1)
        tipi = [t.strip() for t in corpo.split(",") if t.strip() and t.strip() != "None"]
        mappa = dict(re.findall(r'DepositKind\.(\w+) => "([^"]+)"', sorgente.split("ItemIdForDeposit", 1)[1]))
        self.assertEqual(sorted(tipi), sorted(mappa))
        self.assertEqual(sorted(mappa.values()), tr.MINERALI)

    def test_fixture_terreno_aggiornata(self):
        # The C# test TerrainLookTests compares the client's composition plan with this fixture:
        # it must be what pezzi_mappa gives today (python tools/art/fixture_terreno.py rewrites it).
        import fixture_terreno
        with open(fixture_terreno.FIXTURE, encoding="utf-8") as f:
            self.assertEqual(fixture_terreno.contenuto(), f.read())

    def test_densita_deco_come_in_csharp(self):
        # TerrainLook.DecorationDensity (C#) is a copy of DENSITA_DECO: same biomes, same values.
        import re
        from blocchi import terreno as tr
        with open(os.path.join(REPO, "src", "TIndustry.Shared", "Grid", "TerrainLook.cs"), encoding="utf-8") as f:
            sorgente = f.read()
        corpo = re.search(r"DecorationDensity\s*=.*?\{(.*?)\};", sorgente, re.S).group(1)
        self.assertEqual(tr.DENSITA_DECO, {b: int(v) for b, v in re.findall(r'\["(\w+)"\]\s*=\s*(\d+)', corpo)})

    def test_carbone_si_legge(self):
        # Coal pebbles are lumps, not holes: on basalt (dark) and gravel (light) every pebble has a
        # lit pixel clearly lighter than both floors, and a body darker than gravel.
        from blocchi import terreno as tr
        lum = lambda c: (c[0] * 299 + c[1] * 587 + c[2] * 114) // 1000  # noqa: E731
        pavimenti = [SPRITE[f"terreno-{s}-0"]().convert("RGB") for s in ("basalt", "gravel")]
        chiaro_suolo = max(lum(c) for p in pavimenti for _, c in p.getcolors(T * T))
        for v in range(tr.GIACIMENTI):
            with self.subTest(variante=v):
                im = SPRITE[f"giacimento-coal-{v}"]()
                opachi = [(x, y) for y in range(T) for x in range(T) if im.getpixel((x, y))[3] == 255]
                luci = [p for p in opachi if lum(im.getpixel(p)) >= 130]
                self.assertGreaterEqual(len(luci), 5)  # one glint per pebble (5-7 pebbles)
                self.assertGreater(max(lum(im.getpixel(p)) for p in luci), chiaro_suolo - 40)
                self.assertLess(sum(lum(im.getpixel(p)) for p in opachi) / len(opachi), 100)  # still coal-dark

    def test_bordi(self):
        # Edge strip: 8 pieces N, E, S, O, NE, SE, SO, NO; sides and corners are quarter turns
        # of each other; a side piece touches its side only, a corner piece its corner only.
        for f in _content()["terrain"]["floors"]:
            with self.subTest(suolo=f["id"]):
                pezzi = dict(zip(BORDI, fotogrammi_di(SPRITE[f"terreno-{f['id']}-bordo"]())))
                alfa = {k: p.getchannel("A") for k, p in pezzi.items()}
                for a, b in (("N", "E"), ("E", "S"), ("S", "O"), ("O", "N"),
                             ("NE", "SE"), ("SE", "SO"), ("SO", "NO"), ("NO", "NE")):
                    self.assertEqual(alfa[a].transpose(Image.Transpose.ROTATE_270).tobytes(), alfa[b].tobytes(), f"{a}->{b}")
                self.assertEqual((0, 0, T, alfa["N"].getbbox()[3]), alfa["N"].getbbox())
                self.assertLessEqual(alfa["N"].getbbox()[3], T // 3)
                x0, y0, x1, y1 = alfa["NE"].getbbox()
                self.assertEqual((T, 0), (x1, y0))
                self.assertLessEqual(max(x1 - x0, y1 - y0), T // 3)

    def test_solo_sprite_generati(self):
        # Every PNG in the root of godot/assets comes from the registry: no hand-made or third-party
        # sprite can sneak back in.
        cartella = os.path.join(REPO, "godot", "assets")
        png = {f[: -len(".png")] for f in os.listdir(cartella)
               if f.endswith(".png") and os.path.isfile(os.path.join(cartella, f))}
        self.assertEqual([], sorted(png - (set(SPRITE) - set(TEST_SPRITES))))
        # godot/assets/ui holds only generated files: the registry icons, the font (.fnt + page) and
        # their Godot .import files; no third-party icon is left (G4 2/6).
        cartella = os.path.join(cartella, "ui")
        ammessi = {n[len("ui/"):] + ".png" for n in SPRITE if n.startswith("ui/")}
        ammessi |= {f"{n}.{ext}" for n in FONT_UI for ext in ("png", "fnt")}
        ammessi |= {f + ".import" for f in ammessi}
        self.assertEqual([], sorted(set(os.listdir(cartella)) - ammessi))

    def test_icone_ui(self):
        # Every interface icon in both sizes: only white and the outline colour, the white never
        # touches transparency or the edge (closed outline), at least 20% of the pixels full.
        from palette import OUT
        bianco, contorno = (255, 255, 255, 255), OUT + (255,)
        for icona in ICONE_UI:
            for nome, lato in ((f"ui/ui-{icona}", 32), (f"ui/ui-{icona}-16", 16)):
                with self.subTest(icona=nome):
                    self.assertIn(nome, SPRITE)
                    im = SPRITE[nome]()
                    self.assertEqual((lato, lato), im.size)
                    px = im.load()
                    pieni = [(x, y) for x in range(lato) for y in range(lato) if px[x, y][3]]
                    self.assertEqual({bianco, contorno}, {px[p] for p in pieni})
                    self.assertGreaterEqual(len(pieni) / (lato * lato), 0.20)
                    for x, y in pieni:
                        if px[x, y] != bianco:
                            continue
                        for vx, vy in ((x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                            self.assertTrue(0 <= vx < lato and 0 <= vy < lato, f"{nome}: bianco sul bordo")
                            self.assertNotEqual(0, px[vx, vy][3], f"{nome}: bianco senza contorno")
        sagome = [SPRITE[f"ui/ui-{i}-16"]().getchannel("A").tobytes() for i in ICONE_UI]
        self.assertEqual(len(set(sagome)), len(sagome))

    def test_spunte_e_logo(self):
        # Check boxes: on = accent square in the middle (grey when disabled), off = dark inside; the logo uses
        # only the outline, the accent and the belt colours.
        from palette import ACCENTO, NASTRO, OUT, UI_DISATTIVO, UI_FONDO_BLOCCATO
        centro = {"ui/ui-spunta-si": ACCENTO[3], "ui/ui-spunta-si-spenta": UI_DISATTIVO,
                  "ui/ui-spunta-no": UI_FONDO_BLOCCATO, "ui/ui-spunta-no-spenta": UI_FONDO_BLOCCATO}
        for nome, colore in centro.items():
            self.assertEqual(colore + (255,), SPRITE[nome]().getpixel((10, 10)), nome)
        ammessi = {c + (255,) for c in [OUT, *ACCENTO, *NASTRO]}
        im = SPRITE["ui/ui-logo"]()
        usati = {im.getpixel((x, y)) for x in range(im.width) for y in range(im.height)} - {(0, 0, 0, 0)}
        self.assertEqual(set(), usati - ammessi)

    def test_spunte_come_ui_colors(self):
        # The check boxes use the client's colours: the UI_* values (and the accent) equal UiColors' Normale ones.
        import re
        from palette import ACCENTO, UI_BORDO_BLOCCATO, UI_DISATTIVO, UI_FONDO_BLOCCATO, UI_FONDO_SCELTO
        with open(os.path.join(REPO, "godot", "scripts", "ui", "UiColors.cs"), encoding="utf-8") as fh:
            sorgente = fh.read()
        normale = sorgente[sorgente.index("void SetNormale()"):sorgente.index("void SetDaltonici()")]
        valori = dict(re.findall(r'(\w+) = new\("([0-9A-Fa-f]{6})"\)', normale))
        attesi = {"FondoScelto": UI_FONDO_SCELTO, "FondoBloccato": UI_FONDO_BLOCCATO,
                  "BordoBloccato": UI_BORDO_BLOCCATO, "TestoDisattivo": UI_DISATTIVO, "Accento": ACCENTO[3]}
        for nome, rgb in attesi.items():
            self.assertEqual("%02X%02X%02X" % rgb, valori[nome].upper(), nome)

    def test_carattere_insieme(self):
        # Every required glyph exists (drawn or accented), fills the 11-row cell and has ink.
        self.assertEqual(INSIEME_CARATTERE, carattere.INSIEME)
        for ch in INSIEME_CARATTERE:
            with self.subTest(glifo=ch):
                righe = carattere.griglia(ch)
                self.assertEqual(carattere.CELLA, len(righe))
                if ch != " ":
                    self.assertIn("#", "".join(righe))

    def test_carattere_larghezze(self):
        # Coherent widths: tight glyph boxes (ink in the first and last column), 1-5 px wide; digits
        # share one width; an accented letter is as wide as its base letter and differs from it;
        # only accented capitals use the two accent rows on top of the cell.
        self.assertEqual({5}, {carattere.larghezza(c) for c in "0123456789"})
        for ch in INSIEME_CARATTERE:
            with self.subTest(glifo=ch):
                self.assertTrue(1 <= carattere.larghezza(ch) <= 5)
                righe = carattere.griglia(ch)
                if ch != " " and not ch.isdigit():  # digits are tabular: "1" keeps its side bearings
                    self.assertIn("#", "".join(r[0] for r in righe))
                    self.assertIn("#", "".join(r[-1] for r in righe))
                alto = "".join(righe[:carattere.ACCENTO_RIGHE])
                if ch in carattere.ACCENTATE and ch.isupper():
                    self.assertIn("#", alto)
                else:
                    self.assertNotIn("#", alto)
        for ch, (base, _) in carattere.ACCENTATE.items():
            self.assertEqual(carattere.larghezza(base), carattere.larghezza(ch), ch)
            self.assertNotEqual(carattere.griglia(base), carattere.griglia(ch), ch)

    def test_carattere_fnt(self):
        # BMFont text files: a common line with lineHeight and base, one page that is the atlas, one
        # `char id=` line per required glyph whose rectangle in the atlas holds exactly that glyph.
        file = carattere.file_carattere()
        for nome, k in (("carattere-testo", 2), ("carattere-titoli", 3)):
            with self.subTest(font=nome):
                testo = file[f"ui/{nome}.fnt"].decode("utf-8")
                self.assertNotIn("\r", testo)
                campi = []
                for riga in testo.splitlines():
                    tag, _, resto = riga.partition(" ")
                    campi.append((tag, dict(c.split("=", 1) for c in resto.split(" ") if "=" in c)))
                common = next(c for t, c in campi if t == "common")
                self.assertEqual((carattere.CELLA + 1) * k, int(common["lineHeight"]))
                self.assertEqual(9 * k, int(common["base"]))
                self.assertEqual(f'"{nome}.png"', next(c for t, c in campi if t == "page")["file"])
                atlante = Image.open(io.BytesIO(file[f"ui/{nome}.png"])).convert("RGBA")
                self.assertEqual(atlante.size, (int(common["scaleW"]), int(common["scaleH"])))
                char = {int(c["id"]): c for t, c in campi if t == "char"}
                self.assertEqual(sorted(map(ord, INSIEME_CARATTERE)), sorted(char))
                self.assertEqual(len(char), int(next(c for t, c in campi if t == "chars")["count"]))
                for ch in INSIEME_CARATTERE:
                    c = char[ord(ch)]
                    x, y, w, h = (int(c[n]) for n in ("x", "y", "width", "height"))
                    self.assertEqual((carattere.larghezza(ch) + 1) * k, int(c["xadvance"]), ch)
                    self.assertEqual(carattere.glifo(ch, k).tobytes(), atlante.crop((x, y, x + w, y + h)).tobytes(), ch)

    def test_carattere_file_aggiornati(self):
        # The font files in godot/assets/ui are the ones carattere.py writes today (descriptors by
        # bytes; the atlas pages by pixels, as genera.py --controlla --pixel does in CI).
        for rel, dati in carattere.file_carattere().items():
            path = os.path.join(REPO, "godot", "assets", rel)
            self.assertTrue(os.path.exists(path), rel)
            if rel.endswith(".fnt"):
                with open(path, "rb") as f:
                    self.assertEqual(dati, f.read(), rel)
            else:
                with Image.open(path) as a, Image.open(io.BytesIO(dati)) as b:
                    self.assertEqual(b.convert("RGBA").tobytes(), a.convert("RGBA").tobytes(), rel)

    def test_carattere_copre_i_testi(self):
        # Every character the game can show (data/*.json and the C# string literals of the client and
        # of Shared, comments excluded) has a glyph in both fonts (text and titles: level names and
        # other texts can use either); otherwise it would be a box. Known scanner limits: \x and \U
        # escapes are not decoded and literals nested inside interpolation holes are read as code.
        testi = _testi_del_gioco()
        for nome in FONT_UI:
            with self.subTest(font=nome):
                with open(os.path.join(REPO, "godot", "assets", "ui", f"{nome}.fnt"), encoding="utf-8") as f:
                    ids = {int(r.split("id=")[1].split(" ")[0]) for r in f if r.startswith("char id=")}
                mancanti = sorted(f"{ascii(ch)} U+{ord(ch):04X} ({dove})" for ch, dove in testi.items()
                                  if ch not in CONTROLLO and ord(ch) not in ids)
                self.assertEqual([], mancanti)

    def test_letterali_cs(self):
        # The C# literal scanner: comments skipped, escapes decoded, verbatim and char literals read.
        sorgente = 'var a = "x' + BS + 'u2192y"; // "no"' + chr(10) + '/* "no" */ var b = @"v""w"; var c = ' + "'z'; var d = $\"{n} k\";"
        self.assertEqual('x→yv"wz{n} k', _letterali_cs(sorgente))


if __name__ == "__main__":
    unittest.main()
