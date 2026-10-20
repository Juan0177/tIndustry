# Idee da implementare

Idee di gioco non ancora pianificate: quando una diventa un lavoro vero passa nella [roadmap del README](../README.md#stato-e-roadmap) con la sua specifica in `docs/superpowers/specs/`. Chi aggiunge un'idea scrive cosa succede in partita, perché è divertente e cosa resta da decidere.

## Mercato: lo «stonks» mentre vendi

Proposta dell'utente, 20 ottobre 2026, dopo il capitolo del Mercato del [racconto del sito](https://juan0177.github.io/tIndustry/racconto/).

- **Cosa succede.** Oggi al Mercato il prezzo cala mentre vendi e risale piano. L'idea: quando vendi in un colpo solo una quantità enorme di un oggetto, con una probabilità bassa scatta un evento raro, quasi mistico. Lo schermo del Mercato mostra un crollo spettacolare (un fungo atomico nello schermo, il prezzo che precipita, il meme «stonks» col drone in giacca e cravatta) e subito dopo arriva un **bonus dei soldi** di una percentuale a sorpresa (nel racconto +777%).
- **Perché.** Trasforma l'errore classico (svendere tutto insieme) in un momento da ricordare e da raccontare, senza togliere il senso alla regola del prezzo che cala: di solito vendere tutto insieme resta una perdita.
- **Da decidere.**
  - Soglia: quante unità in una vendita, o quanta parte delle scorte, prima che l'evento possa scattare.
  - Probabilità e bonus: fissi, per oggetto, o crescenti con la quantità; un tetto al bonus.
  - Determinismo: la simulazione è deterministica (niente `Random` non seminato in `Step()`), quindi il caso va tirato da un generatore seminato dalla partita, uguale in replay e in co-op.
  - Interfaccia: l'animazione nel pannello del Mercato (fungo, crollo, meme) con le chiavi di `testi.csv` in italiano e inglese; rispetto di «Effetti» bassi e della riduzione del movimento (niente lampo).
  - Equilibrio con l'economia e la campagna: un bonus troppo grande salta i livelli.
