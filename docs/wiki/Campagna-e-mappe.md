# Campagna e mappe

I dieci livelli della campagna con i loro obiettivi, le mappe, i biomi e la partita libera.

![Biomi: rocce, sabbia, neve, prati, pareti e acqua](https://raw.githubusercontent.com/Juan0177/tIndustry/main/docs/img/readme/terreno-biomi.png)

## La campagna

`G` (o **Campagna** dalla Home o dall'HUD) apre l'elenco dei livelli: ogni livello è **BLOCCATO**, **GIOCA** o **COMPLETATO**. Completare un livello sblocca il successivo. Gli obiettivi del livello in corso compaiono in alto a sinistra (`OBIETTIVO`); quando li completi tutti un avviso dice "Livello completato".

Ogni livello parte con soldi e materiali propri e con la Ricerca da capo (solo Nastro T1 e Minatore T1 sbloccati). Le mappe sono prestabilite e crescono con la campagna. Dal livello 4 lo smontaggio rimborsa solo il 50% (vedi [Costruzione](Costruzione)). I livelli 6 e 10 sono di notte.

| # | Livello | Mappa | Biomi | Partenza | Rimborso | Obiettivi |
|---|---|---|---|---|---|---|
| 1 | Primi passi | 200×200 | rocce, prati | $220, 8 Ferro grezzo, 60 Lastre di ferro, 8 Fili di rame | 100% | Vendi 3 ferro grezzo; Guadagna $24 dalle vendite |
| 2 | Prima fabbrica | 250×250 | rocce, prati | $180, 10 Ferro grezzo, 48 Lastre di ferro, 10 Fili di rame | 100% | Magazzino 12 ferro grezzo |
| 3 | Lastre di ferro | 300×300 | rocce, prati | $280, 20 Ferro grezzo, 25 Lastre di ferro, 12 Fili di rame | 100% | Sblocca il forno; Magazzino 12 lastre di ferro |
| 4 | Fili di rame | 350×350 | rocce, sabbia, prati | $520, 20 Ferro grezzo, 55 Lastre di ferro, 6 Fili di rame | 50% | Sblocca l'assemblatore; Magazzino 4 fili di rame; Vendi 2 fili di rame |
| 5 | Economia Foundry | 400×400 | rocce, sabbia, prati | $160, 20 Ferro grezzo, 12 Lastre di ferro, 8 Fili di rame | 50% | Guadagna $150 dalle vendite; Magazzino 20 lastre |
| 6 | Corrente (notte) | 500×500 | rocce, sabbia, prati | $450, 20 Ferro grezzo, 40 Lastre di ferro, 14 Fili di rame, 24 Carbone | 50% | Sblocca il generatore; Sblocca il Nodo T1; Magazzino 16 carbone |
| 7 | Venatura di piombo | 600×600 | rocce, sabbia, prati | $320, 20 Ferro grezzo, 35 Lastre di ferro, 12 Fili di rame, 18 Carbone | 50% | Magazzino 10 piombo grezzo; Magazzino 6 lastre di piombo |
| 8 | Logistica selettiva | 700×700 | rocce, sabbia, prati | $560, 20 Ferro grezzo, 50 Lastre di ferro, 16 Fili di rame | 50% | Sblocca l'incrocio; Sblocca il selezionatore; Magazzino 28 lastre di ferro |
| 9 | Titanio raro | 850×850 | rocce, sabbia, neve, prati | $950, 20 Ferro grezzo, 95 Lastre di ferro, 30 Fili di rame, 30 Carbone, 8 Lastre di piombo | 50% | Magazzino 6 titanio grezzo; Magazzino 3 lastre di titanio; Magazzino 4 silicio |
| 10 | Espansione T2 (notte) | 1000×1000 | rocce, sabbia, neve, prati | $1100, 20 Ferro grezzo, 70 Lastre di ferro, 24 Fili di rame, 20 Carbone, 6 Lastre di piombo | 50% | Sblocca il Minatore T2; Sblocca il Nastro T2; Guadagna $400 dalle vendite; Magazzino 8 grafite |

Tipi di obiettivo:

| Obiettivo | Cosa chiede |
|---|---|
| Vendi N … | Vendere N pezzi di quell'oggetto |
| Guadagna $N dalle vendite | Incassare N dollari vendendo |
| Magazzino N … | Avere N pezzi di quell'oggetto nel CORE |
| Sblocca … | Sbloccare quel blocco nella Ricerca |

Il titanio compare sulle mappe solo dal livello 9, dove serve il Minatore T2.

## Partita libera

**Nuova partita** dalla Home genera una mappa dal **seme**:

| Misura | Lato |
|---|---|
| Media | 1000×1000 |
| Grande | 1500×1500 |
| Enorme | 2000×2000 |

Il seme è già compilato con un numero casuale: puoi cambiarlo (un intero tra -2147483648 e 2147483647), sceglierne un altro con **Nuovo seme** o copiarlo con **Copia**. Lo stesso seme con la stessa misura dà sempre la stessa mappa. La casella **Notte** fa una mappa al buio. **Inizia** (o Invio) avvia, **Indietro** (o Esc) torna alla Home. Per Grande ed Enorme compare "Generazione della mappa…" per qualche secondo.

Si parte con $180, 48 Ferro grezzo, 48 Lastre di ferro e 10 Fili di rame, e lo smontaggio rimborsa il 100%.

## Mappe e biomi

| Bioma | Suoli | Pareti |
|---|---|---|
| Rocce | pietra, basalto, ghiaia | parete di roccia |
| Sabbia | sabbia, sabbia scura | parete di sabbia |
| Neve | neve, ghiaccio | parete di ghiaccio |
| Prati | erba, muschio, fango, terra | parete di arbusti |

In ogni bioma possono esserci laghi e fiumi: **acqua bassa** sulle rive (si costruisce) e **acqua profonda** al centro (non si costruisce, i nastri non la attraversano, il ponte la scavalca). Sulle **pareti** non si costruisce nulla. Il drone vola sopra tutto.

Il CORE parte al centro, in un'area libera con ferro e rame vicini e una strada a terra verso ferro, rame e carbone. I giacimenti sono ferro, rame, carbone, piombo e titanio (vedi [Risorse e oggetti](Risorse-e-oggetti)).

## Vedi anche

- [Primi passi](Primi-passi)
- [Economia e Ricerca](Economia-e-Ricerca)
- [Salvataggi](Salvataggi)
