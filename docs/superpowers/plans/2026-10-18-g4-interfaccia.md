# G4 — Interfaccia in stile Mindustry: piano di implementazione

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** interfaccia coerente con l'arte dei blocchi in stile Mindustry: tema unico, carattere pixel nostro, icone nostre, testi in italiano e inglese, scala solo dell'interfaccia, opzioni di accessibilità e un menu principale con mappa di sfondo.

**Architecture:** un tema Godot costruito da codice (`UiTheme`) a partire da una tavolozza semantica (`UiColors`, con variante per daltonici e alto contrasto) assegnato a `Window.Theme`; i pannelli smettono di creare stili propri. Carattere e icone generati da `tools/art`. Testi tramite `TranslationServer` con un CSV per lingua; la simulazione restituisce codici, non frasi. La scala dell'interfaccia agisce sul `CanvasLayer` dell'HUD e sui pannelli, non sul mondo.

**Tech Stack:** Godot 4.4.1 .NET (C#, `Theme`, `FontFile` da BMFont, `TranslationServer`), Python + Pillow (`tools/art`), xUnit.

**Spec:** [docs/superpowers/specs/2026-10-18-g4-interfaccia-design.md](../specs/2026-10-18-g4-interfaccia-design.md)

## Global Constraints

- Stile Mindustry: pannelli quasi neri (`#14161A` ± tavolozza), bordo 2 px giallo-arancio (`#FFD37F`, l'Accento del progetto), angoli **squadrati** (raggio 0), icone pixel bianche, testi chiari; un solo tema per tutto il client.
- Carattere pixel nostro generato da `tools/art` (ASCII stampabile + `àèéìíòóùú ÀÈÉÌÒÙ « » … · € ×`), esportato come BMFont (`.fnt` + `.png`) in `godot/assets/ui/` e caricato come `FontFile`; due misure: testo (glifi 8 px di altezza, mostrati ×2) e titoli (×3). Se un carattere libero OFL risulta nettamente più leggibile nella tavola di confronto, è ammesso citandolo in `ATTRIBUTION.md` (decisione annotata nel registro).
- Icone dell'interfaccia generate (`ui-*.png`, 16 e 32 px): categorie (strumenti, logistica, produzione, potenza), menu, ricerca, mercato, campagna, lucchetto, potenza, impostazioni, salvataggi, lingua. Le icone di terzi in `godot/assets/ui/` spariscono e `ATTRIBUTION.md` resta solo per eventuali asset liberi usati (il carattere OFL, se scelto).
- Testi: nessuna stringa visibile al giocatore scritta nel codice del client; chiavi in `godot/lingue/testi.csv` (colonne `keys,it,en`), italiano predefinito, inglese completo; nomi di strutture, oggetti, ricette e testi della campagna tradotti per id (`struttura.<id>.nome`, `oggetto.<id>.nome`, `campagna.<id>.nome`, `campagna.<id>.descrizione`, `obiettivo.<id>.<n>`), con l'italiano attuale dei file di dati come valore `it`.
- Simulazione: i motivi di rifiuto diventano un `enum RefusalCode` (Shared) portato da `CommandRejected` e `PlanDropped` con eventuali argomenti (es. oggetto mancante); i test Shared confrontano codici, non frasi; il client li traduce. Nessun altro cambiamento di comportamento della simulazione; nessun cambio di salvataggi.
- Scala dell'interfaccia 75–200% (passo 5) applicata solo all'HUD e ai pannelli; `Window.ContentScaleFactor` resta 1; il mondo e la telecamera non cambiano. I vecchi valori 100/125/150/200 delle impostazioni restano validi.
- Accessibilità: tema ad alto contrasto; tavolozza per daltonici (deuteranopia/protanopia) per i colori con significato (piazzamento valido/non valido, smontaggio/costruzione, energia/denaro, rifiuti); il significato non dipende mai solo dal colore (icona, motivo o testo accanto: es. X sul fantasma non valido, tratteggio sullo smontaggio).
- Le risorse dell'interfaccia create in C# (stili, texture) vengono dal tema o sono liberate dai pannelli: alla fine la pulizia forzata in `World._ExitTree` (GC) si toglie e la chiusura resta pulita (ruling G2a).
- Testi in italiano, commenti in inglese, commit con `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`, mai dati reali del giocatore, Actions solo a mano.

## Review Focus

1. **Testi tagliati o sovrapposti** in inglese (più lungo o più corto) e a scala 150% su 1280×720. Test: playtest `ui` e `tutorial` in `en` e a scala 150%: controllo automatico che nessun `Label`/`Button` visibile abbia testo più largo del suo rettangolo e che i pannelli stiano nella finestra.
2. **Stringhe dimenticate**: test che cerca letterali visibili nei file UI (stringhe tra virgolette passate a `Text`, `TooltipText`, `ShowToast`… che non sono chiavi), con eccezioni elencate in un file.
3. **Motivi di rifiuto** dopo il passaggio ai codici: ogni codice ha una traduzione in entrambe le lingue; un rifiuto con argomento (oggetto mancante) mostra il nome tradotto. Test xUnit + test di copertura del CSV.
4. **Scala che sposta il mondo**: a scala 200% il mondo non deve ingrandirsi né il clic finire sulla cella sbagliata. Test: playtest a 200% piazza un blocco con il mouse e verifica la cella.
5. **Chiusura pulita senza GC forzato**: test playtest `tutorial` cinque volte senza "Leaked unsafe reference".

---

### Task 0: base (controllore)

- [ ] Base catture: l'ultima candidata (G3). Riferimento dati reali `$TEMP/g4-realdata-before.txt`.

### Task 1: carattere e icone dell'interfaccia

**Files:** Create `tools/art/blocchi/interfaccia.py` (icone), `tools/art/carattere.py` (glifi + esportazione BMFont), test Python; Modify `godot/assets/ATTRIBUTION.md`, `tools/art/test_genera.py`; Delete le icone di terzi in `godot/assets/ui/` quando non più usate (Task 2 le sostituisce nel codice: in questo task si generano accanto con nomi nuovi).
- [ ] Test: ogni glifo dell'insieme richiesto esiste; larghezze coerenti; `.fnt` valido (righe `char id=` per ogni glifo, `common lineHeight`); icone a 16 e 32 px con sagoma leggibile (≥ 20% di pixel pieni, contorno). Tavola `.dev/arte/tavola-ui.png` con frase di prova italiana e inglese a ×2 e ×3.
- [ ] Commit; PR "feat(arte): carattere pixel e icone dell'interfaccia (G4 1/6)".

### Task 2: tema unico e tavolozza semantica

**Files:** Create `godot/scripts/ui/UiTheme.cs`, `godot/scripts/ui/UiColors.cs`; Modify tutti i file in `godot/scripts/ui/hud/` e `godot/scripts/ui/panels/` (via `StyleBoxFlat` inline e `AddTheme*Override` sostituiti da variazioni di tipo del tema: `Pannello`, `Scheda`, `Slot`, `SlotScelto`, `Titolo`, `Testo`, `TestoTenue`, `Pulsante`, `PulsantePrimario`…), `World.Drawing.cs` e `ConstructionLayer.cs` (colori da `UiColors`), `World.cs` (assegna `Window.Theme`, toglie la pulizia GC se la chiusura è pulita).
- [ ] Rinvii della G3 da chiudere qui: togliere il rimedio provvisorio della pulizia forzata (GC) in `World._ExitTree` liberando alla fonte le risorse dell'HUD create in C#; sistemare il carattere del toast (causa: mipmap sull'atlante dei font dell'HUD, il tema sostituisce i font); il fantasma di piazzamento deve restare leggibile di notte (non scurito dal `CanvasModulate`).
- [ ] Test: playtest `ui`/`tutorial` OK; conteggio di `new StyleBoxFlat` e `AddTheme*Override` nei file UI ridotto a zero (eccezioni elencate e motivate); chiusura pulita 5/5 senza GC forzato; catture nuove.
- [ ] Commit; PR "feat(ui): tema unico in stile Mindustry (G4 2/6)".

### Task 3: scala solo dell'interfaccia

**Files:** Modify `ClientSettings.cs` (75–200 passo 5, compatibilità), `SettingsPanel.cs` (cursore), `World.cs` (scala sul CanvasLayer `Hud` e sui pannelli; `ContentScaleFactor = 1`), `BuildPalette.cs:357` e ogni calcolo che usava `ContentScaleFactor`, pannelli con dimensioni fisse (ancore e `ScrollContainer`).
- [ ] Test: playtest a 100% e 150% (e uno a 200% che piazza un blocco col mouse e verifica la cella); controllo automatico dei rettangoli dei controlli.
- [ ] Commit; PR "feat(ui): scala solo dell'interfaccia (G4 3/6)".

### Task 4: lingue e codici di rifiuto

**Files:** Shared: `enum RefusalCode` + argomenti, `CommandRejected`/`PlanDropped` portano il codice, `Factory.FormatNeedMessage` diventa dati (codice + oggetti), test Shared aggiornati; client: `godot/lingue/testi.csv`, `Tr()` in tutti i file UI e nei toast di `World.*`, nomi di contenuti e campagna per id, impostazione Lingua (it/en) in `ClientSettings` + `SettingsPanel`; `project.godot` (traduzioni registrate); test di copertura (ogni chiave ha it ed en; ogni struttura, oggetto, ricetta, livello e codice ha le sue chiavi) e test delle stringhe dimenticate.
- [ ] Le righe «Scava:» di `BuildPalette` e `ResearchPanel` (scritte a mano) si ricavano dai dati (`FactoryContent.MinerCanDig` e `terrain.deposits`) e passano dalle chiavi di traduzione.
- [ ] Playtest `ui`/`tutorial` in `it` e `en` con controllo dei rettangoli; commit; PR "feat(ui): italiano e inglese, rifiuti come codici (G4 4/6)".

### Task 5: accessibilità e menu principale

**Files:** `UiColors` (varianti Normale, Daltonici, AltoContrasto), impostazioni e pannello; segni non solo colore (X sul fantasma non valido, tratteggio sullo smontaggio, icona sui chip insufficienti); Home: logo pixel generato (`tools/art`), pulsanti nel nuovo stile, sfondo con la mappa 256² che scorre lentamente (telecamera mossa mentre la Home è aperta, input del mondo disattivato; ferma nei playtest).
- [ ] Il fantasma del Minatore T1 su un giacimento di solo titanio è rosso (motivo da `Factory.CanPlace`, non da una regola copiata nel client), con il segno non solo colore.
- [ ] Test: playtest con ciascuna variante di colori; catture `.dev/g4-task5/`; commit; PR "feat(ui): accessibilità e menu principale (G4 5/6)".

### Task 6: documenti e chiusura

- ADR `docs/decisioni/0006-interfaccia-lingue-carattere.md`; `docs/decisioni.md`; `docs/ARCHITETTURA.md` (tema, come aggiungere un testo o una lingua, codici di rifiuto); `CLAUDE.md` (regola: testi solo tramite chiavi di traduzione; stili solo dal tema); `godot/README.md`, `README.md` (catture nuove), `CHANGELOG.md`, canvas Obsidian; catture candidate `.dev/baseline-g4-candidata` in it e en.
- [ ] Commit; PR "docs: interfaccia, lingue e carattere, ADR 0006 (G4 6/6)".
