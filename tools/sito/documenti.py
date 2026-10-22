"""Repository documents for the website: README tables, work diary, ADRs, release version.

Everything the "sviluppo" page and the showcase need from the Markdown sources of the repository
(README.md, docs/diario/, docs/decisioni/, docs/decisioni.md, the git tags) is read here, so the site is
never written by hand: change the document, rebuild. Standard library only.

Markdown support is deliberately minimal (`inline`): the sources use tables, numbered lists, inline code,
bold, italic and links; nothing else is needed. Relative links become absolute links to the file on
GitHub (`blob/main/`), anchors point to the README on GitHub, http links stay as they are.
"""
import html
import os
import posixpath
import re
import subprocess

import stato

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
REPO_URL = "https://github.com/Juan0177/tIndustry"
BLOB = REPO_URL + "/blob/main/"

# Roadmap state (start of the cell, as written in the README) -> CSS class of the label.
CLASSI_STATO = (("Fatto", "stato-fatto"), ("In corso", "stato-in-corso"), ("Previsto", "stato-previsto"))

_RE_CODICE = re.compile(r"`([^`]+)`")
_RE_LINK = re.compile(r"\[([^\]]+)\]\(([^)\s]+)\)")
_RE_GRASSETTO = re.compile(r"\*\*(.+?)\*\*")
_RE_CORSIVO = re.compile(r"\*([^*]+)\*")
_RE_TAG = re.compile(r"<[^>]+>")
_RE_TITOLO = re.compile(r"^(#{2,3})\s+(.+?)\s*$")
_RE_SEPARATORE = re.compile(r"^[\s|:-]+$")
_RE_VOCE_DIARIO = re.compile(r"^-\s+\[(\d{4}-\d{2}-\d{2})\s+—\s+(.+?)\]\(([^)\s]+)\)")
_RE_TITOLO_ADR = re.compile(r"^#\s+(?:\d{4}\s+—\s+)?(.+?)\s*$")
_RE_PRIMA_FRASE = re.compile(r"^(.*?[.!?]\)?)(?=\s|$)", re.S)


def _leggi(*percorso):
    with open(os.path.join(REPO, *percorso), encoding="utf-8") as fh:
        return fh.read()


def _url(destinazione, base):
    """Absolute URL of a Markdown link target written in a file of folder `base` (repo-relative)."""
    if destinazione.startswith("http"):
        return destinazione
    if destinazione.startswith("#"):
        return REPO_URL + destinazione
    percorso, _, ancora = destinazione.partition("#")
    percorso = posixpath.normpath(posixpath.join(base, percorso)) if base else posixpath.normpath(percorso)
    return BLOB + percorso + ("#" + ancora if ancora else "")


def inline(markdown, base=""):
    """Safe HTML of one line of Markdown: code, bold, italic, links (see the module docstring)."""
    testo = html.escape(markdown.strip())
    testo = _RE_CODICE.sub(r"<code>\1</code>", testo)
    testo = _RE_LINK.sub(lambda m: f'<a href="{html.escape(_url(html.unescape(m.group(2)), base))}">{m.group(1)}</a>',
                         testo)
    testo = _RE_GRASSETTO.sub(r"<b>\1</b>", testo)
    return _RE_CORSIVO.sub(r"<i>\1</i>", testo)


def _senza_grassetto(cella):
    return _RE_GRASSETTO.sub(r"\1", cella)


def _celle(riga):
    riga = riga.strip()
    if riga.startswith("|"):
        riga = riga[1:]
    if riga.endswith("|") and not riga.endswith("\\|"):
        riga = riga[:-1]
    return [c.strip().replace("\\|", "|") for c in re.split(r"(?<!\\)\|", riga)]


def tabella_dopo(markdown, titolo):
    """Raw cells of the first table after the line `## titolo` (or `### titolo`), without the `|---|` row.

    The search stops at the next heading of the same or a higher level; [] when there is no such table."""
    righe = markdown.splitlines()
    livello = None
    for i, riga in enumerate(righe):
        m = _RE_TITOLO.match(riga)
        if m and m.group(2) == titolo:
            livello = len(m.group(1))
            inizio = i + 1
            break
    if livello is None:
        return []
    tabella = []
    for riga in righe[inizio:]:
        m = _RE_TITOLO.match(riga)
        if m and len(m.group(1)) <= livello and not tabella:
            return []
        if riga.lstrip().startswith("|"):
            if not _RE_SEPARATORE.match(riga):
                tabella.append(_celle(riga))
        elif tabella:
            break
    return tabella


def _readme():
    return _leggi("README.md")


def contenuti():
    """(area, contenuto) of the README table "Cosa c'è nel gioco", as inline HTML (area without <b>)."""
    return [(inline(_senza_grassetto(area)), inline(cosa))
            for area, cosa, *_ in tabella_dopo(_readme(), "Cosa c'è nel gioco")[1:]]


def roadmap():
    """(stato, nome, cosa) of the README table "Stato e roadmap", as inline HTML (nome without <b>)."""
    return [(inline(stato), inline(_senza_grassetto(nome)), inline(cosa))
            for stato, nome, cosa, *_ in tabella_dopo(_readme(), "Stato e roadmap")[1:]]


def requisiti():
    """(sistema, file, eseguibile) of the download table under "## Gioca", as plain text (no backticks)."""
    return [tuple(c.replace("`", "") for c in (sistema, file, eseguibile))
            for sistema, file, eseguibile, *_ in tabella_dopo(_readme(), "Gioca")[1:]]


def primi_passi():
    """The numbered steps under "### Prima partita" of the README, as inline HTML."""
    righe = _readme().splitlines()
    passi = []
    dentro = False
    for riga in righe:
        m = _RE_TITOLO.match(riga)
        if m:
            if dentro:
                break
            dentro = m.group(2) == "Prima partita"
            continue
        if not dentro:
            continue
        passo = re.match(r"^\d+\.\s+(.+)$", riga.strip())
        if passo:
            passi.append(inline(passo.group(1)))
        elif passi and riga.strip():
            break
    return passi


def _paragrafo_dopo(markdown, titolo):
    """First paragraph after the heading `## titolo` ("" when missing)."""
    righe = markdown.splitlines()
    paragrafo = []
    dentro = False
    for riga in righe:
        m = _RE_TITOLO.match(riga)
        if m:
            if dentro:
                break
            dentro = m.group(2) == titolo
            continue
        if not dentro:
            continue
        if riga.strip():
            paragrafo.append(riga.strip())
        elif paragrafo:
            break
    return " ".join(paragrafo)


def diario():
    """Entries of the work diary, in the order of the index docs/diario/README.md.

    Each entry: data, titolo (plain text), file (name), url (GitHub), obiettivo (inline HTML of the first
    paragraph after "## Obiettivo" of the entry; "" if the file is missing)."""
    voci = []
    for riga in _leggi("docs", "diario", "README.md").splitlines():
        m = _RE_VOCE_DIARIO.match(riga)
        if not m:
            continue
        data, titolo, file = m.groups()
        percorso = os.path.join(REPO, "docs", "diario", file)
        obiettivo = ""
        if os.path.isfile(percorso):
            with open(percorso, encoding="utf-8") as fh:
                obiettivo = inline(_paragrafo_dopo(fh.read(), "Obiettivo"), base="docs/diario")
        voci.append({"data": data, "titolo": titolo, "file": file,
                     "url": BLOB + "docs/diario/" + file, "obiettivo": obiettivo})
    return voci


def _riassunti_adr():
    """ADR file name -> first sentence (inline HTML) of its row in the table of docs/decisioni.md."""
    riassunti = {}
    for cella, decisione, *_ in tabella_dopo(_leggi("docs", "decisioni.md"), "Decisioni architetturali (ADR)")[1:]:
        link = _RE_LINK.search(cella)
        if not link:
            continue
        frase = _RE_PRIMA_FRASE.match(decisione.strip())
        riassunti[posixpath.basename(link.group(2))] = inline(frase.group(1) if frase else decisione, base="docs")
    return riassunti


def decisioni():
    """ADRs of docs/decisioni/NNNN-*.md sorted by number (numbers are not consecutive).

    Each: numero (int), titolo (plain text, without the "NNNN — " prefix), url (GitHub), riassunto (inline
    HTML of the first sentence of its row in docs/decisioni.md, or "")."""
    riassunti = _riassunti_adr()
    cartella = os.path.join(REPO, "docs", "decisioni")
    adr = []
    for file in sorted(os.listdir(cartella)):
        m = re.match(r"^(\d{4})-.+\.md$", file)
        if not m:
            continue
        titolo = ""
        with open(os.path.join(cartella, file), encoding="utf-8") as fh:
            for riga in fh:
                if riga.startswith("# "):
                    titolo = _RE_TITOLO_ADR.match(riga.rstrip("\n")).group(1)
                    break
        adr.append({"numero": int(m.group(1)), "titolo": titolo,
                    "url": BLOB + "docs/decisioni/" + file, "riassunto": riassunti.get(file, "")})
    return sorted(adr, key=lambda a: a["numero"])


def versione_release(repo=REPO):
    """Latest tag reachable from HEAD (`git describe --tags --abbrev=0`); None without git or tags."""
    try:
        esito = subprocess.run(["git", "describe", "--tags", "--abbrev=0"], cwd=repo, capture_output=True,
                               text=True, stdin=subprocess.DEVNULL, timeout=30)
    except (OSError, subprocess.SubprocessError):
        return None
    versione = esito.stdout.strip()
    return versione if esito.returncode == 0 and versione else None


def classe_stato(stato):
    """CSS class of a roadmap state label (HTML or text): the state starts with "Fatto", "In corso"..."""
    testo = html.unescape(_RE_TAG.sub("", stato)).strip()
    for inizio, classe in CLASSI_STATO:
        if testo.startswith(inizio):
            return classe
    return "stato-altro"


def _sezione(id_, numero, titolo, nota, corpo):
    return (f'<section id="{id_}" class="sez-doc"><div class="contenuto-doc">'
            f'<span class="etichetta">// {numero:02d}</span><h2>{titolo}</h2>'
            f'<p class="nota">{nota}</p>{corpo}</div></section>\n')


def sezioni_sviluppo(radice="../"):
    """HTML body (inside <main>) of the "sviluppo" page: how the game is made, roadmap, diary, ADRs, changelog.

    `radice` is the relative path to the site root, for the links to the other pages."""
    pannelli = "".join(
        f'<article class="pannello-doc"><h3><a href="{a["url"]}">{html.escape(a["titolo"])}</a></h3>'
        f'<p>{a["riassunto"]}</p></article>' for a in decisioni())
    righe = "".join(
        f'<tr><td><span class="stato {classe_stato(stato)}">{stato}</span></td><td>{nome}</td><td>{cosa}</td></tr>'
        for stato, nome, cosa in roadmap())
    voci = "".join(
        f'<li><time>{v["data"]}</time><h3><a href="{v["url"]}">{html.escape(v["titolo"])}</a></h3>'
        f'<p>{v["obiettivo"]}</p></li>' for v in diario())
    elenco = "".join(
        f'<li><a href="{a["url"]}">{a["numero"]:04d} — {html.escape(a["titolo"])}</a></li>' for a in decisioni())
    return (
        _sezione("avanzamento", 1, "Avanzamento",
                 "A che punto siamo con tutti gli aspetti del gioco: la barra è divisa per aspetto, ognuno con la "
                 f'sua percentuale e il link alla sua documentazione; il dettaglio è in <a href="{stato.STATO_URL}">docs/STATO.md</a>.',
                 stato.html_barra(stato.blocchi())) +
        _sezione("come-e-fatto", 2, "Com'è fatto",
                 "Le scelte di fondo dietro al gioco, in breve: simulazione, costruzione, grafica, mappe, "
                 "interfaccia. Ogni scheda porta al documento completo su GitHub; gli sprite e i blocchi "
                 f'si vedono nella pagina degli <a href="{radice}asset/">asset</a>.',
                 f'<div class="pannelli">{pannelli}</div>') +
        _sezione("roadmap", 3, "Roadmap",
                 "Cosa c'è già e cosa arriva. Il gioco è gratuito e open source: ogni tappa finita entra nella "
                 f'release che <a href="{radice}#scarica">scarichi dalla vetrina</a>.',
                 '<table class="tabella"><thead><tr><th>Stato</th><th>Sotto-progetto</th><th>Cosa porta</th>'
                 f'</tr></thead><tbody>{righe}</tbody></table>') +
        _sezione("diario", 4, "Diario",
                 "Come è stato costruito ogni pezzo, tappa per tappa: l'obiettivo di ogni lavoro e il link al "
                 "resoconto completo, con revisioni, scelte e rinvii.",
                 f'<ol class="linea-tempo">{voci}</ol>') +
        _sezione("decisioni", 5, "Decisioni",
                 "L'elenco completo delle decisioni architetturali, numerate nell'ordine in cui sono state prese.",
                 f'<ul class="elenco-adr">{elenco}</ul>') +
        _sezione("changelog", 6, "Changelog",
                 "Le novità versione per versione e gli zip da scaricare.",
                 f'<p>Tutte le modifiche sono nel <a href="{BLOB}CHANGELOG.md">CHANGELOG</a>; ogni versione '
                 f'pubblicata, con gli zip per Windows e Linux, sta nelle <a href="{REPO_URL}/releases">release su '
                 'GitHub</a>.</p>'))
