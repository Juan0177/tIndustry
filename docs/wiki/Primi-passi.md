# Primi passi

Che gioco è tIndustry, come scaricarlo e avviarlo, e come si gioca la prima partita.

![Minatore, nastro e Core](https://raw.githubusercontent.com/Juan0177/tIndustry/main/docs/img/readme/gioco.png)

## Che gioco è

tIndustry è un gioco di logistica industriale ispirato a **Mindustry** e **Tiny Industry**. Estrai minerali, li porti al **CORE** sui nastri, li fondi in lastre e li trasformi in fili, grafite e silicio, vendi al **Mercato** e con i soldi sblocchi nuovi blocchi nella **Ricerca**. Non costruisci a mano: piazzi dei piani e il tuo **drone** li costruisce, come in Mindustry. Il combattimento per ora non c'è.

## Scaricare e avviare

Scarica l'ultima [Release](https://github.com/Juan0177/tIndustry/releases/latest), estrai lo zip e avvia l'eseguibile:

| Sistema | File da scaricare | Eseguibile |
|---|---|---|
| Windows (64 bit) | `tIndustry-win-x64.zip` | `tIndustry.exe` |
| Linux (64 bit) | `tIndustry-linux-x64.zip` | `tIndustry.x86_64` |

Il gioco parte a schermo intero alla risoluzione dello schermo. Al primo avvio la Scala UI si sceglie dall'altezza dello schermo (100% sotto i 1400 pixel, 150% sotto i 2000, 200% oltre); la cambi quando vuoi in [Impostazioni](Impostazioni-e-accessibilita).

## La Home

| Pulsante | Cosa fa |
|---|---|
| **Continua** | Riprende l'ultima partita salvata (compare solo se ce n'è una) |
| **Campagna** | Sceglie un livello della campagna, con obiettivi da raggiungere |
| **Nuova partita** | Partita libera su una mappa generata: scegli misura, seme e, se vuoi, la notte |
| **Gestione salvataggi** | Carica, elimina, duplica e salva con nome |
| **Impostazioni** | Grafica, lingua, scala, colori |
| **Esci** | Chiede conferma e salva la Continua |

Durante una partita Esc (con il cursore) riapre la Home con **Riprendi** al primo posto: la partita resta ferma finché non riprendi.

## L'obiettivo

Nella partita libera non c'è un traguardo: costruisci una fabbrica sempre più grande. Nella [campagna](Campagna-e-mappe) ogni livello ha i suoi obiettivi (vendere, accumulare, sbloccare) e completarlo sblocca il successivo.

## Il tutorial

Alla prima partita compare in basso a sinistra una scheda di tutorial in 8 passi. Non blocca il gioco: ogni passo avanza da solo quando fai quello che chiede, **Invio** passa al successivo, **Backspace** (o **Salta tutorial**) lo chiude. Si può rivedere da Impostazioni con **Rivedi tutorial**.

1. Muovere la visuale (WASD, rotella premuta o trascinamento col sinistro con il cursore; rotella per lo zoom, **H** per tornare al CORE).
2. La palette in basso a destra con le categorie; i tasti `1`–`9`; Esc per tornare al cursore.
3. Piazzare un **Minatore T1** (`2`) su un giacimento di ferro vicino al CORE e guardare il drone che lo costruisce.
4. Tracciare con il **Nastro T1** (`1`) una linea dal minatore al CORE.
5. Aprire il **Mercato** (`M`) e vendere.
6. Aprire la **Ricerca** (`T`).
7. Piazzare un **Forno** (`3`), che va a carbone o a corrente.
8. Aprire le **Impostazioni** (`I`).

## Il ciclo di base

```text
giacimento ──▶ Minatore ──▶ nastri ──▶ CORE ──▶ Mercato ──▶ $ ──▶ Ricerca ──▶ nuovi blocchi
                              │
                              └──▶ Forno ──▶ lastre ──▶ Assemblatore ──▶ fili, grafite, silicio
```

1. **Estrai.** Un Minatore T1 (2×2) su un giacimento di ferro produce Ferro grezzo. Il CORE parte con ferro e rame vicini.
2. **Trasporta.** Una linea di nastri porta il minerale al CORE, che lo mette in magazzino. Il magazzino del CORE paga anche le costruzioni.
3. **Trasforma.** Il Forno fonde il minerale in lastre (serve carbone oppure corrente); l'Assemblatore fa filo di rame, grafite e silicio. Vedi [Produzione](Produzione).
4. **Vendi.** Nel Mercato vendi quello che è nel CORE, oppure accendi la vendita automatica. Vedi [Economia e Ricerca](Economia-e-Ricerca).
5. **Ricerca.** Con soldi e materiali sblocchi forno, assemblatore, nastri più veloci, energia e il resto.

All'inizio di una partita libera hai $180, 48 Ferro grezzo, 48 Lastre di ferro e 10 Fili di rame: abbastanza per i primi minatori e nastri.

## Vedi anche

- [Comandi](Comandi)
- [Costruzione](Costruzione)
- [Campagna e mappe](Campagna-e-mappe)
