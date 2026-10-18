# Sotto-progetto G3 — Effetti, ombre e luci

- **Specifica**: [2026-10-18-g3-effetti-luci-design](../superpowers/specs/2026-10-18-g3-effetti-luci-design.md)
- **Piano**: [2026-10-18-g3-effetti-luci](../superpowers/plans/2026-10-18-g3-effetti-luci.md)
- **ADR**: [0004 — Arte generata da script](../decisioni/0004-arte-generata-da-script.md) (sezione «Effetti e luci (G3)»)
- **Periodo**: 2026-10-18 (una sola esecuzione notturna in modalità Auto, con le modifiche fuori piano del mattino)
- **Esito**: in `main` con le PR [#186](https://github.com/Juan0177/tIndustry/pull/186), [#187](https://github.com/Juan0177/tIndustry/pull/187), [#188](https://github.com/Juan0177/tIndustry/pull/188), [#189](https://github.com/Juan0177/tIndustry/pull/189) e le correzioni finali [#192](https://github.com/Juan0177/tIndustry/pull/192). La base di catture candidata `.dev/baseline-g3-candidata` attende ancora l'OK dell'utente (vedi «Da fare»).

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-10-18-g3-effetti-luci/progress.md`), dai rapporti e dalle revisioni dei task (`task-N-report.md`, `task-N-review.md`), dalla revisione finale (`final-review.md`), dall'ondata di correzioni (`fix-wave-brief.md`, `fix-wave-report.md`, `rereview-fixwave.md`) e dalla storia git (`f1e82ed..main`). Dove le fonti non dicono qualcosa qui non è inventato.

## Obiettivo

Dare vita al mondo senza toccare la simulazione di gioco oltre il minimo: impostazioni grafiche (qualità degli effetti, ombre, bagliore, animazioni dei blocchi, limite di FPS, scala di rendering), un gestore di effetti a particelle con pool fisso, effetti di lavoro, costruzione e ambiente, ombre dei blocchi, bagliore, modalità notte e la Lampada come nuovo blocco di potenza. Scelte e motivi nella sezione «Effetti e luci (G3)» dell'[ADR 0004](../decisioni/0004-arte-generata-da-script.md).

## Task, commit e PR

Un branch per task (`feat/g3-<n>-...`), ciascuno fuso in `main` appena la revisione era pulita e la CI verde, come per la G2b. Modello opus per implementatori e revisioni dei task 1–3, per la revisione finale e per l'ondata di correzioni; sonnet per il task 4 (documenti) e per le riletture.

| Task | Contenuto | Commit | PR |
|---|---|---|---|
| 0 | Elenco dei dati reali prima del lavoro (`$TEMP/g3-realdata-before.txt`); `main` con la G2b fusa (#182–#185); base `.dev/baseline-g2b-candidata` | — | — |
| 1 | Impostazioni grafiche (`ClientSettings`, `SettingsPanel`), `EffectManager` con `effetti.json` e `EffectRules`, `RenderScale` (SubViewport), sprite `fx-*`, playtest della grafica | `a5c80e3`, correzioni `012a312` | [#186](https://github.com/Juan0177/tIndustry/pull/186) |
| 2 | Effetti di lavoro (minatore, forno, generatore, assemblatore, estrattore), costruzione (cantieri, droni, polvere del Core) e ambiente (`AmbientEffects`) | `cb216d2`, correzioni `601727a` | [#187](https://github.com/Juan0177/tIndustry/pull/187) |
| 3 | Ombre dei blocchi, bagliore (HDR 2D), notte (`NightLayer`), Lampada (`Lamp`, `LightingSystem`), lucciole, casella «Notte» in Nuova partita, notte nei livelli 06 e 10 | `6f377a3` | [#188](https://github.com/Juan0177/tIndustry/pull/188) |
| 4 | Documenti (ADR 0004, ARCHITETTURA, README, CHANGELOG, canvas), voce «Sito web su GitHub Pages» nella roadmap dopo la G4, base candidata `.dev/baseline-g3-candidata` | `4760c4c` | [#189](https://github.com/Juan0177/tIndustry/pull/189) |
| Fix finali | Ondata di correzioni dalla revisione finale (10 punti più un'ottimizzazione fuori elenco) | `bf11224`, `901e959`, `1d3e507`, `2806f4b` | [#192](https://github.com/Juan0177/tIndustry/pull/192) |

CI `build-publish.yml` lanciata a mano e verde su ogni task: T1 39109830853 (correzioni 39110508151), T2 39112240576 (correzioni 39113284668), T3 39115863515, T4 39116756717, correzioni finali 39121028956.

Test superati (rapporti dei task): T1 309 (2 ignorati), dopo le correzioni 331; T2 332, dopo le correzioni 333; T3 344; T4 344 (invariati); ondata finale 349 (2 ignorati). Test Python di `tools/art`: 26 al T4, 27 nell'ondata finale.

## Giri di revisione

- **T1** — conforme, «Needs fixes»: 1 Important e 6 Minor. Important: il `SubViewport` della scala di rendering usava il filtro Linear predefinito invece del Nearest del progetto, quindi a 75% e 50% la pixel art risultava sfocata. Minor: un nome sconosciuto in `effetti` nelle impostazioni faceva perdere *tutte* le impostazioni (compreso `TutorialCompleted`); etichette a 14 px che mascheravano la fragilità delle catture; controllo della dimensione del playtest tautologico; chiavi duplicate e overflow nel loader di `effetti.json`; stiramento finale in Linear a 50%.
  - **Giro di correzione 1** (`012a312`): il target copia le impostazioni 2D del root; convertitore tollerante per l'enum `Effetti` (`TolerantEnumConverter`, 12 test); stiramento Nearest esattamente al 50%; Minor chiusi. 331 test. Rilettura (sonnet): tutto risolto.
- **T2** — conforme, «Needs fixes»: 1 Important e 6 Minor. Important: a ogni ricostruzione dei visual ogni blocco in lavoro affitta un secondo emettitore per circa 2 s (il vecchio si dissolve, il nuovo parte), quindi ricostruzioni ravvicinate potevano esaurire il pool da 256 e far perdere fumo ed effetti di costruzione; la prova di stabilità da 60 s non ricostruiva mai. Minor: cantiere del vecchio gioco letto nel primo frame del nuovo, regex del test dei nomi basata sulle righe, 256 nodi nascosti dopo il passaggio a Spenti, accoppiamento sull'ordine di inizializzazione di `ConstructionLayer.Effects`, doppio `Sync` dei figli in `QueueFree`.
  - **Controllo del coordinatore sulle catture**: fumo del forno sottile, leggibile, non copre il blocco. Da correggere: le foglie devono leggersi sull'erba (sprite giallo-bruno d'autunno) e i granelli di sabbia essere più scuri.
  - **Giro di correzione 1** (`601727a`): passaggio dell'emettitore al nuovo visual per chiave (blocco + effetto), figli in cancellazione saltati nel `Sync`, `AttachGame` pulisce cantieri e contatore del Core, foglie e sabbia ridisegnate; la prova fa raffiche di ricostruzioni (0 rifiuti). 333 test. Rilettura (sonnet): tutto risolto.
- **T3** — conforme, approvato: 0 Critical, 0 Important, 8 Minor. Controllo del coordinatore su `luci-notte-lampade`: notte leggibile, lampade con alone, lucciole visibili, HUD non scurito. Minor: (2) `powerUse` 0 = luce gratis, (3) nessun test sull'assenza della chiave `lamps`, (6) summary di `AmbientEffects`, (7) LINQ su `CoreTiles` a ogni frame → ondata finale; (1) ordine dei sistemi in ARCHITETTURA → T4; (8) fantasma di piazzamento scurito di notte → G4; (4) HUD fuso in HDR e (5) tetto di 128 luci non testato → accettati.
- **T4** — approvato dalla revisione (sonnet), solo Minor: G4 in «In corso» sul canvas, albero delle cartelle di ARCHITETTURA senza `visuals/effects` e `godot/effetti`, nessuna riga di prodotto in `decisioni.md` su notte e impostazioni grafiche. Tutti nell'ondata finale.
- **Revisione finale** (opus, `f1e82ed..24d67a0`): **si può unire, sì**. 0 Critical, 0 Important, 3 Minor. Verificati dal revisore: build 0 errori e 0 avvisi, 344 test superati e 2 ignorati. Il revisore non ha giudicato i salvataggi v12 con lampade aperti da build precedenti, i livelli 06/10 salvati prima della G3 e l'assenza di un'impostazione per spegnere la notte sui PC deboli (tutte conseguenze di regole già accettate).
- **Ondata finale di correzioni** (PR #192, opus): vedi sotto. Rilettura (sonnet): **10 su 10 risolti**, 4 Minor residui accettati.

### Revisione finale e ondata di correzioni: i punti

Punti di forza segnalati: `LightingSystem` deterministico (lista in ordine di id, nessun Dictionary né orologio, consumatore a priorità più bassa); salvataggi v12 identici byte per byte nelle partite di giorno senza lampade (`Lamps` nullo e `Night` solo se vero); Lampada collegata a ogni switch e lista di Shared e client; `RenderScale` e bagliore composti senza conflitti (un solo glow, HUD fuori dal buio); `EffectManager` con passaggio per chiave e pool a tetto fisso; congelamento coerente dei playtest; nessuna perdita alla chiusura.

- **Minor 1**: la prova di stabilità non verificava davvero «il pool non cresce»: confrontava solo i nodi totali e il tetto assoluto di 256, e l'ADR scriveva «non cresce» con numeri che mostravano +3.
- **Minor 2**: la notte non era mai misurata sulla mappa Enorme.
- **Minor 3**: `ApplyGlow` creava un `World3D` sul SubViewport anche a bagliore spento.
- **Parcheggiati dai task**: lampada con `powerUse` 0 accesa gratis; nessun test sull'assenza di `"lamps"`; summary di `AmbientEffects` fuori posto; LINQ su `CoreTiles`; G4 in «In corso» sul canvas; albero delle cartelle; riga di `decisioni.md`.

Correzioni (commit `bf11224`, `901e959`, `1d3e507`, `2806f4b`):

1. `ValidateLamp` in `FactoryContent` rifiuta `powerUse` mancante o ≤ 0 con messaggio italiano e guardia `powerUse > 0f` in `LightingSystem`; test a 4 casi.
2. Test: un salvataggio di giorno senza lampade non contiene la chiave `"lamps"`.
3. Summary di `AmbientEffects.Sync` rimessi a posto.
4. `NightLayer`: centro del Core in cache (confronto per riferimento su `CoreTiles`, che non cambia durante la partita) invece di quattro passate LINQ a frame.
5. Prova di stabilità: pool a 60 s non oltre il 3% del pool a 10 s (osservato 147 → 148–150); ADR 0004 riscritto «stabile entro il 3 %».
6. `PlaytestHugeMapNightAsync`: notte sulla mappa Enorme a zoom minimo, 237 FPS medi, 183 minimo, 128 luci (solo stampa dei numeri, come le altre prove sulla Enorme).
7. `ApplyGlow`: il `World3D` si crea solo con il bagliore acceso; da acceso a spento azzera ambiente e HDR.
8. Canvas: G4 solo tra i «Prossimi», colonna «In corso» con la scheda «Niente in corso»; rigenerato.
9. ARCHITETTURA: albero con `godot/scripts/visuals/effects/` e `godot/effetti/`.
10. `decisioni.md`: riga su notte opzionale e impostazioni grafiche leggere.

Fuori elenco: scorciatoia a bounding box in `Generator.IsAdjacentTo` (la simulazione impiegava 218 ms a frame con circa 575 lampade × generatori), con test di equivalenza esaustivo; resta O(lampade × generatori).

## Fuori piano, nella stessa sessione

Due lavori nati dai commenti dell'utente durante la mattina, svolti in parallelo agli ultimi task.

- **Droni ridisegnati a mano nello stile delle unità di Mindustry** (PR [#190](https://github.com/Juan0177/tIndustry/pull/190), `da8cfc7`, `5f6d1ce`, `8f40dd3`): l'utente ha notato che i droni sembravano aerei e ha chiesto un aspetto futuristico, simmetrico e il più vicino possibile a Mindustry, con motore come quello di Mindustry (niente rotori). Dopo vari prototipi, l'ultimo disegnato a mano in ASCII è quello scelto: i droni sono disegnati a mano nello script (`tools/art/blocchi/drone.py`, metà larga specchiata), con rampe proprie in `palette.py`; `DroneVisual` usa il filtro Linear per le texture (i droni ruotano liberamente) e un bagliore del motore che pulsa (fermo nei playtest). Mindustry è stato consultato solo come riferimento (GPL, mai copiato). CI 39117522735, poi 39117780765 verde dopo la correzione di una stringa di test sbagliata spinta nel primo commit di fix. Revisione (sonnet): approvabile, 6 Minor; corretti il test della direzione del muso (a Est) e quello del `ValueError`, i commenti e la frase della documentazione; parcheggiati gli indici di rampa inutilizzati e il controllo a occhio del filtro Linear con scala di rendering al 50%.
- **Titanio solo con il Minatore T2** (PR [#191](https://github.com/Juan0177/tIndustry/pull/191), `02972d9`, `ca124f1`): dopo una ricerca su come Mindustry genera i minerali (relazione fuori dal repo), l'utente ha scelto l'opzione 1: il titanio si scava solo con il Minatore T2. Regola unica `FactoryContent.MinerCanDig` (tier del minatore contro durezza del giacimento in `content.json`), nuovo motivo di rifiuto `NeedsAdvancedMiner` su tutte le strade di piazzamento, `RestoreSite` toglie e rimborsa un cantiere T1 sul titanio, salvataggi v12 con byte invariati, livello 9 con soldi e materiali alzati del costo del T2 (400 → 950 soldi, 45 → 95 lastre, 18 → 30 fili). Il generatore di mappe **`biomi-2`** (nuove frequenze di comparsa dei giacimenti) è **rinviato a dopo la G4** ed è una voce della roadmap. CI 39119170709, poi 39119808503 dopo le correzioni della revisione; 356 test. Revisione (opus): approvabile, 4 Minor; corretti il rifiuto silenzioso durante il trascinamento, la documentazione di `CanPlace` e un commento di test; fantasma non rosso sul titanio e testi «Scava:» → G4. Non fatto: rallentamento in base alla durezza.
- **Base di catture**: l'utente ha approvato `.dev/baseline-g2b-candidata` come base ufficiale delle catture (promossa a `$TEMP/baseline-g2b` la mattina del 2026-10-18). `.dev/baseline-g3-candidata` attende l'approvazione.

## Prestazioni

Misure con `TINDUSTRY_PLAYTEST_EFFETTI=1 TINDUSTRY_PLAYTEST_PERF=1` (effetti Alti; vincolo: media almeno 144 FPS). Dal rapporto del Task 4 e dell'ondata finale.

| Misura | Valore |
|---|---|
| 200 nastri, 97 edifici | 231 FPS medi, 153 minimo, 1133 nodi |
| Effetti 60 s | 230 FPS medi, 70 minimo; nodi 1224 (10 s) → 1227 (60 s); particelle 377 → 372; pool 147 → 150 |
| Notte, 13 lampade, 61 luci a zoom 0,25 | 228 FPS medi (minimo 77) |
| Enorme 2000², zoom 0,08, di giorno | fermo 240 medi (minimo 150), panoramica 240 (minimo 206); processo 573 MiB, video 83 MiB |
| Enorme di notte (ondata finale) | 237 medi, 183 minimo, 128 luci |

Il rapporto del T3 segnala una misura da 60 s con minimo 0 (un frame lungo isolato, media 218) non ripresentata nella seconda esecuzione.

## Decisioni prese in autonomia

- **Decisione**: un branch per task da `main`, ciascuno fuso a revisione pulita e CI verde; opus per implementatori e revisori dei task 1–3, sonnet per il task 4. — come la G2b. — se sbagliata: solo costo.
- **Decisione**: Effetti Spenti ferma anche il tempo dello shader dell'acqua della G2b, e i playtest forzano Spenti salvo `TINDUSTRY_PLAYTEST_EFFETTI=1`. — acqua e particelle devono seguire la stessa impostazione e le catture devono restare stabili. — se sbagliata: catture instabili o impostazione incompleta.
- **Decisione**: la scala di rendering usa un `SubViewport` che condivide il `World2D` (`RenderScale.cs`) invece di `ContentScaleMode=Viewport`. — il secondo lega la risoluzione alla scala dell'interfaccia. — se sbagliata: rifare la scala; il picking a 75% e 50% è stato verificato.
- **Decisione**: `EffectRules` resta nel client, compilato nei test come file collegato. — il vincolo globale vieta riferimenti agli effetti in `src/TIndustry.Shared`. — se sbagliata: spostamento di due file.
- **Decisione**: giro 1 del T1 = Important più tutti i Minor, stiramento Nearest esattamente al 50%. — pixel art nitida al fattore 2 esatto. — se sbagliata: un altro giro.
- **Decisione**: la causa dell'atlante dei font del toast si rinvia alla G4, dove il tema sostituisce i font. — le etichette a 14 px sono un rimedio per le catture, non una correzione. — se sbagliata: catture fragili fino alla G4.
- **Decisione**: le lucciole passano dal T2 al T3, con il bagliore notturno. — hanno senso solo con la notte. — se sbagliata: nessuna.
- **Decisione**: `ZIndex` 15 per l'`EffectManager` e timeout di playtest a 300 s con `EFFETTI=1`. — a 12 il fumo finiva sotto la bocca del forno e la lampada del minatore. — se sbagliata: ritocco di una costante.
- **Decisione**: giro 1 del T2 = passaggio degli emettitori per chiave più tutti i Minor, più foglie e sabbia ridisegnate. — il pool poteva esaurirsi; foglie e granelli non si leggevano. — se sbagliata: un giro in più.
- **Decisione**: `hdr_2d` acceso e spento a runtime con il Bagliore, non in `project.godot`; ombre, bagliore e notte forzati spenti nei playtest come le particelle. — HDR costa anche quando il bagliore è spento. — se sbagliata: costo fisso o catture instabili.
- **Decisione**: la Lampada è un tipo minimo con un proprio `LightingSystem` dopo `CraftingSystem`; costo (2 lastre di ferro e 1 filo di rame, ricerca dopo «Nodo T1») scelto dall'implementatore. — consumatore a priorità più bassa. — se sbagliata: bilanciamento da rivedere.
- **Decisione**: nessuna ombra aggiuntiva sulle pareti e nessun bagliore diurno dei generatori. — gli sprite delle pareti hanno già parete e ombra. — se sbagliata: un ritocco d'arte.
- **Decisione**: la notte è una regola salvata della partita (campo additivo dentro la v12, `Night` solo se vero, `lamps` nullo se vuoto). — le partite di giorno senza lampade restano identiche byte per byte. — se sbagliata: i salvataggi con lampade non si leggono da build precedenti.
- **Decisione**: la voce «Sito web su GitHub Pages» entra nella roadmap dopo la G4 (richiesta dell'utente del mattino). — dopo il rifacimento grafico. — se sbagliata: spostare una riga.
- **Decisione**: titanio solo con il Minatore T2 adesso; generatore `biomi-2` dopo la G4. — un generatore pubblicato non si modifica mai, quindi cambiare le frequenze richiede un generatore nuovo. — se sbagliata: i giacimenti restano come ora fino al `biomi-2`.
- **Decisione**: tolleranza del 3% (non 2%) sulla crescita del pool nella prova. — sei esecuzioni, massimo 150. — se sbagliata: prova instabile.
- **Decisione**: accettata la scorciatoia a bounding box in `Generator.IsAdjacentTo`. — la simulazione era a 218 ms a frame con circa 575 lampade; equivalenza dimostrata da un test esaustivo. — se sbagliata: un risultato diverso nel posizionamento, escluso dal test.
- **Decisione**: accettati i residui della rilettura dell'ondata finale (prova Enorme di notte senza pass/fail, 177 contro 183 come variabilità tra esecuzioni, riga vuota in `AmbientEffects.cs`). — cosmetici o già decisi. — se sbagliata: una soglia in più.

## Problemi rinviati

Alla **G4** (interfaccia, [piano](../superpowers/plans/2026-10-18-g4-interfaccia.md)):

- Fantasma di piazzamento scurito di notte (viene scurito dal `CanvasModulate`).
- Fantasma non rosso per il Minatore T1 sul titanio.
- Righe «Scava:» scritte a mano in `BuildPalette` e `ResearchPanel`: derivarle dai dati.
- Pulizia forzata (GC) delle risorse dell'HUD all'uscita, rimedio provvisorio.
- Carattere del toast (causa: mipmap sull'atlante dei font dell'HUD).

Altrove:

- **`biomi-2`** (frequenze di comparsa dei giacimenti): dopo la G4.
- Un salvataggio v12 con lampade aperto da una build precedente alla G3 perde le lampade in silenzio, e un livello 06/10 salvato prima della G3 si ricarica di giorno: conseguenze accettate della regola additiva.
- Nessuna impostazione per spegnere la notte sui PC deboli (la notte è una proprietà della mappa).
- Rallentamento dello scavo in base alla durezza: non fatto.
- Strisce `acqua-*-anim` non usate dal client (dalla G2b): non assegnato.

## Verifiche finali

Dai rapporti dell'ondata finale (PR #192) e della revisione finale:

- `dotnet build tIndustry.sln -c Release`: 0 errori, 0 avvisi.
- `dotnet test`: 349 superati, 2 ignorati.
- `python -m unittest tools/art/test_genera.py`: 27 OK.
- Playtest `ui` e `tutorial`: PLAYTEST OK, 0 ERROR; prova PERF OK (numeri sopra).
- CI `build-publish.yml` lanciata a mano, verde (ultima esecuzione 39121028956).
- Dati reali del giocatore: invariati (`g3-realdata-before.txt`).
- Titanio (PR #191): 356 test superati, `MapTool valida` OK su tutti e 10 i livelli.

## Da fare

- **L'utente giudica le catture della G3** (ombre, bagliore, notte, Lampada, lucciole, impostazioni, droni nuovi) e solo con l'OK si **promuove la base candidata** `.dev/baseline-g3-candidata`. Fino ad allora la base ufficiale è `.dev/baseline-g2b-candidata`, già approvata.
- Pianificare e fare la **G4**.
