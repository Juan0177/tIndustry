"""Blocks as the game draws them: layers, motions and states, mirrored from the Godot client.

BLOCCHI is the table the assets page builds its composed cards from: one entry per block, each with
its layers (sprite, position, how it animates, which state shows it) and its named states. Every entry
cites the C# file it mirrors; when the client moves something in code (assembler arms, smelter pulse,
bridge chevrons), the motion is reproduced here frame by frame at the same speed.

Images are composed at the in-game scale: SCALA = 2 screen pixels per art pixel (World.TileSize 64,
Sprites.PixelsPerCell 32, Nearest filter). Shadows (graphics setting "Ombre") and the G3 particle
effects are left out: they belong to the ground and the effect manager, not to the block. Glows use
the plain intensity (BuildingPad.GlowBoost = 1, i.e. the "Bagliore" setting off).
"""
import json
import math
import os
from dataclasses import dataclass, field

from PIL import Image

SITO_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(SITO_DIR, "..", ".."))

T = 32  # art pixels per cell (Sprites.PixelsPerCell)
SCALA = 2  # screen pixels per art pixel (World.TileSize / Sprites.PixelsPerCell)
TICK = 1 / 60  # simulation and frame rate the client is tuned for

with open(os.path.join(REPO, "data", "content.json"), encoding="utf-8") as _fh:
    _CONTENT = json.load(_fh)
# Belt tiers scroll at their item rate: MindustryBeltVisual advances `rate` cells per second and shows
# frame (int)(scroll * count) % count, count being the strip's frame count (one per pixel of tread), so the
# strip plays at rate * count frames per second and one loop is one cell, 1 / rate s.
VELOCITA_NASTRO = {c["id"]: c["rateItemsPerSecond"] for c in _CONTENT["conveyors"]}
ASSETS = os.path.join(REPO, "godot", "assets")
PASSO_MINIMO = 0.02  # s: GIF delays below 2/100 s are not honoured by browsers


def _fotogrammi(nome):
    """Frame count of a strip of godot/assets/: square frames side by side along the width."""
    with Image.open(os.path.join(ASSETS, nome + ".png")) as im:
        return max(1, im.width // im.height)


def _num(v):
    """Number as the Italian interface writes it (decimal comma)."""
    return f"{v:g}".replace(".", ",")


def _colore(r, g, b):
    """Godot Color (0-1 floats) to 8-bit RGB."""
    return tuple(int(math.floor(v * 255 + 0.5)) for v in (r, g, b))


def _arrotonda(v):
    """Mathf.Round: half away from zero."""
    return math.floor(abs(v) + 0.5) * (1 if v >= 0 else -1)


@dataclass
class Strato:
    """One layer, drawn in list order (later = on top, as the client's child order and ZIndex give)."""
    sprite: str | None = None  # PNG name in godot/assets/; None = drawn by `disegna`
    fps: float = 0  # strip ("-anim") frames per second while the state moves; 0 = frame 0
    quando: str | None = None  # flag the state must have for the layer to show
    flip: bool = False  # mirrored left-right (Sprite2D.FlipH)
    ruota: int = 0  # clockwise quarter turns in degrees (Sprite2D.RotationDegrees)
    cella: tuple = (0, 0)  # top-left cell of the layer inside the block
    centrato: bool = False  # native-size sprite centred on its cell (not stretched over the footprint)
    ripeti: int = 0  # >0: the sprite tiled along x over this many art px, centred across its row
    sposta: object = None  # (t, flags) -> (dx, dy) in screen px
    tinta: object = None  # t -> (rgb multiplier, alpha multiplier) (CanvasItem.Modulate)
    disegna: object = None  # (image, t, flags) -> None, for layers the client draws in code
    extra: bool = False  # added by the client on top of the art: not part of the registry icon


@dataclass
class Stato:
    nome: str
    flag: frozenset = frozenset()
    durata: float = 0  # loop length in seconds; 0 = still image
    passo: float = 0  # seconds between two rendered frames


@dataclass
class Blocco:
    id: str
    fonte: str  # C# file this entry mirrors
    celle: tuple  # footprint in cells (w, h)
    strati: list
    stati: list
    parti: list  # [(sprite, role)] shown in the parts modal, in order
    icona: str | None = None  # registry icon sprite (the block's <id>.png), when it is a composition
    stato_icona: str | None = None  # state whose first frame, without client extras, is the icon
    nota: str = ""
    fps_parti: dict = field(default_factory=dict)  # strip -> fps, for the modal


def _stato(nome, *flag, durata=0.0, passo=0.0):
    return Stato(nome, frozenset(flag), durata, passo)


# ---------------------------------------------------------------- programmatic layers

def _punto_in(poligono, x, y):
    """Even-odd test, the coverage rule of a filled polygon at a pixel centre."""
    dentro = False
    n = len(poligono)
    for i in range(n):
        (x0, y0), (x1, y1) = poligono[i], poligono[(i + 1) % n]
        if (y0 > y) != (y1 > y) and x < x0 + (y - y0) * (x1 - x0) / (y1 - y0):
            dentro = not dentro
    return dentro


def _riempi(im, poligono, colore):
    """Polygon2D without antialiasing: every pixel whose centre is inside."""
    xs, ys = [p[0] for p in poligono], [p[1] for p in poligono]
    for y in range(max(0, int(min(ys))), min(im.height, int(max(ys)) + 2)):
        for x in range(max(0, int(min(xs))), min(im.width, int(max(xs)) + 2)):
            if _punto_in(poligono, x + 0.5, y + 0.5):
                im.putpixel((x, y), colore + (255,))


# ExtractorVisual: filter dot, a 12-sided Polygon2D of radius 0.11 cell in the centre well; ItemPalette.IdleGrey
# without a filter, else ItemVisualColors.Rgb(item) (src/TIndustry.Shared/Content/ItemVisualColors.cs).
GRIGIO_FILTRO = _colore(0.42, 0.44, 0.47)
COLORE_RAME = (232, 128, 48)  # ItemVisualColors: copper-ore


def _punto_filtro(im, t, flag):
    lato = T * SCALA
    r = lato * 0.11
    c = lato / 2
    poligono = [(c + math.cos(i * math.tau / 12) * r, c + math.sin(i * math.tau / 12) * r) for i in range(12)]
    _riempi(im, poligono, COLORE_RAME if "filtro" in flag else GRIGIO_FILTRO)


# BridgeSpanVisual: 3 chevrons over the middle 55 % of the span, lit one after the other (0, 1, 2, all off)
# every CycleSeconds / 4; colour Blu4. Span here: entry at cell 0, exit at cell 3 (BeltGrid span 2-5).
PONTE_CELLE = 4
PONTE_CICLO = 0.9
BLU4 = _colore(0.525, 0.655, 0.722)


def _frecce_ponte(im, t, flag):
    fase = int(t / (PONTE_CICLO / 4) + 1e-9) % 4
    if fase >= 3:
        return
    lungo = PONTE_CELLE * T * SCALA
    meta_spessore = T * SCALA * (22 / T) * 0.5  # ThicknessScale = DeckPixels / PixelsPerCell
    interno = lungo * 0.55
    cx = -interno * 0.5 + (fase + 0.5) / 3 * interno + lungo / 2
    cy = T * SCALA / 2
    h = meta_spessore * 0.55
    _riempi(im, [(cx + 7, cy), (cx - 6, cy - h), (cx - 6 * 0.35, cy), (cx - 6, cy + h)], BLU4)


# World.AddCoreVisual: cyan Line2D (width 2) round the footprint. It is centred on the edge, so only its
# inner pixel falls on the block; the outer one lies on the ground and is left out here.
COLORE_BORDO_CORE = _colore(0.35, 0.78, 0.9)


def _bordo_core(im, t, flag):
    w, h = im.size
    for x in range(w):
        im.putpixel((x, 0), COLORE_BORDO_CORE + (255,))
        im.putpixel((x, h - 1), COLORE_BORDO_CORE + (255,))
    for y in range(h):
        im.putpixel((0, y), COLORE_BORDO_CORE + (255,))
        im.putpixel((w - 1, y), COLORE_BORDO_CORE + (255,))


# ---------------------------------------------------------------- programmatic motions

def _polso_forno(t):
    """SmelterVisual: _pulse += delta * 3.6 while crafting (phase 0, as in playtests);
    Modulate = (0.85 + 0.3 wave) light, alpha 0.8 + 0.2 wave."""
    onda = 0.5 + 0.5 * math.sin(t * 3.6)
    return 0.85 + 0.3 * onda, 0.8 + 0.2 * onda


# AssemblerVisual: _press moves toward 1 while crafting (speed 2.8) and toward 0 when idle (3.5), by
# Max(delta, 1/30) * speed per frame; arm offset = Round(Lerp(0.28, 0.11, press) * span / artPixel) * artPixel.
# The "work cycle" state crafts for 1.25 s then idles for 0.75 s, at the client's 60 frames per second.
ASSEMBLATORE_CICLO = 2.0
ASSEMBLATORE_LAVORO = 1.25


def _pressa(tick):
    p = 0.0
    for k in range(tick):
        lavora = k * TICK < ASSEMBLATORE_LAVORO
        passo = max(TICK, 1 / 30) * (2.8 if lavora else 3.5)
        p = min(1.0, p + passo) if lavora else max(0.0, p - passo)
    return p


def _braccio(verso):
    def sposta(t, flag):
        if "ciclo" in flag:
            p = _pressa(int(_arrotonda(t / TICK)) % int(_arrotonda(ASSEMBLATORE_CICLO / TICK)))
        else:
            p = 1.0 if "chiuso" in flag else 0.0
        span = 2 * T * SCALA
        x = _arrotonda((0.28 + (0.11 - 0.28) * p) * span / SCALA) * SCALA
        return verso * x, 0
    return sposta


# ---------------------------------------------------------------- the table

def _minatore(bid):
    # godot/scripts/visuals/buildings/MinerVisual.cs: base, drill strip at 12 fps while working (Efficiency > 0),
    # for the advanced miner the lamp shown while powered.
    strati = [Strato(bid + "-base"), Strato(bid + "-drill-anim", fps=12)]
    parti = [(bid, "icona"), (bid + "-base", "base ferma"), (bid + "-drill-anim", "trivella, gira al lavoro")]
    giro = 8 / 12
    if bid == "miner":
        stati = [_stato("Al lavoro", durata=giro, passo=1 / 12), _stato("Fermo")]
        nota = "Fermo: fuori da un giacimento."
        stato_icona = "Al lavoro"
    else:
        strati.append(Strato("miner-advanced-lamp", quando="corrente"))
        parti.append(("miner-advanced-lamp", "lampada, accesa con corrente"))
        stati = [_stato("Al lavoro, con corrente", "corrente", durata=giro, passo=1 / 12),
                 _stato("Al lavoro, senza corrente", durata=giro, passo=1 / 12),
                 _stato("Fermo, con corrente", "corrente"), _stato("Fermo, senza corrente")]
        nota = "La corrente accende la lampada e raddoppia la velocità."
        stato_icona = "Al lavoro, senza corrente"
    return Blocco(bid, "godot/scripts/visuals/buildings/MinerVisual.cs", (2, 2), strati, stati, parti,
                  icona=bid, stato_icona=stato_icona, nota=nota, fps_parti={bid + "-drill-anim": 12})


def _nastro(tier):
    # godot/scripts/visuals/belts/MindustryBeltVisual.cs (shared tier clock), ScrollingBeltStrip.cs (straight,
    # T merge and side joins, East), BeltCornerTile.cs (corner art: in from West, out South). The belt always scrolls; with
    # the setting "Animazioni dei blocchi" off it holds its frame.
    n = _fotogrammi(tier + "-anim")
    fps = VELOCITA_NASTRO[tier] * n
    giro = n / fps
    # Whole strip frames per rendered frame, as many as keep the GIF delay at PASSO_MINIMO or more.
    passo = math.ceil(PASSO_MINIMO * fps) / fps
    unioni = [("join-left", "Unione a sinistra", "unione laterale (blocco col punto): dietro e a sinistra"),
              ("join-right", "Unione a destra", "unione laterale (blocco col punto): dietro e a destra"),
              ("join-both", "Unione dai tre lati", "unione da nastri: dietro e ai due lati"),
              ("join-building-both", "Edifici ai due lati", "unione con un edificio ai lati: dietro e ai due lati")]
    return Blocco(
        tier, "godot/scripts/visuals/belts/MindustryBeltVisual.cs", (1, 1),
        [Strato(tier + "-anim", fps=fps, quando="dritto"), Strato(tier + "-corner-anim", fps=fps, quando="curva"),
         Strato(tier + "-t-anim", fps=fps, quando="t")]
        + [Strato(f"{tier}-{u}-anim", fps=fps, quando=u) for u, _, _ in unioni],
        [_stato("Dritto", "dritto", durata=giro, passo=passo), _stato("Curva", "curva", durata=giro, passo=passo),
         _stato("T", "t", durata=giro, passo=passo)]
        + [_stato(nome, u, durata=giro, passo=passo) for u, nome, _ in unioni]
        + [_stato("Animazioni spente", "dritto")],
        [(tier, "icona"), (tier + "-anim", "tratto dritto"), (tier + "-corner-anim", "curva"),
         (tier + "-t-anim", "unione a T: due ingressi ai lati")]
        + [(f"{tier}-{u}-anim", parte) for u, _, parte in unioni],
        icona=tier, stato_icona="Dritto",
        nota=f"{_num(VELOCITA_NASTRO[tier])} {'oggetto' if VELOCITA_NASTRO[tier] == 1 else 'oggetti'} al secondo: "
             f"{fps:g} fotogrammi al secondo.",
        fps_parti={tier + "-anim": fps, tier + "-corner-anim": fps, tier + "-t-anim": fps}
        | {f"{tier}-{u}-anim": fps for u, _, _ in unioni})


def _core(n):
    # godot/scripts/world/World.Visuals.cs (AddCoreVisual, CoreLightFrame): body, nucleus light strip at
    # 2.5 fps on top (each frame replaces the nucleus), cyan outline. In game only core-1 for now.
    corpo = f"core-{n}"
    luce = f"core-{n}-light-anim"
    return Blocco(
        corpo, "godot/scripts/world/World.Visuals.cs", (n + 1, n + 1),
        [Strato(corpo), Strato(luce, fps=2.5), Strato(disegna=_bordo_core, extra=True)],
        [_stato("Luce del nucleo", durata=4 / 2.5, passo=1 / 2.5), _stato("Animazioni spente")],
        [(corpo, "corpo (e icona)"), (luce, "luce del nucleo, sostituisce il nucleo")],
        icona=corpo, stato_icona="Luce del nucleo",
        nota="" if n == 1 else "Livello non ancora in gioco: solo sprite.",
        fps_parti={luce: 2.5})


BLOCCHI = [
    _minatore("miner"),
    _minatore("miner-advanced"),
    # godot/scripts/visuals/buildings/SmelterVisual.cs: base; while crafting the mouth strip at 8 fps with a
    # pulsing Modulate. 3.5 s = 28 glow frames and almost exactly two pulses (3.6 rad/s), so the loop is seamless.
    Blocco("smelter", "godot/scripts/visuals/buildings/SmelterVisual.cs", (2, 2),
           [Strato("smelter-base"), Strato("smelter-glow-anim", fps=8, quando="lavoro", tinta=_polso_forno)],
           [_stato("Al lavoro", "lavoro", durata=3.5, passo=1 / 16), _stato("Fermo")],
           [("smelter", "icona"), ("smelter-base", "base ferma"), ("smelter-glow-anim", "bocca accesa, solo al lavoro")],
           icona="smelter", stato_icona="Al lavoro", fps_parti={"smelter-glow-anim": 8},
           nota="La bocca pulsa mentre fonde."),
    # godot/scripts/visuals/buildings/AssemblerVisual.cs: base and two arms (the right one mirrored) that close
    # while crafting and open when idle.
    Blocco("assembler", "godot/scripts/visuals/buildings/AssemblerVisual.cs", (2, 2),
           [Strato("assembler-base"), Strato("assembler-arm", sposta=_braccio(-1)),
            Strato("assembler-arm", flip=True, sposta=_braccio(1))],
           [_stato("Ciclo di lavoro", "ciclo", durata=ASSEMBLATORE_CICLO, passo=2 * TICK),
            _stato("Al lavoro", "chiuso"), _stato("Fermo")],
           [("assembler", "icona"), ("assembler-base", "base ferma"), ("assembler-arm", "braccio (il destro è specchiato)")],
           icona="assembler", stato_icona="Fermo", nota="Le braccia si chiudono mentre lavora."),
    # godot/scripts/visuals/buildings/ExtractorVisual.cs: the sprite and the filter dot in its centre well.
    Blocco("extractor", "godot/scripts/visuals/buildings/ExtractorVisual.cs", (1, 1),
           [Strato("extractor"), Strato(disegna=_punto_filtro, extra=True)],
           [_stato("Senza filtro"), _stato("Filtro: rame", "filtro")],
           [("extractor", "blocco (e icona); il punto del filtro lo disegna il gioco")],
           icona="extractor", stato_icona="Senza filtro"),
    # godot/scripts/visuals/buildings/GeneratorVisual.cs: base and rotor strip at 10 fps while generating; idle it
    # stops where it is.
    Blocco("generator", "godot/scripts/visuals/buildings/GeneratorVisual.cs", (2, 2),
           [Strato("generator-base"), Strato("generator-rotor-anim", fps=10)],
           [_stato("In funzione", durata=4 / 10, passo=1 / 10), _stato("Fermo")],
           [("generator", "icona"), ("generator-base", "base ferma"), ("generator-rotor-anim", "rotore, gira in funzione")],
           icona="generator", stato_icona="In funzione", fps_parti={"generator-rotor-anim": 10}),
    # godot/scripts/visuals/buildings/LampVisual.cs: base and the lit dome while it gets power.
    Blocco("lamp", "godot/scripts/visuals/buildings/LampVisual.cs", (1, 1),
           [Strato("lamp-base"), Strato("lamp-glow", quando="accesa")],
           [_stato("Accesa", "accesa"), _stato("Spenta")],
           [("lamp", "icona"), ("lamp-base", "base, cupola spenta"), ("lamp-glow", "cupola accesa")],
           icona="lamp", stato_icona="Accesa", nota="Di notte fa anche luce attorno."),
    *[_nastro(t) for t in ("conveyor-basic", "conveyor-fast", "conveyor-express")],
    # godot/scripts/visuals/belts/MindustryBeltVisual.cs (AddSpecial): the open crossing and, on every side
    # without an input or output (IGameView.IsJunctionSideConnected), the North rail piece turned to that side.
    Blocco("junction", "godot/scripts/visuals/belts/MindustryBeltVisual.cs", (1, 1),
           [Strato("junction-base"), Strato("junction-bordo", quando="chiuso-ns"),
            Strato("junction-bordo", quando="chiuso-ns", ruota=180)],
           [_stato("Incrocio a 4 vie"), _stato("Solo orizzontale", "chiuso-ns")],
           [("junction", "icona: incrocio aperto su quattro lati"), ("junction-base", "base aperta"),
            ("junction-bordo", "binario di un lato senza collegamento (disegnato a Nord, ruotato)")],
           icona="junction", stato_icona="Incrocio a 4 vie",
           nota="Un lato senza ingresso né uscita tiene il binario."),
    # godot/scripts/visuals/belts/MindustryBeltVisual.cs (AddSpecial): sorter sprite and, upright on top, the
    # 10 px belt sprite of the filtered item (BeltGrid.DefaultSorterFilter = iron-ore).
    Blocco("sorter", "godot/scripts/visuals/belts/MindustryBeltVisual.cs", (1, 1),
           [Strato("sorter"), Strato("iron-ore-belt", centrato=True, extra=True)],
           [_stato("Filtro: ferro")],
           [("sorter", "blocco (e icona)"), ("iron-ore-belt", "oggetto filtrato, al centro")],
           icona="sorter", stato_icona="Filtro: ferro"),
    # godot/scripts/visuals/belts/BridgeSpanVisual.cs and MindustryBeltVisual.cs: deck tiled over the whole span
    # (z 2), the chevrons on it, both ends on top (z 3).
    Blocco("conveyor-bridge", "godot/scripts/visuals/belts/BridgeSpanVisual.cs", (PONTE_CELLE, 1),
           [Strato("conveyor-bridge-span", ripeti=PONTE_CELLE * T), Strato(disegna=_frecce_ponte, extra=True),
            Strato("conveyor-bridge"), Strato("conveyor-bridge", cella=(PONTE_CELLE - 1, 0))],
           [_stato("In funzione", durata=PONTE_CICLO, passo=PONTE_CICLO / 4), _stato("Fotogramma fermo")],
           [("conveyor-bridge", "estremità (e icona)"), ("conveyor-bridge-span", "16 px di impalcato, ripetuti")],
           nota="Ponte su 3 celle: le frecce si accendono in fila."),
    *[_core(n) for n in range(1, 5)],
]


# ---------------------------------------------------------------- rendering

class Immagini:
    """Sprite PNGs of godot/assets/, loaded once."""

    def __init__(self, cartella):
        self.cartella = cartella
        self._cache = {}

    def __call__(self, nome):
        if nome not in self._cache:
            self._cache[nome] = Image.open(os.path.join(self.cartella, nome + ".png")).convert("RGBA")
        return self._cache[nome]

    def fotogrammi(self, nome):
        im = self(nome)
        lato = im.height
        return [im.crop((i * lato, 0, (i + 1) * lato, lato)) for i in range(max(1, im.width // lato))]


def _ingrandisci(im):
    return im.resize((im.width * SCALA, im.height * SCALA), Image.NEAREST)


def componi(blocco, stato, t, immagini, grezzo=False):
    """Frame of `blocco` in `stato` at time t (s), at the in-game scale. grezzo: without the client extras and
    tints, i.e. only the art layers (what the registry icon composes)."""
    w, h = blocco.celle
    out = Image.new("RGBA", (w * T * SCALA, h * T * SCALA), (0, 0, 0, 0))
    for s in blocco.strati:
        if s.quando and s.quando not in stato.flag:
            continue
        if grezzo and s.extra:
            continue
        if s.disegna:
            s.disegna(out, t, stato.flag)
            continue
        if s.sprite.endswith("-anim"):
            frames = immagini.fotogrammi(s.sprite)
            im = frames[int(t * s.fps + 1e-9) % len(frames)]
        else:
            im = immagini(s.sprite)
        if s.flip:
            im = im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        if s.ruota:
            im = im.rotate(-s.ruota)
        if s.ripeti:
            fila = Image.new("RGBA", (s.ripeti, im.height), (0, 0, 0, 0))
            for x in range(0, s.ripeti, im.width):
                fila.paste(im, (x, 0))
            im = fila
        im = _ingrandisci(im)
        if s.tinta and not grezzo:
            luce, alfa = s.tinta(t)
            r, g, b, a = im.split()
            r, g, b = (c.point(lambda v: min(255, int(v * luce + 0.5))) for c in (r, g, b))
            a = a.point(lambda v: int(v * alfa + 0.5))
            im = Image.merge("RGBA", (r, g, b, a))
        x0, y0 = s.cella[0] * T * SCALA, s.cella[1] * T * SCALA
        if s.centrato or s.ripeti:
            # Centred on its cell (Sprite2D Centered at the cell centre); a tiled row spans from x0.
            if not s.ripeti:
                x0 += (T * SCALA - im.width) // 2
            y0 += (T * SCALA - im.height) // 2
        dx, dy = s.sposta(t, stato.flag) if s.sposta else (0, 0)
        livello = Image.new("RGBA", out.size, (0, 0, 0, 0))
        livello.paste(im, (int(x0 + dx), int(y0 + dy)))
        out.alpha_composite(livello)
    return out


def fotogrammi_stato(blocco, stato, immagini):
    """[(frame, duration ms)] of one state: a single still frame, or the whole loop with centisecond
    durations rounded on the running total (GIF delays are in 1/100 s), so the loop keeps its length."""
    if not stato.durata:
        return [(componi(blocco, stato, 0.0, immagini), 0)]
    n = int(_arrotonda(stato.durata / stato.passo))
    out = []
    for k in range(n):
        inizio = int(_arrotonda(k * stato.passo * 100))
        fine = int(_arrotonda((k + 1) * stato.passo * 100))
        out.append((componi(blocco, stato, k * stato.passo, immagini), (fine - inizio) * 10))
    return out
