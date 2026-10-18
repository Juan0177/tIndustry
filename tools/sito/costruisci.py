"""Build the tIndustry website (GitHub Pages) into _site/.

    python tools/sito/costruisci.py            # writes _site/
    python tools/sito/costruisci.py --out DIR  # somewhere else

Pages today:
- index.html        landing page (name, short presentation, links, the README captures of
                    docs/img/readme/ with Italian captions);
- asset/index.html  every sprite registered in tools/art: blocks composed as the game draws them (states
                    as switches, their pieces in a pop-up), the other sprites enlarged (strips as
                    animated GIFs), one composed window of terrain per biome and the interface font.

Everything comes from the repository (sprites in godot/assets/, the registry and the font in
tools/art/, the block compositions in composti.py, the captures copied from the playtests into
docs/img/readme/), so the site never needs editing by hand: change
the art, rebuild. The landing page captures are PNG files copied into _site/catture/; every other image
is a data: URI. Every page links the site's own stile.css and sito.js (copied from tools/sito/) and
nothing is loaded from outside the site; without the script everything still reads and works: state
switches are radio buttons styled with :checked (rules generated per page), the pieces pop-up is an
HTML popover (Esc, click outside and the close button hide it, focus goes back to the button). Published by .github/workflows/pages.yml
(manual or release tags only).
"""
import argparse
import collections
import html
import os
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
import fabbrica  # noqa: E402
from documenti import REPO_URL  # noqa: E402
import tavola  # noqa: E402
from blocchi import SPRITE  # noqa: E402
from blocchi import terreno as tr  # noqa: E402

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
    "{background:var(--bordo);color:var(--bg);border-color:var(--bordo)}"
    f".blocco>input:nth-of-type({k}):focus-visible~.scelte>label:nth-of-type({k})"
    "{outline:2px solid var(--testo);outline-offset:1px}"
    for k in range(1, MAX_STATI + 1))

# Files of tools/sito/ copied as they are into the site.
STATICI = ("stile.css", "sito.js")

LENTE = ('<svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">'
         '<circle cx="10" cy="10" r="6.5"/><path d="M15 15l6 6"/></svg>')


# Image helpers live in fabbrica.py (the showcase uses them too); old names kept for this module.
_data_url, _png, _gif = fabbrica.data_url, fabbrica.png, fabbrica.gif
immagine_stato = fabbrica.immagine_stato


def _pagina(titolo, corpo, nav, radice="", drone=False, stile_extra=""):
    """Whole HTML page in the site shell: fixed bar (construction stripe, name, menu from the (text, href)
    links of nav), main, footer. radice is the way back to the site root ("" or "../")."""
    menu = "".join(f'<a href="{html.escape(href)}">{html.escape(testo)}</a>' for testo, href in nav)
    extra = f"<style>{stile_extra}</style>" if stile_extra else ""
    drone_html = '<div id="drone" aria-hidden="true"></div>' if drone else ""
    return (f'<!doctype html><html lang="it"><head><meta charset="utf-8">'
            f'<meta name="viewport" content="width=device-width,initial-scale=1">'
            f'<title>{html.escape(titolo)}</title><link rel="stylesheet" href="{radice}stile.css">{extra}'
            f'<script src="{radice}sito.js" defer></script></head><body>'
            f'<header><div class="cantiere-striscia"></div><div class="barra">'
            f'<a class="marchio" href="{radice or "./"}">tIndustry</a><nav aria-label="Menu">{menu}</nav></div></header>'
            f"{drone_html}<main>{corpo}</main>"
            f'<footer>tIndustry · <a href="{REPO_URL}">codice su GitHub</a> · '
            f"pagina generata da tools/sito/costruisci.py</footer></body></html>")


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
            durate = [int(composti._arrotonda((k + 1) * 100 / fps) - composti._arrotonda(k * 100 / fps)) * 10
                      for k in range(n)]
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
            f'{len(blocco.parti)} pezzi</span>{nota}</figcaption>'
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
    nav = ([("Home", "../")] + [(FAMIGLIE.get(f, f), f"#{f}") for f in gruppi]
           + [("Ambienti", "#ambienti"), ("Carattere", "#carattere")])
    intro = ('<p class="etichetta">Asset</p><h1>Tutta la grafica del gioco</h1>'
             f'<p class="nota">{totale} sprite, {animate} animazioni, {len(composti.BLOCCHI)} blocchi '
             "composti come in gioco (stati con i bottoni, pezzi con la lente). Pixel-art originale "
             f'generata da <a href="{REPO_URL}/tree/main/tools/art">tools/art</a>, ingrandita senza filtro.</p>')
    return _pagina("tIndustry · asset", intro + "".join(sezioni), nav, radice="../", stile_extra=_STILE_STATI)


# Captures of the README (docs/img/readme/, copied from the playtests), shown on the landing page.
CATTURE_DIR = os.path.join(REPO, "docs", "img", "readme")
CATTURE = [
    ("gioco.png", "Fabbrica: il minatore porta il minerale al CORE sul nastro"),
    ("interfaccia-hud.png", "HUD: fondi, energia, risorse del CORE e palette con il pannello del blocco"),
    ("interfaccia-palette.png", "Palette a griglia fissa come Mindustry, solo i blocchi sbloccati"),
    ("ricerca.png", "Ricerca: albero a rami dal CORE"),
    ("mercato.png", "Mercato: prezzi dinamici e vendita automatica"),
    ("tutorial.png", "Campagna con il tutorial non bloccante"),
    ("interfaccia-home.png", "Home con il logo pixel e la mappa che scorre dietro"),
    ("interfaccia-en.png", "Tutti i testi anche in inglese"),
    ("interfaccia-daltonici.png", "Colori per daltonici: anteprima rifiutata con la X, smontaggio tratteggiato"),
    ("interfaccia-altocontrasto.png", "Alto contrasto: pannelli neri, testo bianco, segni col bordo nero"),
    ("blocchi.png", "Blocchi di produzione e potenza in funzione"),
    ("terreno-biomi.png", "Biomi: suoli, bordi, pareti in rilievo e acqua"),
    ("terreno-minerali.png", "Giacimenti a puntini"),
]


def _sezione_catture():
    # Files next to the page (catture/), not data: URIs: a dozen 1280x720 captures would make the
    # landing page several MB to parse before anything shows, and lazy loading would not apply.
    figure = []
    for nome, didascalia in CATTURE:
        testo = html.escape(didascalia)
        figure.append(f'<figure data-cattura="{html.escape(nome)}"><img src="catture/{html.escape(nome)}" '
                      f'alt="{testo}" width="1280" height="720" loading="lazy">'
                      f"<figcaption>{testo}</figcaption></figure>")
    return ('<section id="catture"><h2>Catture <small>' + str(len(figure)) + "</small></h2>"
            '<p class="nota">Dal gioco, scattate dai playtest automatici (finestra 1280×720).</p>'
            '<div class="griglia-catture">' + "".join(figure) + "</div></section>")


def pagina_ingresso():
    corpo = (
        '<p class="etichetta">Gioco di logistica industriale</p><h1>tIndustry</h1>'
        '<div class="pannello"><p>Gioco di logistica industriale ispirato a Mindustry e Tiny Industry, '
        "in Godot 4 .NET: miniere, nastri, forni e assemblatori, ricerca, mercato e una campagna. "
        "Tutta la grafica è pixel-art originale generata da script; l'interfaccia è in italiano e in inglese.</p></div>"
        '<div class="sezioni">'
        '<a href="#catture"><b>Catture</b>Il gioco com\'è oggi: fabbrica, HUD, palette, Ricerca, Mercato, '
        "campagna, Home, inglese e colori per daltonici.</a>"
        '<a href="asset/"><b>Asset</b>I blocchi come in gioco, con stati e pezzi, tutti gli sprite con le '
        "animazioni, gli ambienti e il carattere dell'interfaccia.</a>"
        f'<a href="{REPO_URL}"><b>Codice</b>Repository su GitHub: sorgenti, documenti, roadmap.</a>'
        "</div>"
        + _sezione_catture()
        + '<p class="nota">Download e diario di sviluppo arriveranno.</p>'
    )
    nav = [("Catture", "#catture"), ("Asset", "asset/"), ("GitHub", REPO_URL)]
    return _pagina("tIndustry", corpo, nav)


def costruisci(out_dir, assets_dir=os.path.join(REPO, "godot", "assets")):
    """Write the whole site into out_dir; returns the written paths (relative)."""
    pagine = {"index.html": pagina_ingresso(), "asset/index.html": pagina_asset(assets_dir)}
    for rel, testo in pagine.items():
        path = os.path.join(out_dir, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(testo)
    catture = os.path.join(out_dir, "catture")
    os.makedirs(catture, exist_ok=True)
    for nome, _ in CATTURE:
        shutil.copyfile(os.path.join(CATTURE_DIR, nome), os.path.join(catture, nome))
    for nome in STATICI:
        shutil.copyfile(os.path.join(SITO_DIR, nome), os.path.join(out_dir, nome))
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
