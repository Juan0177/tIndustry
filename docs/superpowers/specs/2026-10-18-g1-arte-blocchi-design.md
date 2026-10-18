# G1 — Arte dei blocchi generata da script

- **Stato**: bozza, 2026-10-18 (da approvare)
- **Roadmap grafica**: **G1** → [G2a mappe e terreno](2026-10-18-g2a-mappe-terreno-design.md) → [G2b arte del terreno](2026-10-18-g2b-arte-terreno-design.md) → [G3 effetti e luci](2026-10-18-g3-effetti-luci-design.md) → [G4 interfaccia](2026-10-18-g4-interfaccia-design.md). Dopo: sotto-progetto 4 (tier e drone comandato) e sotto-progetto Liquidi.
- **Collegati**: [ADR 0003 — Costruzione come Mindustry](../../decisioni/0003-costruzione-come-mindustry.md), [diario del sotto-progetto 3](../../diario/2026-10-17-costruzione-in-corso.md)

## Obiettivo

Sostituire le icone di terze parti usate per edifici e oggetti con pixel-art originale in stile Mindustry. La genera uno script Python del progetto, sempre uguale a ogni esecuzione, così il gioco resta completamente aperto e senza asset di altri. Il gameplay non cambia: cambia solo l'aspetto.

## Decisioni prese con l'utente

| Tema | Decisione |
|---|---|
| Stile | Pixel-art **nostra**, generata da script, nello stile dei campioni approvati (`.dev/pixelart-campioni`). Nessun file di Mindustry. |
| Ambito | **Mondo e oggetti**: edifici, logistica, Core, drone, le 12 icone degli oggetti. Le icone dell'interfaccia restano per la G4. |
| Risoluzione | **32 px per cella**, mostrati ×2 senza filtro (la cella del mondo è 64 px). |
| Animazioni | **Di base**: nastri, trivella del minatore, bagliore del forno, rotore del generatore, luce lenta del Core. Particelle e luci vere: G3. |
| Core | **4 tier**: 2×2 (iniziale, da tutorial), 3×3, 4×4, 5×5. In G1 solo gli sprite; in partita resta il 2×2 fino al sotto-progetto 4. |
| Drone | **3 tier**: alpha, beta, gamma. Il Core iniziale e il 3×3 usano alpha (i valori di oggi del drone sono quelli di alpha). |
| Strumento | **Approccio A**: generatore Python con tavolozza e forme comuni; disegni a caratteri solo dove le forme non bastano. |
| Struttura | Si possono cambiare strutture del progetto se lo rendono migliore e più scalabile, purché ogni cambio sia scritto (registro, ADR o ARCHITETTURA, diario). |

## Ambito

**Dentro**

- Edifici: `miner`, `miner-advanced`, `smelter`, `assembler`, `extractor`, `generator`, `power-node`, `power-node-t2`.
- Logistica: `conveyor-basic`, `conveyor-fast`, `conveyor-express` (oggi riusa lo sprite del veloce: avrà il suo), curve dei nastri, `junction`, `splitter`, `sorter`, `conveyor-bridge` con la campata.
- Core: `core-1` (2×2) … `core-4` (5×5).
- Drone: `drone-1` (alpha), `drone-2` (beta), `drone-3` (gamma) e il raggio di costruzione.
- Oggetti, in due misure (piccola sui nastri, grande nell'HUD): `iron-ore`, `copper-ore`, `coal`, `lead-ore`, `titanium-ore`, `iron-plate`, `lead-plate`, `titanium-plate`, `copper-wire`, `graphite`, `silicon`, `money`.
- Animazioni di base elencate sopra.

**Fuori**

- Terreno, giacimenti e rocce (G2); particelle, fumo, luci (G3); icone di categorie, menu, vendita, lucchetto, potenza (G4).
- Simulazione, salvataggi, regole, gameplay dei tier (sotto-progetto 4).

## Generatore (`tools/art/`)

- `palette.py`: tutti i colori; rampe di 4–6 toni per materiale (metallo, fuliggine, rame, ferro, piombo, titanio, bagliore, accento). Luce da in alto a sinistra, contorno scuro.
- `forme.py`: mattoncini comuni (pannello in rilievo, bullone, tubo, griglia, ingranaggio, cerchio, bagliore, ombra, striscia di fotogrammi).
- `blocchi/`: un modulo per famiglia (`produzione`, `logistica`, `potenza`, `core`, `drone`, `oggetti`), una funzione per sprite.
- `genera.py`: un comando (`python tools/art/genera.py`) scrive tutti i PNG in `godot/assets/` e la tavola di confronto in `.dev/arte/`. Niente casualità senza seme fisso: stesse uscite a ogni esecuzione.
- Dipendenza: solo Pillow (già usata dagli strumenti di sviluppo).

## Integrazione nel client

- **File**: restano in `godot/assets/` (piatto). Nomi = id di `data/content.json`. Parti di un blocco con suffisso (`miner-base`, `miner-drill`, `smelter-glow`, …). Animazioni = strisce orizzontali di fotogrammi (`<id>-anim.png`). Tier: `core-1…4`, `drone-1…3`.
- **Edifici a strati**: base fissa, parti mobili, bagliore sopra. `MinerVisual`, `SmelterVisual`, `GeneratorVisual`, `AssemblerVisual`, `ExtractorVisual` e `BuildingPad` passano ai nuovi strati. La velocità delle animazioni segue lo stato che il client già legge (fermo senza energia o materiale).
- **Nastri**: lo scorrimento resta in `MindustryBeltVisual` / `ScrollingBeltStrip`, con i nuovi sprite (rullo, bordi, curve).
- **Oggetti sui nastri**: circa 10 px su 32 per cella.
- **Core**: in partita `core-1`; gli altri tier si vedono in una pagina di prova del generatore (tavola) e nelle catture del playtest dedicate, se servono.
- **Drone**: `DroneVisual` usa `drone-1` al posto della forma disegnata dal codice; beta e gamma pronti per il sotto-progetto 4.
- **HUD e pannelli**: palette di costruzione, albero della ricerca, Mercato e HUD usano gli stessi sprite (versione grande per gli oggetti). I percorsi oggi sparsi nel codice (`BuildPalette`, `TechTreeCanvas`, `World`, `MindustryBeltVisual`, …) passano da un solo punto che dà la texture di un id, così aggiungere un blocco non richiede di toccare più file.

## Verifica

- Test del generatore: due esecuzioni producono PNG identici byte per byte.
- Controllo di copertura: ogni id di edifici, strutture e oggetti in `content.json` ha il suo sprite; misure multiple di 32 px per cella del blocco; strisce di animazione con larghezza multipla del fotogramma.
- Build (0 errori, 0 avvisi), test, playtest `ui` e `tutorial` verdi. Le catture cambiano: a fine G1 una nuova base candidata, promossa solo dopo l'ok dell'utente.
- Revisione dell'aspetto: a ogni task grafico la tavola in `.dev/arte/` (nuovo accanto al vecchio, 1× e 2×, su fondo chiaro e scuro). Criteri: sagoma riconoscibile anche in bianco e nero; basi dei blocchi diverse tra loro; il minatore non sembra un ventilatore; contrasto leggibile a zoom 0,4.

## Consegna

PR piccole e in ordine: generatore, tavolozza e forme → logistica → produzione e potenza → Core e drone → oggetti e HUD → documentazione. Ognuna in main solo con revisione superata e CI verde.

Documenti: ADR **0004 — Arte generata da script** (perché, regole di tavolozza e luce, come rigenerare); `ATTRIBUTION.md` ridotto alle icone dell'interfaccia; ARCHITETTURA con "come aggiungere lo sprite di un blocco"; README (anteprima grafica aggiornata con catture vere); diario e mappa Obsidian a fine piano.

## Rischi

- **Gusto**: il lavoro notturno decide da solo seguendo questa spec e i campioni; la tavola finale va all'utente e ciò che non piace si rifà.
- **Leggibilità a zoom basso**: i dettagli da 1 px spariscono a zoom 0,4; le sagome devono reggere da sole.
- **Strati e prestazioni**: più nodi per edificio; con centinaia di edifici il numero di nodi va tenuto sotto controllo (misura nel playtest con 200 blocchi).
