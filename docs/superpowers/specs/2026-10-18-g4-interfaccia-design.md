# G4 — Interfaccia in stile Mindustry

- **Stato**: approvata il 2026-10-18
- **Roadmap grafica**: [G1](2026-10-18-g1-arte-blocchi-design.md) → [G2a](2026-10-18-g2a-mappe-terreno-design.md) → [G2b](2026-10-18-g2b-arte-terreno-design.md) → [G3](2026-10-18-g3-effetti-luci-design.md) → **G4**

## Obiettivo

Un'interfaccia coerente con l'arte dei blocchi, nello stile di Mindustry, con un carattere pixel nostro, testi in più lingue, scala regolabile e opzioni di accessibilità. Spariscono le ultime icone di terze parti.

## Decisioni prese con l'utente

| Tema | Decisione |
|---|---|
| Stile | **Come Mindustry**: pannelli scuri quasi neri, bordo sottile giallo-arancio, icone pixel bianche, testi chiari. |
| Ambito | **HUD** in partita, **pannelli** (Ricerca, Mercato, Campagna, Impostazioni, salvataggi), **menu principale** (sfondo animato della mappa, logo pixel), **icone dell'interfaccia**. |
| Carattere | **Pixel font nostro**, generato dallo script, con lettere accentate. Se un carattere libero (licenza OFL) risulta molto più leggibile, si può usare citandolo; decisione nel piano con una tavola di confronto. |
| Extra | **Scala dell'interfaccia**, **lingue** (italiano e inglese), **accessibilità** (contrasto alto, colori per daltonici). |

## Architettura (client)

- Un **tema** Godot unico (`godot/ui/tema.tres` o generato da codice in `HudStyle`), con colori, bordi, margini e caratteri; tutti i pannelli lo usano invece di stili sparsi. Il piano elenca gli stili oggi sparsi da riunire.
- **Icone dell'interfaccia** generate da `tools/art/` (categorie, menu, ricerca, vendita, Campagna, lucchetto, potenza, impostazioni, salvataggi…), 16 e 32 px. `ATTRIBUTION.md` resta solo per eventuali asset liberi esterni (es. il carattere OFL, se scelto).
- **Carattere**: `tools/art/` disegna i glifi (ASCII più lettere accentate italiane e i segni usati), esportati come font bitmap per Godot; 2 misure (testo e titoli).
- **Lingue**: tutti i testi del client e i nomi in `data/` passano da chiavi di traduzione (file CSV o PO di Godot in `godot/lingue/`); italiano predefinito, inglese completo; scelta nelle impostazioni. I testi della simulazione (motivi di rifiuto) diventano codici tradotti dal client, così `TIndustry.Shared` resta senza lingua e il server futuro non dipende dal testo.
- **Scala**: impostazione 75%–200% applicata al tema e ai pannelli (non al mondo); i pannelli si adattano senza sovrapporsi (verificato nei playtest a 1280×720 e a scala 150%).
- **Accessibilità**: tema a contrasto alto; tavolozza per daltonici per i colori che portano significato (energia, rifiuti, segni di smontaggio, oggetti); il significato non dipende mai solo dal colore (icona o testo accanto).
- **Menu principale**: sfondo con una mappa generata che scorre lentamente con la nuova arte, logo pixel di tIndustry, pulsanti nel nuovo stile.

## Verifica

- Playtest `ui` e `tutorial` in italiano e in inglese, a scala 100% e 150%: nessun testo tagliato o pannello sovrapposto (controllo automatico dei rettangoli dei controlli dove possibile).
- Controllo che non restino testi fissi nel codice del client (ricerca di stringhe tra virgolette nei file UI, con eccezioni elencate).
- Catture per la revisione dell'utente; nuova base delle catture dopo il suo ok.

## Documenti

ADR **0006 — Interfaccia, lingue e carattere**. ARCHITETTURA (tema, come aggiungere un testo o una lingua). godot/README e README (nuove catture). CLAUDE.md (regola: testi solo tramite chiavi di traduzione). Diario e mappa Obsidian.
