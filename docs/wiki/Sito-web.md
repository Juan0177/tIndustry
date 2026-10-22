# Sito web

Come si costruisce e si pubblica il sito di tIndustry su GitHub Pages, interamente generato dal repository.

## Cos'è

Il sito ([juan0177.github.io/tIndustry](https://juan0177.github.io/tIndustry/)) nasce da `tools/sito/costruisci.py` e non si scrive mai a mano: sprite, dati, catture e documenti vengono dal repository, quindi per cambiarlo si cambiano l'arte o i documenti e si ricostruisce. Le pagine:

| Pagina | Contenuto |
|---|---|
| `index.html` | La vetrina: una fabbrica di tIndustry disegnata con gli sprite veri alla scala del gioco |
| `sviluppo/index.html` | Come è fatto il gioco: ADR, roadmap, diario e decisioni, letti da `README.md`, `docs/diario/` e `docs/decisioni/` |
| `asset/index.html` | Ogni sprite registrato in `tools/art`, i blocchi composti come li disegna il gioco, il terreno e il carattere |
| `racconto/index.html` | Il racconto guidato dallo scroll, «dal giacimento al CORE»: otto capitoli su quattordici pannelli, fuori dalla barra e con `noindex` finché non è approvato |

## Il riferimento attuale

La direzione del sito è la specifica [2026-10-20 — racconto guidato dallo scroll](https://github.com/Juan0177/tIndustry/blob/main/docs/superpowers/specs/2026-10-20-sito-scroll-narrativo.md): una fabbrica fissa a tutto schermo che si costruisce mentre si scorre, con la camera che segue il minerale fino al CORE. Le specifiche del sito «fabbrica» del 18 e del 19 ottobre sono **superate** e restano solo come storico.

## I file

| File | Ruolo |
|---|---|
| `costruisci.py` | Costruisce tutto il sito in `_site/` (o in un'altra cartella con `--out`) |
| `fabbrica.py` | Le scene a celle della vetrina (nastri, blocchi, giacimenti, energia, terreno) e il controllo che ogni catena vada da chi produce a chi consuma |
| `composti.py` | I blocchi come li disegna il client: strati, movimenti e stati |
| `documenti.py` | Lettura dei documenti del repository (README, diario, ADR, versione) |
| `racconto.py`, `racconto.js` | Dati e motore del racconto guidato dallo scroll, su canvas |
| `sito.js`, `stile.css`, `drone.css` | Script e stili del sito |
| `test_*.py`, `test_*.mjs` | Test Python e Node |

## Comandi

```bash
python tools/sito/costruisci.py                               # scrive _site/
python tools/sito/costruisci.py --out DIR                     # altrove
python -m unittest discover -s tools/sito -p "test_*.py"      # test Python (fabbrica, documenti, pagine, racconto)
node --test tools/sito/test_sito.mjs tools/sito/test_racconto.mjs   # test degli script
```

Servono Python con Pillow e, per i test degli script, Node.js. `_site/` è ignorata da git.

## Pubblicazione

Il sito si pubblica col workflow **Pages** (`.github/workflows/pages.yml`), **a mano** oppure su un tag di release `v*`, mai su push o pull request. Va lanciato da `main`, perché l'ambiente `github-pages` pubblica solo dal ramo principale e dai tag `v*`:

```bash
gh workflow run pages.yml --ref main
```

Il workflow esegue tutti i test del sito, costruisce `_site/` e lo pubblica. La prima volta va scelto *GitHub Actions* come sorgente in Settings → Pages.

## Per approfondire

- [Specifica del racconto guidato dallo scroll](https://github.com/Juan0177/tIndustry/blob/main/docs/superpowers/specs/2026-10-20-sito-scroll-narrativo.md)
- [tools/sito/costruisci.py](https://github.com/Juan0177/tIndustry/blob/main/tools/sito/costruisci.py) (docstring) · [.github/workflows/pages.yml](https://github.com/Juan0177/tIndustry/blob/main/.github/workflows/pages.yml)
- [Diario del sito «fabbrica»](https://github.com/Juan0177/tIndustry/blob/main/docs/diario/2026-10-19-sito-fabbrica.md)
- [Arte generata](Arte-generata) · [Come contribuire](Come-contribuire) · [Test e playtest](Test-e-playtest)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Agenti che giocano](Agenti-che-giocano) · [Torna alla Home](Home) · [Pagina successiva: Come contribuire →](Come-contribuire)

<!-- /navigazione -->
