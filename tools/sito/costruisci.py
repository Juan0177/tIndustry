"""Build the tIndustry website (GitHub Pages) into _site/.

    python tools/sito/costruisci.py            # writes _site/
    python tools/sito/costruisci.py --out DIR  # somewhere else

Pages today:
- index.html        landing page (name, short presentation, links);
- asset/index.html  every sprite registered in tools/art: blocks composed as the game draws them (states
                    as switches, their pieces in a pop-up), the other sprites enlarged (strips as
                    animated GIFs), one composed window of terrain per biome and the interface font.

Everything comes from the repository (sprites in godot/assets/, the registry and the font in
tools/art/, the block compositions in composti.py), so the site never needs editing by hand: change
the art, rebuild. Pages are self-contained (images as data: URIs, inline CSS) and script-free: state
switches are radio buttons styled with :checked, the pieces pop-up is an HTML popover (Esc, click outside
and the close button hide it, focus goes back to the button). Published by .github/workflows/pages.yml
(manual or release tags only).
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
for _d in (ART_DIR, SITO_DIR):
    if _d not in sys.path:
        sys.path.insert(0, _d)

from PIL import Image  # noqa: E402

import carattere  # noqa: E402
import composti  # noqa: E402
import tavola  # noqa: E402
from blocchi import SPRITE  # noqa: E402
from blocchi import terreno as tr  # noqa: E402

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

MAX_STATI = max(len(b.stati) for b in composti.BLOCCHI)

# State switches without scripts: the k-th radio of a card shows its k-th image and marks its k-th label.
_STILE_STATI = "".join(
    f".blocco>input:nth-of-type({k}):checked~.box>img:nth-of-type({k}){{display:block}}"
    f".blocco>input:nth-of-type({k}):checked~.scelte>label:nth-of-type({k})"
    "{background:var(--bordo);color:var(--bg);border-color:var(--bordo)}"
    f".blocco>input:nth-of-type({k}):focus-visible~.scelte>label:nth-of-type({k})"
    "{outline:2px solid var(--testo);outline-offset:1px}"
    for k in range(1, MAX_STATI + 1))

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
figure{margin:0;background:var(--pan);border:2px solid var(--linea);padding:8px;min-width:0}
.box{height:136px;display:flex;align-items:center;justify-content:center;
  background:repeating-conic-gradient(#262a31 0 25%,#20232a 0 50%) 0 0/16px 16px}
img{image-rendering:pixelated;max-width:100%;height:auto}
figcaption{margin-top:6px;font-size:12px;word-break:break-all}
figcaption span{display:block;color:var(--tenue)}
.blocco{position:relative}
.blocco .box{height:auto;min-height:136px;padding:4px}
.blocco .box>img.st{display:none}
.blocco.largo{grid-column:span 2}
@media (max-width:360px){.blocco.largo{grid-column:auto}}
.blocco figcaption{word-break:normal;overflow-wrap:anywhere;padding-right:40px}
.sr{position:absolute;opacity:0;width:1px;height:1px;margin:0;pointer-events:none}
.scelte{display:flex;flex-wrap:wrap;gap:4px;margin-top:6px}
.scelte label{font-size:12px;border:1px solid var(--linea);padding:1px 6px;cursor:pointer;background:var(--bg)}
.scelte label:hover{border-color:var(--bordo)}
button{font:inherit;color:var(--testo);background:var(--bg);border:2px solid var(--linea);cursor:pointer}
button:hover,button:focus-visible{border-color:var(--bordo);outline:none}
.lente{position:absolute;right:8px;bottom:8px;width:34px;height:34px;padding:0;display:flex;
  align-items:center;justify-content:center}
.lente svg{width:20px;height:20px;stroke:var(--bordo);fill:none;stroke-width:2.5}
.modale{background:var(--pan);color:var(--testo);border:2px solid var(--bordo);padding:16px;
  width:min(calc(100vw - 32px),760px);max-height:calc(100vh - 32px);overflow:auto}
.modale::backdrop{background:rgba(0,0,0,.7)}
.modale h3{margin:0 0 4px;color:var(--bordo);font-size:17px}
.modale .chiudi{position:sticky;top:0;float:right;padding:2px 10px}
.pezzi{display:grid;grid-template-columns:repeat(auto-fill,minmax(min(100%,200px),1fr));gap:10px;margin-top:12px}
.pezzi .box{height:auto;min-height:200px}
.pannello{background:var(--pan);border:2px solid var(--bordo);padding:16px;margin:16px 0}
.carattere img{display:block;margin:8px 0}
.ambienti{display:grid;grid-template-columns:repeat(auto-fill,minmax(min(100%,420px),1fr));gap:12px}
.ambienti img{display:block;width:100%}
.ambienti figcaption{font-size:13px;word-break:normal}
.sezioni{display:grid;grid-template-columns:repeat(auto-fill,minmax(240px,1fr));gap:12px}
.sezioni a{display:block;background:var(--pan);border:2px solid var(--linea);padding:14px;text-decoration:none;color:var(--testo)}
.sezioni a:hover{border-color:var(--bordo)}
.sezioni b{display:block;color:var(--bordo);font-size:16px}
footer{color:var(--tenue);padding:16px;text-align:center;font-size:13px}
""" + _STILE_STATI

LENTE = ('<svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">'
         '<circle cx="10" cy="10" r="6.5"/><path d="M15 15l6 6"/></svg>')


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


def _pagina(titolo, corpo, intestazione, fisso=False):
    classe = ' class="fisso"' if fisso else ""
    return (f'<!doctype html><html lang="it"><head><meta charset="utf-8">'
            f'<meta name="viewport" content="width=device-width,initial-scale=1">'
            f"<title>{html.escape(titolo)}</title><style>{STILE}</style></head><body>"
            f"<header{classe}>{intestazione}</header><main>{corpo}</main>"
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
    menu = "".join(f'<a href="#{f}">{html.escape(FAMIGLIE.get(f, f))}</a>' for f in gruppi)
    intestazione = (f'<h1>tIndustry · asset</h1><nav><a href="../">Home</a>{menu}<a href="#ambienti">Ambienti</a>'
                    f'<a href="#carattere">Carattere</a></nav>'
                    f'<p class="nota">{totale} sprite, {animate} animazioni, {len(composti.BLOCCHI)} blocchi '
                    "composti come in gioco (stati con i bottoni, pezzi con la lente). Pixel-art originale "
                    f'generata da <a href="{REPO_URL}/tree/main/tools/art">tools/art</a>, ingrandita senza filtro.</p>')
    return _pagina("tIndustry · asset", "".join(sezioni), intestazione, fisso=True)


def pagina_ingresso():
    corpo = (
        '<div class="pannello"><p>Gioco di logistica industriale ispirato a Mindustry e Tiny Industry, '
        "in Godot 4 .NET: miniere, nastri, forni e assemblatori, ricerca, mercato e una campagna. "
        "Tutta la grafica è pixel-art originale generata da script.</p></div>"
        '<div class="sezioni">'
        '<a href="asset/"><b>Asset</b>I blocchi come in gioco, con stati e pezzi, tutti gli sprite con le '
        "animazioni, gli ambienti e il carattere dell'interfaccia.</a>"
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
