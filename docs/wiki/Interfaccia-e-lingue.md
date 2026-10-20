# Interfaccia e lingue

Le regole dell'interfaccia del client: testi solo per chiave, stili solo dal tema, carattere pixel generato, e come aggiungere un testo o una lingua.

## Testi solo per chiave

Ogni testo visibile passa da una chiave di **`godot/lingue/testi.csv`** (colonne `keys,it,en`; italiano e inglese sempre entrambi). Nel codice del client non si scrivono frasi:

- etichette, pulsanti e suggerimenti fissi contengono la chiave e Godot li traduce da solo, anche al cambio di lingua;
- i testi composti usano `Testi.F(chiave, argomenti…)` (segnaposto `{0}`, `{1}`…, cultura invariante) e i pannelli li riscrivono quando cambia la lingua;
- nomi e descrizioni dei dati si traducono per id (`struttura.<id>.nome`, `oggetto.<id>.nome`, `campagna.<id>.nome`, `obiettivo.<id>.<n>`…), con l'italiano di `data/` come riserva.

La simulazione non scrive frasi: dà **codici** (`RefusalCode` per i rifiuti, `SaveLoadError` per i caricamenti falliti) che il client traduce con le chiavi `rifiuto.<Code>` e `salvataggi.errore.<Code>`.

Il test **`TestiTests`** controlla che ogni chiave abbia `it` ed `en` con gli stessi segnaposto, che le chiavi usate nel client esistano, che ci siano quelle di contenuti, livelli e codici, e che nel client non restino frasi scritte nel codice.

## Stili solo dal tema

Il client ha **un solo tema**, costruito nel codice da `UiTheme` (`godot/scripts/ui/UiTheme.cs`) e applicato alla finestra e alla radice dell'HUD.

- Nei pannelli **niente `StyleBoxFlat` e niente `AddTheme*Override`**. Un aspetto nuovo è una variante del tema (`ThemeTypeVariation`, costanti come `Titolo`, `TestoTenue`, `Scheda`, `PulsantePrimario`) aggiunta in `UiTheme`.
- I colori stanno in `UiColors`, in **tre varianti**: `Normale`, `Daltonici`, `AltoContrasto` (voce *Colori* delle Impostazioni). Un colore nuovo va definito in tutte e tre.
- Un significato non sta mai solo nel colore: X sull'anteprima rifiutata, tratteggio sullo smontaggio, barra sul mancante, «!» sull'energia bassa.

## Carattere, icone e scala

- Il **carattere pixel** è nostro: `tools/art/carattere.py` lo disegna e lo esporta come BMFont in `godot/assets/ui/` (`carattere-testo` a 16 px, `carattere-titoli` a 24 e 48). Il tema usa solo queste misure intere, così il testo resta nitido. Un testo con un segno nuovo chiede prima il glifo: il test `test_carattere_copre_i_testi` lo pretende.
- **Icone, logo e caselle di spunta** sono generati da `tools/art` come gli altri sprite (vedi [Arte generata](Arte-generata)).
- La **Scala UI** (75–200%) ingrandisce solo HUD e pannelli; il mondo resta 1:1 con i pixel della finestra.

## Aggiungere un testo

1. Una riga in `godot/lingue/testi.csv`: chiave in minuscolo con punti (`pannello.cosa`), testo italiano, testo inglese, con gli stessi segnaposto. Un testo con virgole va tra virgolette.
2. Nel codice la chiave in `Text` o `TooltipText`, oppure `Testi.T(chiave)` / `Testi.F(chiave, …)`.
3. Un segno nuovo: prima il glifo in `tools/art/carattere.py`, poi rigenera.
4. `dotnet test` e `python -m unittest tools/art/test_genera.py`. L'importazione di Godot rigenera i file `.translation`, che non si committano.

## Aggiungere una lingua

1. Una colonna nuova in `testi.csv` col codice della lingua, riempita per ogni chiave, più `lingua.<codice>` col nome della lingua in sé stessa.
2. Il codice in `Testi.Lingue` e il file `res://lingue/testi.<codice>.translation` in `locale/translations` di `project.godot`.
3. Controlla i formati dei numeri (`Testi.Secondi`) e allarga `TestiTests` e il controllo dei testi nei playtest (`TINDUSTRY_PLAYTEST_LINGUA=<codice>`).

## Per approfondire

- [ADR 0006 — Interfaccia, lingue e carattere](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0006-interfaccia-lingue-carattere.md)
- [docs/ARCHITETTURA.md, Interfaccia (G4)](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md#interfaccia-g4) e [Come aggiungere un testo o una lingua](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md#come-aggiungere-un-testo-o-una-lingua)
- [godot/lingue/testi.csv](https://github.com/Juan0177/tIndustry/blob/main/godot/lingue/testi.csv)
- [Arte generata](Arte-generata) · [Test e playtest](Test-e-playtest) · [Architettura](Architettura)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Mappe e terreno](Mappe-e-terreno) · [Torna alla Home](Home) · [Pagina successiva: Test e playtest →](Test-e-playtest)

<!-- /navigazione -->
