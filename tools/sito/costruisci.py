"""Build the tIndustry website (GitHub Pages) into _site/.

    python tools/sito/costruisci.py            # writes _site/
    python tools/sito/costruisci.py --out DIR  # somewhere else

Pages:
- index.html           the landing page, a factory of tIndustry: every section is a floor of 18 cells
                       (fabbrica.py: belts with the real pieces and items, blocks, deposits, power, terrain)
                       with the text, the CRT frames and the buttons laid over it; two long chains run down
                       the sides into the Core, where the download button is; a disturbed-signal section
                       teases the attack and defence units; a drone builds the buttons on click (sito.js; the bar's
                       Animazioni button pauses everything);
- sviluppo/index.html  how the game is made (ADRs), roadmap, work diary and decisions, read from README.md,
                       docs/diario/ and docs/decisioni/ (documenti.py);
- asset/index.html     every sprite registered in tools/art: blocks composed as the game draws them (states
                       as switches, their pieces in a pop-up), the other sprites enlarged (strips as
                       animated GIFs), one composed window of terrain per biome and the interface font.

Everything comes from the repository (sprites in godot/assets/, the registry and the font in tools/art/,
the block compositions in composti.py, data/content.json, the captures of docs/img/readme/, the documents),
so the site never needs editing by hand: change the art or the documents, rebuild. The images of the floors
are files of the site (fabbrica/), the captures too (catture/); the asset page keeps its data: URIs. One
stylesheet (stile.css, the sum of stile.css and drone.css of this folder) and one script (sito.js) are files
of the site; nothing is loaded from outside it. Published by .github/workflows/pages.yml (manual or
release tags only).
"""
import argparse
import base64
import collections
import html
import io
import json
import math
import os
import random
import re
import shutil
import sys

SITO_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(SITO_DIR, "..", ".."))
ART_DIR = os.path.join(REPO, "tools", "art")
for _d in (ART_DIR, SITO_DIR):
    if _d not in sys.path:
        sys.path.insert(0, _d)

from PIL import Image  # noqa: E402

import carattere  # noqa: E402
import composti  # noqa: E402
import documenti  # noqa: E402
import fabbrica  # noqa: E402
import tavola  # noqa: E402
from blocchi import SPRITE  # noqa: E402
from blocchi import terreno as tr  # noqa: E402

REPO_URL = documenti.REPO_URL
SITO_URL = "https://juan0177.github.io/tIndustry/"
RELEASE_URL = REPO_URL + "/releases/latest"
AMBRA = (255, 211, 127)  # UiColors.Accento, the title colour
ROSSO = (255, 42, 74)
MAGENTA = (255, 79, 216)
STATICI = ("sito.js", "racconto.js")  # copied as they are; stile.css is assembled from stile.css + drone.css
# The bar's pause/play toggle for every animation (sito.js shows it and remembers the choice); hidden
# without the script, when nothing could stop the belts anyway. Not on the asset page: its belts are
# plain GIFs, which no style can stop.
BOTTONE_ANIMAZIONI = ('<button type="button" id="animazioni" class="animazioni" data-ferma="Ferma le animazioni" '
                      'data-avvia="Avvia le animazioni" aria-label="Ferma le animazioni" title="Ferma le animazioni" hidden>'
                      '<span class="icona" aria-hidden="true"></span><span class="testo">Animazioni</span></button>')

# Families of the sprite registry (module names in tools/art/blocchi), in page order.
FAMIGLIE = {
    "produzione": "Produzione", "logistica": "Logistica", "potenza": "Potenza", "core": "Core",
    "drone": "Droni", "oggetti": "Oggetti", "effetti": "Effetti", "interfaccia": "Interfaccia",
    "terreno": "Terreno",
}

FRASI_CARATTERE = (
    "Il nastro porta 12 lingotti di rame al Core: è già più veloce!",
    "The quick brown fox jumps over the lazy dog: 0123456789 (100%)",
)

MAX_STATI = max(len(b.stati) for b in composti.BLOCCHI)

# State switches without scripts: the k-th radio of a card shows its k-th image and marks its k-th label.
_STILE_STATI = "".join(
    f".blocco>input:nth-of-type({k}):checked~.box>img:nth-of-type({k}){{display:block}}"
    f".blocco>input:nth-of-type({k}):checked~.scelte>label:nth-of-type({k})"
    "{background:var(--ambra);color:var(--bg);border-color:var(--ambra)}"
    f".blocco>input:nth-of-type({k}):focus-visible~.scelte>label:nth-of-type({k})"
    "{outline:2px solid var(--testo);outline-offset:1px}"
    for k in range(1, MAX_STATI + 1))

LENTE = ('<svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">'
         '<circle cx="10" cy="10" r="6.5"/><path d="M15 15l6 6"/></svg>')

# Captures of the README (docs/img/readme/, copied from the playtests), with the captions of the site.
CATTURE_DIR = os.path.join(REPO, "docs", "img", "readme")
CATTURE = [
    ("gioco.png", "Fabbrica: il minatore porta il minerale al CORE"),
    ("ricerca.png", "Ricerca: albero a rami dal CORE"),
    ("tutorial.png", "Campagna: 10 livelli, tutorial che non blocca"),
    ("interfaccia-en.png", "Tutti i testi anche in inglese"),
    ("interfaccia-hud.png", "HUD: fondi, energia, risorse del CORE"),
    ("interfaccia-daltonici.png", "Colori per daltonici: la X sull'anteprima rifiutata"),
    ("interfaccia-palette.png", "Palette come in Mindustry"),
    ("interfaccia-altocontrasto.png", "Alto contrasto: pannelli neri, testo bianco"),
    ("mercato.png", "Mercato: prezzi dinamici, vendita automatica"),
]
DIDASCALIE = dict(CATTURE)


def _data_url(data, mime):
    return f"data:{mime};base64," + base64.b64encode(data).decode("ascii")


def _png(im):
    buf = io.BytesIO()
    im.save(buf, "PNG")
    return buf.getvalue()


def _gif(frames, ms=110):
    """Animated GIF of RGBA frames; alpha below 128 becomes the transparent index (GIF has 1-bit alpha).
    ms: one duration for every frame or a list, one per frame; equal consecutive frames are merged."""
    durate = list(ms) if isinstance(ms, (list, tuple)) else [ms] * len(frames)
    uniti = []
    for f, d in zip(frames, durate):
        if uniti and uniti[-1][0].tobytes() == f.tobytes():
            uniti[-1][1] += d
        else:
            uniti.append([f, d])
    conv = []
    for f, _ in uniti:
        trasparente = f.getchannel("A").point(lambda v: 255 if v < 128 else 0)
        p = f.convert("RGB").convert("P", palette=Image.Palette.ADAPTIVE, colors=255)
        p.paste(255, mask=trasparente)
        conv.append(p)
    buf = io.BytesIO()
    conv[0].save(buf, "GIF", save_all=True, append_images=conv[1:], duration=[d for _, d in uniti], loop=0,
                 disposal=2, transparency=255)
    return buf.getvalue()


# ---------------------------------------------------------------- shell

NAV_VETRINA = (("#come-funziona", "Come funziona"), ("#catture", "Catture"), ("#blocchi", "Blocchi"),
               ("#ambienti", "Ambienti"), ("#interfaccia", "Interfaccia"), ("#scarica", "Scarica"))


def _nav(voci):
    return "".join(f'<a href="{html.escape(h)}">{html.escape(t)}</a>' for h, t in voci)


def _pagina(titolo, corpo, nav, radice="", classe="", descrizione="", stile_extra="", drone=False, avanza=False,
            percorso="", animazioni=True):
    """The shell: head, sticky bar with the stripes, main, footer, the script. `percorso` is the page's path
    under the site root ("" for the landing page, "sviluppo/" …) for its canonical and Open Graph URLs;
    `animazioni` puts the pause/play button in the bar (not on a page whose only motion is GIFs)."""
    extra = f"<style>{stile_extra}</style>" if stile_extra else ""
    url = SITO_URL + percorso
    meta = f'<link rel="canonical" href="{url}">'
    if descrizione:
        meta += (f'<meta name="description" content="{html.escape(descrizione)}">'
                 f'<meta property="og:title" content="{html.escape(titolo)}"><meta property="og:type" content="website">'
                 f'<meta property="og:description" content="{html.escape(descrizione)}">'
                 f'<meta property="og:image" content="{SITO_URL}catture/gioco.png"><meta property="og:url" content="{url}">'
                 '<meta name="twitter:card" content="summary_large_image">')
    meta += f'<link rel="icon" href="{radice}favicon.png" type="image/png">'
    pie = (f'<footer><a href="{radice}">Vetrina</a><a href="{radice}sviluppo/">Sviluppo</a><a href="{radice}asset/">Asset</a>'
           f'<a href="{REPO_URL}">GitHub</a>'
           + f'<span>sito <a href="{REPO_URL}/tree/main/tools/sito">generato dal repository</a>, come il gioco</span></footer>')
    testa = ('<div id="avanza" aria-hidden="true"></div>' if avanza else "")
    coda = (f'<div id="drone-cantiere" aria-hidden="true" hidden><img class="px" src="{radice}fabbrica/drone-1.png" alt="" width="48" height="48">'
            '<svg class="raggio" aria-hidden="true"><line x1="0" y1="0" x2="0" y2="0"/><circle cx="0" cy="0" r="5"/></svg></div>'
            if drone else "")
    return (f'<!doctype html><html lang="it"><head><meta charset="utf-8">'
            f'<meta name="viewport" content="width=device-width,initial-scale=1">'
            f"<title>{html.escape(titolo)}</title>{meta}"
            f'<link rel="stylesheet" href="{radice}stile.css">{extra}</head>'
            f'<body class="{classe}">{testa}<header class="barra"><a class="marca" href="{radice}">tINDUSTRY</a>'
            f"<nav>{nav}</nav>{BOTTONE_ANIMAZIONI if animazioni else ''}</header><main>{corpo}</main>{pie}{coda}"
            f'<script src="{radice}sito.js" defer></script></body></html>')


# ---------------------------------------------------------------- asset page (unchanged content)

def _famiglia(nome):
    return SPRITE[nome].__module__.rsplit(".", 1)[-1]


def famiglie(assets_dir):
    """{family: [sprite name, …]} for the registered sprites whose PNG exists, in page order."""
    per_famiglia = collections.defaultdict(list)
    for nome in sorted(SPRITE):
        if os.path.exists(os.path.join(assets_dir, nome + ".png")):
            per_famiglia[_famiglia(nome)].append(nome)
    ordine = [f for f in FAMIGLIE if f in per_famiglia] + sorted(set(per_famiglia) - set(FAMIGLIE))
    return {f: per_famiglia[f] for f in ordine}


def famiglia_blocco(blocco):
    return _famiglia(blocco.icona or blocco.parti[0][0])


def assorbiti(blocco):
    """Sprites shown only in the block's pieces pop-up: its parts of the same family (an item drawn on a
    sorter stays in the item grid too)."""
    fam = famiglia_blocco(blocco)
    return {n for n, _ in blocco.parti if _famiglia(n) == fam}


def _zoom(w, h):
    return max(1, min(4, 128 // max(w, h)))


def _immagine_sprite(assets_dir, nome, fps=None, massimo=128):
    """(img tag, info) of one sprite: a still PNG, or a GIF for an "-anim" strip."""
    im = Image.open(os.path.join(assets_dir, nome + ".png")).convert("RGBA")
    if nome.endswith("-anim") and im.width > im.height:
        lato, n = im.height, im.width // im.height
        frames = [im.crop((i * lato, 0, (i + 1) * lato, lato)) for i in range(n)]
        if fps:
            # Every `salto`-th frame, so no GIF delay falls under PASSO_MINIMO (browsers stretch shorter ones).
            salto = math.ceil(composti.PASSO_MINIMO * fps)
            frames = frames[::salto]
            durate = [int(composti._arrotonda((k + 1) * salto * 100 / fps) - composti._arrotonda(k * salto * 100 / fps)) * 10
                      for k in range(len(frames))]
        else:
            durate = 110
        url = _data_url(_gif(frames, durate), "image/gif")
        w = h = lato
        info = f"{lato}×{lato}, {n} fotogrammi" + (f", {fps:g} al secondo" if fps else "")
    else:
        url, w, h = _data_url(_png(im), "image/png"), im.width, im.height
        info = f"{w}×{h}"
    z = max(1, min(4, massimo // max(w, h)))
    alt = html.escape(nome.rsplit("/", 1)[-1])
    return f'<img src="{url}" width="{w * z}" height="{h * z}" alt="{alt}">', info


def _cella(assets_dir, nome):
    img, info = _immagine_sprite(assets_dir, nome)
    nome_breve = html.escape(nome.rsplit("/", 1)[-1])
    return (f'<figure data-sprite="{html.escape(nome)}"><div class="box">{img}</div>'
            f"<figcaption><b>{nome_breve}</b><span>{info}</span></figcaption></figure>")


def immagine_stato(blocco, stato, immagini):
    """(bytes, mime) of one state: an animated GIF when it moves, a PNG when it is still."""
    fotogrammi = composti.fotogrammi_stato(blocco, stato, immagini)
    if len(fotogrammi) == 1:
        return _png(fotogrammi[0][0]), "image/png"
    return _gif([f for f, _ in fotogrammi], [d for _, d in fotogrammi]), "image/gif"


def _scheda_blocco(assets_dir, blocco, immagini):
    bid = html.escape(blocco.id)
    w, h = blocco.celle[0] * composti.T, blocco.celle[1] * composti.T
    z = max(composti.SCALA, _zoom(w, h))  # same zoom as the plain cards, never below the in-game scale
    piu_stati = len(blocco.stati) > 1
    radio, immagini_html, etichette = [], [], []
    for k, stato in enumerate(blocco.stati):
        dati, mime = immagine_stato(blocco, stato, immagini)
        nome = html.escape(stato.nome)
        classe = ' class="st"' if piu_stati else ""
        immagini_html.append(f'<img{classe} src="{_data_url(dati, mime)}" width="{w * z}" height="{h * z}" '
                             f'alt="{bid}: {nome}" data-stato="{nome}">')
        if piu_stati:
            rid = f"s-{bid}-{k}"
            radio.append(f'<input class="sr" type="radio" name="s-{bid}" id="{rid}"{" checked" if k == 0 else ""}>')
            etichette.append(f'<label for="{rid}">{nome}</label>')
    scelte = f'<div class="scelte" role="group" aria-label="Stato">{"".join(etichette)}</div>' if piu_stati else ""
    nota = f"<span>{html.escape(blocco.nota)}</span>" if blocco.nota else ""

    pezzi = []
    for nome, ruolo in blocco.parti:
        img, info = _immagine_sprite(assets_dir, nome, blocco.fps_parti.get(nome), massimo=192)
        pezzi.append(f'<figure class="pezzo" data-sprite="{html.escape(nome)}"><div class="box">{img}</div>'
                     f'<figcaption><b>{html.escape(nome)}</b><span>{html.escape(ruolo)}</span>'
                     f"<span>{info}</span></figcaption></figure>")
    pid = f"p-{bid}"
    fonte = html.escape(blocco.fonte)
    modale = (f'<div popover id="{pid}" class="modale" role="dialog" aria-labelledby="{pid}-t">'
              f'<button type="button" class="chiudi" popovertarget="{pid}" popovertargetaction="hide" autofocus>'
              f"Chiudi</button>"
              f'<h3 id="{pid}-t">Pezzi di {bid}</h3>'
              f'<p class="nota">Il gioco li sovrappone in quest\'ordine, come in '
              f'<a href="{REPO_URL}/blob/main/{fonte}">{fonte.rsplit("/", 1)[-1]}</a>. '
              f"Esc o un clic fuori per chiudere.</p>"
              f'<div class="pezzi">{"".join(pezzi)}</div></div>')
    largo = " largo" if w * z > 160 else ""
    return (f'<figure class="blocco{largo}" id="blocco-{bid}">{"".join(radio)}'
            f'<div class="box">{"".join(immagini_html)}</div>{scelte}'
            f'<figcaption><b>{bid}</b><span>{blocco.celle[0]}×{blocco.celle[1]} celle, '
            f'{len(blocco.parti)} {"pezzo" if len(blocco.parti) == 1 else "pezzi"}</span>{nota}</figcaption>'
            f'<button type="button" class="lente" popovertarget="{pid}" aria-label="Pezzi di {bid}" '
            f'title="Pezzi di {bid}">{LENTE}</button>{modale}</figure>')


def biomi():
    """Land biomes of data/content.json (those with a wall), in tools/art order."""
    return list(tr.BIOMI)


def _sezione_ambienti(assets_dir, immagini):
    schede = []
    for b, bioma in enumerate(biomi()):
        finestra = tavola._finestra(b, bioma, immagini)
        suoli = [f["id"] for f in tr.TERRENO["floors"] if f["biome"] == bioma]
        parete = next(w["id"] for w in tr.TERRENO["walls"] if w["biome"] == bioma)
        minerali = [tr.MINERALI[(2 * b + k) % len(tr.MINERALI)] for k in range(2)]  # as tavola._finestra
        url = _data_url(_png(finestra), "image/png")
        lato = finestra.width * composti.SCALA
        schede.append(
            f'<figure data-bioma="{html.escape(bioma)}"><img src="{url}" width="{lato}" height="{lato}" '
            f'alt="Ambiente {html.escape(bioma)}"><figcaption><b>{html.escape(bioma.capitalize())}</b>'
            f"<span>Suoli: {html.escape(', '.join(suoli))}; parete: {html.escape(parete)}; "
            f"giacimenti: {html.escape(', '.join(minerali))}; acqua bassa e profonda.</span></figcaption></figure>")
    return ('<section id="ambienti"><h2>Ambienti <small>' + str(len(schede)) + "</small></h2>"
            '<p class="nota">Un pezzo di mappa 12×12 per bioma, composto con la regola del gioco '
            "(varianti, bordi, pareti, giacimenti, decorazioni), a grandezza di gioco. L'acqua è il fondale "
            "fermo: in gioco le increspature le aggiunge uno shader.</p>"
            f'<div class="ambienti">{"".join(schede)}</div></section>')


def pagina_asset(assets_dir):
    gruppi = famiglie(assets_dir)
    immagini = composti.Immagini(assets_dir)
    blocchi_per_famiglia = collections.defaultdict(list)
    nascosti = set()
    for b in composti.BLOCCHI:
        blocchi_per_famiglia[famiglia_blocco(b)].append(b)
        nascosti |= assorbiti(b)
    sezioni = []
    for f, nomi in gruppi.items():
        composte = [_scheda_blocco(assets_dir, b, immagini) for b in blocchi_per_famiglia.get(f, [])]
        semplici = [_cella(assets_dir, n) for n in nomi if n not in nascosti]
        conto = len(composte) + len(semplici)
        sezioni.append(f'<section id="{f}"><h2>{html.escape(FAMIGLIE.get(f, f))} <small>{conto}</small></h2>'
                       f'<div class="griglia">{"".join(composte + semplici)}</div></section>')
    sezioni.append(_sezione_ambienti(assets_dir, immagini))
    righe = [carattere.scrivi(FRASI_CARATTERE[0], 3)] + [carattere.scrivi(t, 2) for t in FRASI_CARATTERE]
    meta = len(carattere.INSIEME) // 2
    righe += [carattere.scrivi(carattere.INSIEME[:meta], 2), carattere.scrivi(carattere.INSIEME[meta:], 2)]
    righe_html = "".join(f'<img src="{_data_url(_png(r), "image/png")}" width="{r.width}" height="{r.height}" alt="">'
                         for r in righe)
    sezioni.append('<section id="carattere"><h2>Carattere dell\'interfaccia</h2>'
                   f'<div class="pannello carattere">{righe_html}</div></section>')
    totale = sum(len(n) for n in gruppi.values())
    animate = sum(1 for n in gruppi.values() for x in n if x.endswith("-anim"))
    menu = "".join(f'<a href="#{f}">{html.escape(FAMIGLIE.get(f, f))}</a>' for f in gruppi)
    intro = (f'<div class="intro"><h1>tIndustry · asset</h1><nav>{menu}<a href="#ambienti">Ambienti</a>'
             f'<a href="#carattere">Carattere</a></nav>'
             f'<p class="nota">{totale} sprite, {animate} animazioni, {len(composti.BLOCCHI)} blocchi '
             "composti come in gioco (stati con i bottoni, pezzi con la lente). Pixel-art originale "
             f'generata da <a href="{REPO_URL}/tree/main/tools/art">tools/art</a>, ingrandita senza filtro.</p></div>')
    nav = _nav((("../", "Vetrina"), ("../sviluppo/", "Sviluppo"), ("#produzione", "Blocchi"), ("#ambienti", "Ambienti"),
                ("#carattere", "Carattere")))
    return _pagina("tIndustry · asset", intro + "".join(sezioni), nav, radice="../", classe="asset", animazioni=False,
                   descrizione="Tutti gli sprite di tIndustry: blocchi come in gioco, nastri, oggetti, terreno, carattere.",
                   stile_extra=_STILE_STATI, percorso="asset/")


# ---------------------------------------------------------------- landing page content

def _at(x, y, w=None, h=None):
    """Position of a content element in scene px (--x/--y/--w CSS variables; absolute on desktop, stacked on phones)."""
    s = f"--x:{x:.0f}px;--y:{y:.0f}px"
    if w is not None:
        s += f";--w:{w:.0f}px"
    if h is not None:
        s += f";height:{h:.0f}px"
    return s


def _breve(testo_html, massimo=110):
    """A README cell cut down for a card: plain text up to the first semicolon, without parentheses, at most
    `massimo` characters cut at the last comma that fits; escaped again for the page."""
    testo = html.unescape(re.sub(r"<[^>]+>", "", testo_html)).split(";")[0]
    testo = re.sub(r"\s*\([^)]*\)", "", testo).strip().rstrip(",")
    if len(testo) > massimo:
        taglio = testo[:massimo]
        testo = (taglio.rsplit(",", 1)[0] if "," in taglio else taglio.rsplit(" ", 1)[0]).strip()
    return html.escape(testo)


def _titolo(a, numero, testo, x, y):
    im = carattere.scrivi(testo, k=4, colore=AMBRA)
    url = a.png("titolo-" + fabbrica._slug(testo), im)
    return (f'<div class="titolo" data-rivela style="{_at(x, y)}"><span class="etichetta">// {numero}</span>'
            f'<h2><img class="px" src="{url}" alt="{html.escape(testo)}" width="{im.width}" height="{im.height}"></h2></div>')


def _crt(radice, nome, x, y, w, h, extra="", classe=""):
    didascalia = html.escape(DIDASCALIE[nome])
    return (f'<figure class="crt {classe}" data-rivela style="{_at(x, y, w, h)}{extra}">'
            f'<a href="{radice}catture/{nome}" title="Apri la cattura intera"><img src="{radice}catture/{nome}" '
            f'alt="" width="1280" height="720" loading="lazy"></a><figcaption>{didascalia}</figcaption></figure>')


def _grafico_mercato(a, prezzo, nome_oggetto):
    """The market screen: a price line that rises and falls forever (seeded walk, duplicated for a seamless loop)."""
    rnd = random.Random(42)
    v, punti = 60.0, []
    for _ in range(48):
        v = min(105, max(15, v + rnd.uniform(-9, 9)))
        punti.append(v)
    # The price follows the line: point i of the walk is worth prezzo * value / 60 (two digits). sito.js reads where
    # the scrolling line has got to and turns the digit wheels to the point at its right edge.
    prezzi = [max(1, min(99, round(prezzo * p / 60))) for p in punti]
    rulli = "".join(f'<span class="rullo"><span class="cifre" style="--c:{c}">'
                    + "".join(f"<span>{k}</span>" for k in range(10)) + "</span></span>"
                    for c in f"{prezzi[-1]:02d}")
    punti += punti
    linea = " ".join(f"{i * 10},{120 - p:.1f}" for i, p in enumerate(punti))
    area = f"M0,120 L{linea.replace(' ', ' L')} L{(len(punti) - 1) * 10},120 Z"
    griglia = "".join(f'<line x1="0" y1="{y}" x2="470" y2="{y}"/>' for y in (30, 60, 90))
    return (f'<figure class="crt schermo" data-rivela style="{_at(806, 384, 314, 230)}"><div class="schermo-testa">'
            f'<img class="px" src="{a.sprite("ui/ui-mercato")}" alt="" width="32" height="32"><b>Mercato</b>'
            f'<span>{html.escape(nome_oggetto)} <span class="prezzo" data-prezzi="{",".join(map(str, prezzi))}" '
            f'aria-hidden="true">${rulli}</span><span class="sr">${prezzo}</span></span><i class="live"></i></div>'
            f'<svg viewBox="0 0 470 120" preserveAspectRatio="none" aria-hidden="true"><g class="griglia">{griglia}</g>'
            f'<g class="scorre"><path class="area" d="{area}"/><polyline points="{linea}"/></g></svg>'
            '<figcaption>Il prezzo cala mentre vendi e risale piano</figcaption></figure>')


def _crepa(seme, lato=220):
    """Shattered glass as inline SVG: a hole, radial cracks with kinks, two jagged rings (seeded)."""
    rnd = random.Random(seme)
    cx = cy = lato / 2
    raggi = []
    n = rnd.randint(9, 13)
    for i in range(n):
        ang = i * math.tau / n + rnd.uniform(-0.25, 0.25)
        lungo = rnd.uniform(0.45, 1.0) * lato / 2
        pts, r = [(cx, cy)], 0.0
        while r < lungo:
            r += rnd.uniform(12, 26)
            a2 = ang + rnd.uniform(-0.18, 0.18)
            pts.append((cx + math.cos(a2) * r, cy + math.sin(a2) * r))
        raggi.append('<polyline points="' + " ".join(f"{x:.1f},{y:.1f}" for x, y in pts) + '"/>')
    anelli = []
    for frazione in (0.22, 0.42):
        pts = []
        for i in range(n):
            ang = i * math.tau / n + rnd.uniform(-0.2, 0.2)
            r = frazione * lato / 2 * rnd.uniform(0.8, 1.2)
            pts.append((cx + math.cos(ang) * r, cy + math.sin(ang) * r))
        anelli.append('<path class="riflesso" d="M' + " L".join(f"{x:.1f},{y:.1f}" for x, y in pts) + ' Z"/>')
    foro = '<path class="foro" d="M' + " L".join(
        f"{cx + math.cos(i * math.tau / 7) * rnd.uniform(4, 7):.1f},{cy + math.sin(i * math.tau / 7) * rnd.uniform(4, 7):.1f}"
        for i in range(7)) + ' Z"/>'
    return f'<svg viewBox="0 0 {lato} {lato}" aria-hidden="true">{"".join(raggi)}{"".join(anelli)}{foro}</svg>'


def _numeri():
    """The numbers of the game, from data/: structures, belt tiers, recipes, minerals, biomes, campaign levels."""
    c = composti._CONTENT
    with open(os.path.join(REPO, "data", "campaign.json"), encoding="utf-8") as fh:
        livelli = len(json.load(fh)["levels"])
    tier = sum(1 for s in c["structures"] if s["id"].startswith("conveyor-") and s["id"] != "conveyor-bridge")
    voci = ((len(c["structures"]), "blocchi"), (tier, "tier di nastri"), (len(c["recipes"]), "ricette"),
            (len(tr.MINERALI), "minerali"), (len(tr.BIOMI), "biomi"), (livelli, "livelli di campagna"))
    return (f'<ul class="numeri" style="{_at(192, 204, 600)}">'
            + "".join(f'<li data-rivela><b>{n}</b><span>{t}</span></li>' for n, t in voci) + "</ul>")


AVVISI = (("ATTENZIONE", "", 700, 36, -0.0), ("PERICOLO", "magenta", 860, 150, -1.8),
          ("UNITÀ OSTILI IN AVVICINAMENTO", "", 540, 236, -3.6), ("DIFESE NON DISPONIBILI", "magenta", 240, 320, -5.4),
          ("INTRUSIONE RILEVATA", "", 820, 300, -7.2))


def _contenuto(a, sid, radice, versione):
    """The content of a landing-page section, in scene coordinates."""
    if sid == "hero":
        logo = a.immagini("ui/ui-logo")
        url = a.png("logo-x8", logo.resize((logo.width * 8, logo.height * 8), Image.NEAREST))
        return (f'<div class="hero-testo"><h1><img class="px logo" src="{url}" alt="tIndustry" width="{logo.width * 8}" '
                f'height="{logo.height * 8}"></h1>'
                "<p>Estrai i minerali, portali al CORE sui nastri, fondili in lastre e fili, vendi al Mercato "
                "e reinvesti in Ricerca per sbloccare nuovi blocchi.</p>"
                '<p class="nota">Logistica industriale ispirata a Mindustry e Tiny Industry. Gioco gratuito e open source '
                "per Windows e Linux, in italiano e in inglese.</p>"
                f'<p class="bottoni"><a class="btn primario" href="{RELEASE_URL}" data-costruisci>Scarica</a>'
                f'<a class="btn" href="{REPO_URL}" data-costruisci>Codice su GitHub</a></p></div>'
                '<p class="scorri etichetta">scorri ▼</p>')
    if sid == "come-funziona":
        schede = "".join(f'<div class="scheda" data-rivela><b>{area}</b><span>{_breve(testo)}</span></div>'
                         for area, testo in documenti.contenuti())
        return (_titolo(a, "02", "Come funziona", 336, 22)
                + f'<p class="nota" style="{_at(336, 100, 170)}">Il minerale arriva dall\'alto, il forno lo fonde col carbone '
                  "e la lastra continua lungo il bordo. In partita è la stessa catena: minatore, nastro, forno, assemblatore, CORE.</p>"
                + f'<div class="schede" style="{_at(512, 128, 576)}">{schede}</div>')
    if sid == "catture":
        return (_titolo(a, "03", "Catture", 96, 22)
                + _crt(radice, "gioco.png", 29, 205, 397, 230, ";z-index:2", classe="sotto-ponte")
                + _crt(radice, "ricerca.png", 448, 64, 320, 218)
                + _crt(radice, "tutorial.png", 64, 461, 352, 166)
                + _crt(radice, "interfaccia-en.png", 448, 307, 280, 186))
    if sid == "blocchi":
        # Three rows at the scale of the game (one cell = 64 px), each row entering left to right as it scrolls in.
        righe = ((("miner", "Al lavoro", "Minatore T1"), ("miner-advanced", "Al lavoro, con corrente", "Minatore T2"),
                  ("smelter", "Al lavoro", "Forno"), ("assembler", "Ciclo di lavoro", "Assemblatore")),
                 (("extractor", "Filtro: rame", "Estrattore"), ("generator", "In funzione", "Generatore"),
                  ("lamp", "Accesa", "Lampada"), ("junction", "Incrocio a 4 vie", "Incrocio"),
                  ("core-1", "Luce del nucleo", "CORE T1")),
                 (("conveyor-bridge", "In funzione", "Ponte"), ("core-2", "Luce del nucleo", "CORE T2")))
        figure = "".join(
            f'<figure data-rivela style="--i:{i}"><img class="px" src="{a.blocco(b, s)}" alt="{html.escape(n)}" '
            f'width="{fabbrica._blocco(b).celle[0] * fabbrica.C}" height="{fabbrica._blocco(b).celle[1] * fabbrica.C}">'
            f'<figcaption>{html.escape(n)}</figcaption></figure>'
            for riga in righe for i, (b, s, n) in enumerate(riga))
        return (_titolo(a, "04", "Blocchi come in gioco", 192, 22)
                + f'<p class="nota" style="{_at(192, 96, 620)}">Gli stessi sprite e le stesse animazioni che vedi in partita: '
                  "pixel-art originale del gioco.</p>"
                + _numeri()
                + f'<div class="vetrina-blocchi" style="{_at(192, 300, 608)}">{figure}'
                  f'<a class="rimando" href="{radice}asset/" data-costruisci>Tutti i blocchi e gli sprite → Asset</a></div>')
    if sid == "sotto-attacco":
        tit = carattere.scrivi("Sotto attacco", k=4, colore=AMBRA)
        sagome = "".join(
            f'<img class="px sagoma s{i}" src="{a.sprite(n)}" alt="" width="{a.immagini(n).width * 4}" height="{a.immagini(n).height * 4}">'
            for i, n in enumerate(("drone-2", "drone-3", "drone-2", "drone-3")))
        avvisi = ""
        for k, (testo, classe, x, y, rit) in enumerate(AVVISI, 1):
            im = carattere.scrivi(testo, k=2, colore=MAGENTA if classe else ROSSO)
            url = a.png("avviso-" + fabbrica._slug(testo), im)
            avvisi += (f'<div class="avviso avviso-{k} {classe}" style="{_at(x, y)};--rit:{rit}s" aria-hidden="true">'
                       f'<img class="px" src="{url}" alt="" width="{im.width}" height="{im.height}"></div>')
        crepe = "".join(f'<div class="crepa" style="{_at(x, y, 220)};--rit:{rit}s" aria-hidden="true">{_crepa(seme)}</div>'
                        for seme, x, y, rit in ((1, 120, 40, 0), (2, 760, 170, -7), (3, 420, -40, -14), (4, 900, -30, -21)))
        return ('<div class="lampo" aria-hidden="true"></div><div class="rumore" aria-hidden="true"></div>'
                + f'<div class="segnale" style="{_at(192, 48, 640)}"><span class="etichetta">// ?? · Segnale disturbato</span>'
                  f'<h2><img class="px tit" src="{a.png("titolo-sotto-attacco", tit)}" alt="Sotto attacco" width="{tit.width}" height="{tit.height}"></h2>'
                  "<p>Unità di attacco e difesa, previste per una versione futura. Il segnale va e viene: per ora si vedono solo le sagome.</p>"
                  f'<div class="sagome">{sagome}</div></div>' + avvisi + crepe)
    if sid == "ambienti":
        finestre = ""
        for b, bioma in enumerate(biomi()):
            im = tavola._finestra(b, bioma, a.immagini)
            im = im.resize((im.width // 2, im.height // 2), Image.NEAREST)  # 12 cells at 16 px: exact half
            finestre += (f'<figure class="bioma" data-rivela><img class="px" src="{a.png("bioma-" + bioma, im)}" '
                         f'alt="Ambiente {bioma}" width="{im.width}" height="{im.height}"><figcaption>{bioma.capitalize()}</figcaption></figure>')
        return (_titolo(a, "05", "Ambienti", 192, 22)
                + f'<div class="finestre" style="{_at(128, 96, 400)}">{finestre}</div>'
                + f'<p class="nota" style="{_at(560, 110, 250)}">Quattro biomi generati dal seme, con pareti, laghi e fiumi: '
                  "ogni partita ha la sua mappa. Suoli con varianti e bordi, pareti in rilievo, acqua bassa su cui costruire "
                  "e profonda da scavalcare col ponte, giacimenti a puntini.</p>")
    if sid == "interfaccia":
        return (_titolo(a, "06", "Interfaccia e accessibilità", 160, 22)
                + _crt(radice, "interfaccia-hud.png", 77, 96, 346, 196)
                + _crt(radice, "interfaccia-daltonici.png", 448, 96, 256, 196)
                + _crt(radice, "interfaccia-altocontrasto.png", 77, 318, 294, 180)
                + _crt(radice, "mercato.png", 397, 318, 307, 180)
                + _crt(radice, "interfaccia-palette.png", 800, 130, 281, 186, ";z-index:2;--rientro:84px", classe="sotto-ponte"))
    if sid == "scarica":
        mercato = {m["itemId"]: m for m in composti._CONTENT["market"]}
        prezzo = mercato["iron-plate"]["sellPrice"]
        nome = mercato["iron-plate"]["displayName"]
        bottone = f"Scarica {html.escape(versione)}" if versione else "Scarica l'ultima release"
        sistemi = " e ".join(html.escape(s) for s, _, _ in documenti.requisiti())
        return (_titolo(a, "07", "Scarica", 192, 150)
                + f'<div class="scarica-box" style="{_at(384, 448, 384)}">'
                  f'<a class="btn primario grande striscia" href="{RELEASE_URL}" data-costruisci>{bottone}</a>'
                  f'<a class="btn" href="{REPO_URL}" data-costruisci>Codice su GitHub</a>'
                  f'<p class="requisiti">{sistemi} · zip pronto, .NET già incluso · gratuito e open source</p></div>'
                + _grafico_mercato(a, prezzo, nome))
    raise KeyError(sid)


def pagina_vetrina(archivio, versione):
    sezioni = []
    for scena in fabbrica.VETRINA:
        scena_html = fabbrica.html_scena(scena, archivio)
        contenuto = _contenuto(archivio, scena.id, archivio.radice, versione)
        classe = "sez " + scena.classe.replace("scena-attacco", "sez-attacco") if scena.classe else "sez"
        sfondo = ""
        if scena.id == "hero":
            classe += " hero"
        if scena.sfondo:  # the capture spreads over the whole section, beyond the floor, on wide screens
            sfondo = f'<div class="hero-bg" style="background-image:url({archivio.sfondo(*scena.sfondo)})"></div>'
        if scena.bioma:  # the terrain across the whole width (the floor keeps its own copy for phones)
            sfondo += (f'<div class="terra-larga" aria-hidden="true" style="background-image:'
                       f'url({archivio.terreno(scena.bioma, scena.righe, scena.seme)})"></div>')
        sezione = (f'<section id="{scena.id}" class="{classe}" data-bioma="{scena.bioma or ""}">{sfondo}'
                   f'<div class="pavimento">{scena_html}<div class="contenuto">{contenuto}</div></div></section>')
        if scena.id == "hero":
            sezione = f'<div class="pin">{sezione}</div>'
        sezioni.append(sezione)
    nav = _nav(NAV_VETRINA + (("sviluppo/", "Sviluppo"), ("asset/", "Asset")))
    noscript = '<noscript><style>@media (max-width:1183px){.sez{overflow-x:auto}.scena{width:1152px}}</style></noscript>'
    # The captures open over the page (sito.js); without the script their links open the PNGs.
    visore = ('<dialog id="visore" class="visore" aria-label="Cattura del gioco"><img alt="">'
             '<div class="visore-barra"><span class="visore-didascalia"></span><span class="visore-conta"></span>'
             '<button type="button" class="visore-prec" aria-label="Cattura precedente">&lt;</button>'
             '<button type="button" class="visore-succ" aria-label="Cattura successiva">&gt;</button>'
             '<button type="button" class="visore-chiudi" autofocus>Chiudi</button></div></dialog>')
    return _pagina("tIndustry · logistica industriale, gratuito e open source", noscript + "".join(sezioni) + visore, nav,
                   radice="", classe="vetrina",
                   descrizione="tIndustry è un gioco gratuito e open source di logistica industriale ispirato a Mindustry "
                               "e Tiny Industry: minatori, nastri, forni, Mercato e Ricerca. Per Windows e Linux, in italiano e in inglese.",
                   drone=True, avanza=True)


def pagina_sviluppo(archivio):
    striscia, fermo = archivio.nastro(fabbrica.T1, "E")
    nastro = (f'<div class="nastro-testa" aria-hidden="true"><div class="nastro v-e" '
              f'style="background-image:url({striscia});--giro:2s"><div class="fermo" style="background-image:url({fermo})"></div></div></div>')
    nav = _nav((("../", "Vetrina"), ("#come-e-fatto", "Com'è fatto"), ("#roadmap", "Roadmap"), ("#diario", "Diario"),
                ("#decisioni", "Decisioni"), ("#changelog", "Changelog"), ("../asset/", "Asset")))
    testa = ('<div class="contenuto-doc intro-doc"><h1>Com\'è fatto tIndustry</h1>'
             "<p>Le scelte dietro al gioco, la strada fatta e quella che resta, raccontate dai documenti del repository.</p></div>")
    corpo = documenti.sezioni_sviluppo("../").replace('<table class="tabella">', '<div class="tabella-scorri"><table class="tabella">')
    corpo = corpo.replace("</table>", "</table></div>")
    return _pagina("tIndustry · sviluppo", nastro + testa + corpo, nav, radice="../", classe="sviluppo",
                   descrizione="Com'è fatto tIndustry: decisioni architetturali, roadmap e diario di lavoro.", percorso="sviluppo/")


# ---------------------------------------------------------------- build

def stile():
    """The site stylesheet: stile.css plus the drone fragment, when present."""
    parti = []
    for nome in ("stile.css", "drone.css"):
        percorso = os.path.join(SITO_DIR, nome)
        if os.path.exists(percorso):
            with open(percorso, encoding="utf-8") as fh:
                parti.append(fh.read().rstrip() + "\n")
    return "\n".join(parti)


def pagina_racconto(archivio, versione, dati):
    """The scroll-driven story, all its chapters: a sticky canvas (racconto.js) behind fourteen panels, one
    screen each. Without the script the still poster stands behind the same panels, and the download links of the
    last one, which racconto.js shows inside the market screen instead, stay in the panel."""
    import racconto
    logo = archivio.immagini("ui/ui-logo")
    url_logo = archivio.png("logo-x8", logo.resize((logo.width * 8, logo.height * 8), Image.NEAREST))
    scarica = f"Scarica {versione}" if versione else "Scarica l'ultima release"
    pannelli = (
        ("sinistra", f'<h1><img class="px logo" src="{url_logo}" alt="tIndustry" width="{logo.width * 8}" '
                     f'height="{logo.height * 8}"></h1>'
                     "<p class=\"forte\">Ogni fabbrica comincia da un giacimento.</p>"
                     "<p>tIndustry è un gioco gratuito e open source di logistica industriale. Scorri: costruiamo "
                     "insieme la prima catena.</p>"
                     f'<p class="bottoni"><a class="btn primario" href="{RELEASE_URL}">{html.escape(scarica)}</a>'
                     '<a class="btn" href="../">Torna alla vetrina</a></p>'),
        ("destra", '<span class="etichetta">// 01 · il drone</span><h2>Il drone costruisce</h2>'
                   "<p>Scegli un blocco e il drone vola sul posto: prima compare il piano, poi il cantiere sale dal "
                   "basso, come in Mindustry. Il minatore si posa sul giacimento di ferro e comincia a scavare.</p>"),
        ("sinistra", '<span class="etichetta">// 02 · i nastri</span><h2>Nastro dopo nastro</h2>'
                     "<p>Il minerale esce dal lato del minatore e la prima curva lo porta giù. Ogni cella di nastro è "
                     "un cantiere: il minerale corre solo dove il nastro è finito, "
                     f"{composti._num(fabbrica.RATE[fabbrica.T1])} celle al secondo sui nastri T1.</p>"),
        ("destra", '<span class="etichetta">// 03 · il forno</span><h2>Ferro e carbone</h2>'
                   "<p>Il forno fonde il ferro col carbone. Un secondo minatore lo scava dal giacimento accanto e un "
                   "nastro lo porta al forno dall'altro lato.</p>"),
        ("sinistra", '<span class="etichetta">// 04 · le lastre</span><h2>Le prime lastre</h2>'
                     "<p>Quando arrivano tutti e due il forno si accende e le lastre di ferro escono sul nastro, verso "
                     "il resto della fabbrica.</p>"),
        ("destra", '<span class="etichetta">// 05 · la corrente</span><h2>La corrente</h2>'
                   "<p>Dall'altro lato del minatore il carbone va al generatore. I nodi portano la corrente lontano: "
                   "le linee si stendono da un nodo all'altro fino alle lampade.</p>"),
        ("sinistra", '<span class="etichetta">// 06 · la sera</span><h2>Si fa buio</h2>'
                     "<p>Il giorno finisce e la fabbrica resta accesa: le lampade e la bocca del forno illuminano i "
                     "nastri che continuano a girare.</p>"),
        ("sinistra", '<span class="etichetta">// 07 · sotto attacco</span><h2>Di notte arrivano</h2>'
                     "<p>Sagome nere attraversano il fiume, dritte verso la fabbrica, e si fermano appena fuori dalla "
                     "luce delle lampade. Il drone le vede, trema e si mette l'elmetto.</p>"
                     '<p class="sollievo" data-rivela>Respira: se non riesci ad abbatterli e il CORE sopravvive, al '
                     "mattino se ne vanno da soli. Se invece distruggono il CORE è Game Over.</p>"
                     '<p class="nota">Unità e difese sono in arrivo: sono nella roadmap.</p>'),
        ("sinistra", '<span class="etichetta">// 08 · l\'alba</span><h2>Il giorno le scaccia</h2>'
                     "<p>Torna il giorno e i superstiti scappano oltre il fiume, fino a sparire. Il drone si toglie "
                     "l'elmetto e torna al lavoro.</p>"),
        ("destra", '<span class="etichetta">// 09 · il mondo</span><h2>Il mondo intorno</h2>'
                   "<p>Ogni partita ha la sua mappa, generata dal seme: rocce, prati, neve e sabbia, fiumi da "
                   "scavalcare col ponte, giacimenti sparsi. Dieci livelli di campagna, da 200×200 a 1000×1000 celle.</p>"),
        ("sinistra", '<span class="etichetta">// 10 · il CORE</span><h2>Tutto finisce nel CORE</h2>'
                     "<p>Lastre e rame si uniscono nell'unione a T e scendono nel CORE; da ovest arriva anche il "
                     "piombo, da linee che erano lì da prima. Il CORE tiene tutto per la Ricerca e per il Mercato.</p>"),
        ("sinistra", '<span class="etichetta">// 11 · il mercato</span><h2>Il prezzo vola</h2>'
                   "<p>Le lastre di ferro non sono mai valse tanto. Il drone riempie una valigetta: 9.999 lastre da "
                   "vendere tutte insieme, subito.</p>"),
        ("sinistra", '<span class="etichetta">// 12 · vendi tutto</span><h2>Stonks</h2>'
                     "<p>Al Mercato il prezzo scende mentre vendi e risale piano. Vendere tutto in un colpo lo fa "
                     "crollare fino al prezzo normale. Meglio poco alla volta.</p>"),
        ("sinistra", '<span class="etichetta">// 13 · il jackpot</span><h2>Il jackpot</h2>'
                     "<p>Un rialzo improvviso del Mercato è raro. Vendere oltre una soglia aumenta la probabilità del "
                     f"jackpot, come il bonus dei soldi +{racconto.BONUS}% di stavolta: dopo un crollo, infatti, il "
                     "mercato resta fermo per un po' e ogni tanto scende sotto il prezzo normale.</p>"
                     '<p class="sollievo">Tranquillo: succede a ogni oggetto per conto suo. Se crolla il '
                     "ferro, il rame resta com'era.</p>"
                     '<p class="nota">Il jackpot è ancora un\'idea: la trovi nel file delle idee. Il drone intanto ha '
                     "cambiato canale.</p>"
                     f'<p class="bottoni senza-tv"><a class="btn primario" href="{RELEASE_URL}">{html.escape(scarica)}</a>'
                     f'<a class="btn" href="{REPO_URL}">GitHub</a></p>'),
    )
    capitoli = "".join(f'<section class="capitolo" data-lato="{lato}"><div class="capitolo-vista">'
                       f'<div class="pannello" data-rivela>{testo}</div></div></section>'
                       for lato, testo in pannelli)
    corpo = (f'<div class="racconto" data-cartella="../racconto/" style="--capitoli:{len(pannelli)}">'
             f'<div class="racconto-scena"><img class="racconto-poster px" src="../racconto/{dati["poster"]}" alt="" '
             'width="1600" height="768"><canvas id="racconto-tela" aria-hidden="true"></canvas>'
             f'<p class="racconto-tv"><a class="btn primario" href="{RELEASE_URL}">Scarica</a>'
             f'<a class="btn" href="{REPO_URL}">GitHub</a></p></div>'
             f'<div class="racconto-capitoli">{capitoli}</div></div>'
             f'<script type="application/json" id="racconto-dati">{racconto.json_della_pagina(dati)}</script>')
    nav = _nav((("../", "Vetrina"), ("../sviluppo/", "Sviluppo"), ("../asset/", "Asset")))
    pagina = _pagina("tIndustry · racconto (prototipo)", corpo, nav, radice="../", classe="racconto-pagina",
                     descrizione="Il racconto di tIndustry: scorrendo, il drone costruisce la fabbrica dal giacimento di ferro "
                                 "al CORE, tra forno, corrente, la notte e l'attacco.",
                     avanza=True, percorso="racconto/")
    pagina = pagina.replace('<meta charset="utf-8">', '<meta charset="utf-8"><meta name="robots" content="noindex">', 1)
    return pagina.replace('<script src="../sito.js" defer></script>',
                          '<script src="../sito.js" defer></script><script src="../racconto.js" defer></script>', 1)


def costruisci(out_dir, assets_dir=os.path.join(REPO, "godot", "assets")):
    """Write the whole site into out_dir; returns the written pages (relative paths, sorted)."""
    immagini = composti.Immagini(assets_dir)
    versione = documenti.versione_release()
    archivio = fabbrica.Archivio(immagini, out_dir, radice="")
    archivio.sprite("drone-1")
    with open(os.path.join(out_dir, "favicon.png"), "wb") as fh:
        fh.write(_png(immagini("core-1").resize((32, 32), Image.NEAREST)))
    pagine = {"index.html": pagina_vetrina(archivio, versione)}
    sotto = fabbrica.Archivio(immagini, out_dir, radice="../")  # same files, URLs from a sub-folder
    pagine["sviluppo/index.html"] = pagina_sviluppo(sotto)
    pagine["asset/index.html"] = pagina_asset(assets_dir)
    import racconto
    pagine["racconto/index.html"] = pagina_racconto(sotto, versione, racconto.esporta(out_dir, immagini))
    for rel, testo in pagine.items():
        path = os.path.join(out_dir, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(testo)
    with open(os.path.join(out_dir, "stile.css"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(stile())
    for nome in STATICI:
        shutil.copyfile(os.path.join(SITO_DIR, nome), os.path.join(out_dir, nome))
    catture = os.path.join(out_dir, "catture")
    os.makedirs(catture, exist_ok=True)
    for nome, _ in CATTURE:  # same pixels, best lossless compression
        with Image.open(os.path.join(CATTURE_DIR, nome)) as im:
            im.save(os.path.join(catture, nome), "PNG", optimize=True)
    # Pages serves the folder as is (no Jekyll processing).
    open(os.path.join(out_dir, ".nojekyll"), "w").close()
    return sorted(pagine)


def _cli(argv):
    parser = argparse.ArgumentParser(description="Costruisce il sito di tIndustry.")
    parser.add_argument("--out", default=os.path.join(REPO, "_site"), help="cartella di uscita (predefinita _site/)")
    args = parser.parse_args(argv)
    for rel in costruisci(args.out):
        print(os.path.join(args.out, rel))


if __name__ == "__main__":
    _cli(sys.argv[1:])
