# Decisioni

Indice delle decisioni del progetto. Le decisioni architetturali (ADR) stanno in `docs/decisioni/`. Le scelte prese durante i lavori si trovano nel [diario](diario/README.md), una voce per piano.

## Decisioni architetturali (ADR)

| ADR | Decisione | Stato |
|---|---|---|
| [0001 — Il server multiplayer sarà in C#](decisioni/0001-server-csharp.md) | Il server per il co-op riusa la simulazione `TIndustry.Shared` in C#, senza un secondo linguaggio. | accettata, 2026-10-15 |
| [0002 — Sincronizzazione multiplayer ibrida](decisioni/0002-sincronizzazione-ibrida.md) | Un giocatore ospita la partita e ordina i comandi. Tutti simulano in modo deterministico. Checksum per accorgersi delle divergenze, istantanea per recuperare o per entrare a partita in corso. | accettata, 2026-10-15 |
| [0003 — Costruzione come Mindustry](decisioni/0003-costruzione-come-mindustry.md) | Piazzare crea un piano; il drone automatico lo raggiunge e un cantiere separato lo costruisce nel tempo, prelevando dal Core man mano. Smontaggio con rimborso graduale; `InstantBuild` per test e demo; salvataggi v11. | accettata, 2026-10-17 |
| [0004 — Arte generata da script](decisioni/0004-arte-generata-da-script.md) | Sprite di edifici, nastri, Core, drone e oggetti generati da `tools/art/genera.py`: pixel-art nostra a 32 px per cella mostrata ×2, tavolozza unica con luce da in alto a sinistra, strisce di fotogrammi quadrati, un solo accesso `Sprites` nel client, oggetti senza tinta. Mai PNG ritoccati a mano. Dalla G3 anche particelle, Lampada e texture della luce. | accettata, 2026-10-18 |
| [0005 — Mappe: generate da seme e prestabilite in file](decisioni/0005-mappe-seme-e-file.md) | Partite normali dal seme col generatore a biomi (interi soli, partenza garantita, Media/Grande/Enorme fino a 2000²), campagna da `data/maps/` (PNG del terreno e dei giacimenti più json, validati da `tools/MapTool`). Cella di 3 byte (suolo, parete, giacimento), tabella `terrain` nei dati, regole in un solo punto. Salvataggi v12 col generatore; i v11 restano sul generatore classico. | accettata, 2026-10-18 |

## Scelte di prodotto (dalle conversazioni)

- **Co-op**: cooperativo, ospitato da un giocatore, con il giocatore come piccolo drone, come in Mindustry.
- **Costruzione**: il più simile possibile a Mindustry. Rimborso allo smontaggio al 100% in partita base e sandbox; in campagna è fissato per livello (100% nei livelli 1–3, 50% dal 4). Un cantiere annullato restituisce sempre tutto quanto speso. Niente riposizionamento gratuito degli edifici.
- **Grafica**: pixel-art originale nostra, generata da script, in stile Mindustry ([ADR 0004](decisioni/0004-arte-generata-da-script.md)). Nessun file di Mindustry e nessun cambio di licenza.
- **Tier**: il Core ha quattro livelli (da 2×2 a 5×5) e il drone tre tier (alpha, beta, gamma), con effetti sul gameplay come in Mindustry. Gli sprite ci sono dalla G1; il gameplay sarà pianificato con il sotto-progetto 4.
- **Mappe**: partite normali da seme in tre misure (Media 1000, Grande 1500, Enorme 2000) con tutti i biomi; campagna con mappe prestabilite di misure diverse per livello; pareti e acqua profonda bloccano la costruzione, l'acqua bassa no ([ADR 0005](decisioni/0005-mappe-seme-e-file.md)). Pompe e liquidi: sotto-progetto a parte.
- **CI**: GitHub Actions solo manuali o su tag di release.
- **Dati del giocatore**: mai toccati da sviluppo e playtest, che usano sempre un profilo temporaneo.
