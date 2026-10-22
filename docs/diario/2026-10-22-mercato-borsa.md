# Mercato come una borsa (specifica 1 di 3)

- **Specifica**: [2026-10-22-mercato-borsa-design](../superpowers/specs/2026-10-22-mercato-borsa-design.md)
- **Piano**: [2026-10-22-mercato-borsa](../superpowers/plans/2026-10-22-mercato-borsa.md)
- **Issue**: [#268](https://github.com/Juan0177/tIndustry/issues/268) (la proposta e le tre specifiche), [#273](https://github.com/Juan0177/tIndustry/issues/273) (il sito allineato al nuovo Mercato, chiusa nel task 3)
- **Periodo**: 2026-10-22 e 2026-10-23
- **Esito**: in `main` con le PR [#274](https://github.com/Juan0177/tIndustry/pull/274), [#275](https://github.com/Juan0177/tIndustry/pull/275), [#276](https://github.com/Juan0177/tIndustry/pull/276), [#277](https://github.com/Juan0177/tIndustry/pull/277), [#278](https://github.com/Juan0177/tIndustry/pull/278), [#279](https://github.com/Juan0177/tIndustry/pull/279), [#280](https://github.com/Juan0177/tIndustry/pull/280), [#281](https://github.com/Juan0177/tIndustry/pull/281) (gli otto task) e #282 (documenti e chiusura, task 9). Piano completo; restano alle specifiche 2 e 3 lo schermo di borsa e il magazzino del CORE.

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-10-22-mercato-borsa/progress.md`), dai rapporti dei task (`task-N-report.md`) e dalla prova di 15 minuti (B4). Dove le fonti non dicono qualcosa qui non è inventato.

## Obiettivo

Dare a ogni oggetto un mercato suo, come un titolo in borsa: il prezzo si muove sempre, ogni 10-20 minuti sale in un picco fino a 3×B, e se si vende troppo l'oggetto crolla per 20 minuti. Chi aspetta i picchi guadagna più della sola vendita automatica; il «Vendi tutto» nel picco può vincere un jackpot, con una rete di sicurezza (pity) per chi è sfortunato. Tutto nella simulazione, deterministico (generatore seminato nello stato), con i numeri nei dati (`marketRules`) e non nel codice. Sostituisce il vecchio prezzo che dipendeva solo dalle scorte e il bonus di vendita di «Potenzia CORE».

## Task, commit e PR

Un ramo per task (`feat/mercato-<N>-<nome>`) da `main` aggiornato, `build-publish.yml` lanciata a mano sul ramo, poi merge con merge commit. Base del piano: `939e2c1`.

| Task | Contenuto | Commit | PR |
|---|---|---|---|
| 1 | Valori base B e regole del mercato nei dati (`marketRules`) con validazione | `4915615` | [#274](https://github.com/Juan0177/tIndustry/pull/274) |
| 2 | Andamento dei prezzi e picchi per oggetto: `MarketState`, `MarketRandom`, `MarketSystem` | `2096ef4` | [#275](https://github.com/Juan0177/tIndustry/pull/275) |
| 3 | Vendite al prezzo del clic, calo e crollo; sito allineato (#273) | `473e635`, `c4030e9`; sito `ac75796`, `b0ae432` | [#276](https://github.com/Juan0177/tIndustry/pull/276) |
| 4 | Jackpot, pity e attesa dopo un tentativo a vuoto | `bf32a63`, `aeea878`, `c5ec046`, `a3e58d6` | [#277](https://github.com/Juan0177/tIndustry/pull/277) |
| 5 | Vendita automatica per oggetto al prezzo minimo | `a693b21`, `959420f` | [#278](https://github.com/Juan0177/tIndustry/pull/278) |
| 6 | Salvataggi v14 con lo stato del mercato | `2f6e08c`, `2ee326e`, `4b2a3c1` | [#279](https://github.com/Juan0177/tIndustry/pull/279) |
| 7 | Vista del mercato per il client e per gli agenti | `dd83313` | [#280](https://github.com/Juan0177/tIndustry/pull/280) |
| 8 | Schermata panoramica del Mercato | `cfa5a20`, `7dabeca` | [#281](https://github.com/Juan0177/tIndustry/pull/281) (catture sul ramo `pr-media`, cartella `pr-281`) |
| 9 | Documenti, diario e chiusura | questo commit | #282 |

Dopo il merge del task 3 il workflow Pages è stato lanciato a mano da `main`, come chiesto dall'utente, per pubblicare il sito nuovo.

## Ordine di esecuzione e perché

Ordine del piano, 1-9, senza salti: ogni task consuma l'interfaccia del precedente (valori e regole → stato → vendite → jackpot → vendita automatica → salvataggi → vista → schermata). Il controllo di coerenza prima di partire ha trovato tre punti (decisioni R1, R2, R4 sotto): il task 3 toglie membri usati dal client e dagli agenti, tocca il sito, e il nome `GameState.Market` era già preso.

La prova di 15 minuti (B4) è stata fatta nella simulazione prima del task 9, con i semi 7 e 11: il gioco vero non era raggiungibile dal server `tindustry` (`godot4` non nel PATH, server avviato con il codice di inizio sessione). Il task 9 e la revisione finale dell'intero piano sono andati in parallelo (file diversi: documenti contro codice).

## Prova di 15 minuti (B4)

Risultati nella simulazione, con i valori del piano:

- il prezzo normale resta fra 1,03 e 1,56×B;
- gli oggetti da pochi dollari cambiano prezzo in dollari di rado (il carbone, B 3, circa 10 volte in 15 minuti), la lastra di titanio 320-370 volte;
- il primo picco di ogni oggetto arriva fra 10 e 20 minuti: nei primi 15 minuti ce ne sono da 1 a 3 in tutto e nessuno sulla lastra di ferro in due semi;
- un crollo dura 20 minuti, con 70-100 secondi sotto B nei primi 5 (0,61-0,62×B).

L'utente (23/10) ha approvato di tenere invariati i valori del piano: «procedi col Task 9 con le proposte del piano invariate». Se in partita risultassero troppo avari i primi picchi, si ritoccano in `marketRules` (nessun codice).

## Differenze fra specifica e codice

Quello che il codice fa, dove si discosta da quanto scritto:

- lo stato dei prezzi è `GameState.Exchange` (`GameState.Market`, il vecchio catalogo dei valori, è stato tolto nella revisione finale: i valori B si leggono da `Content.Market`);
- l'oscillazione del crollo è nei dati (`crashJitter`), non nel codice;
- del «50/50 perso» è attuato solo l'effetto reale: i cali sotto B sono il 20% meno probabili per 5 minuti; il calo da vendite nel crollo è 0, quindi un «recupero accelerato» non poteva agire;
- soldi e incassi delle vendite si fermano a `int.MaxValue` invece di avvolgersi;
- un salvataggio corrotto si ripara al caricamento (senza avviso), non si rifiuta;
- i salvataggi sono alla versione 14 (i v11-v13 si leggono ancora, con un mercato nuovo);
- il jackpot è provato nella simulazione ma non scatta mai in partita finché non c'è lo schermo di borsa (specifica 2).

## Giri di revisione

- **T1**: approvato al primo giro, solo minori (rinviati, sotto).
- **T2**: approvato al primo giro, solo minori.
- **T3** (opus): simulazione approvata con un Important, il jitter del crollo scritto nel codice (R6), più i minori economici. Giro 1 (`c4030e9`, 4 corretti): pavimento del calo a B invece del minimo della fascia normale, `SetAutoSell` di nuovo nel replay, messaggi di `SelfTestCoreUpgrade`, jitter nei dati. Il sito (#273) approvato (`ac75796`); minori economici corretti in `b0ae432`: prezzo solito della lastra da 23 a circa 37 nel pannello 11, la riga del README sui «prezzi dinamici».
- **T4** (opus): servono correzioni. I1: due «Vendi tutto» sullo stesso oggetto nello stesso tick dovevano essere `SellFailed` (scorte a 0) prima dell'attesa (R7). I2: il recupero accelerato dopo un 50/50 perso era codice morto (R8). Giro 1 (`aeea878`, 4 corretti, 1 aperto): `checked(pieces * unit)` lanciava `OverflowException` nella simulazione. Giro 2 (`c5ec046`): somme sature (R9) e vendite registrate in blocco invece che un pezzo alla volta; ritocco `a3e58d6` al test degli incassi enormi (niente cronometro, controlla il jackpot).
- **T5** (opus): approvato con un Important: l'interruttore unico del pannello inviava N comandi e quindi N avvisi con stati intermedi sbagliati. Giro 1 (`959420f`): un solo avviso. La ri-revisione l'ha fatta il controllore sul diff di 4 righe del client (il pannello si riscrive nel task 8).
- **T6** (opus): servono correzioni. I1: `WindowSecond` negativo mandava in crash `Step`; I2: una fase fuori dall'enum bloccava l'oggetto; minori: un oggetto mancante dal salvataggio faceva il picco subito, prezzi negativi dal file, un commento XML spostato. Giro 1 (`2ee326e`, 5 corretti): politica «riparare al caricamento, mai rifiutare». Giro 2 (`4b2a3c1`): limiti anche per oscillazione e finestra caricate, riletto dal controllore sul diff di 2 righe.
- **T7** (opus): approvato al primo giro.
- **T8** (opus): approvato; i minori sono stati corretti subito (`7dabeca`): righe ricostruite nello stesso frame (`RemoveChild` prima di `QueueFree`, niente nomi doppi), `MarketSparkline.Show` diventa `SetData`, colore dell'attesa. `--controlla` segnala solo `drone-1-elmetto.png`, già diverso su `main`.
- **Finale**: revisione dell'intero piano (`939e2c1..f7f425b`) in parallelo al task 9.

### Revisione finale

_Da compilare dal controllore: esito della revisione finale dell'intero piano, correzioni e rilettura._

## Decisioni prese in autonomia

- **Decisione: dal task 3 ogni task che cambia o toglie membri usati dal client o dagli agenti aggiorna subito le chiamate (il minimo indispensabile, il pannello si riscrive nel task 8) e la verifica include `--build-solutions` — `main` deve restare verde e Godot qui c'è — costo se sbagliata: un po' di codice del client toccato due volte.** (R1)
- **Decisione: la PR del task 3 comprende l'issue #273 (sito: grafico «Scarica», didascalia, capitolo Mercato del racconto) con i test del sito verdi; dopo il merge, workflow Pages a mano da `main` — richiesto dall'utente — costo se sbagliata: PR più grande.** (R2)
- **Decisione: merge con merge commit — abitudine del progetto (stack di PR con merge commit) — costo se sbagliata: storia diversa, nessun danno.** (R3)
- **Decisione: lo stato del mercato si chiama `GameState.Exchange` (tipo `MarketState`), perché `GameState.Market` è già il `MarketCatalog` — evita di rinominare il catalogo in molti file — costo se sbagliata: un rename meccanico.** (R4)
- **Decisione: il seme del mercato si riapplica nel setter di `Terrain` (`Reseed`); il task 6 ripristina il mercato dopo il terreno — altrimenti il caricamento riseminerebbe — costo se sbagliata: mercato riseminato al caricamento.** (R5)
- **Decisione: il jitter del crollo (0,04) va in `marketRules` come `crashJitter`, anche se il piano lo scrive nel codice — la specifica vieta numeri del mercato nel codice — costo se sbagliata: un campo dati in più.** (R6)
- **Decisione: in `ApplySellAll` il controllo delle scorte viene prima dell'attesa, come dice la Review Focus del piano — costo se sbagliata: il secondo clic mostra «scorte finite» invece di «aspetta».** (R7)
- **Decisione: nel crollo il calo da vendite è 0, quindi del «50/50 perso» resta solo l'effetto reale (cali sotto B il 20% meno probabili); si toglie il ramo morto del recupero accelerato e si prova l'effetto sui cali — la specifica dice «in pratica» entrambi, ma uno non può agire — costo se sbagliata: va riaggiunto un ramo di 3 righe e deciso cosa vuol dire «torna verso B più in fretta» nel crollo.** (R8)
- **Decisione: le somme di `Money` e `SaleIncome` si saturano a `int.MaxValue` invece di avvolgersi (niente tetto per vendita) — dopo circa 320 jackpot grossi un `int` traboccherebbe in negativo — costo se sbagliata: il tetto andrebbe sostituito da `long` in un task apposito.** (R9)
- **Decisione: salvataggi corrotti riparati al caricamento, mai rifiutati, e senza avviso — un oggetto del mercato fuori misura non deve far perdere una partita — costo se sbagliata: un errore del file passerebbe inosservato.** (task 6)
- **Decisione: il task 9 tiene i valori del piano invariati dopo la prova B4 — l'utente (23/10): «procedi col Task 9 con le proposte del piano invariate» — costo se sbagliata: ritocco di `marketRules`, nessun codice.**

## Problemi rinviati

Minori dei task, non portanti, parcheggiati:

- **Task 1**: messaggio «marketRules.1 (100%)» poco leggibile per `jackpotCap` e `crashDipChance`; i campi interi (secondi, pezzi) accettano frazioni arrotondate e non controllano l'overflow nel cast; `FactoryContent.MarketRules` è `null!` senza `Load` (attenzione ai test che costruiscono i contenuti a mano); `normalAverage` non è controllata dentro `[normalMin, normalMax]`.
- **Task 2**: `Reseed` non azzera `CrashEndTick`, `BoostEndTick`, `PityMisses` e `WindowPieces`; `Random = new MarketRandom(0)` ridondante; alcuni test deboli (`DifferentGameSeeds`, `ItemsMoveIndependently`, `EverySpikeHasASellingWindow`), interpolazione troncata (±1 millesimo), `NormalPriceStaysInBand` lento. (Il rinvio sul primo picco assoluto caricando oltre il minuto 20 è stato ripreso nel task 6.)
- **Task 3**: manca un test di crollo durante un picco e di vendita durante il crollo; nessun test per l'errore di validazione di `crashJitter`; «solo il minimo» nel crollo semplifica i cali 0,6-0,7 nel racconto del sito; `test_racconto.py` fissa la terna (23, 30, 69).
- **Task 4**: il test del 50/50 prova `MarketMath.CrashDipChance`, non che `CrashPrice` lo chiami con `tick < BoostEndTick`.
- **Task 5**: il ponte `Factory.AutoSellAtCore` è tutto-o-niente (impostazioni parziali per oggetto perse al salvataggio fino alla v14, ripreso dal task 6).
- **Task 6**: i tick assoluti (`NextSpikeTick`, `CrashEndTick`…) si caricano così come sono; il salvataggio corrotto è riparato senza avviso.
- **Task 7**: nessun test sui flag crollo/tentativo/jackpot della risposta di `vendi` né sul rifiuto `SellAllCooldown` agli agenti; `AgentGame` riportava `SaleResolved` come «altro» (ripreso e risolto nel task 7).
- **Task 8**: `ForPlaytestSetHistory` è pubblico (accettabile); jackpot e «Venduto» si contendono il toast (il jackpot non è raggiungibile in partita); `Factory.MarketItems` e `MarketItem` allocano e copiano la storia a ogni chiamata, quindi il pannello non li deve chiamare a ogni frame senza motivo.
- **Commento di `SaleResolved`** (`SimEvents.cs`): dice ancora «none yet» per i campi del jackpot, superato dal task 4; non toccato dal task dei documenti.
- **Sonda `oggetti-fluidi`** dei playtest: intermittente (una volta su quattro il 22/10); rilanciarla prima di indagare.
- **Prova in partita vera** dei valori del mercato (B4 è stata fatta nella simulazione): da rifare quando `godot4` è raggiungibile dal server `tindustry`.

## Verifiche finali

_I risultati delle verifiche del task 9 sono nel rapporto del task; la revisione finale dell'intero piano è nella sezione «Revisione finale» sopra._

## Da fare

- **Specifica 2**: schermo di borsa (blocco accanto al CORE, sequenza del «Vendi tutto» col drone 2.5D, LED, moduli, ramo Finanza della Ricerca). Con lo schermo il jackpot scatta anche in partita.
- **Specifica 3**: magazzino del CORE e livelli (massimo per oggetto, linee che si fermano a CORE pieno, drone più veloce per livello); lì «Potenzia CORE» riprende un effetto.
- Rifare la campagna (fuori dalle specifiche del mercato): gli obiettivi di guadagno sono stati ritoccati solo quanto serviva ai test.
- Commento sull'issue [#268](https://github.com/Juan0177/tIndustry/issues/268) con ciò che resta alle specifiche 2 e 3.
