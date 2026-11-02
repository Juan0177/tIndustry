# Formato dei salvataggi

Come è fatto un salvataggio, quali versioni si leggono e cosa fare quando il formato cambia.

## Dove stanno

La radice dei dati del giocatore è `GameDataPaths.Root`: `%LOCALAPPDATA%/tIndustry` su Windows, `~/.local/share/tIndustry` su Linux, oppure la cartella indicata da `TINDUSTRY_DATA_DIR`. Sotto ci sono `godot-saves/` (i salvataggi) e `campaignProgress.json`. Le impostazioni (`settings.json`) seguono un'altra regola, descritta in [ARCHITETTURA.md](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md#salvataggi-e-impostazioni).

Nelle prove usa sempre un profilo temporaneo (vedi [Compilare e avviare](Compilare-e-avviare)): i salvataggi veri non si toccano.

## Com'è fatto

Un salvataggio è un file JSON (`SaveFileData` / `FactorySaveStore`, in `src/TIndustry.Shared/Save/FactorySave.cs`) con:

- i metadati mostrati nell'elenco (fondi, seme, mappa, livello);
- l'**istantanea canonica** della simulazione, in base64 (la stessa usata per il checksum, vedi [Architettura](Architettura)).

Contiene solo lo stato applicato: comandi in coda e orologio non sono salvati. Porta anche le regole della partita, i droni con la loro coda di piani, i cantieri a metà, il livello del CORE e i magazzini.

La scrittura è atomica: prima `<slot>.json.tmp`, poi la rinomina sullo slot.

## Versioni

| Versione | Stato |
|---|---|
| **17** | Corrente e unica che si legge (`FactorySaveData.CurrentVersion` e `MinReadableVersion`): aggiunge i laser della rete di corrente (`PowerLinks`, coppie di id) e la carica di ogni batteria (`Batteries`); toglie la riserva unica di corrente (`PowerBuffer`, `PowerCapacity`) |
| 16 | Rifiutata con il codice `OlderVersion` (sostituisce la notte fissa con il tempo del mondo (`Rules.DayNight`: `Cycle`, `AlwaysDay` o `AlwaysNight`) e aggiunge il tick del primo blocco (`FirstBuildTick`) e se il primo attacco è già scattato (`FirstAttackRaised`)) |
| 15 | Rifiutata con il codice `OlderVersion` (aggiungeva il livello del CORE (`CoreTier`), i container e i vault (`Storages`, col contenuto dei depositi a sé), la spunta «solo a CORE pieno» e l'accumulatore della vendita automatica per oggetto) |
| 14 e precedenti | Rifiutate con il codice `OlderVersion` («versione non più supportata»): il codice di conversione è stato tolto |

**Regola: cambiare il formato vuol dire alzare `CurrentVersion`.** Un campo nuovo, un campo tolto o un significato diverso richiedono una versione nuova e, se possibile, la lettura di quelle precedenti.

## Il terreno non si salva

Il terreno è dato, non stato: il salvataggio dice come rifarlo.

- `"classico"`: rigenerato da seme e misura col generatore classico;
- `"biomi-1"` (o `"biomi"` nei primi v12): rigenerato con `MapGenSettings.Normal(misura, seme)`;
- `"campagna"`: riletto dal file della mappa `MapId` in `data/maps/`, che deve avere ancora la stessa misura.

Se il terreno rigenerato ha un'impronta diversa da quella salvata, o la mappa manca, è cambiata o il generatore è sconosciuto, il caricamento si ferma con un errore: la fabbrica non finisce mai su un altro terreno. Per questo **un generatore pubblicato non si modifica mai**: una generazione diversa è un generatore nuovo con un nome nuovo (vedi [Mappe e terreno](Mappe-e-terreno)).

## Errori di caricamento

Ogni caricamento fallito dà un `SaveLoadFailure` con un codice `SaveLoadError` (`OlderVersion`, `Unreadable`, e i casi della mappa come `UnknownGenerator`, `TerrainChanged`, `CampaignMapMissing`, `CampaignMapChanged`…). Il client lo traduce con la chiave `salvataggi.errore.<Code>`. La partita in corso non cambia. Al caricamento anche i cantieri vengono validati: uno non più costruibile sparisce e rende al Core quanto aveva preso.

## Per approfondire

- [docs/ARCHITETTURA.md, Salvataggi e impostazioni](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md#salvataggi-e-impostazioni)
- [src/TIndustry.Shared/Save/FactorySave.cs](https://github.com/Juan0177/tIndustry/blob/main/src/TIndustry.Shared/Save/FactorySave.cs)
- [ADR 0002](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0002-sincronizzazione-ibrida.md) (istantanea) e [ADR 0005](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0005-mappe-seme-e-file.md) (terreno per nome)
- [Architettura](Architettura) · [Mappe e terreno](Mappe-e-terreno) · [Test e playtest](Test-e-playtest)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Contenuti e dati](Contenuti-e-dati) · [Torna alla Home](Home) · [Pagina successiva: Arte generata →](Arte-generata)

<!-- /navigazione -->
