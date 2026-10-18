"""Build the tIndustry website (GitHub Pages) into _site/.

    python tools/sito/costruisci.py            # writes _site/
    python tools/sito/costruisci.py --out DIR  # somewhere else

Pages today:
- index.html        landing page (name, short presentation, links);
- asset/index.html  every sprite registered in tools/art (enlarged, strips as animated GIFs)
                    and the interface font.

Everything comes from the repository (sprites in godot/assets/, the registry and the font in
tools/art/), so the site never needs editing by hand: change the art, rebuild. Pages are
self-contained (images as data: URIs, inline CSS, no scripts). Published by
.github/workflows/pages.yml (manual or release tags only).
"""
import argparse
import base64
import collections
import html
import io
import os
import sys

SITO_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(SITO_DIR, "..", ".."))
ART_DIR = os.path.join(REPO, "tools", "art")
if ART_DIR not in sys.path:
    sys.path.insert(0, ART_DIR)

from PIL import Image  # noqa: E402

import carattere  # noqa: E402
from blocchi import SPRITE  # noqa: E402

REPO_URL = "https://github.com/Juan0177/tIndustry"

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

STILE = """
:root{--bg:#14161a;--pan:#1d2026;--linea:#2c3038;--bordo:#ffd37f;--testo:#e6e6ec;--tenue:#9aa0aa}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--testo);font:15px/1.5 system-ui,sans-serif}
a{color:var(--bordo)}
header{background:var(--bg);border-bottom:2px solid var(--bordo);padding:12px 16px}
header.fisso{position:sticky;top:0;z-index:1}
h1{margin:0 0 6px;font-size:22px;color:var(--bordo)}
h2{color:var(--bordo);margin:28px 0 10px;font-size:18px}
h2 small{color:var(--tenue);font-weight:normal}
nav{display:flex;flex-wrap:wrap;gap:6px 14px}
nav a{color:var(--testo);text-decoration:none}
nav a:hover{color:var(--bordo)}
main{padding:8px 16px 40px;max-width:1400px;margin:auto}
p.nota{color:var(--tenue);margin:4px 0 0}
.griglia{display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:10px}
figure{margin:0;background:var(--pan);border:2px solid var(--linea);padding:8px}
.box{height:136px;display:flex;align-items:center;justify-content:center;
  background:repeating-conic-gradient(#262a31 0 25%,#20232a 0 50%) 0 0/16px 16px}
img{image-rendering:pixelated;max-width:100%;height:auto}
figcaption{margin-top:6px;font-size:12px;word-break:break-all}
figcaption span{display:block;color:var(--tenue)}
.pannello{background:var(--pan);border:2px solid var(--bordo);padding:16px;margin:16px 0}
.carattere img{display:block;margin:8px 0}
.sezioni{display:grid;grid-template-columns:repeat(auto-fill,minmax(240px,1fr));gap:12px}
.sezioni a{display:block;background:var(--pan);border:2px solid var(--linea);padding:14px;text-decoration:none;color:var(--testo)}
.sezioni a:hover{border-color:var(--bordo)}
.sezioni b{display:block;color:var(--bordo);font-size:16px}
footer{color:var(--tenue);padding:16px;text-align:center;font-size:13px}
"""


def _data_url(data, mime):
    return f"data:{mime};base64," + base64.b64encode(data).decode("ascii")


def _png(im):
    buf = io.BytesIO()
    im.save(buf, "PNG")
    return buf.getvalue()


def _gif(frames, ms=110):
    """Animated GIF of RGBA frames; alpha below 128 becomes the transparent index (GIF has 1-bit alpha)."""
    conv = []
    for f in frames:
        trasparente = f.getchannel("A").point(lambda v: 255 if v < 128 else 0)
        p = f.convert("RGB").convert("P", palette=Image.Palette.ADAPTIVE, colors=255)
        p.paste(255, mask=trasparente)
        conv.append(p)
    buf = io.BytesIO()
    conv[0].save(buf, "GIF", save_all=True, append_images=conv[1:], duration=ms, loop=0,
                 disposal=2, transparency=255)
    return buf.getvalue()


def _pagina(titolo, corpo, intestazione, fisso=False):
    classe = ' class="fisso"' if fisso else ""
    return (f'<!doctype html><html lang="it"><head><meta charset="utf-8">'
            f'<meta name="viewport" content="width=device-width,initial-scale=1">'
            f"<title>{html.escape(titolo)}</title><style>{STILE}</style></head><body>"
            f"<header{classe}>{intestazione}</header><main>{corpo}</main>"
            f'<footer>tIndustry · <a href="{REPO_URL}">codice su GitHub</a> · '
            f"pagina generata da tools/sito/costruisci.py</footer></body></html>")


def famiglie(assets_dir):
    """{family: [sprite name, …]} for the registered sprites whose PNG exists, in page order."""
    per_famiglia = collections.defaultdict(list)
    for nome, fn in sorted(SPRITE.items()):
        if os.path.exists(os.path.join(assets_dir, nome + ".png")):
            per_famiglia[fn.__module__.rsplit(".", 1)[-1]].append(nome)
    ordine = [f for f in FAMIGLIE if f in per_famiglia] + sorted(set(per_famiglia) - set(FAMIGLIE))
    return {f: per_famiglia[f] for f in ordine}


def _cella(assets_dir, nome):
    im = Image.open(os.path.join(assets_dir, nome + ".png")).convert("RGBA")
    if nome.endswith("-anim") and im.width > im.height:
        lato, n = im.height, im.width // im.height
        url = _data_url(_gif([im.crop((i * lato, 0, (i + 1) * lato, lato)) for i in range(n)]), "image/gif")
        w = h = lato
        info = f"{lato}×{lato}, {n} fotogrammi"
    else:
        url, w, h = _data_url(_png(im), "image/png"), im.width, im.height
        info = f"{w}×{h}"
    z = max(1, min(4, 128 // max(w, h)))
    nome_breve = html.escape(nome.rsplit("/", 1)[-1])
    return (f'<figure><div class="box"><img src="{url}" width="{w * z}" height="{h * z}" alt="{nome_breve}">'
            f"</div><figcaption><b>{nome_breve}</b><span>{info}</span></figcaption></figure>")


def pagina_asset(assets_dir):
    gruppi = famiglie(assets_dir)
    sezioni = []
    for f, nomi in gruppi.items():
        celle = "".join(_cella(assets_dir, n) for n in nomi)
        sezioni.append(f'<section id="{f}"><h2>{html.escape(FAMIGLIE.get(f, f))} <small>{len(nomi)}</small></h2>'
                       f'<div class="griglia">{celle}</div></section>')
    righe = [carattere.scrivi(FRASI_CARATTERE[0], 3)] + [carattere.scrivi(t, 2) for t in FRASI_CARATTERE]
    meta = len(carattere.INSIEME) // 2
    righe += [carattere.scrivi(carattere.INSIEME[:meta], 2), carattere.scrivi(carattere.INSIEME[meta:], 2)]
    immagini = "".join(f'<img src="{_data_url(_png(r), "image/png")}" width="{r.width}" height="{r.height}" alt="">'
                       for r in righe)
    sezioni.append('<section id="carattere"><h2>Carattere dell\'interfaccia</h2>'
                   f'<div class="pannello carattere">{immagini}</div></section>')
    totale = sum(len(n) for n in gruppi.values())
    animate = sum(1 for n in gruppi.values() for x in n if x.endswith("-anim"))
    menu = "".join(f'<a href="#{f}">{html.escape(FAMIGLIE.get(f, f))}</a>' for f in gruppi)
    intestazione = (f'<h1>tIndustry · asset</h1><nav><a href="../">Home</a>{menu}<a href="#carattere">Carattere</a></nav>'
                    f'<p class="nota">{totale} sprite, {animate} animazioni. Pixel-art originale generata da '
                    f'<a href="{REPO_URL}/tree/main/tools/art">tools/art</a>, ingrandita senza filtro.</p>')
    return _pagina("tIndustry · asset", "".join(sezioni), intestazione, fisso=True)


def pagina_ingresso():
    corpo = (
        '<div class="pannello"><p>Gioco di logistica industriale ispirato a Mindustry e Tiny Industry, '
        "in Godot 4 .NET: miniere, nastri, forni e assemblatori, ricerca, mercato e una campagna. "
        "Tutta la grafica è pixel-art originale generata da script.</p></div>"
        '<div class="sezioni">'
        '<a href="asset/"><b>Asset</b>Tutti gli sprite del gioco, con le animazioni, e il carattere dell\'interfaccia.</a>'
        f'<a href="{REPO_URL}"><b>Codice</b>Repository su GitHub: sorgenti, documenti, roadmap.</a>'
        "</div>"
        '<p class="nota">Il sito è all\'inizio: catture, download e diario di sviluppo arriveranno.</p>'
    )
    return _pagina("tIndustry", corpo, "<h1>tIndustry</h1>")


def costruisci(out_dir, assets_dir=os.path.join(REPO, "godot", "assets")):
    """Write the whole site into out_dir; returns the written paths (relative)."""
    pagine = {"index.html": pagina_ingresso(), "asset/index.html": pagina_asset(assets_dir)}
    for rel, testo in pagine.items():
        path = os.path.join(out_dir, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(testo)
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
