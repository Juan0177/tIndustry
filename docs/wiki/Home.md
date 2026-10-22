![tIndustry](https://raw.githubusercontent.com/Juan0177/tIndustry/main/docs/img/readme/banner.png)

# Wiki di <img src="https://raw.githubusercontent.com/Juan0177/tIndustry/main/docs/img/logo.png" alt="tIndustry" height="48">

**tIndustry** è un gioco di logistica industriale ispirato a **Mindustry** e **Tiny Industry**: estrai minerali, portali al CORE sui nastri, fondili in lastre e fili, vendili al Mercato e reinvesti in Ricerca per sbloccare nuovi blocchi.

[Scarica l'ultima versione](https://github.com/Juan0177/tIndustry/releases/latest) · [Sito](https://juan0177.github.io/tIndustry/) · [Codice](https://github.com/Juan0177/tIndustry) · [Novità](https://github.com/Juan0177/tIndustry/blob/main/CHANGELOG.md)

## A che punto siamo

<!-- barra-stato:inizio -->
**Avanzamento del gioco: 39%** (26 passi fatti su 66, [dettaglio](https://github.com/Juan0177/tIndustry/blob/main/docs/STATO.md))

```text
███░│████│█░░░│████│█░░░│█░░░│█░░░│██░░│███░│███░│░░░░│░░░░│░░░░│░░░░│███░│███░
```

| # | Aspetto | Avanzamento | % |
| --- | --- | --- | --- |
| 1 | [Fondamenta tecniche](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md) | `███████░░░` | 67% (4/6) |
| 2 | [Costruzione](Costruzione) | `██████████` | 100% (2/2) |
| 3 | [Logistica](Logistica) | `███░░░░░░░` | 29% (2/7) |
| 4 | [Produzione](Produzione) | `██████████` | 100% (2/2) |
| 5 | [Energia](Energia) | `██░░░░░░░░` | 20% (1/5) |
| 6 | [Mercato ed economia](Mercato-come-una-borsa) | `██░░░░░░░░` | 20% (1/5) |
| 7 | [Mappe e terreno](Mappe-e-terreno) | `███░░░░░░░` | 33% (2/6) |
| 8 | [Campagna](Campagna-e-mappe) | `█████░░░░░` | 50% (1/2) |
| 9 | [Grafica ed effetti](Arte-generata) | `███████░░░` | 67% (2/3) |
| 10 | [Interfaccia](Interfaccia-e-lingue) | `███████░░░` | 67% (2/3) |
| 11 | [Drone e tier](https://github.com/Juan0177/tIndustry#stato-e-roadmap) | `░░░░░░░░░░` | 0% (0/4) |
| 12 | [Liquidi](https://github.com/Juan0177/tIndustry#stato-e-roadmap) | `░░░░░░░░░░` | 0% (0/4) |
| 13 | [Attacco e difesa](https://github.com/Juan0177/tIndustry#stato-e-roadmap) | `░░░░░░░░░░` | 0% (0/4) |
| 14 | [Co-op](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0002-sincronizzazione-ibrida.md) | `░░░░░░░░░░` | 0% (0/4) |
| 15 | [Sito web](Sito-web) | `████████░░` | 80% (4/5) |
| 16 | [Wiki]() | `████████░░` | 75% (3/4) |
<!-- barra-stato:fine -->

Il Wiki ha tre parti: una per chi gioca, una per chi lavora sul codice e una con le idee in discussione.

## Giocare

Per chi vuole imparare il gioco: comandi, blocchi, ricette, numeri.

| Pagina | Cosa trovi |
|---|---|
| [Primi passi](Primi-passi) | Scaricare, avviare e fare la prima partita |
| [Comandi](Comandi) | Mouse e tastiera |
| [Risorse e oggetti](Risorse-e-oggetti) | Minerali, oggetti e ricette |
| [Produzione](Produzione) | Minatori, forni, assemblatori, estrattori |
| [Logistica](Logistica) | Nastri, ponti, incroci, sdoppiatore, selezionatore |
| [Energia](Energia) | Generatori, nodi, lampade |
| [Costruzione](Costruzione) | Piani, drone e cantieri |
| [Economia e Ricerca](Economia-e-Ricerca) | Core, Mercato, albero della Ricerca |
| [Campagna e mappe](Campagna-e-mappe) | Livelli, biomi, partita libera |
| [Impostazioni e accessibilità](Impostazioni-e-accessibilita) | Lingue, scala, colori, effetti |
| [Salvataggi](Salvataggi) | Salvare, continuare, compatibilità |
| [Domande frequenti](Domande-frequenti) | Problemi comuni in partita |

## Sviluppare

Per chi vuole compilare il gioco, capirne la struttura o contribuire. Queste pagine riassumono e rimandano ai documenti del repository, che restano la fonte di verità.

| Pagina | Cosa trovi |
|---|---|
| [Panoramica](Sviluppo-Panoramica) | Cartelle del progetto e dove guardare |
| [Compilare e avviare](Compilare-e-avviare) | Requisiti, build, test, avvio |
| [Architettura](Architettura) | Simulazione deterministica, comandi, client Godot |
| [Contenuti e dati](Contenuti-e-dati) | `data/`, come aggiungere un blocco |
| [Formato dei salvataggi](Salvataggi-formato) | Versioni e compatibilità |
| [Arte generata](Arte-generata) | Sprite prodotti da script |
| [Mappe e terreno](Mappe-e-terreno) | Generatori e `MapTool` |
| [Interfaccia e lingue](Interfaccia-e-lingue) | Testi, tema, carattere |
| [Test e playtest](Test-e-playtest) | Test automatici e partite simulate |
| [Agenti che giocano](Agenti-che-giocano) | Il server MCP per far giocare gli agenti |
| [Sito web](Sito-web) | Come si genera e si pubblica il sito |
| [Come contribuire](Come-contribuire) | Flusso di lavoro e convenzioni |

## In discussione

Idee non ancora nel gioco, spiegate per tutti: si cercano pareri.

| Pagina | Cosa trovi |
|---|---|
| [Mercato come una borsa](Mercato-come-una-borsa) | Prezzi che si muovono, rialzi, crolli, jackpot e rete di sicurezza, con due esempi e cinque domande |
