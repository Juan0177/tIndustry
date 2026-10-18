# Ricerca: generazione dei giacimenti, Mindustry e tIndustry

Data: 2026-10-18. Ricerca in sola lettura: nessun file del repo è stato modificato.
Sonde usate (fuori dal repo, nello scratchpad): `probe/` (console C# che chiama `MapGenerator.Generate` e `CampaignMapFile.Load` dalla `TIndustry.Shared.dll` Release) e `mindustry_sim.py` (riscrittura approssimata, codice mio, delle regole di Mindustry con un simplex 2D).

---

## 1. Come fa Mindustry

Fonti: `Anuken/Mindustry` ramo master, file `content/Blocks.java`, `content/Items.java`, `world/blocks/environment/Floor.java`, `maps/Maps.java`, `maps/filters/OreFilter.java`, `OreMedianFilter.java`, `ScatterFilter.java`, `maps/generators/BasicGenerator.java`, `PlanetGenerator.java`, `maps/planet/SerpuloPlanetGenerator.java`, `world/blocks/production/Drill.java`. Riassunto con parole mie, solo i numeri.

### 1.1 Principi comuni

- **Il minerale è un "overlay" sul suolo**, non un blocco: sta solo su suoli con `hasSurface()` (niente acqua profonda, catrame, ecc.). Alla fine della generazione si tolgono gli overlay finiti su suoli senza superficie.
- **Giacimenti infiniti.** Una cella di minerale non si esaurisce mai.
- **Trivella = quadrato di celle.** La velocità è proporzionale al numero di celle del minerale dominante sotto la trivella (`dominantItems`). Tempo per pezzo: `drillTime + 50 × durezza`, diviso per le celle. Trivella meccanica 2×2 `drillTime` 600, tier 2; pneumatica 2×2 `drillTime` 400, tier 3; laser 3×3 `drillTime` 280.
- **Durezza** (`Items.java`): sabbia 0, rame 1, piombo 1, scrap 0, carbone 2, titanio 3, torio 4. Una trivella non scava un minerale con durezza superiore al suo tier: **il titanio richiede la pneumatica**, il torio la laser. La rarità "economica" del titanio viene soprattutto da qui, non solo dalla mappa.
- **Sabbia dal suolo**: `sand` e `darksand` hanno `itemDrop = sand` (si scavano con le trivelle ovunque ci sia quel suolo, il giocatore non può a mano). Su Erekir ci sono anche i minerali nelle pareti (`wallOre`: grafite, berillio, tungsteno, torio), messi da `wallOre(...)` solo su celle di parete con almeno un vicino libero.

### 1.2 Mappe dell'editor (filtri di default)

`Maps.addDefaultOres` aggiunge un `OreFilter` per ogni minerale con `oreDefault = true`, nell'ordine dei blocchi, con soglia e scala del minerale:

| Minerale | `oreThreshold` | `oreScale` |
|---|---|---|
| rame | 0,810 | 23,48 |
| piombo | 0,828 | 23,95 |
| carbone | 0,846 | 24,43 |
| titanio | 0,864 | 24,90 |
| torio | 0,882 | 25,38 |

`OreFilter`: rumore simplex 2D a 2 ottave, `falloff` 0,3, scala `scl`; se il valore supera la soglia la cella prende il minerale (filtri successivi sovrascrivono i precedenti). Default del filtro: `scl` 23, `threshold` 0,81, `octaves` 2, `falloff` 0,3, `tilt` 0, `target` qualunque. La soglia sale di 0,018 per ogni gradino del minerale: è **una scala di rarità esplicita**, con il rame il più comune e il torio il più raro. `OreMedianFilter` (raggio 2, percentile 0,5) è un filtro opzionale che pulisce i bordi frastagliati; `ScatterFilter` (probabilità 0,013) sparge decorazioni o blocchi.

### 1.3 Settori procedurali di Serpulo (`SerpuloPlanetGenerator`)

- **Quali minerali.** Sempre rame e piombo (indici 0 e 1). Poi, per settore, un rumore 3D sulla posizione del settore sulla sfera più `poles` (|latitudine|, 0 all'equatore, 1 ai poli): carbone se > 0,325, titanio se > 0,65, torio se > 0,91 (e settore diverso da 218); scrap con probabilità 25%. Quindi **titanio e torio mancano in molti settori** e sono più frequenti verso i poli. La minaccia (`threat`) non cambia i minerali.
- **Frequenza per minerale**: `rand(-0,1; 0,01) − 0,01·i + 0,04·poles`.
- **Posa** (per ogni cella con superficie, coordinate spostate di (−4, +23)): si prova il minerale con indice più alto per primo (scrap, torio, titanio, carbone, piombo, rame); vince il primo che passa due prove:
  - A: |0,5 − rumore(2 ottave, falloff 0,7, scala 40 + 2i)| > 0,22 + 0,01i
  - B: |0,5 − rumore(1 ottava, scala 30 + 4i)| > 0,37 + freq
  L'uso di |0,5 − n| crea **macchie su entrambe le code** del rumore (vene a coppie, forme a fascia), e l'intersezione di due rumori a scale diverse spezza le fasce in blob di 10–30 celle.
- **Scala**: la mappa di un settore è ~280–320 celle di lato (`getSizeScl` 3200 × raggio del settore); il rumore è campionato sulla sfera in modo che 1 cella ≈ 1 unità, quindi le scale 30–48 sono in celle.
- **Dopo**: `median(2)` agisce su suoli e blocchi (non sugli overlay), poi si tolgono i minerali sui suoli senza superficie. Lo scrap nasce anche attorno alle rovine (0–4 per mappa, footprint pieno, 40% a distanza 1, 20% a distanza 2; mai entro 18 celle dallo spawn), e col 33% trasforma il suolo in metallo danneggiato.
- **Partenza**: nessuna garanzia esplicita di rame o piombo vicino al Core. Funziona perché rame e piombo ci sono sempre, sono l'ultima scelta del ciclo (riempiono ciò che resta) e la densità è alta: in un raggio di 50 celle cadono ~10 macchie di ciascuno. Lo spawn è su un anello a `width/2,55 − rand(13, 23)` dal centro, in una stanza di raggio 8–15; i fiumi stanno a 12 + qualcosa celle dallo spawn.
- **Settori a mano** (Ground Zero, Frozen Forest…): mappe disegnate, ricchissime di rame/piombo vicino al Core; il titanio arriva solo in settori successivi. La progressione della rarità è per **settore**, non per distanza dentro la mappa.

### 1.4 Numeri misurati sulla riscrittura (approssimata)

`mindustry_sim.py`, 300×300, simplex 2D al posto del 3D (le soglie sono le stesse ma la distribuzione del rumore è solo simile: valori indicativi, ±30%).

| | rame | piombo | carbone | titanio | torio | totale |
|---|---|---|---|---|---|---|
| Editor, copertura | 3,4–4,3% | 2,7–2,8% | 1,8–2,0% | 1,3–1,4% | 0,8% | 10–11% |
| Editor, macchia media | 18–24 | 17–19 | 15 | 12–15 | 11–13 | |
| Serpulo, copertura (poles 0,5, tutti presenti) | 1,3–2,4% | 1,2–3,4% | 1,4–2,4% | 1,3–5,2% | 2,3–4,9% | 10–14% |
| Serpulo, macchia media | 11–16 | 14–22 | 17–26 | 21–37 | 30–43 | |
| Macchie entro 50 dallo spawn | 8–15 | 8–16 | 2–14 | 4–15 | 2–10 | |
| Cella più vicina allo spawn | 1–18 | 3–32 | 9–27 | 1–21 | 1–29 | |

Nota: in Serpulo, se il settore ha titanio e torio, questi hanno priorità e coprono anche più del rame; la rarità vera la danno (a) il fatto che manchino nella maggior parte dei settori e (b) il tier della trivella.

---

## 2. Come facciamo noi (`biomi-1`)

File: `src/TIndustry.Shared/Grid/MapGenerator.cs`, `Noise.cs`, `MapGenSettings.cs`, `TerrainMap.cs`, `Factory.cs` (minatore), `docs/decisioni/0005-mappe-seme-e-file.md`, `data/maps/*.json`, `data/content.json`.

- **Minerali**: ferro, rame, carbone, piombo, titanio (`DepositKind`). Niente sabbia, scrap, minerali nelle pareti.
- **Infiniti**, come Mindustry. **Minatore 2×2**, velocità proporzionale alle celle del minerale dominante (`CountDepositTiles`), `MiningDurationSeconds` 2. **Nessuna durezza e nessun tier**: il Minatore T1 scava il titanio come il ferro.
- **Posa** (per cella, dopo acqua e pareti; mai su pareti o acqua):
  - "Campo di minerale": `Value2D(cella 90) > 30000` → circa metà mappa può avere minerale, l'altra metà no (zone vuote larghe ~90 celle).
  - Per ogni minerale `Fractal(cella 14, 2 ottave)`; soglia `55000 − bonus[bioma][minerale]` (scala 0–65535, poi `DepositPercent`). Vince il minerale che supera la soglia di più.
  - Bonus per bioma (ferro, rame, carbone, piombo, titanio): rocce 7000/0/6000/1500/0, sabbia 0/7000/0/6000/0, neve 1500/0/0/0/7000, prati 3500/3500/3500/3000/1500.
  - **Stessa soglia base per tutti i minerali**: nessuna scala di rarità; la differenza la fanno solo i biomi.
- **Partenza**: disco libero di raggio 10 attorno al Core (giacimenti tolti), ferro 2×2 a (core.X − 4, core.Y), rame 2×2 accanto; poi `EnsureLandPaths` garantisce che ferro, rame e carbone siano **raggiungibili** a terra (scava un passaggio a L o posa un 2×2 se la mappa non ne ha). Nessuna garanzia di **distanza** e nessuna garanzia per il piombo.
- **Distanza dal Core**: nessun effetto. Titanio vicino al Core quanto il ferro.
- **Campagna**: mappe in file generate con `biomi-1` (200→1000 di lato), biomi rocce+prati fino al livello 3, +sabbia dal 4, +neve dal 9; titanio escluso fino al livello 8 (`--escludi titanio`).

### 2.1 Numeri misurati (sonda C#)

Partite normali, tutti i biomi, impostazioni al 100%. "Vicina" = cella naturale più vicina al Core oltre il disco di partenza (i 2×2 garantiti esclusi). "Entro R" = macchie (4-connesse) con almeno una cella entro R celle dal Core.

**Media 1000×1000, semi 1, 2, 3, 42, 777** (terra 70–76%, pareti 13–16%, acqua 11–13%):

| | ferro | rame | carbone | piombo | titanio | totale |
|---|---|---|---|---|---|---|
| Copertura della mappa | 1,2–1,8% | 0,8–1,05% | 1,0–1,7% | 0,9–1,1% | 1,0–1,4% | 6,4–7,1% |
| Macchie | 344–429 | 266–350 | 296–390 | 252–348 | 281–341 | |
| Macchia media / mediana | 34–42 / 17–20 | 29–38 / 13–20 | 35–43 / 18–21 | 32–36 / 15–21 | 37–46 / 15–19 | |
| Macchia massima | 385–593 | 300–536 | 348–618 | 328–545 | 341–660 | |
| Cella naturale più vicina | 16–29 | 27–52 | 16–50 | 16–45 | 19–46 | |
| Macchie entro 50 | 4–8 | 0–4 | 1–7 | 1–5 | 1–4 | |
| Macchie entro 100 | 11–25 | 5–15 | 7–20 | 7–12 | 3–15 | |

**Grande 1500 (semi 1, 2) ed Enorme 2000 (semi 1, 2)**: stesse coperture (ferro 1,3–1,7%, rame ~1,0%, carbone 1,3–1,6%, piombo ~1,0–1,1%, titanio ~1,2%), macchia media 34–41. La partenza peggiora per caso: Grande seme 1 ha **ferro naturale a 71, rame a 90, carbone a 65, titanio a 64** (zero macchie di qualunque minerale entro 50, un solo rame entro 100 contro 17 titani).

**Densità dentro e fuori dal bioma favorito** (copertura del suolo del bioma): il minerale col bonus 7000 copre 3,5–5,6% del suo bioma, fuori 0,3–0,7% (prati ~1,0–2,5% per tutti). Rapporto 7–10×: un bioma senza bonus per il rame (rocce, neve) ha quasi zero rame.

**Campagna** (mappe in `data/maps/`):

| Livello (lato, biomi) | ferro | rame | carbone | piombo | titanio | rame più vicino |
|---|---|---|---|---|---|---|
| 01 (200, rocce+prati) | 5,3% | 1,25% | 5,0% | 1,0% | — | 38 |
| 02 (250) | 4,2% | 0,9% | 2,7% | 0,9% | — | 16 |
| 03 (300) | 1,3% | 0,7% | 2,1% | 0,4% | — | 33 |
| 04 fili (350, +sabbia) | 2,2% | 0,7% | 1,8% | 1,2% | — | 39 |
| 05 (400) | 1,1% | 1,2% | 1,4% | 0,7% | — | 58 |
| 06 (500) | 2,1% | 1,0% | 1,9% | 1,1% | — | 24 |
| 07 piombo (600) | 1,7% | 1,2% | 1,6% | 0,8% | — | 34 (piombo 47) |
| 08 (700) | 2,0% | 0,9% | 1,7% | 1,25% | — | 14 |
| 09 titanio (850, +neve) | 1,6% | 1,1% | 1,1% | 1,2% | 1,1% | 22 (titanio 20) |
| 10 (1000) | 1,6% | 1,2% | 1,2% | 0,9% | 1,3% | 48 |

Tutte passano `valida` (minerali degli obiettivi entro 60), ma il livello 4 "fili", che chiede rame, ne ha meno di tutti (0,7%, macchia media 20) e il livello 7 "piombo" ha il piombo a 47 celle.

---

## 3. Confronto

| Aspetto | Mindustry | tIndustry `biomi-1` |
|---|---|---|
| Lato tipico della mappa | ~300 (settore), editor a scelta | 1000–2000 (partite), 200–1000 (campagna) |
| Copertura totale | 10–14% | 6,4–7,5% |
| Scala di rarità per minerale | Sì: soglie 0,81→0,882 (editor); titanio/torio solo in certi settori (Serpulo) | **No**: soglia base unica, rarità solo dal bioma |
| Rame (minerale base) | il più comune, 2–4% | il meno comune insieme al piombo, ~1% |
| Titanio | 1,3% (editor), assente in molti settori, **serve trivella tier 3**, scavo più lento (durezza 3) | 1,0–1,4%, come il ferro, **scavabile col T1 alla stessa velocità** |
| Dimensione macchia media | 11–25 celle (editor 12–24) | 30–46 celle (mediana ~18), massimo ~600 |
| Macchie entro 50 dal Core, per minerale | ~8–15 | 0–8 (rame spesso 0–2) |
| Cella più vicina dei minerali base | 1–18 | 16–52 (fino a 90 su Grande) |
| Garanzia alla partenza | nessuna, ma densità alta; settori iniziali disegnati | 2×2 di ferro + 2×2 di rame (un minatore ciascuno) + carbone raggiungibile a qualunque distanza |
| Effetto della distanza | nessuno dentro la mappa; progressione fra settori | nessuno |
| Vuoti grandi | no | sì: maschera "campo di minerale" a cella 90 toglie metà mappa |
| Specializzazione per bioma | blanda (suolo, latitudine) | forte: 7–10× fra bioma favorito e gli altri |
| Infiniti / trivella ∝ celle | sì / sì | sì / sì |
| Sabbia dal suolo, scrap, minerali in parete | sì | no |

---

## 4. Squilibri concreti trovati

1. **Il titanio non è raro né lontano.** Copre quanto il ferro (1,0–1,4%), sta a 19–46 celle dal Core nelle Medie, e senza tier/durezza il Minatore T1 lo scava come il ferro. Il suo prezzo di mercato (12 vs 8 del ferro grezzo, 45 la lastra) non è bilanciato da alcuna difficoltà di accesso. Nelle partite normali l'intera progressione ferro → rame → piombo → titanio è disponibile al minuto uno.
2. **Rame scarso proprio dove serve.** È un minerale base (fili, livello 4), ma nelle mappe a seme è il meno coperto (~1%) e ha il bonus solo nella sabbia: entro 50 dal Core ci sono spesso 0–2 macchie di rame. In Mindustry il rame è il più comune.
3. **La partenza dipende dal caso sulle mappe grandi.** La garanzia è di un 2×2 di ferro e uno di rame (un minatore ciascuno) e di carbone *raggiungibile*, non *vicino*. Su Grande seme 1 il primo ferro naturale è a 71, il rame a 90, il carbone a 65, e non c'è nessuna macchia di niente entro 50. Il piombo non è garantito affatto.
4. **Macchie poche e grandi, con vuoti larghi.** Rumore a cella 14 + maschera a cella 90 → macchie medie di 35–45 celle (mediana ~18, massimi ~600) separate da zone vuote larghe ~90 celle. Mindustry ha macchie di 12–25 celle ma 2–3 volte più fitte: il giocatore trova sempre qualcosa nel raggio dello schermo.
5. **Biomi troppo esclusivi.** Bonus 7000 contro 0 dà 7–10× di densità: rocce e neve sono quasi senza rame, sabbia quasi senza ferro e carbone. Con la partenza al centro, il bioma del centro decide cosa è facile.
6. **Campagna sbilanciata nei primi livelli.** Con solo rocce+prati (livelli 1–3) ferro e carbone coprono 4–5% mentre rame e piombo ~1%; il livello 4 (fili di rame) ha il rame più povero di tutta la campagna (0,7%).
7. **Nessuna progressione spaziale** su mappe di 1000–2000 celle: non c'è motivo di espandersi verso l'esterno per trovare minerali migliori, a differenza della progressione per settori di Mindustry.

---

## 5. Proposte per un futuro `biomi-2` (in ordine di priorità)

Vincolo: `biomi-1` non si tocca (impronte in `MapGeneratorTests.HashIsStableAcrossRuns`); `biomi-2` è una copia di `MapGenerator` con nome nuovo, `Factory.RestoreTerrain` sceglie per nome, e il client userà `biomi-2` solo per le partite nuove. Le mappe della campagna sono file: rigenerarle con `biomi-2` è possibile (stessa misura → i salvataggi si caricano) ma sposterebbe i giacimenti sotto le fabbriche salvate, quindi va deciso a parte.

1. **Scala di rarità per minerale (soglia base per minerale).** Come le soglie 0,81→0,882 di Mindustry: una base diversa per minerale invece di 55000 per tutti. Obiettivo di copertura sulla mappa: ferro ~2,5%, rame ~2,5%, carbone ~1,8%, piombo ~1,5%, titanio ~0,6–0,8%. *Perché*: è lo squilibrio maggiore (punti 1 e 2), ed è un parametro solo.
2. **Gradiente di distanza dal Core.** Moltiplicatore della soglia in funzione della distanza: entro ~120 celle titanio quasi assente (o zero) e minerali base più fitti; titanio pieno oltre ~250–300 celle (scalato col lato della mappa). *Perché*: sostituisce la progressione per settori di Mindustry sulle nostre mappe grandi e dà un motivo per espandersi e costruire logistica lunga (nastri T2, ponti), che è il cuore del gioco.
3. **Partenza garantita "vera".** Oltre ai 2×2 attuali (da tenere: il tutorial li usa), garantire una macchia naturale di almeno 12–20 celle (3–4 minatori) di ferro e di rame entro 20–30 celle, carbone e piombo entro 40–60; se mancano, posarle (non solo scavare un passaggio). *Perché*: oggi su Grande/Enorme la partenza può essere a 70–90 celle dai minerali (punto 3); Mindustry ottiene lo stesso effetto con densità alta e settori iniziali disegnati.
4. **Macchie più piccole e più fitte, senza vuoti larghi.** Rumore del minerale a cella ~10 invece di 14 e maschera del campo a cella ~45–60 con soglia più bassa (o eliminata a favore dell'intersezione di due rumori, come le prove A e B di Serpulo). Obiettivo: macchia media 15–25 celle, almeno 5 macchie di ciascun minerale base entro 50 dal Core. *Perché*: punto 4; rende la mappa leggibile e la scelta del sito più interessante.
5. **Biomi meno esclusivi.** Bonus del minerale tipico ~4000–5000 e un minimo per ferro e rame in tutti i biomi (per esempio 2000), così il rapporto scende da 7–10× a ~3×. *Perché*: punto 5; un Core in neve o rocce non deve restare senza rame.
6. **Copertura totale un po' più alta**, ~8–9% (da 6,5–7%). Non serve arrivare al 10–14% di Mindustry, perché le nostre mappe sono 10–40 volte più grandi in area; conta la densità vicino al Core, che le proposte 2–4 sistemano. *Perché*: meno giri a vuoto, senza appiattire la scelta.
7. **(Fuori dal generatore, non serve `biomi-2`) Durezza o tier del titanio.** Titanio scavabile solo dal Minatore T2, o tempo di scavo `base + k × durezza` come Mindustry (durezze: ferro/rame/piombo 1, carbone 2, titanio 3). *Perché*: è la metà "economica" della rarità del titanio in Mindustry; è un cambio di contenuto/simulazione (e forse di salvataggio se cambia lo stato dei minatori), indipendente dal terreno. Potrebbe da solo risolvere buona parte del punto 1.
8. **Strumento di misura prima di pubblicare `biomi-2`.** Un comando `MapTool statistiche` (la sonda di questa ricerca: copertura, macchie, media, vicina, entro 50/100 per minerale) e test di intervallo sulle statistiche di `biomi-2` per alcuni semi e misure. *Perché*: il generatore pubblicato poi è congelato; i numeri vanno fissati prima.
9. **Facoltativo, contenuti nuovi.** Sabbia dai suoli sabbia/sabbia scura (come Mindustry), scrap attorno a rovine, minerali in parete. *Perché*: varietà e uso dei biomi; richiedono oggetti e ricette nuove, quindi vanno dopo il bilanciamento.

Campagna: dopo `biomi-2`, valutare di rigenerare almeno i livelli 1–4 con più rame (o con la sabbia fra i biomi dal livello 3), e aggiungere a `valida` un controllo di quantità (celle o macchie entro 60) oltre a quello di presenza.

---

## Appendice: come rifare le misure

- `probe/` nello scratchpad: `dotnet run -c Release -- seed` (Media 5 semi, Grande 2, Enorme 2) e `dotnet run -c Release -- campagna` (mappe di `data/maps/`). Legge solo la DLL Release e `data/`, non scrive niente.
- `mindustry_sim.py`: `python mindustry_sim.py` (numpy). Riscrittura approssimata, simplex 2D al posto del 3D di Serpulo.
