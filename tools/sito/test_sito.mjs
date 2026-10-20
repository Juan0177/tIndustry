// Tests of the pure functions of sito.js: `node --test tools/sito/test_sito.mjs` (Node 22).
// sito.js is required without a DOM: it exports intercetta, avanzamento, bersaglio and politica and stops.
import { test } from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const { intercetta, avanzamento, bersaglio, politica } = require("./sito.js");

const link = (attributi = ["data-costruisci"]) => ({
  hasAttribute: (nome) => attributi.includes(nome),
  href: "https://example.invalid/releases/latest",
});
const clic = (extra = {}) => ({
  button: 0, ctrlKey: false, metaKey: false, shiftKey: false, altKey: false, defaultPrevented: false, ...extra,
});
const media = (riduci) => (query) => ({ matches: riduci && query === "(prefers-reduced-motion: reduce)" });

test("intercetta: clic normale su un link con data-costruisci", () => {
  assert.equal(intercetta(clic(), link(), media(false)), true);
});

test("intercetta: tasto centrale", () => {
  assert.equal(intercetta(clic({ button: 1 }), link(), media(false)), false);
  assert.equal(intercetta(clic({ button: 2 }), link(), media(false)), false);
});

test("intercetta: Ctrl e gli altri modificatori", () => {
  assert.equal(intercetta(clic({ ctrlKey: true }), link(), media(false)), false);
  for (const tasto of ["metaKey", "shiftKey", "altKey"]) {
    assert.equal(intercetta(clic({ [tasto]: true }), link(), media(false)), false, tasto);
  }
});

test("intercetta: link senza data-costruisci", () => {
  assert.equal(intercetta(clic(), link([]), media(false)), false);
  assert.equal(intercetta(clic(), link(["href"]), media(false)), false);
  assert.equal(intercetta(clic(), null, media(false)), false);
});

test("intercetta: prefers-reduced-motion", () => {
  assert.equal(intercetta(clic(), link(), media(true)), false);
});

test("politica: il bottone vince sul sistema, il sistema da solo riduce", () => {
  assert.equal(politica("no", false), "statico");
  assert.equal(politica("no", true), "statico");
  assert.equal(politica("si", true), "pieno");
  assert.equal(politica("si", false), "pieno");
  assert.equal(politica(null, false), "pieno");
  assert.equal(politica(null, true), "ridotto");
  assert.equal(politica(undefined, true), "ridotto");
  assert.equal(politica("boh", true), "ridotto");
});

test("intercetta: evento gia' gestito (defaultPrevented)", () => {
  assert.equal(intercetta(clic({ defaultPrevented: true }), link(), media(false)), false);
});

test("intercetta: Invio da tastiera (senza button) e senza matchMedia", () => {
  assert.equal(intercetta({}, link()), true);
  assert.equal(intercetta(clic({ button: undefined }), link(), media(false)), true);
  assert.equal(intercetta(clic(), { dataset: { costruisci: "" } }, media(false)), true, "anche con dataset");
});

test("avanzamento: 0 all'inizio, 50 a meta', 100 in fondo", () => {
  assert.equal(avanzamento(0, 3000, 800), 0);
  assert.equal(avanzamento(1100, 3000, 800), 50);
  assert.equal(avanzamento(2200, 3000, 800), 100);
});

test("avanzamento: pagina che non scorre e valori fuori corsa", () => {
  assert.equal(avanzamento(0, 700, 800), 0);
  assert.equal(avanzamento(100, 800, 800), 0);
  assert.equal(avanzamento(5000, 3000, 800), 100);
  assert.equal(avanzamento(-5, 3000, 800), 0);
});

test("bersaglio: preferisce i pezzi fuori dalla fascia centrale e resta nel viewport", () => {
  const caso = () => 0;
  const r = (left, top) => ({ left, top, width: 64, height: 64, right: left + 64, bottom: top + 64 });
  const meta = bersaglio([r(600, 300), r(100, 300)], 1280, 720, caso, 76);
  assert.ok(meta.x >= 24 && meta.x <= 1256 && meta.y >= 76 && meta.y <= 696, JSON.stringify(meta));
  assert.ok(meta.x < 320, "vicino al pezzo laterale, non a quello al centro");
  const centrale = bersaglio([r(600, 300)], 1280, 720, caso, 76);
  assert.ok(centrale.x > 320 && centrale.x < 960, "senza alternative accetta il centro");
  const vuoto = bersaglio([], 1280, 720, caso, 76);
  assert.ok(vuoto.x <= 1280 * 0.15 + 24 || vuoto.x >= 1280 * 0.85 - 24, "nessun pezzo: ai margini");
  assert.ok(vuoto.y >= 76 && vuoto.y <= 696);
  const fuoriVista = bersaglio([r(600, -500), r(100, 900)], 1280, 720, caso, 76);
  assert.ok(fuoriVista.x <= 1280 * 0.15 + 24 || fuoriVista.x >= 1280 * 0.85 - 24, "pezzi fuori vista ignorati");
});
