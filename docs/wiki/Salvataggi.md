# Salvataggi

Come si salva e si riprende una partita: Continua, salvataggio automatico, slot, Gestione salvataggi, dove stanno i file e quali versioni si caricano.

## Continua e salvataggio automatico

**Continua** è il salvataggio della partita in corso. Il gioco lo scrive da solo quando:

- premi `Esc` con il cursore (o **Menu**) e torni alla Home;
- esci dal gioco (**Esci** o chiudendo la finestra).

Si salva solo una partita che hai davvero giocato: la mappa che scorre dietro la Home all'avvio non sovrascrive mai la tua Continua. La prima volta che una sessione sovrascrive la Continua, quella di prima viene copiata in `continua.prev.json`, così resta sempre la partita che avevi prima di questa sessione.

Nella Home, **Continua** compare solo se c'è un salvataggio da continuare.

## Tasti rapidi

| Tasto | Azione | File |
|---|---|---|
| `F5` | Salva la Continua | `continua.json` |
| `F9` | Carica la Continua | `continua.json` |
| `F6` | Salva lo slot rapido | `slot-1.json` |
| `F7` | Carica lo slot rapido | `slot-1.json` |

## Gestione salvataggi

Dalla Home, **Gestione salvataggi** elenca gli slot. Per ognuno mostra il tipo di mappa:

| Scritta | Partita |
|---|---|
| Biomi · 1500 · seme N | Partita libera generata dal seme |
| Mappa *livello* · L×A | Livello della campagna |
| Classico · L×A · seme N | Partita di una versione vecchia, col terreno di prima |

| Pulsante | Effetto |
|---|---|
| **Carica** (o `Invio`) | Carica lo slot scelto (frecce su e giù per scegliere) |
| **Elimina** | Elimina lo slot; servono due clic per confermare |
| **Duplica Continua** | Copia la Continua in un nuovo slot |
| **Salva come** | Salva la partita in corso in un nuovo slot, con un nome scelto dal gioco |

## Cosa contiene un salvataggio

Nastri con i loro oggetti, edifici, fondi e magazzino, sblocchi della Ricerca, vendite e stato del Mercato (prezzi, picchi e crolli di ogni oggetto), CORE, regole della partita, il drone con la coda dei piani e i cantieri a metà, il livello della campagna in corso. Il terreno non si salva: si rifà dal seme o dal file della mappa del livello.

## Dove stanno i file

| Cosa | Windows | Linux |
|---|---|---|
| Salvataggi | `%LOCALAPPDATA%\tIndustry\godot-saves\` | `~/.local/share/tIndustry/godot-saves/` |
| Progressi della campagna | `%LOCALAPPDATA%\tIndustry\campaignProgress.json` | `~/.local/share/tIndustry/campaignProgress.json` |
| Impostazioni | `settings.json` nella cartella utente di Godot, sotto `%APPDATA%\Godot\app_userdata\tIndustry\tIndustry\` | `settings.json` sotto `~/.local/share/godot/app_userdata/tIndustry/tIndustry/` |

Ogni salvataggio è un file `.json`. Per fare una copia di sicurezza basta copiare la cartella `godot-saves`.

## Compatibilità fra versioni

I salvataggi sono alla **versione 14**.

| Versione del salvataggio | Si carica? |
|---|---|
| 14 | Sì |
| 13 | Sì, con il Mercato rifatto da zero |
| 12 | Sì, con il Mercato rifatto da zero |
| 11 | Sì, col terreno classico di allora e il Mercato rifatto da zero |
| 10 e precedenti | No: "Salvataggio di una versione precedente: non compatibile." |

Se il terreno rigenerato non corrisponde a quello del salvataggio, o la mappa di un livello manca o ha cambiato misura, il caricamento si ferma con un messaggio chiaro invece di mettere la fabbrica su un altro terreno.

## Vedi anche

- [Primi passi](Primi-passi)
- [Campagna e mappe](Campagna-e-mappe)
- [Domande frequenti](Domande-frequenti)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Impostazioni e accessibilità](Impostazioni-e-accessibilita) · [Torna alla Home](Home) · [Pagina successiva: Domande frequenti →](Domande-frequenti)

<!-- /navigazione -->
