// Tests of the pure functions of racconto.js: `node --test tools/sito/test_racconto.mjs` (Node 22).
import { test } from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const { interpola, avanzamento, zoomPer, fuoco, smorza, posizioneDrone, puntoSulPercorso, angoloVerso, limitaCamera } = require("./racconto.js");

test("interpola: estremi, meta' con smoothstep, fuori corsa", () => {
  const k = [{ t: 0, x: 0, y: 0, vista: 10 }, { t: 1, x: 10, y: 20, vista: 20 }];
  assert.deepEqual(interpola(k, -1), { x: 0, y: 0, vista: 10 });
  assert.deepEqual(interpola(k, 0.5), { x: 5, y: 10, vista: 15 });
  assert.deepEqual(interpola(k, 2), { x: 10, y: 20, vista: 20 });
  assert.ok(interpola(k, 0.25).x < 2.5);   // eased: slower at the start
});

test("avanzamento: prima, durante, dopo", () => {
  assert.equal(avanzamento(0.1, 0.2, 0.4), 0);
  assert.ok(Math.abs(avanzamento(0.3, 0.2, 0.4) - 0.5) < 1e-9);
  assert.equal(avanzamento(0.5, 0.2, 0.4), 1);
  assert.equal(avanzamento(0.4, 0.4, 0.4), 1);
});

test("zoomPer e fuoco: orizzontale, verticale", () => {
  assert.equal(zoomPer(1600, 900, 10, 32), 5);       // 16:9: 10 cells across
  assert.equal(zoomPer(1600, 600, 10, 32), 5);       // wider: crop top and bottom
  assert.equal(zoomPer(390, 844, 10, 32), 390 / 192); // portrait: 6 cells across
  assert.equal(fuoco(1600, 900), 0.5);
  assert.equal(fuoco(390, 844), 0.36);
});

test("smorza: si avvicina senza superare", () => {
  const v = smorza(0, 100, 0.1, 9);
  assert.ok(v > 50 && v < 100);
  assert.equal(smorza(100, 100, 0.1, 9), 100);
});

test("posizioneDrone: attesa, volo, lavoro", () => {
  const c = [{ x: 10, y: 10, w: 2, h: 2, t0: 0.4, t1: 0.6 }];
  assert.deepEqual(posizioneDrone(c, [0, 0], 0.2, 0.1), { x: 0, y: 0, lavora: -1 });
  const volo = posizioneDrone(c, [0, 0], 0.2, 0.3);
  assert.equal(volo.lavora, -1);
  assert.ok(volo.x > 0 && volo.x < 10.65);
  assert.deepEqual(posizioneDrone(c, [0, 0], 0.2, 0.5), { x: 10.65, y: 10.1, lavora: 0 });
  assert.equal(posizioneDrone(c, [0, 0], 0.2, 0.9).lavora, -1);
});

test("puntoSulPercorso: segmenti e oltre la fine", () => {
  const p = [[0, 0], [0.5, 0], [0.5, 1], [2.5, 1]];
  assert.deepEqual(puntoSulPercorso(p, 0), { x: 0, y: 0 });
  assert.deepEqual(puntoSulPercorso(p, 1), { x: 0.5, y: 0.5 });
  assert.deepEqual(puntoSulPercorso(p, 2.5), { x: 1.5, y: 1 });
  assert.deepEqual(puntoSulPercorso(p, 9), { x: 2.5, y: 1 });
});

test("angoloVerso: la via piu' breve", () => {
  const a = angoloVerso(Math.PI * 0.9, -Math.PI * 0.9, 1);
  assert.ok(Math.abs(Math.cos(a) - Math.cos(-Math.PI * 0.9)) < 1e-9);
  assert.ok(angoloVerso(Math.PI * 0.9, -Math.PI * 0.9, 0.5) > Math.PI * 0.9);   // through pi, not through 0
});

test("limitaCamera: la vista resta dentro la mappa", () => {
  // 40x24 cells of 32 px; zoom 1: a 640x480 view shows 20x15 cells
  assert.deepEqual(limitaCamera(20, 12, 640, 480, 1, 32, 40, 24, 0.5), { x: 20, y: 12 });   // inside: unchanged
  assert.deepEqual(limitaCamera(2, 2, 640, 480, 1, 32, 40, 24, 0.5), { x: 10, y: 7.5 });    // pushed in from the top left
  assert.deepEqual(limitaCamera(39, 23, 640, 480, 1, 32, 40, 24, 0.5), { x: 30, y: 16.5 }); // and from the bottom right
  assert.deepEqual(limitaCamera(20, 23, 640, 480, 1, 32, 40, 24, 0.36), { x: 20, y: 14.4 }); // portrait focus: 36 % above
  const grande = limitaCamera(5, 5, 640, 960, 0.5, 32, 40, 24, 0.5);                       // taller than the map: centred
  assert.equal(grande.y, 12);
});
