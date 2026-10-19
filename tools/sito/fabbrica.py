"""The factory floor of the landing page: belts, pieces, blocks, deposits, power and terrain as data in cell
coordinates, drawn with the game's own sprites at the in-game scale (SCALA = 2, 64 px per cell).

A Scena is a list of elements on a floor LARGHEZZA cells wide and `righe` cells tall. html_scena() turns it
into the absolute-positioned HTML of the site; the images (frame 0 of the belts turned to their heading,
animated GIFs of corners and joins, block states from composti, deposits, terrain) are written once into
fabbrica/ by an Archivio, so the pages stay small. controlla() and catene() walk the belts cell by cell:
every chain must start at a block that produces (or come from the scene above) and end in a block that
consumes, in the Core, or leave from the bottom edge towards the next scene.

Belt treads scroll with a CSS animation of the background position (frame k of a strip is frame 0 moved
k px along the flow, so sliding frame 0 by one cell per 1 / rate seconds is the strip itself); items are
sprites that cross each run in n / rate seconds (MindustryBeltVisual, tools/art/blocchi/logistica.py).
"""
import io
import math
import os
import random
import unicodedata
from dataclasses import dataclass, field

from PIL import Image

import composti

SITO_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(SITO_DIR, "..", ".."))

C = composti.T * composti.SCALA  # screen px per cell (64)
LARGHEZZA = 18  # cells per row of the floor (1152 px)
RATE = composti.VELOCITA_NASTRO  # cells per second per tier
T1, T2, T3 = "conveyor-basic", "conveyor-fast", "conveyor-express"
PRODUTTORI = {"miner", "miner-advanced", "extractor", "smelter", "assembler"}
CONSUMATORI = {"smelter", "assembler", "generator"}  # plus every core-N
VERSI = {"E": (1, 0), "S": (0, 1), "W": (-1, 0), "N": (0, -1)}
ROT = {"E": 0, "S": 90, "W": 180, "N": -90}  # ScrollingBeltStrip.RotationFor: art drawn East, clockwise degrees
# BeltCornerTile.ResolveTransform: the corner art enters from West and exits South, a clockwise turn for a belt
# heading East. Clockwise turns (heading before -> after) rotate from the heading; counter-clockwise ones mirror
# the art first, then rotate.
_ORARIE = {("E", "S"): 0, ("S", "W"): 90, ("W", "N"): 180, ("N", "E"): -90}
_ANTIORARIE = {("W", "S"): 0, ("S", "E"): -90, ("E", "N"): 180, ("N", "W"): 90}


def trasformazione_curva(da, a):
    """(mirror, clockwise degrees) of the corner art for a belt heading `da` that turns to head `a`."""
    if (da, a) in _ORARIE:
        return False, _ORARIE[(da, a)]
    if (da, a) in _ANTIORARIE:
        return True, _ANTIORARIE[(da, a)]
    raise ValueError(f"curva impossibile: {da} -> {a}")


def _sinistra(v):
    return {"E": "N", "N": "W", "W": "S", "S": "E"}[v]


def _destra(v):
    return {"E": "S", "S": "W", "W": "N", "N": "E"}[v]


# ---------------------------------------------------------------- the model

@dataclass
class Tratto:
    """Straight belt run of n cells from (x, y) heading `verso`, carrying `oggetti` (item ids, cycled)."""
    x: int
    y: int
    n: int
    verso: str
    tier: str
    oggetti: tuple = ()
    basso: bool = False  # anchored to the bottom edge of the scene (the hero grows with the screen)

    @property
    def celle(self):
        dx, dy = VERSI[self.verso]
        return [(self.x + dx * k, self.y + dy * k) for k in range(self.n)]


@dataclass
class Pezzo:
    """One logistics piece: curva (tier, da, a), t / unione_edificio / unione (tier, verso, lato for unione),
    incrocio, selezionatore (oggetto, verso), sdoppiatore (verso = the flow entering it: it splits left and
    right, never straight), ponte (n, verso). `flussi` draws items crossing the piece: (verso, item, tier)."""
    x: int
    y: int
    tipo: str
    tier: str = T1
    da: str = "E"
    a: str = "S"
    verso: str = "S"
    n: int = 2
    oggetto: str = "iron-ore"
    lato: str = "left"
    flussi: tuple = ()
    basso: bool = False

    @property
    def celle(self):
        if self.tipo == "ponte":
            dx, dy = VERSI[self.verso]
            return [(self.x + dx * k, self.y + dy * k) for k in range(self.n)]
        return [(self.x, self.y)]


@dataclass
class Blocco:
    x: int
    y: int
    id: str
    stato: str
    basso: bool = False

    @property
    def celle(self):
        w, h = _blocco(self.id).celle
        return [(self.x + i, self.y + j) for j in range(h) for i in range(w)]


@dataclass
class Giacimento:
    x: int
    y: int
    w: int
    h: int
    minerale: str
    seme: int = 1
    basso: bool = False


@dataclass
class Nodo:
    x: int
    y: int

    @property
    def celle(self):
        return [(self.x, self.y)]


@dataclass
class Linea:
    """Power line: a polyline of cell coordinates (floats allowed), drawn in UiColors.Energia."""
    punti: tuple


@dataclass
class Scena:
    id: str
    righe: int
    bioma: str | None  # None: no terrain (the hero has its own background)
    elementi: list
    seme: int = 1
    classe: str = ""
    sfondo: tuple | None = None  # (capture of docs/img/readme/, crop box) drawn under everything (the hero)


def _blocco(bid):
    return next(b for b in composti.BLOCCHI if b.id == bid)


# ---------------------------------------------------------------- walking the belts

def _occupazione(scena):
    """{cell: element} for the solid elements (belts, pieces, blocks, nodes); bridge spans are not solid."""
    celle = {}
    for e in scena.elementi:
        if isinstance(e, (Giacimento, Linea)):
            continue
        lista = e.celle
        if isinstance(e, Pezzo) and e.tipo == "ponte":
            lista = [lista[0], lista[-1]]
        for c in lista:
            celle.setdefault(c, []).append(e)
    return celle


def _ingresso(e, cella):
    """Heading of the flow entering a belt-like element at `cella`, or None if it accepts any heading."""
    if isinstance(e, Tratto):
        return e.verso
    if e.tipo == "curva":
        return e.da
    if e.tipo in ("t", "unione_edificio", "unione", "ponte"):
        return e.verso
    if e.tipo in ("sdoppiatore", "selezionatore"):
        return e.verso  # they face the flow
    return None  # incrocio: straight through on both axes


def _uscite(e, cella, entrata):
    """[(next cell, heading)] the flow takes after `cella` entered heading `entrata`."""
    if isinstance(e, Tratto):
        v = e.verso
    elif e.tipo == "curva":
        v = e.a
    elif e.tipo in ("t", "unione_edificio", "unione"):
        v = e.verso
    elif e.tipo == "ponte":
        if cella != e.celle[0]:
            return []
        dx, dy = VERSI[e.verso]
        ex, ey = e.celle[-1]
        return [((ex + dx, ey + dy), e.verso)]
    elif e.tipo == "sdoppiatore":
        # BeltGrid: a splitter hands items to the left and right of its facing, alternately, never straight.
        return [((cella[0] + VERSI[h][0], cella[1] + VERSI[h][1]), h) for h in (_sinistra(e.verso), _destra(e.verso))]
    elif e.tipo == "selezionatore":
        v = e.verso
    else:
        v = entrata
    dx, dy = VERSI[v]
    return [((cella[0] + dx, cella[1] + dy), v)]


def _dentro(scena, cella):
    return 0 <= cella[0] < LARGHEZZA and 0 <= cella[1] < scena.righe


def _segui(scena, occupato, cella, entrata, visti):
    """Walk from `cella` (entered heading `entrata`) to the end of the chain: ("blocco", id), ("sotto", x),
    ("fuori", cell) or ("vuoto", cell). Returns (cells walked, end)."""
    percorso = []
    while True:
        if not _dentro(scena, cella):
            if cella[1] >= scena.righe and entrata == "S":
                return percorso, ("sotto", cella[0])
            return percorso, ("fuori", cella)
        qui = occupato.get(cella)
        if not qui:
            return percorso, ("vuoto", cella)
        e = qui[0]
        if isinstance(e, Blocco):
            return percorso, ("blocco", e.id)
        if isinstance(e, Nodo):
            return percorso, ("nodo", cella)
        if isinstance(e, Tratto) and VERSI[entrata] == (-VERSI[e.verso][0], -VERSI[e.verso][1]):
            return percorso, ("contromano", cella)  # a belt is never entered from its head
        if (cella, entrata) in visti:
            return percorso, ("ciclo", cella)
        visti.add((cella, entrata))
        percorso.append(cella)
        uscite = [(c, h) for c, h in _uscite(e, cella, entrata)
                  if _dentro(scena, c) and occupato.get(c) or not _dentro(scena, c)]
        if isinstance(e, Pezzo) and e.tipo == "sdoppiatore":
            # The first side output is this chain; the other side is a chain of its own (see catene()).
            uscite = uscite[:1]
        if not uscite:
            c, h = _uscite(e, cella, entrata)[0]
            return percorso, ("vuoto", c)
        cella, entrata = uscite[0]


def catene(scena):
    """Every chain of the scene: {"sorgente", "celle", "fine"}; sources are producing blocks, the top edge and
    the side outputs of splitters."""
    occupato = _occupazione(scena)
    partenze = []
    for cella, lista in sorted(occupato.items()):
        e = lista[0]
        if isinstance(e, (Blocco, Nodo)):
            continue
        if isinstance(e, Pezzo) and e.tipo == "ponte" and cella != e.celle[0]:
            continue
        entrata = _ingresso(e, cella)
        candidati = [entrata] if entrata else list(VERSI)
        for h in candidati:
            dx, dy = VERSI[h]
            dietro = (cella[0] - dx, cella[1] - dy)
            if dietro[1] < 0 and h == "S":
                partenze.append((cella, h, ("sopra", cella[0])))
            elif dietro in occupato and isinstance(occupato[dietro][0], Blocco) \
                    and (occupato[dietro][0].id in PRODUTTORI):
                partenze.append((cella, h, ("blocco", occupato[dietro][0].id)))
            elif dietro in occupato and isinstance(occupato[dietro][0], Pezzo) \
                    and occupato[dietro][0].tipo == "sdoppiatore" and h == _destra(occupato[dietro][0].verso):
                partenze.append((cella, h, ("sdoppiatore", dietro)))  # the second side output
    out = []
    for cella, h, sorgente in partenze:
        celle, fine = _segui(scena, occupato, cella, h, set())
        if fine[0] == "contromano" and not celle:
            continue  # a producer beside the head of a belt that flows towards it: no chain starts there
        out.append({"sorgente": sorgente, "celle": celle, "fine": fine, "primo": occupato[cella][0]})
    return out


def controlla(scena):
    """Problems of a scene: overlapping solids, belts nobody feeds, chains that end in nothing."""
    problemi = []
    occupato = _occupazione(scena)
    for cella, lista in sorted(occupato.items()):
        if len(lista) > 1:
            problemi.append(f"{scena.id}: due elementi in {cella}: {[type(e).__name__ for e in lista]}")
        if not _dentro(scena, cella):
            problemi.append(f"{scena.id}: {type(lista[0]).__name__} fuori dalla scena in {cella}")
    coperte = set()
    for catena in catene(scena):
        coperte |= set(catena["celle"])
        if catena["fine"][0] not in ("blocco", "sotto"):
            problemi.append(f"{scena.id}: catena da {catena['sorgente']} finisce in {catena['fine']}")
        elif catena["fine"][0] == "blocco" and not (catena["fine"][1] in CONSUMATORI
                                                  or catena["fine"][1].startswith("core-")):
            problemi.append(f"{scena.id}: catena da {catena['sorgente']} entra in {catena['fine'][1]}, che non consuma")
    for cella, lista in sorted(occupato.items()):
        e = lista[0]
        if isinstance(e, (Tratto, Pezzo)) and cella not in coperte:
            if isinstance(e, Pezzo) and e.tipo == "ponte" and cella != e.celle[0]:
                continue
            problemi.append(f"{scena.id}: nessuna catena passa da {cella} ({type(e).__name__})")
    return problemi


def entrate_dall_alto(scena):
    return {c["sorgente"][1] for c in catene(scena) if c["sorgente"][0] == "sopra"}


def uscite_dal_basso(scena):
    return {c["fine"][1] for c in catene(scena) if c["fine"][0] == "sotto"}


# ---------------------------------------------------------------- images

class Archivio:
    """Writes the images of the scenes into <out>/fabbrica/ once and hands out their URLs."""

    def __init__(self, immagini, out_dir, radice=""):
        self.immagini = immagini
        self.cartella = os.path.join(out_dir, "fabbrica")
        self.radice = radice
        self._url = {}

    def _scrivi(self, nome, dati):
        if nome not in self._url:
            os.makedirs(self.cartella, exist_ok=True)
            with open(os.path.join(self.cartella, nome), "wb") as fh:
                fh.write(dati)
            self._url[nome] = f"{self.radice}fabbrica/{nome}"
        return self._url[nome]

    def png(self, nome, im):
        buf = io.BytesIO()
        im.save(buf, "PNG", optimize=True)
        return self._scrivi(nome + ".png", buf.getvalue())

    def gif(self, nome, fotogrammi):
        """Animated GIF of composed frames (at SCALA), written at 1x: the page scales it back with pixelated
        rendering, so the bytes halve and nothing changes on screen."""
        import costruisci
        return self._scrivi(nome + ".gif", costruisci._gif([_meta(f) for f, _ in fotogrammi], [d for _, d in fotogrammi]))

    # --- sprites of the scenes, all at art size (1x); the HTML gives their size on screen
    def x2(self, nome, im):
        return self.png(nome, im)

    def nastro(self, tier, verso):
        """(animated strip, still frame 0) of a belt heading `verso`: the real 32-frame strip of the game, so the
        rails stand still while the tread moves (the still frame serves prefers-reduced-motion)."""
        n = len(self.immagini.fotogrammi(tier + "-anim"))
        gif = self.striscia(f"nastro-{tier}-{verso.lower()}", tier + "-anim", RATE[tier] * n, ruota=ROT[verso])
        im = self.immagini.fotogrammi(tier + "-anim")[0]
        im = {"E": im, "S": im.rotate(-90), "W": im.rotate(180), "N": im.rotate(90)}[verso]
        return gif, self.png(f"nastro-{tier}-{verso.lower()}-fermo", im)

    def striscia(self, nome, sprite, fps, flip=False, ruota=0):
        """Animated GIF of a strip, mirrored / turned, sampled like composti (every whole frame that keeps the
        GIF delay at 20 ms or more)."""
        b = composti.Blocco("x", "", (1, 1), [composti.Strato(sprite, fps=fps, flip=flip, ruota=ruota)], [], [])
        passo = math.ceil(composti.PASSO_MINIMO * fps) / fps
        st = composti.Stato("a", frozenset(), len(self.immagini.fotogrammi(sprite)) / fps, passo)
        return self.gif(nome, composti.fotogrammi_stato(b, st, self.immagini))

    def curva(self, tier, da, a):
        flip, ruota = trasformazione_curva(da, a)
        n = len(self.immagini.fotogrammi(tier + "-corner-anim"))
        return self.striscia(f"curva-{tier}-{da.lower()}{a.lower()}", tier + "-corner-anim", RATE[tier] * n, flip, ruota)

    def pezzo_girato(self, nome, tier, suffisso, verso):
        n = len(self.immagini.fotogrammi(f"{tier}-{suffisso}-anim"))
        return self.striscia(f"{nome}-{tier}-{verso.lower()}", f"{tier}-{suffisso}-anim", RATE[tier] * n, ruota=ROT[verso])

    def ponte(self, verso):
        """The whole 4-cell bridge of composti (deck, the three chevrons lighting up in turn, both ends), turned
        to `verso`."""
        b = _blocco("conveyor-bridge")
        st = next(x for x in b.stati if x.nome == "In funzione")
        fotogrammi = [(f.rotate(-ROT[verso], expand=True) if ROT[verso] else f, d)
                      for f, d in composti.fotogrammi_stato(b, st, self.immagini)]
        return self.gif(f"ponte-{verso.lower()}", fotogrammi)

    def blocco(self, bid, stato):
        import costruisci
        b = _blocco(bid)
        s = next(x for x in b.stati if x.nome == stato)
        fotogrammi = composti.fotogrammi_stato(b, s, self.immagini)
        nome = f"blocco-{bid}-{_slug(stato)}"
        if len(fotogrammi) == 1:
            return self.png(nome, _meta(fotogrammi[0][0]))
        return self.gif(nome, fotogrammi)

    def sprite(self, nome):
        return self.x2(_slug(nome), self.immagini(nome))

    def sfondo(self, cattura, box):
        """A README capture without its HUD, as the background of the hero."""
        with Image.open(os.path.join(REPO, "docs", "img", "readme", cattura)) as im:
            return self.png("sfondo-" + _slug(cattura.rsplit(".", 1)[0]), im.convert("RGB").crop(box))

    def terreno(self, bioma, righe, seme):
        from blocchi import terreno as tr
        suoli = [f["id"] for f in tr.TERRENO["floors"] if f["biome"] == bioma]
        rnd = random.Random(seme)
        im = Image.new("RGBA", (LARGHEZZA * composti.T, righe * composti.T))
        for j in range(righe):
            for i in range(LARGHEZZA):
                s = suoli[0] if rnd.random() < 0.72 else rnd.choice(suoli)
                im.paste(self.immagini(f"terreno-{s}-{rnd.randrange(4)}"), (i * composti.T, j * composti.T))
        return self.png(f"terreno-{bioma}-{righe}-{seme}", im)


def _meta(im):
    """A frame composed at SCALA back to art size (every pixel of the art is a SCALA x SCALA block)."""
    return im.resize((im.width // composti.SCALA, im.height // composti.SCALA), Image.NEAREST)


def _slug(testo):
    """File-name-safe ASCII slug (accents stripped, anything else a dash)."""
    piatto = unicodedata.normalize("NFKD", testo.lower()).encode("ascii", "ignore").decode()
    return "".join(ch if ch.isalnum() else "-" for ch in piatto).strip("-")


# ---------------------------------------------------------------- HTML

def _pos(x, y, w=1, h=1, righe=None, basso=False):
    if basso:
        return f"left:{x*C:.0f}px;bottom:{(righe - y - h)*C:.0f}px;width:{w*C:.0f}px;height:{h*C:.0f}px"
    return f"left:{x*C:.0f}px;top:{y*C:.0f}px;width:{w*C:.0f}px;height:{h*C:.0f}px"


def _img(url, x, y, w=1, h=1, z=3, rot=0, righe=None, basso=False, classe="px", lazy=True):
    r = f"transform:rotate({rot}deg);" if rot else ""
    carica = ' loading="lazy"' if lazy else ""
    return (f'<img class="{classe}" src="{url}" alt="" width="{w*C:.0f}" height="{h*C:.0f}"{carica} '
            f'style="{_pos(x, y, w, h, righe, basso)};{r}z-index:{z}">')


def _oggetti(a, verso, oggetti, n, tier):
    """Items crossing n cells heading `verso` at the tier's speed, one per cell, staggered so that every run of
    the page shares the same phase (position in cells = t * rate + i: continuous across runs and pieces)."""
    lungo = n * C
    durata = n / RATE[tier]
    out = ""
    for i in range(n):
        o = oggetti[i % len(oggetti)]
        out += (f'<img class="ogg" src="{a.sprite(o + "-belt")}" alt="" width="20" height="20" style="--L:{lungo}px;'
                f'--dur:{durata:.3f}s;--rit:{-i * durata / n:.3f}s;animation-name:va-{verso.lower()}">')
    return out


def _html_tratto(t, a, scena):
    orizzontale = t.verso in "EW"
    w, h = (t.n, 1) if orizzontale else (1, t.n)
    x0 = t.x - t.n + 1 if t.verso == "W" else t.x
    y0 = t.y - t.n + 1 if t.verso == "N" else t.y
    giro = 1 / RATE[t.tier]
    striscia, fermo = a.nastro(t.tier, t.verso)
    return (f'<div class="nastro v-{t.verso.lower()}" style="{_pos(x0, y0, w, h, scena.righe, t.basso)};'
            f'background-image:url({striscia});--giro:{giro:g}s">'
            f'<div class="fermo" style="background-image:url({fermo})"></div>'
            + _oggetti(a, t.verso, t.oggetti, t.n, t.tier) + "</div>")


def _html_pezzo(p, a, scena):
    r = scena.righe
    flussi = ""
    if p.flussi:
        flussi = (f'<div class="flusso" style="{_pos(p.x, p.y, 1, 1, r, p.basso)}">'
                  + "".join(_oggetti(a, v, (o,), 1, tier) for v, o, tier in p.flussi) + "</div>")
    if p.tipo == "curva":
        return _img(a.curva(p.tier, p.da, p.a), p.x, p.y, righe=r, basso=p.basso) + flussi
    if p.tipo == "t":
        return _img(a.pezzo_girato("t", p.tier, "t", p.verso), p.x, p.y, righe=r, basso=p.basso) + flussi
    if p.tipo == "unione_edificio":
        return _img(a.pezzo_girato("unione-edificio", p.tier, "join-building-both", p.verso), p.x, p.y, righe=r, basso=p.basso) + flussi
    if p.tipo == "unione":
        # ScrollingBeltStrip side join: the belt with a building (the "block with the dot") on its left or right.
        return _img(a.pezzo_girato(f"unione-{p.lato}", p.tier, f"join-{p.lato}", p.verso), p.x, p.y, righe=r, basso=p.basso) + flussi
    if p.tipo == "incrocio":
        return _img(a.sprite("junction"), p.x, p.y, righe=r, basso=p.basso) + flussi
    if p.tipo == "sdoppiatore":
        # MindustryBeltVisual.AddSpecial: splitter and sorter turn with their facing (ScrollingBeltStrip.RotationFor).
        return _img(a.sprite("splitter"), p.x, p.y, rot=ROT[p.verso], righe=r, basso=p.basso) + flussi
    if p.tipo == "selezionatore":
        # The sorter turned to its facing and, upright on top, the belt sprite of its filter item.
        return (_img(a.sprite("sorter"), p.x, p.y, rot=ROT[p.verso], righe=r, basso=p.basso)
                + _img(a.sprite(p.oggetto + "-belt"), p.x + 11 / 32, p.y + 11 / 32, 20 / 64, 20 / 64, z=4, righe=r, basso=p.basso)
                + flussi)
    if p.tipo == "ponte" and p.n == composti.PONTE_CELLE:
        w, h = (1, p.n) if p.verso in "SN" else (p.n, 1)
        return _img(a.ponte(p.verso), p.x, p.y, w, h, z=5, righe=r)
    if p.tipo == "ponte":
        # BridgeSpanVisual: both ends on top of the deck, the deck (16x22 px) tiled over the span.
        dx, dy = VERSI[p.verso]
        estremo = a.sprite("conveyor-bridge")
        out = _img(estremo, p.x, p.y, z=5, rot=ROT[p.verso], righe=r)
        out += _img(estremo, p.x + dx * (p.n - 1), p.y + dy * (p.n - 1), z=5, rot=ROT[p.verso], righe=r)
        span = a.immagini("conveyor-bridge-span")
        if p.verso in "SN":
            url = a.x2("ponte-impalcato-v", span.rotate(-90, expand=True))
            stile = _pos(p.x + 0.5 - 11 / 32, p.y + 1, 22 / 32, p.n - 2, r)
        else:
            url = a.x2("ponte-impalcato-o", span)
            stile = _pos(p.x + 1, p.y + 0.5 - 11 / 32, p.n - 2, 22 / 32, r)
        return out + f'<div class="impalcato" style="{stile};background-image:url({url})"></div>'
    raise ValueError(p.tipo)


def _html_linea(linea, scena):
    seg = "".join(f'<line x1="{a[0]*C:.0f}" y1="{a[1]*C:.0f}" x2="{b[0]*C:.0f}" y2="{b[1]*C:.0f}"/>'
                  for a, b in zip(linea.punti, linea.punti[1:]))
    nodi = "".join(f'<circle cx="{p[0]*C:.0f}" cy="{p[1]*C:.0f}" r="4"/>' for p in linea.punti)
    w, h = LARGHEZZA * C, scena.righe * C
    return f'<svg class="corrente" viewBox="0 0 {w} {h}" style="width:{w}px;height:{h}px">{seg}{nodi}</svg>'


def html_scena(scena, archivio):
    """The <div class="scena"> of a scene: terrain, then every element in list order."""
    a = archivio
    parti = []
    if scena.bioma:
        parti.append(f'<div class="terra" style="background-image:url({a.terreno(scena.bioma, scena.righe, scena.seme)})"></div>')
    for e in scena.elementi:
        if isinstance(e, Tratto):
            parti.append(_html_tratto(e, a, scena))
        elif isinstance(e, Pezzo):
            parti.append(_html_pezzo(e, a, scena))
        elif isinstance(e, Blocco):
            w, h = _blocco(e.id).celle
            parti.append(_img(a.blocco(e.id, e.stato), e.x, e.y, w, h, z=4, righe=scena.righe, basso=e.basso))
        elif isinstance(e, Giacimento):
            rnd = random.Random(e.seme)
            for j in range(e.h):
                for i in range(e.w):
                    parti.append(_img(a.sprite(f"giacimento-{e.minerale}-{rnd.randrange(2)}"), e.x + i, e.y + j,
                                      z=1, righe=scena.righe, basso=e.basso))
        elif isinstance(e, Nodo):
            parti.append(_img(a.sprite("power-node"), e.x, e.y, z=4, righe=scena.righe))
        elif isinstance(e, Linea):
            parti.append(_html_linea(e, scena))
    classe = f"scena {scena.classe}".strip()
    return (f'<div class="{classe}" style="width:{LARGHEZZA * C}px;height:{scena.righe * C}px">'
            + "".join(parti) + "</div>")


# ---------------------------------------------------------------- the landing page

def _margine(nodo_y, righe, da_sopra=True, verso_sotto=True):
    """The power line down the right margin (x = 17.5) with its node, joined to the scene edges."""
    out = []
    if da_sopra:
        out.append(Linea(((17.5, 0), (17.5, nodo_y + 0.5))))
    if verso_sotto:
        out.append(Linea(((17.5, nodo_y + 0.5), (17.5, righe))))
    out.append(Nodo(17, nodo_y))
    return out


VETRINA = [
    # 01 hero: a miner on iron starts the dorsale; the whole bottom-left corner is anchored to the bottom edge,
    # because the hero is as tall as the screen.
    Scena("hero", 8, None, [
        Giacimento(1, 5, 2, 3, "iron-ore", 3, basso=True),
        Blocco(1, 6, "miner", "Al lavoro", basso=True),
        Pezzo(0, 6, "curva", T1, da="W", a="S", basso=True),
        Tratto(0, 7, 1, "S", T1, ("iron-ore",), basso=True),
    ], sfondo=("gioco.png", (0, 170, 1030, 720))),
    # 02 come funziona: the dorsale feeds the smelter; coal from a miner above goes through a splitter facing
    # South, which hands it left (West, into the smelter after a corner) and right (East, then down to the generator).
    Scena("come-funziona", 9, "rocce", [
        Tratto(0, 0, 3, "S", T1, ("iron-ore",)), Blocco(0, 3, "smelter", "Al lavoro"),
        Tratto(0, 5, 4, "S", T1, ("iron-plate",)),
        Giacimento(2, 0, 3, 2, "coal", 5), Blocco(2, 0, "miner", "Al lavoro"),
        Pezzo(2, 2, "sdoppiatore", verso="S"),
        Pezzo(1, 2, "curva", T1, da="W", a="S"), Pezzo(3, 2, "curva", T1, da="E", a="S"),
        Tratto(3, 3, 3, "S", T1, ("coal",)), Blocco(3, 6, "generator", "In funzione"),
        Nodo(6, 7), Blocco(7, 7, "lamp", "Accesa"),
        Linea(((4, 7), (6.5, 7.5))), Linea(((6.5, 7.5), (7.5, 7.5))), Linea(((6.5, 7.5), (17.5, 8.5))),
        *_margine(8, 9, da_sopra=False),
    ], seme=1),
    # 03 catture: the dorsale bridges over the big capture; lead -> smelter -> assembler (+coal) -> silicon.
    Scena("catture", 10, "sabbia", [
        Tratto(0, 0, 3, "S", T1, ("iron-plate",)), Pezzo(0, 3, "ponte", n=4, verso="S"),
        Tratto(0, 7, 3, "S", T1, ("iron-plate",)),
        Giacimento(13, 0, 2, 3, "lead-ore", 7), Blocco(13, 1, "miner-advanced", "Al lavoro, con corrente"),
        Pezzo(15, 1, "curva", T2, da="E", a="S"), Pezzo(15, 2, "unione", T2, verso="S", lato="right"),
        Tratto(15, 3, 1, "S", T2, ("lead-ore",)),
        Blocco(15, 4, "smelter", "Al lavoro"), Tratto(15, 6, 2, "S", T2, ("lead-plate",)),
        Blocco(15, 8, "assembler", "Ciclo di lavoro"),
        Giacimento(10, 8, 3, 2, "coal", 9), Blocco(11, 8, "miner", "Al lavoro"), Tratto(13, 8, 2, "E", T2, ("coal",)),
        Pezzo(17, 8, "curva", T2, da="E", a="S"), Pezzo(17, 9, "unione", T2, verso="S", lato="right"),
        *_margine(5, 10), Linea(((17.5, 5.5), (14, 2))), Linea(((17.5, 5.5), (16, 5))),
    ], seme=2),
    # 04 blocchi: the dorsale's assembler; the second line moves inwards, the copper crosses it at the junction
    # and enters the assembler through the sorter. Tall: the strip of blocks is pinned inside it.
    Scena("blocchi", 14, "sabbia", [
        Tratto(0, 0, 2, "S", T1, ("iron-plate",)), Blocco(0, 2, "assembler", "Ciclo di lavoro"),
        Tratto(0, 4, 10, "S", T1, ("copper-wire",)),
        Pezzo(17, 0, "curva", T2, da="S", a="W"), Tratto(16, 0, 3, "W", T2, ("silicon",)),
        Pezzo(13, 0, "curva", T2, da="W", a="S"), Tratto(13, 1, 1, "S", T2, ("silicon",)),
        Pezzo(13, 2, "incrocio", flussi=(("S", "silicon", T2), ("W", "copper-ore", T1))),
        Tratto(13, 3, 11, "S", T2, ("silicon",)),
        Giacimento(15, 2, 2, 3, "copper-ore", 11), Blocco(15, 2, "miner", "Al lavoro"),
        Tratto(14, 2, 1, "W", T1, ("copper-ore",)), Tratto(12, 2, 10, "W", T1, ("copper-ore",)),
        Pezzo(2, 2, "selezionatore", oggetto="copper-ore", verso="W", flussi=(("W", "copper-ore", T1),)),
        *_margine(6, 14),
    ], seme=3),
    # ?? sotto attacco: the two long lines and the power line cross the disturbed signal.
    Scena("sotto-attacco", 6, None, [
        Tratto(0, 0, 6, "S", T1, ("copper-wire",)), Tratto(13, 0, 6, "S", T2, ("silicon",)),
        Linea(((17.5, 0), (17.5, 6))),
    ], classe="scena-attacco"),
    # 05 ambienti: titanium -> smelter beside the second line, which takes the plate from the building.
    Scena("ambienti", 8, "neve", [
        Tratto(0, 0, 8, "S", T1, ("copper-wire",)),
        Tratto(13, 0, 4, "S", T2, ("silicon",)), Pezzo(13, 4, "unione_edificio", T2, verso="S"),
        Tratto(13, 5, 3, "S", T2, ("silicon", "titanium-plate")),
        Giacimento(15, 0, 2, 3, "titanium-ore", 13), Blocco(15, 0, "miner-advanced", "Al lavoro, con corrente"),
        Pezzo(14, 0, "curva", T1, da="W", a="S"), Pezzo(14, 1, "unione", T1, verso="S", lato="left"),
        Tratto(14, 2, 2, "S", T1, ("titanium-ore",)),
        Blocco(14, 4, "smelter", "Al lavoro"),
        *_margine(3, 8), Linea(((17.5, 3.5), (15, 5))), Linea(((17.5, 3.5), (16, 1))),
    ], seme=4),
    # 06 interfaccia: the second line bridges over the palette capture; lamps lit from the margin node.
    Scena("interfaccia", 8, "prati", [
        Tratto(0, 0, 8, "S", T1, ("copper-wire",)),
        Tratto(13, 0, 2, "S", T2, ("silicon", "titanium-plate")), Pezzo(13, 2, "ponte", n=4, verso="S"),
        Tratto(13, 6, 2, "S", T2, ("silicon", "titanium-plate")),
        Blocco(12, 1, "lamp", "Accesa"), Blocco(11, 6, "lamp", "Accesa"),
        *_margine(4, 8), Linea(((17.5, 4.5), (12.5, 1.5))), Linea(((17.5, 4.5), (11.5, 6.5))),
    ], seme=5),
    # 07 scarica: both lines turn into the Core T3; the market screen (content) hangs from the margin node.
    Scena("scarica", 11, "prati", [
        Tratto(0, 0, 4, "S", T1, ("copper-wire",)), Pezzo(0, 4, "curva", T1, da="S", a="E"),
        Tratto(1, 4, 6, "E", T1, ("copper-wire",)),
        Tratto(13, 0, 4, "S", T2, ("silicon", "titanium-plate")), Pezzo(13, 4, "curva", T2, da="S", a="W"),
        Tratto(12, 4, 2, "W", T2, ("silicon", "titanium-plate")),
        Blocco(7, 2, "core-3", "Luce del nucleo"),
        *_margine(1, 11, verso_sotto=False), Linea(((17.5, 1.5), (15, 7.8))),
    ], seme=6),
]
