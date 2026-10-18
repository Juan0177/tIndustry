# G2a — Mappe e terreno: biomi, pareti, acqua, mappe a seme e della campagna

- **Stato**: bozza, 2026-10-18 (da approvare)
- **Roadmap grafica**: [G1](2026-10-18-g1-arte-blocchi-design.md) → **G2a** → [G2b](2026-10-18-g2b-arte-terreno-design.md) → [G3](2026-10-18-g3-effetti-luci-design.md) → [G4](2026-10-18-g4-interfaccia-design.md). Dopo la grafica: sotto-progetto 4 (tier e drone comandato), poi un sotto-progetto **Liquidi**.

## Obiettivo

Dare alle mappe la struttura di Mindustry. Le partite normali si generano da un seme in tre misure, con tutti i biomi su una sola mappa. La campagna usa mappe prestabilite, salvate come file, di misure diverse per livello. Il terreno ha pareti che bloccano, acqua bassa costruibile e acqua profonda bloccante. È la parte **di regole** della G2: l'aspetto arriva nella G2b. Qui il client disegna il nuovo terreno con colori semplici, come fa oggi.

## Decisioni prese con l'utente

| Tema | Decisione |
|---|---|
| Biomi | 4: **rocce e basalto**, **sabbia e deserto**, **neve e ghiaccio**, **prati e paludi**. Più biomi nella stessa mappa. |
| Giacimenti | Puntini di minerale sul suolo (aspetto: G2b); in regole un giacimento resta una proprietà della cella. |
| Pareti | Bloccano la costruzione; una roccia-parete per bioma. |
| Acqua | **Bassa**: si costruisce sopra. **Profonda**: non si costruisce, i nastri non la attraversano, i ponti sì. Pompe, tubi e ricette con liquidi: sotto-progetto **Liquidi** a parte. |
| Partite normali | Mappa finita, generata dal seme, in 3 misure: **media 1000×1000, grande 1500×1500, enorme 2000×2000**, con tutti i biomi. Seme casuale già compilato, modificabile e visibile. |
| Campagna | Mappe **prestabilite in file** con misure diverse per livello (da 200 a 1000 per lato). Le creo io dal generatore nuovo con i biomi adatti a ogni livello, poi le ritocco; l'utente le guarda. |

## Modello (TIndustry.Shared)

- `TerrainTile` passa da `(TerrainKind, DepositKind)` a **suolo + parete + giacimento**:
  - `FloorKind`: suoli per bioma (es. `Basalt`, `Stone`, `Gravel`, `Sand`, `DarkSand`, `Dune`, `Snow`, `Ice`, `Grass`, `Moss`, `Mud`), più `ShallowWater` e `DeepWater`. Elenco esatto e colori di base fissati nel piano.
  - `WallKind`: `None` o una parete per bioma (`BasaltWall`, `Sandstone`, `IceWall`, `Shrub/Rock`…).
  - `DepositKind`: come oggi (ferro, rame, carbone, piombo, titanio).
  - Il bioma di una cella si ricava dal suolo (tabella in `data/content.json`, sezione `terrain`), così aggiungere un suolo è un dato e non codice.
- **Regole**: `CanPlace` rifiuta una cella con parete o acqua profonda, con motivi nuovi e testi in italiano ("C'è una parete", "Acqua profonda"). I nastri non si piazzano su acqua profonda; il ponte la scavalca (l'uscita del ponte deve stare su una cella costruibile). I minatori lavorano solo su giacimenti di celle costruibili. Il drone vola sopra tutto (nessun cambio).
- **Determinismo**: la generazione usa solo il seme e interi o float con lo stesso ordine; nessun `Random` non seminato, nessun ordine di dizionari.

## Generatore a seme

- `MapGenerator` (nuovo, in Shared) con parametri `MapGenSettings` (misura, seme, biomi ammessi, densità di pareti, acqua, giacimenti).
- Biomi con rumore a bassa frequenza (zone grandi), suoli con rumore fine, pareti in macchie e creste ai margini dei biomi, acqua come laghi e fiumi (profonda al centro, bassa ai bordi), giacimenti a gruppi con frequenze per bioma (es. titanio sulla neve, rame nel deserto).
- **Partenza garantita**: attorno al Core un'area libera costruibile, con ferro e rame a breve distanza; il percorso dal Core ai primi giacimenti non è chiuso da pareti o acqua profonda (verifica con riempimento).
- Prestazioni: generare 2000×2000 deve restare sotto qualche secondo (misura nel test, soglia nel piano) e la memoria sotto controllo (celle compatte: byte per suolo/parete/giacimento).

## Mappe della campagna in file

- Formato: `data/maps/<livello>.png` (un colore per suolo, parete e acqua) più `data/maps/<livello>.json` (giacimenti, posizione del Core, eventuali punti speciali). Facile da ritoccare con qualunque editor di immagini e da leggere in diff.
- Uno strumento `tools/maps/` crea il file di un livello dal generatore (seme, misura, biomi) e lo riconverte; lo stesso strumento valida una mappa (partenza garantita, misure, colori noti).
- I livelli di `data/campaign.json` puntano alla loro mappa invece di `mapWidth`/`mapHeight`/seme. Misure per livello proposte nel piano (crescono con la campagna), obiettivi di oggi rispettati.

## Partita normale: nuova schermata

Nella schermata Nuova partita: misura (media, grande, enorme) e seme (casuale già compilato, modificabile, copiabile). Aspetto semplice: lo stile definitivo arriva con la G4.

## Salvataggi

Versione **12**. Una partita normale salva seme e impostazioni (il terreno si rigenera, come oggi). Una partita della campagna salva l'id della mappa. Le modifiche fatte dal giocatore al terreno (oggi nessuna) restano fuori. Migrazione v11 → v12: le partite esistenti tengono il vecchio generatore, marcato come "classico", così il terreno sotto le loro fabbriche non cambia. Nessun salvataggio esistente diventa illeggibile.

## Client

- `World.Drawing` disegna il terreno a pezzi (chunk) con colori per suolo, parete e acqua. Disegnare 2000×2000 celle senza cali di frequenza richiede di disegnare solo i pezzi visibili, preparati una volta. La grafica vera arriva nella G2b.
- Il fantasma del piazzamento mostra il rifiuto su pareti e acqua profonda.

## Verifica

- Test: stesso seme → stessa mappa (checksum); partenza garantita su molti semi; regole di piazzamento su pareti, acqua bassa e profonda, ponte sopra l'acqua profonda; migrazione v11 → v12; validazione dei file della campagna.
- Playtest `ui` e `tutorial` verdi (il tutorial usa la sua mappa disegnata). Misura della frequenza con mappa enorme.

## Documenti

ADR **0005 — Mappe: generate da seme e prestabilite in file**. ARCHITETTURA (terreno, mappe, come aggiungere un suolo o un livello). CLAUDE.md: salvataggi v12. Diario e mappa Obsidian.

## Rischi

- Mappe enormi e prestazioni del client: chunk e disegno solo del visibile sono obbligatori.
- Campagna: cambiare le mappe può rendere un livello più facile o più difficile; le mappe vanno riguardate dall'utente.
