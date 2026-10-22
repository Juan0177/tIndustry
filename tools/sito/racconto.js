/*
 * tIndustry site: the scroll-driven story (tools/sito/racconto.js, copied next to the pages by costruisci.py).
 *
 * The page has a sticky full-screen <canvas> behind the chapter panels. The scroll progress through the story
 * (0..1) is the timeline: it places the camera (key frames, smoothstep between them) and decides how far every
 * construction has risen, so scrolling back takes the factory apart again (scrub). Wall-clock time only drives
 * what moves by itself: belt treads, items, working blocks, the drone's hover and its beam. The story runs
 * through the eight chapters: deposit and belts, smelter and plates, power, night and the attack (a close-up between
 * cinema bars: the silhouettes come over the river, the drone sees them, trembles and puts its helmet on; enemies
 * come only at night and the survivors flee at dawn), the world, the Core and the market (a spike to the triple of
 * the base value, a briefcase of 9999 plates thrown into the screen, the crash as a falling rocket-bomb that goes
 * off as a mushroom cloud at the floor, «stonks» and the jackpot bonus, and the drone changing channel with a remote:
 * the game's channel, where the real download and GitHub links stand over the screen).
 *
 * The data (map, camera, constructions in chain order, item path, sprite list) comes from racconto.py as JSON in
 * <script id="racconto-dati">. Drawing follows the game: art scaled with nearest-neighbour, plans as the construct
 * shader at progress 0 (dark silhouette, amber outline), sites rising from the bottom with an amber scan line,
 * the drone (drone-1) in 2.5D: the stacked layers of racconto.modello_drone (domed hull, raised sensor, stepped
 * rotor ducts) over its 35 % shadow, enlarged x4 and drawn smaller with filtering like DroneVisual, a forward
 * lean in flight and the build beam to its site.
 *
 * Smooth scrolling (the decision of 20 October): wheel input is eased towards its target with an exponential
 * approach, only when the motion policy of sito.js is full (html.animato); keyboard, scrollbar, touch and anchors
 * stay native, and any of them stops the easing at once. With html.statico time stops (treads, items, hover):
 * only the visitor's own scrolling moves the story.
 *
 * Pure functions (interpola, avanzamento, zoomPer, fuoco, smorza, posizioneDrone, puntoSulPercorso, angoloVerso) are
 * exported for `node --test`: the file can be required without a DOM.
 */
"use strict";

(function () {
  // ------------------------------------------------------------------ pure functions (also for node)

  function limita(v, a, b) { return v < a ? a : v > b ? b : v; }
  function liscia(u) { return u * u * (3 - 2 * u); }

  /* Camera at progress t from key frames sorted by t ({t, x, y, vista}): smoothstep between neighbours. */
  function interpola(chiavi, t) {
    if (!chiavi.length) return { x: 0, y: 0, vista: 10 };
    if (t <= chiavi[0].t) return { x: chiavi[0].x, y: chiavi[0].y, vista: chiavi[0].vista };
    for (var i = 0; i < chiavi.length - 1; i++) {
      var a = chiavi[i], b = chiavi[i + 1];
      if (t < b.t) {
        var u = liscia((t - a.t) / (b.t - a.t));
        return { x: a.x + (b.x - a.x) * u, y: a.y + (b.y - a.y) * u, vista: a.vista + (b.vista - a.vista) * u };
      }
    }
    var z = chiavi[chiavi.length - 1];
    return { x: z.x, y: z.y, vista: z.vista };
  }

  /* How far a site has risen at t: 0 before t0, 1 after t1. */
  function avanzamento(t, t0, t1) {
    if (t1 <= t0) return t >= t1 ? 1 : 0;
    return limita((t - t0) / (t1 - t0), 0, 1);
  }

  /* Screen px per art px for `vista` cells across a 16:9 frame (wider screens crop the top and bottom). On a
     portrait screen the width shows 60 % of those cells: the camera gets closer, but the chain stays in view. */
  function zoomPer(vw, vh, vista, cella) {
    cella = cella || 32;
    if (vh > vw) return vw / (vista * 0.6 * cella);
    return Math.max(vw, vh * 16 / 9) / (vista * cella);
  }

  /* Height fraction of the screen where the camera centre sits: higher on portrait screens, where the chapter
     panels cover the bottom. */
  function fuoco(vw, vh) { return vh > vw ? 0.36 : 0.5; }

  /* Camera centre (cells) kept so the view never shows past the map: the visible span is vw / (z * cella) cells
     across and vh / (z * cella) down, with the centre at `fy` of the height; a map smaller than the view stays
     centred on that axis. */
  function limitaCamera(cx, cy, vw, vh, z, cella, larghezza, righe, fy) {
    var lw = vw / (z * cella), lh = vh / (z * cella);
    var x = lw >= larghezza ? larghezza / 2 : limita(cx, lw / 2, larghezza - lw / 2);
    var sopra = lh * fy, sotto = lh * (1 - fy);
    var y = lh >= righe ? righe / 2 - lh / 2 + sopra : limita(cy, sopra, righe - sotto);
    return { x: x, y: y };
  }

  /* Market price at t: the base, soaring (sale) to the top on a log scale, then crashing (crollo) to the bottom. */
  function prezzoMercato(m, t) {
    var P = m.prezzo, su = liscia(avanzamento(t, m.sale[0], m.sale[1])), giu = liscia(avanzamento(t, m.crollo[0], m.crollo[1]));
    var alto = P.base * Math.pow(P.alto / P.base, su);
    return alto * Math.pow(P.basso / P.alto, giu);
  }

  /* The rocket-bomb at fraction u of the crash: it leaves (x0, y0) and hits (x1, y1), falling faster and faster
     (y grows with u squared); `ang` points its nose along the fall. Screen art px. */
  function percorsoCaduta(x0, y0, x1, y1, u) {
    return { x: x0 + (x1 - x0) * u, y: y0 + (y1 - y0) * u * u, ang: Math.atan2((y1 - y0) * (2 * u + 0.15), x1 - x0) };
  }

  /* Height of a price on the market screen's log scale (racconto.py y_prezzo does the same). */
  function yPrezzo(M, v) {
    var lmin = Math.log(M.prezzo.basso * 0.5), lmax = Math.log(M.prezzo.alto * 1.3), ih = M.interno[3];
    return ih - 8 - (Math.log(v) - lmin) / (lmax - lmin) * (ih - 34);
  }

  /* A price as the Italian interface writes it: 2.500, 37, 4,20, 0,01. */
  function testoPrezzo(v) {
    if (v >= 100) return String(Math.round(v)).replace(/\B(?=(\d{3})+(?!\d))/g, ".");
    if (v >= 10) return String(Math.round(v));
    return v.toFixed(2).replace(".", ",");
  }

  /* Exponential approach of `attuale` to `meta` after dt seconds (frame-rate independent easing). */
  function smorza(attuale, meta, dt, k) {
    return meta + (attuale - meta) * Math.exp(-k * dt);
  }

  /* Where the drone hovers at t (cells) and which construction it works on (-1: flying or idle). It waits at
     `partenza` until `ingresso`, flies to each site in the last VOLO before its t0 (eased), stays over it until t1
     and waits there until it is time to leave for the next one. `extra` stops ({t, x, y, guarda}, sorted by t) go
     in between the builds by their time: the guard post of the attack, the market after the last build. */
  var VOLO = 0.04;   // at most this much progress to fly to the next site: long pauses are spent waiting on guard
  function posizioneDrone(costruzioni, partenza, ingresso, t, extra) {
    var tappe = [{ t: ingresso, x: partenza[0], y: partenza[1], lavora: -1 }];
    var soste = extra || [], s = 0;
    function sosta(prima) {   // the extra stops due before `prima`
      for (; s < soste.length && soste[s].t < prima; s++) {
        var e = soste[s];
        tappe.push({ t: e.t, x: e.x, y: e.y, lavora: -1, guarda: e.guarda || null });
      }
    }
    costruzioni.forEach(function (c, i) {
      if (c.fisso) return;   // already there before the story: never visited
      sosta(c.t0 - VOLO);
      var sopra = { x: c.x + c.w / 2 - 0.35, y: c.y + c.h / 2 - 0.9 };
      var prima = tappe[tappe.length - 1];
      if (c.t0 - VOLO > prima.t) tappe.push({ t: c.t0 - VOLO, x: prima.x, y: prima.y, lavora: -1 });
      tappe.push({ t: c.t0, x: sopra.x, y: sopra.y, lavora: -1 });
      tappe.push({ t: c.t1, x: sopra.x, y: sopra.y, lavora: i });
    });
    sosta(Infinity);
    if (t <= tappe[0].t) return { x: tappe[0].x, y: tappe[0].y, lavora: -1 };
    for (var k = 0; k < tappe.length - 1; k++) {
      var a = tappe[k], b = tappe[k + 1];
      if (t < b.t) {
        var u = b.t > a.t ? (t - a.t) / (b.t - a.t) : 1;
        if (b.lavora >= 0) return { x: b.x, y: b.y, lavora: b.lavora };   // over its site, building
        u = liscia(u);
        var p = { x: a.x + (b.x - a.x) * u, y: a.y + (b.y - a.y) * u, lavora: -1 };
        if (a.guarda && b.guarda) p.guarda = b.guarda;   // standing still, facing something
        return p;
      }
    }
    var z = tappe[tappe.length - 1];
    var fine = { x: z.x, y: z.y, lavora: -1 };
    if (z.guarda) fine.guarda = z.guarda;
    return fine;
  }

  /* Point at distance s (cells) along a polyline of [x, y] points (cells). */
  function puntoSulPercorso(percorso, s) {
    if (s <= 0) return { x: percorso[0][0], y: percorso[0][1] };
    for (var i = 0; i < percorso.length - 1; i++) {
      var a = percorso[i], b = percorso[i + 1];
      var l = Math.abs(b[0] - a[0]) + Math.abs(b[1] - a[1]);   // axis-aligned segments
      if (s <= l) {
        var u = l ? s / l : 0;
        return { x: a[0] + (b[0] - a[0]) * u, y: a[1] + (b[1] - a[1]) * u };
      }
      s -= l;
    }
    var z = percorso[percorso.length - 1];
    return { x: z[0], y: z[1] };
  }

  /* Turn `attuale` (radians) towards `meta` the short way by the fraction f. */
  function angoloVerso(attuale, meta, f) {
    var d = Math.atan2(Math.sin(meta - attuale), Math.cos(meta - attuale));
    return attuale + d * f;
  }

  if (typeof module !== "undefined" && module.exports) {
    module.exports = { interpola: interpola, avanzamento: avanzamento, zoomPer: zoomPer, smorza: smorza,
      posizioneDrone: posizioneDrone, puntoSulPercorso: puntoSulPercorso, angoloVerso: angoloVerso, fuoco: fuoco,
      limitaCamera: limitaCamera, prezzoMercato: prezzoMercato, testoPrezzo: testoPrezzo, percorsoCaduta: percorsoCaduta,
      yPrezzo: yPrezzo };
  }
  if (typeof document === "undefined" || typeof window === "undefined") return;

  // ------------------------------------------------------------------ page

  var doc = document, radice = doc.documentElement;
  var racconto = doc.querySelector(".racconto");
  var tela = doc.getElementById("racconto-tela");
  var datiEl = doc.getElementById("racconto-dati");
  if (!racconto || !tela || !datiEl || !tela.getContext) return;
  var ctx = tela.getContext("2d");
  if (!ctx) return;
  var dati = JSON.parse(datiEl.textContent);
  var CELLA = dati.cella;
  var cartella = racconto.getAttribute("data-cartella") || "";

  function statico() { return radice.classList.contains("statico"); }
  function riduceSistema() { return !!(window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches); }
  function animato() { return radice.classList.contains("animato"); }

  // --- images
  var img = {}, daCaricare = 0;
  function carica(nome, file) {
    daCaricare++;
    var i = new Image();
    i.onload = i.onerror = function () { daCaricare--; if (!daCaricare) pronto(); };
    i.src = cartella + file;
    img[nome] = i;
  }
  carica("terreno", dati.terreno);
  Object.keys(dati.sprite).forEach(function (n) { carica(n, dati.sprite[n].file); });

  // --- scroll progress through the story
  function progresso() {
    var r = racconto.getBoundingClientRect();
    var scena = racconto.querySelector(".racconto-scena");
    var alto = scena ? scena.getBoundingClientRect().height : window.innerHeight;
    var corsa = r.height - alto;
    var cima = scena ? parseFloat(getComputedStyle(scena).top) || 0 : 0;
    return corsa > 0 ? limita((cima - r.top) / corsa, 0, 1) : 0;
  }

  // --- smooth wheel scrolling
  var dolce = { attivo: false, meta: 0, attuale: 0 };
  function massimo() { return Math.max(0, radice.scrollHeight - window.innerHeight); }
  function scorreDaSe(el) {   // an element with its own scrolling under the pointer keeps the wheel
    for (; el && el !== doc.body && el !== radice; el = el.parentElement) {
      var s = getComputedStyle(el);
      if (/(auto|scroll)/.test(s.overflowY + s.overflowX) && (el.scrollHeight > el.clientHeight || el.scrollWidth > el.clientWidth)) return true;
    }
    return false;
  }
  window.addEventListener("wheel", function (ev) {
    if (!animato() || ev.ctrlKey || ev.defaultPrevented || scorreDaSe(ev.target)) return;
    var d = ev.deltaY * (ev.deltaMode === 1 ? 16 : ev.deltaMode === 2 ? window.innerHeight : 1);
    if (!d) return;
    ev.preventDefault();
    if (!dolce.attivo) { dolce.attuale = dolce.meta = window.pageYOffset; }
    dolce.meta = limita(dolce.meta + d, 0, massimo());
    dolce.attivo = true;
    sveglia();
  }, { passive: false });
  function fermaDolce() { dolce.attivo = false; }
  ["keydown", "mousedown", "touchstart"].forEach(function (n) { window.addEventListener(n, fermaDolce, { passive: true }); });

  // --- canvas size
  var dpr = 1, larghezza = 0, altezza = 0;
  function misura() {
    var r = tela.getBoundingClientRect();
    dpr = Math.min(window.devicePixelRatio || 1, 2);
    larghezza = r.width; altezza = r.height;
    var w = Math.round(r.width * dpr), h = Math.round(r.height * dpr);
    if (tela.width !== w || tela.height !== h) { tela.width = w; tela.height = h; }
  }

  // --- drawing helpers (world units: art px; one cell = CELLA)
  function fotogramma(nome, k, sx, sy, w, h, dx, dy, dw, dh) {
    var s = dati.sprite[nome];
    ctx.drawImage(img[nome], k * s.w + (sx || 0), sy || 0, w || s.w, h || s.h, dx, dy, dw || s.w, dh || s.h);
  }
  function girato(nome, k, x, y, w, h, gradi, specchio) {   // mirror first, then rotate (BeltCornerTile)
    ctx.save();
    ctx.translate(x + w / 2, y + h / 2);
    if (gradi) ctx.rotate(gradi * Math.PI / 180);
    if (specchio) ctx.scale(-1, 1);
    fotogramma(nome, k, 0, 0, 0, 0, -w / 2, -h / 2, w, h);
    ctx.restore();
  }
  function fotogrammaAnimato(nome, tempo) {
    var s = dati.sprite[nome];
    if (s.fotogrammi <= 1) return 0;
    if (s.fps) return Math.floor(tempo * s.fps) % s.fotogrammi;
    var totale = s.durate.reduce(function (a, b) { return a + b; }, 0), ms = (tempo * 1000) % totale;
    for (var k = 0; k < s.durate.length; k++) { ms -= s.durate[k]; if (ms < 0) return k; }
    return 0;
  }

  var AMBRA = "#FFD37F";

  function disegnaCostruzione(c, t, tempo) {
    if (t < c.piano) return;   // the plan shows up when the drone starts the piece before
    var x = c.x * CELLA, y = c.y * CELLA, w = c.w * CELLA, h = c.h * CELLA;
    var p = avanzamento(t, c.t0, c.t1);
    var acceso = p >= 1 && (c.nastro || (c.attivo !== undefined && t >= c.attivo));
    var pieno = acceso ? c.lavoro : c.fermo;
    var k = acceso ? fotogrammaAnimato(pieno, tempo) : 0;
    if (p < 1) girato(c.piano_sprite, 0, x, y, w, h, c.rot, c.specchio);
    if (p <= 0) return;
    ctx.save();
    if (p < 1) { ctx.beginPath(); ctx.rect(x, y + h * (1 - p), w, h * p); ctx.clip(); }
    girato(pieno, k, x, y, w, h, c.rot, c.specchio);
    ctx.restore();
    if (p < 1) {   // the scan line on the front, breathing
      ctx.globalAlpha = 0.65 + 0.35 * Math.sin(tempo * 6);
      ctx.fillStyle = AMBRA;
      ctx.fillRect(x, y + h * (1 - p) - 0.5, w, 1);
      ctx.globalAlpha = 1;
    }
  }

  /* Items of every chain, one per cell at the belt's speed, once the chain's source works and only along the cells
     built so far (in flow order): a finished chain carries them into its block, or out of the map. A flow through
     the T merge (`unione`: from distance s on, k times faster) spreads its items k cells apart there; `sfasa`
     shifts a flow by part of a cell, so the two flows on the merged belt take turns. */
  function disegnaOggetti(t, tempo) {
    dati.flussi.forEach(function (f) {
      var fase = (tempo * dati.velocita + (f.sfasa || 0)) % 1, U = f.unione;
      if (t < f.avvio) return;
      var finiti = 0;
      while (finiti < f.celle.length) {
        var c = dati.costruzioni[f.celle[finiti]];
        if (avanzamento(t, c.t0, c.t1) < 1) break;
        finiti++;
      }
      if (!finiti) return;
      var limite = finiti;
      if (finiti === f.celle.length) {   // the whole path, to the block's edge or past the map's border
        limite = 0;
        for (var i = 0; i < f.percorso.length - 1; i++) {
          limite += Math.abs(f.percorso[i + 1][0] - f.percorso[i][0]) + Math.abs(f.percorso[i + 1][1] - f.percorso[i][1]);
        }
      }
      var s = dati.sprite[f.oggetto];
      for (var d = fase; ; d += 1) {
        var lungo = U && d > U.s ? U.s + (d - U.s) * U.k : d;
        if (lungo > limite) break;
        var q = puntoSulPercorso(f.percorso, lungo);
        fotogramma(f.oggetto, 0, 0, 0, 0, 0, q.x * CELLA - s.w / 2, q.y * CELLA - s.h / 2);
      }
    });
  }

  var ENERGIA = "#FBE36B";   // UiColors.Energia, the colour of the power lines in the client
  function disegnaLinee(t) {
    dati.linee.forEach(function (l) {
      var u = avanzamento(t, l.t0, l.t1);
      if (u <= 0) return;
      var x0 = l.da[0] * CELLA, y0 = l.da[1] * CELLA;
      var x1 = x0 + (l.a[0] - l.da[0]) * CELLA * u, y1 = y0 + (l.a[1] - l.da[1]) * CELLA * u;
      ctx.strokeStyle = "rgba(251,227,107,0.35)"; ctx.lineWidth = 2.4;
      ctx.beginPath(); ctx.moveTo(x0, y0); ctx.lineTo(x1, y1); ctx.stroke();
      ctx.strokeStyle = ENERGIA; ctx.lineWidth = 0.9;
      ctx.beginPath(); ctx.moveTo(x0, y0); ctx.lineTo(x1, y1); ctx.stroke();
      ctx.fillStyle = ENERGIA;
      ctx.beginPath(); ctx.arc(x1, y1, 1.6, 0, Math.PI * 2); ctx.fill();
    });
  }

  /* How dark it is at t: night falls (t0..t1) and lifts again at dawn (alba0..alba1). */
  function notte(t) {
    var N = dati.notte;
    return avanzamento(t, N.t0, N.t1) * (1 - avanzamento(t, N.alba0 || 2, N.alba1 || 2));
  }

  /* Night as the client's NightLayer: the world multiplied by a dark blue, then warm light around the lamps and
     the smelter, added on top once they are on. */
  function disegnaNotte(t) {
    var n = notte(t);
    if (n <= 0) return;
    var W = dati.larghezza * CELLA, H = dati.righe * CELLA, col = dati.notte.colore;
    ctx.save();
    ctx.globalCompositeOperation = "multiply";
    ctx.fillStyle = "rgb(" + col.map(function (v) { return Math.round(255 * (1 - n * (1 - v))); }).join(",") + ")";
    ctx.fillRect(-W, -H, W * 3, H * 3);
    ctx.globalCompositeOperation = "lighter";
    dati.luci.forEach(function (l) {
      if (t < l.da) return;
      var forza = Math.min(1, (t - l.da) / 0.015) * n * l.energia * 0.3;
      var r = l.raggio * CELLA, x = l.x * CELLA, y = l.y * CELLA;
      var rgb = l.colore.map(function (v) { return Math.round(v * 255); }).join(",");
      var g = ctx.createRadialGradient(x, y, 0, x, y, r);
      g.addColorStop(0, "rgba(" + rgb + "," + forza.toFixed(3) + ")");
      g.addColorStop(0.45, "rgba(" + rgb + "," + (forza * 0.45).toFixed(3) + ")");
      g.addColorStop(1, "rgba(" + rgb + ",0)");
      ctx.fillStyle = g;
      ctx.fillRect(x - r, y - r, r * 2, r * 2);
    });
    ctx.restore();
  }

  /* The attack (roadmap «Attacco e difesa»: enemies come only at night). Black silhouettes rimmed in red cross the
     river from the east, stop outside the light of the lamps and, when the day returns, the survivors run back east. */
  function xNemico(chiavi, t) {
    if (t <= chiavi[0][0]) return null;
    if (t >= chiavi[chiavi.length - 1][0]) return null;
    for (var i = 0; i < chiavi.length - 1; i++) {
      var a = chiavi[i], b = chiavi[i + 1];
      if (t < b[0]) return a[1] + (b[1] - a[1]) * liscia((t - a[0]) / (b[0] - a[0]));
    }
    return null;
  }
  function disegnaNemici(t, tempo) {
    ctx.save();
    ctx.shadowColor = "rgba(255,42,74,0.95)";
    ctx.shadowBlur = 16 * dpr;
    dati.nemici.forEach(function (n, i) {
      var x = xNemico(n.chiavi, t);
      if (x === null) return;
      var s = dati.sprite[n.sprite];
      var primo = n.chiavi[0][0], ultimo = n.chiavi[n.chiavi.length - 1][0];
      ctx.globalAlpha = Math.min(1, (t - primo) / 0.015, (ultimo - t) / 0.015);
      var fuga = t > n.chiavi[n.chiavi.length - 2][0];   // running away: turned east
      ctx.save();
      ctx.translate(x * CELLA + Math.sin(tempo * 1.3 + i * 2) * 3, n.y * CELLA + Math.cos(tempo * 1.1 + i) * 3);
      ctx.rotate(fuga ? 0 : Math.PI);   // facing the factory (west) while they come
      fotogramma(n.sprite, 0, 0, 0, 0, 0, -s.w, -s.h, s.w * 2, s.h * 2);   // units loom larger than the drone
      ctx.restore();
    });
    ctx.restore();
  }

  /* ...and a slow red glow at the edges of the screen (no flashing: one breath every 2.5 s). */
  function disegnaAllarme(t, tempo) {
    var A = dati.allarme;
    var e = Math.min(avanzamento(t, A.t0, A.t0 + 0.02), 1 - avanzamento(t, A.t1 - 0.02, A.t1));
    if (e <= 0) return;
    var W = tela.width, H = tela.height;
    var g = ctx.createRadialGradient(W / 2, H / 2, Math.min(W, H) * 0.35, W / 2, H / 2, Math.max(W, H) * 0.75);
    g.addColorStop(0, "rgba(255,42,74,0)");
    g.addColorStop(1, "rgba(255,42,74," + (e * (0.32 + 0.14 * Math.sin(tempo * 2.5))).toFixed(3) + ")");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, W, H);
  }

  var drone = { ang: Math.PI * 0.75, inclina: 0, ux: 0, uy: 0 };
  function disegnaDrone(t, tempo, dt) {
    var D = dati.drone;
    var dove = posizioneDrone(dati.costruzioni, D.partenza, D.ingresso, t, D.tappe);
    // Flight direction from the timeline itself (a little before and after t), not from frame deltas: the same
    // pose at the same scroll position, however fast the visitor scrolls.
    var prima = posizioneDrone(dati.costruzioni, D.partenza, D.ingresso, t - 0.003, D.tappe);
    var dopo = posizioneDrone(dati.costruzioni, D.partenza, D.ingresso, t + 0.003, D.tappe);
    var mx = dopo.x - prima.x, my = dopo.y - prima.y, passo = Math.sqrt(mx * mx + my * my);
    var vola = dove.lavora < 0 && passo > 0.002;
    var meta = drone.ang;
    if (dove.lavora >= 0) {
      var c = dati.costruzioni[dove.lavora];
      meta = Math.atan2(c.y + c.h / 2 - dove.y, c.x + c.w / 2 - dove.x);
    } else if (dove.guarda) {
      meta = Math.atan2(dove.guarda[1] - dove.y, dove.guarda[0] - dove.x);
    } else if (vola) {
      meta = Math.atan2(my, mx);
    }
    var f = 1 - Math.exp(-dt * 8);
    drone.ang = angoloVerso(drone.ang, meta, f);
    // Leaning into the flight: each layer moves a little along the heading, so the drone tilts forwards.
    if (vola) { drone.ux = mx / passo; drone.uy = my / passo; }
    drone.inclina += ((vola ? 0.5 : 0) - drone.inclina) * f;
    var lx = drone.ux * drone.inclina, ly = drone.uy * drone.inclina;
    var s = dati.sprite.drone, lato = s.lato, n = s.fotogrammi;
    var bob = Math.sin(tempo * 2.6) * 1.6;   // DroneVisual: ±0.05 tile, only the body moves
    var A = dati.attacco, paura = A ? Math.min(avanzamento(t, A.trema[0], A.trema[0] + 0.006), 1 - avanzamento(t, A.trema[1], A.trema[1] + 0.006)) : 0;
    var tremaX = 0, tremaY = 0;
    if (paura > 0) {   // trembling with fear, quick and small (smaller when the system asks for less motion)
      var ampiezza = paura * (riduceSistema() ? 0.35 : 1.2);
      tremaX = (Math.sin(tempo * 61) + Math.sin(tempo * 97)) * 0.5 * ampiezza;
      tremaY = (Math.sin(tempo * 73 + 1) + Math.sin(tempo * 43)) * 0.4 * ampiezza;
    }
    var cx = dove.x * CELLA + tremaX, cy = dove.y * CELLA - bob + tremaY;
    var co = Math.cos(drone.ang), si = Math.sin(drone.ang);
    var m = ctx.getTransform();   // where the drone is on screen (CSS px), for window.tIndustryRacconto
    drone.schermo = { x: (m.a * cx + m.e) / dpr, y: (m.d * cy + m.f) / dpr };
    ctx.imageSmoothingEnabled = true;   // enlarged x4 art drawn smaller with filtering: crisp at any heading
    ctx.save();   // the shadow stays on the ground, down-right (light from the top-left)
    ctx.translate(dove.x * CELLA + 4, dove.y * CELLA + 6);
    ctx.rotate(drone.ang);
    fotogramma("drone-ombra", 0, 0, 0, 0, 0, -lato / 2, -lato / 2, lato, lato);
    ctx.restore();
    // engine glow at the tail, low, before the hull so half of it hides behind (DroneVisual)
    var r = 2.4 + 0.6 * Math.sin(tempo * 9);
    ctx.fillStyle = "rgba(255,179,71,0.9)";
    ctx.beginPath();
    ctx.arc(cx - co * 12 + lx, cy - si * 12 - 1 + ly, r, 0, Math.PI * 2);
    ctx.fill();
    for (var k = 0; k < n; k++) {
      ctx.save();
      ctx.translate(cx + lx * k, cy - k + ly * k);
      ctx.rotate(drone.ang);
      fotogramma("drone", k, 0, 0, 0, 0, -lato / 2, -lato / 2, lato, lato);
      ctx.restore();
    }
    // the helmet: it drops onto the hull when the attack starts (a small bounce) and lifts off at dawn
    var E = dati.elmetto, he = dati.sprite.elmetto;
    if (E && he && t >= E.cade && t < E.via) {
      var su = 0;
      if (t < E.posato) {
        var u = (t - E.cade) / (E.posato - E.cade);
        su = u < 0.7 ? (1 - u / 0.7) * 26 : Math.sin((u - 0.7) / 0.3 * Math.PI) * 3;
      } else if (t > E.toglie) {
        su = (t - E.toglie) / (E.via - E.toglie) * 30;
      }
      ctx.globalAlpha = t > E.toglie ? 1 - (t - E.toglie) / (E.via - E.toglie) : 1;
      for (var q = 0; q < he.fotogrammi; q++) {
        var z = n + q;
        ctx.save();
        ctx.translate(cx + lx * z, cy - z - su + ly * z);
        ctx.rotate(drone.ang);
        fotogramma("elmetto", q, 0, 0, 0, 0, -lato / 2, -lato / 2, lato, lato);
        ctx.restore();
      }
      ctx.globalAlpha = 1;
    }
    ctx.imageSmoothingEnabled = false;
    if (paura > 0) disegnaPaura(t, tempo, cx, cy - n, paura);
    disegnaTelecomando(t, tempo, cx + co * 9 + lx * (n - 1), cy - (n - 1) + si * 9 + ly * (n - 1));
    disegnaValigetta(t, tempo, cx + lx * (n - 1), cy - (n - 1) + ly * (n - 1));
    disegnaPorte(t);
    disegnaBonus(t, tempo);
    if (dove.lavora >= 0) {   // the build beam from the nose to a point wandering over the site
      var b = dati.costruzioni[dove.lavora];
      var px = (b.x + b.w / 2) * CELLA + Math.sin(tempo * 3.1) * b.w * CELLA * 0.3;
      var py = (b.y + b.h / 2) * CELLA + Math.cos(tempo * 2.3) * b.h * CELLA * 0.3;
      var nx = cx + co * 10 + lx * (n - 1), ny = cy - (n - 1) + si * 10 + ly * (n - 1);
      ctx.strokeStyle = "rgba(255,211,127,0.25)"; ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(nx, ny); ctx.lineTo(px, py); ctx.stroke();
      ctx.strokeStyle = AMBRA; ctx.lineWidth = 1;
      ctx.beginPath(); ctx.moveTo(nx, ny); ctx.lineTo(px, py); ctx.stroke();
      ctx.fillStyle = AMBRA; ctx.fillRect(px - 1.5, py - 1.5, 3, 3);
    }
  }

  /* The fright of the attack: a «!» popping over the drone when it sees the silhouettes, then drops of sweat
     sliding off its hull while it trembles. */
  function disegnaPaura(t, tempo, x, y, paura) {
    var A = dati.attacco;
    var u = avanzamento(t, A.vede, A.vede + 0.012);
    if (u > 0 && u < 1) {
      var k = 1.70158, v = Math.min(1, u * 4) - 1, pop = 1 + (k + 1) * v * v * v + k * v * v;   // pops with an overshoot
      ctx.save();
      ctx.globalAlpha = 1 - Math.max(0, (u - 0.75) / 0.25);
      ctx.translate(x + 8, y - 26);
      ctx.scale(pop * 1.5, pop * 1.5);
      ctx.fillStyle = "#2b2b2b"; ctx.fillRect(-5, -9, 10, 18);
      ctx.fillStyle = "#ffffff"; ctx.fillRect(-4, -8, 8, 16);
      ctx.fillStyle = "#ff2a4a"; ctx.fillRect(-1.5, -6, 3, 8); ctx.fillRect(-1.5, 4, 3, 3);
      ctx.restore();
    }
    for (var g = 0; g < 2; g++) {   // sweat: each drop slides off the hull and fades, one after the other
      var f = (tempo * 0.9 + g * 0.5) % 1;
      var gx = x + (g ? -9 : 8), gy = y - 6 + f * 12;
      ctx.globalAlpha = paura * (1 - f);
      ctx.fillStyle = "#8fd3ff";
      ctx.beginPath(); ctx.moveTo(gx, gy - 3); ctx.lineTo(gx + 1.6, gy + 0.5); ctx.arc(gx, gy + 0.6, 1.6, 0, Math.PI); ctx.fill();
    }
    ctx.globalAlpha = 1;
  }

  /* The remote: the drone takes it out after the bonus, a click (red light at its tip) sends the infrared dots to
     the sensor on the screen's frame. */
  function disegnaTelecomando(t, tempo, x, y) {
    var M = dati.mercato, C = M && M.canale;
    if (!C || t < M.bonus[0]) return;
    var su = avanzamento(t, M.bonus[0], M.bonus[0] + 0.006);
    ctx.save();
    ctx.globalAlpha = su;
    ctx.translate(x, y + 3);
    ctx.rotate(-0.5);
    ctx.scale(1.5, 1.5);
    ctx.fillStyle = "#2b2b2b"; ctx.fillRect(-4, -2.5, 11, 5);
    ctx.fillStyle = "#3a3d46"; ctx.fillRect(-3, -1.5, 9, 3);
    ctx.fillStyle = "#8a8f9c"; ctx.fillRect(-1, -1, 1, 1); ctx.fillRect(1, -1, 1, 1); ctx.fillRect(-1, 0.5, 1, 1);
    var clic = avanzamento(t, C.tasto, C.cambio[0] + 0.004);
    ctx.fillStyle = clic > 0 && clic < 1 ? "#ff2a4a" : "#6a1a24"; ctx.fillRect(6, -1, 2, 2);
    ctx.restore();
    var punta = { x: x + Math.cos(-0.5) * 11, y: y + 3 + Math.sin(-0.5) * 11 };
    ctx.globalAlpha = 1;
    if (clic > 0 && clic < 1) {   // the infrared dots, from the tip to the sensor
      var sx = M.x * CELLA + C.sensore[0], sy = M.y * CELLA + C.sensore[1];
      ctx.globalAlpha = 1 - Math.max(0, clic - 0.7) / 0.3;
      ctx.strokeStyle = "#ff4a5a"; ctx.lineWidth = 1;   // the click: a ring opening at the tip
      ctx.beginPath(); ctx.arc(punta.x, punta.y, 2 + 8 * Math.min(1, clic * 3), 0, Math.PI * 2); ctx.stroke();
      ctx.fillStyle = "#ff4a5a";
      for (var k = 0; k < 5; k++) {
        var f = Math.min(1, clic * 1.6 - k * 0.12);
        if (f <= 0) continue;
        ctx.fillRect(punta.x + (sx - punta.x) * f - 1.5, punta.y + (sy - punta.y) * f - 1.5, 3, 3);
      }
      ctx.globalAlpha = 1;
    }
  }

  /* The cinema bars of the attack's close-up (screen space), in when the shot starts, out when the ice breaks,
     with a dark vignette that closes in on the middle of the shot. */
  function disegnaBarre(t) {
    var A = dati.attacco;
    if (!A) return;
    var b = A.barre, e = Math.min(liscia(avanzamento(t, b[0], b[1])), 1 - liscia(avanzamento(t, b[2], b[3])));
    if (e <= 0) return;
    var W = tela.width, H = tela.height;
    var v = ctx.createRadialGradient(W / 2, H * 0.45, Math.min(W, H) * 0.3, W / 2, H * 0.45, Math.max(W, H) * 0.62);
    v.addColorStop(0, "rgba(0,0,0,0)");
    v.addColorStop(1, "rgba(0,0,0," + (0.6 * e).toFixed(3) + ")");
    ctx.fillStyle = v;
    ctx.fillRect(0, 0, W, H);
    var h = Math.round(tela.height * (altezza > larghezza ? 0.06 : 0.1) * e);
    ctx.fillStyle = "#000";
    ctx.fillRect(0, 0, tela.width, h);
    ctx.fillRect(0, tela.height - h, tela.width, h);
  }

  // --- the market (the last chapter)
  var CIFRE = {   // 3x5 pixel digits for the screen and the briefcase display
    "0": "111101101101111", "1": "010110010010111", "2": "111001111100111", "3": "111001111001111",
    "4": "101101111001001", "5": "111100111001111", "6": "111100111101111", "7": "111001001010010",
    "8": "111101111101111", "9": "111101111001111", "$": "011110010011110", ",": "000000000010100",
    ".": "000000000000010", "-": "000000111000000", "%": "101001010100101", "+": "000010111010000",
    " ": "000000000000000"
  };
  function cifre(c2d, testo, x, y, k, colore) {
    c2d.fillStyle = colore;
    for (var i = 0; i < testo.length; i++) {
      var g = CIFRE[testo.charAt(i)] || CIFRE[" "];
      for (var j = 0; j < 15; j++) if (g.charAt(j) === "1") c2d.fillRect(x + (i * 4 + j % 3) * k, y + Math.floor(j / 3) * k, k, k);
    }
  }
  var lo = null;   // the screen's own low-resolution canvas: one pixel per art pixel, drawn scaled (pixel look)
  function disegnaMercato(t, tempo) {
    var M = dati.mercato;
    if (!M || t < M.sale[0] - 0.06) { posaTv(t, M); return; }
    var ix = M.interno[0], iy = M.interno[1], iw = M.interno[2], ih = M.interno[3];
    if (!lo) { lo = doc.createElement("canvas"); lo.width = iw; lo.height = ih; }
    var g = lo.getContext("2d");
    g.imageSmoothingEnabled = false;
    g.fillStyle = "#05070a"; g.fillRect(0, 0, iw, ih);
    g.fillStyle = "#132029";
    for (var gy = 24; gy < ih; gy += 22) g.fillRect(0, gy, iw, 1);
    var tit = dati.sprite["mercato-titolo"];
    g.drawImage(img["mercato-titolo"], 0, 0, tit.w, tit.h, 4, 4, tit.w, tit.h);
    // the price line: the recent history of the price on a log scale. From the crash on, its head is a
    // rocket-bomb falling faster and faster, the line its trail, down to the bottom (the floor B).
    var crollato = t >= M.crollo[0];
    var tStoria = Math.min(t, M.crollo[0]);
    var x0 = M.caduta[0], x1 = M.caduta[1];
    var y0 = yPrezzo(M, M.prezzo.alto), y1 = yPrezzo(M, M.prezzo.basso);
    g.strokeStyle = crollato ? "#ff4a5a" : "#58d68d"; g.lineWidth = 1;
    g.beginPath();
    var nPunti = crollato ? 34 : 56;
    for (var i = 0; i <= nPunti; i++) {
      var tt = tStoria - (nPunti - i) * 0.0016;
      var v = prezzoMercato(M, tt) * (1 + 0.06 * Math.sin(i * 1.7 + tempo * 3) * Math.sin(i * 0.37) * (crollato && i > nPunti - 3 ? 0 : 1));
      var x = crollato ? 4 + i * ((x0 - 4) / nPunti) : 4 + i * ((iw - 8) / 56);
      if (i) g.lineTo(x, yPrezzo(M, v)); else g.moveTo(x, yPrezzo(M, v));
    }
    var caduta = avanzamento(t, M.crollo[0], M.crollo[1]);
    if (crollato) {   // the trail of the fall
      for (var s2 = 1; s2 <= 24; s2++) {
        var c = percorsoCaduta(x0, y0, x1, y1, caduta * s2 / 24);
        g.lineTo(c.x, c.y);
      }
    }
    g.stroke();
    if (crollato && t < M.boom[0]) {   // the rocket-bomb at the head of the line, nose first, flame behind
      var b = percorsoCaduta(x0, y0, x1, y1, caduta);
      g.save();
      g.translate(b.x, b.y);
      g.rotate(b.ang);
      var fiamma = 4 + (Math.floor(tempo * 24) % 3) * 2;
      g.fillStyle = "#ff8a1c"; g.fillRect(-11 - fiamma, -2, fiamma, 4);
      g.fillStyle = "#ffe58a"; g.fillRect(-11 - fiamma / 2, -1, fiamma / 2, 2);
      g.fillStyle = "#6a6e79"; g.fillRect(-11, -5, 4, 10);                  // tail fins
      g.fillStyle = "#3a3d46"; g.fillRect(-9, -3, 13, 6);                   // body
      g.fillStyle = "#8a8f9c"; g.fillRect(-8, -3, 11, 1);                   // glint
      g.fillStyle = "#ff4a5a"; g.fillRect(-3, -3, 2, 6);                    // red band
      g.fillStyle = "#2b2b2b"; g.fillRect(4, -2, 3, 4); g.fillRect(7, -1, 2, 2);   // nose
      g.restore();
    }
    var prezzo = prezzoMercato(M, t);
    var testo = "$" + testoPrezzo(prezzo);
    cifre(g, testo, iw - 6 - testo.length * 12, 18, 3, crollato ? "#ff4a5a" : "#58d68d");
    var quota = crollato ? (prezzo / M.prezzo.alto - 1) * 100 : (prezzo / M.prezzo.base - 1) * 100;
    if (t > M.sale[0]) {   // the change since the soaring started (green), or since the top (red)
      var q = crollato ? quota.toFixed(1).replace(".", ",") : "+" + Math.round(quota);
      cifre(g, q + "%", iw - 6 - (q.length + 1) * 8, 40, 2, crollato ? "#ff4a5a" : "#58d68d");
    }
    // the mushroom cloud, inside the screen
    var u = avanzamento(t, M.boom[0], M.boom[1]);
    if (u > 0 && u < 1) {
      var cx = M.caduta[1], suolo = Math.min(ih - 4, yPrezzo(M, M.prezzo.basso) + 4);   // where the bomb hit the bottom
      var su = Math.min(1, u / 0.45), cap = Math.min(1, Math.max(0, (u - 0.15) / 0.5)), fumo = Math.max(0, (u - 0.5) / 0.5);
      var fuoco = function (a) { return "rgba(" + Math.round(255 - 120 * fumo) + "," + Math.round(170 - 90 * fumo) + "," + Math.round(60 + 40 * fumo) + "," + a + ")"; };
      g.fillStyle = "rgba(90,70,60," + (0.6 * (1 - fumo * 0.5)) + ")";   // dust on the ground
      g.beginPath(); g.ellipse(cx, suolo, 20 + 70 * su, 6 + 6 * su, 0, 0, Math.PI * 2); g.fill();
      var cima = suolo - 18 - 70 * su;
      g.fillStyle = fuoco(0.95);   // the stem
      g.fillRect(cx - 5 - 3 * su, cima, 10 + 6 * su, suolo - cima);
      g.strokeStyle = fuoco(0.8); g.lineWidth = 2;   // the ring around the stem
      g.beginPath(); g.ellipse(cx, cima + (suolo - cima) * 0.5, 14 + 16 * cap, 4 + 3 * cap, 0, 0, Math.PI * 2); g.stroke();
      [[1, 0.95], [0.75, 1], [0.45, 1]].forEach(function (s, k) {   // the cap: dark rim, fire, hot core
        var r = (18 + 38 * cap) * s[0];
        g.fillStyle = k === 0 ? "rgba(" + Math.round(120 - 40 * fumo) + ",60,50," + s[1] + ")" : k === 1 ? fuoco(1) : "rgba(255,240,180," + (1 - fumo) + ")";
        g.beginPath(); g.ellipse(cx, cima - 4, r * 1.25, r * 0.7, 0, 0, Math.PI * 2); g.fill();
      });
      var lampo = Math.max(0, 1 - u / 0.12) * (riduceSistema() ? 0.35 : 1);   // one flash, softer if motion is reduced
      if (lampo > 0) { g.fillStyle = "rgba(255,245,215," + lampo + ")"; g.fillRect(0, 0, iw, ih); }
    }
    // the ending: the stonks parody fades in
    var mm = avanzamento(t, M.meme[0], M.meme[1]);
    if (mm > 0) {
      g.globalAlpha = mm;
      g.drawImage(img.stonks, 0, 0);
      g.globalAlpha = 1;
    }
    // the remote changes channel: the old picture folds into a line, static, then the game's channel rolls in
    var C = M.canale, ch = C ? avanzamento(t, C.cambio[0], C.cambio[1]) : 0, piega = 1;
    if (ch > 0) {
      if (ch < 0.3) {
        piega = Math.max(0.02, 1 - ch / 0.3);
      } else if (ch < 0.62) {
        if (riduceSistema()) { g.fillStyle = "#1c2230"; g.fillRect(0, 0, iw, ih); }
        else {   // static: grey pixels changing every frame
          var seme = Math.floor(tempo * 30) * 7919;
          for (var py = 0; py < ih; py += 2) {
            for (var px = 0; px < iw; px += 2) {
              seme = (seme * 1103515245 + 12345) & 0x7fffffff;
              var gr = 40 + (seme >> 16) % 180;
              g.fillStyle = "rgb(" + gr + "," + gr + "," + gr + ")";
              g.fillRect(px, py, 2, 2);
            }
          }
        }
      } else {
        var rullo = Math.round((1 - liscia(Math.min(1, (ch - 0.62) / 0.3))) * ih);   // the picture rolls into place
        g.drawImage(img.canale, 0, rullo);
        g.drawImage(img.canale, 0, rullo - ih);
        if (t < C.cambio[1] + 0.01) {   // the channel number, top left, for a moment
          var osd = dati.sprite["canale-osd"];
          g.drawImage(img["canale-osd"], 0, 0, osd.w, osd.h, 6, 4, osd.w, osd.h);
        }
      }
    }
    var scossa = u > 0 && u < 0.5 ? (1 - u * 2) * 3 : 0;   // the screen shakes with the blast
    var sx = Math.round(Math.sin(t * 9000) * scossa), sy = Math.round(Math.cos(t * 7000) * scossa);
    ctx.imageSmoothingEnabled = false;
    var alto = Math.max(1, Math.round(ih * piega));
    ctx.drawImage(lo, 0, 0, iw, ih, M.x * CELLA + ix + sx, M.y * CELLA + iy + sy + Math.round((ih - alto) / 2), iw, alto);
    if (piega < 1) {   // the old tube's bright line
      ctx.fillStyle = "rgba(235,245,255," + (1 - piega) * 0.8 + ")";
      ctx.fillRect(M.x * CELLA + ix, M.y * CELLA + iy + Math.round(ih / 2) - 1, iw, 2);
    }
    posaTv(t, M);
  }

  /* The real links (download, GitHub) over the lower part of the game's channel: HTML laid on the canvas, shown
     once the channel is on. */
  var tv = racconto.querySelector(".racconto-tv"), tvAcceso = null;
  function posaTv(t, M) {
    if (!tv) return;
    var acceso = !!(M && M.canale && t >= M.canale.cambio[1] - 0.002);
    if (acceso !== tvAcceso) { tv.classList.toggle("acceso", acceso); tvAcceso = acceso; }
    if (!acceso) return;
    var b = dati.sprite.canale.bottoni, m = ctx.getTransform();
    var x0 = M.x * CELLA + M.interno[0] + b[0], y0 = M.y * CELLA + M.interno[1] + b[1];
    var x1 = M.x * CELLA + M.interno[0] + b[2], y1 = M.y * CELLA + M.interno[1] + b[3];
    var s = tv.style;
    s.left = ((m.a * x0 + m.e) / dpr).toFixed(1) + "px";
    s.top = ((m.d * y0 + m.f) / dpr).toFixed(1) + "px";
    s.width = (m.a * (x1 - x0) / dpr).toFixed(1) + "px";
    s.height = (m.d * (y1 - y0) / dpr).toFixed(1) + "px";
    s.setProperty("--tv", limita(Math.round(m.a * (x1 - x0) / dpr / 30), 12, 20) + "px");   // buttons grow with the screen
  }

  /* After stonks: the money bonus pops out over the screen with a bounce, coins bursting out of it. */
  function disegnaBonus(t, tempo) {
    var M = dati.mercato, s = dati.sprite.bonus;
    if (!M || !M.bonus || !s || t < M.bonus[0]) return;
    var u = avanzamento(t, M.bonus[0], M.bonus[1]);
    // over the top of the screen, so the drone in a suit and «stonks» below stay in sight
    var cx = M.x * CELLA + M.interno[0] + M.interno[2] / 2 + 20, cy = M.y * CELLA + M.interno[1] + 30;
    var C = M.canale, via = C ? avanzamento(t, C.tasto, C.cambio[0] + 0.004) : 0;   // gone with the old channel
    if (via >= 1) return;
    var k = 1.70158, v = u - 1, pop = (1 + (k + 1) * v * v * v + k * v * v) * (1 - 0.6 * via);   // ease out, small overshoot
    for (var i = 0; i < 14; i++) {   // the coins: out in every direction, then falling
      var a = i / 14 * Math.PI * 2 + 0.3, vel = 60 + (i * 37) % 40, q = Math.min(1, u * 1.4);
      var x = cx + Math.cos(a) * vel * q, y = cy + Math.sin(a) * vel * q - 30 * q + 90 * q * q;
      ctx.globalAlpha = (1 - Math.max(0, (u - 0.7) / 0.3) * 0.6) * (1 - via);
      ctx.fillStyle = "#2b2b2b"; ctx.beginPath(); ctx.arc(x, y, 3.4, 0, Math.PI * 2); ctx.fill();
      ctx.fillStyle = (i + Math.floor(tempo * 8)) % 2 ? "#facc15" : "#ffe58a";
      ctx.beginPath(); ctx.ellipse(x, y, 2.6 * Math.abs(Math.cos(tempo * 6 + i)) + 0.6, 2.6, 0, 0, Math.PI * 2); ctx.fill();
    }
    ctx.globalAlpha = 1 - via;
    ctx.save();
    ctx.translate(cx, cy);
    ctx.scale(pop, pop);
    ctx.shadowColor = "rgba(250,204,21," + (0.5 + 0.3 * Math.sin(tempo * 4)) + ")";
    ctx.shadowBlur = 18 * dpr;
    fotogramma("bonus", 0, 0, 0, 0, 0, -s.w / 2, -s.h / 2);
    ctx.restore();
    ctx.globalAlpha = 1;
  }

  /* The hatches on top of the screen: they swing open for the briefcase and close after it. */
  function disegnaPorte(t) {
    var M = dati.mercato;
    if (!M) return;
    var apri = Math.min(avanzamento(t, M.porte[0], M.porte[0] + 0.006), 1 - avanzamento(t, M.porte[1] - 0.006, M.porte[1]));
    var bx = M.x * CELLA + M.bocca[0], by = M.y * CELLA + M.bocca[1], bw = M.bocca[2];
    [[bx, 1], [bx + bw, -1]].forEach(function (h) {
      ctx.save();
      ctx.translate(h[0], by + 1);
      ctx.rotate(-h[1] * apri * Math.PI * 0.6);
      ctx.fillStyle = "#6a6e79"; ctx.fillRect(h[1] > 0 ? 0 : -bw / 2, -1.5, bw / 2, 3);
      ctx.fillStyle = "#2b2b2b"; ctx.fillRect(h[1] > 0 ? 0 : -bw / 2, 1, bw / 2, 0.8);
      ctx.restore();
    });
  }

  /* The briefcase: under the drone while the count climbs (items fly in from the Core), then thrown in an arc
     into the slot of the screen, where it is sucked in. */
  function disegnaValigetta(t, tempo, cx, cy) {
    var V = dati.valigetta, M = dati.mercato, s = dati.sprite.valigetta;
    if (!V || t < V.appare || t >= V.dentro) return;
    var appesa = { x: cx, y: cy + 14 + Math.sin(tempo * 3) * 1.2 };
    var bocca = { x: M.x * CELLA + M.bocca[0] + M.bocca[2] / 2, y: M.y * CELLA + M.bocca[1] };
    var p = appesa, giro = 0, scala = Math.min(1, (t - V.appare) / 0.008);
    var u = avanzamento(t, V.lancio[0], V.lancio[1]);
    if (t >= V.lancio[0] && !dati.lancioDa) dati.lancioDa = appesa;   // where the throw starts
    if (t < V.lancio[0]) dati.lancioDa = null;
    if (u > 0) {
      var a = dati.lancioDa || appesa;
      p = { x: a.x + (bocca.x - a.x) * u, y: a.y + (bocca.y - 4 - a.y) * u - 70 * 4 * u * (1 - u) };
      giro = u * Math.PI * 3;
      if (u >= 1) {   // sucked in: spinning, shrinking, down into the slot
        var r = avanzamento(t, V.lancio[1], V.dentro);
        p = { x: bocca.x, y: bocca.y - 4 + 6 * r };
        giro = Math.PI * 3 + r * Math.PI * 6;
        scala = 1 - r;
      }
    } else if (t < V.conta[1]) {   // items flying from the Core into the open case
      var core = dati.fissi[0], o = dati.sprite["oggetto-iron-plate"];
      for (var k = 0; k < 5; k++) {
        var f = (tempo * 1.4 + k / 5) % 1, q0x = (core.x + 1.5) * CELLA, q0y = (core.y + 1.5) * CELLA;
        var qx = q0x + (appesa.x - q0x) * f, qy = q0y + (appesa.y - q0y) * f - 40 * 4 * f * (1 - f);
        if (t > V.conta[0]) fotogramma("oggetto-iron-plate", 0, 0, 0, 0, 0, qx - o.w / 2, qy - o.h / 2);
      }
    }
    if (scala <= 0) return;
    ctx.save();
    ctx.translate(p.x, p.y);
    ctx.rotate(giro);
    ctx.scale(scala, scala);
    fotogramma("valigetta", 0, 0, 0, 0, 0, -s.w / 2, -s.h / 2);
    var n = Math.round(V.quanti * liscia(avanzamento(t, V.conta[0], V.conta[1])));
    var testo = ("    " + n).slice(-4);
    cifre(ctx, testo, -s.w / 2 + s.lcd[0], -s.h / 2 + s.lcd[1], 1, "#7cfc8a");
    ctx.restore();
  }

  // --- the frame
  var ultimo = 0, tempoFermo = 0, inVista = true;
  function disegna(ora) {
    var dt = ultimo ? Math.min(0.1, (ora - ultimo) / 1000) : 0;
    ultimo = ora;
    if (dolce.attivo) {
      dolce.attuale = smorza(dolce.attuale, dolce.meta, dt, 9);
      if (Math.abs(dolce.attuale - dolce.meta) < 0.5) { dolce.attuale = dolce.meta; dolce.attivo = false; }
      // each step lands at once: the html{scroll-behavior:smooth} of stile.css would restart a native glide
      window.scrollTo({ top: dolce.attuale, behavior: "instant" });
    }
    if (!statico()) tempoFermo = ora / 1000;   // time stands still while the animations are paused
    var tempo = tempoFermo;
    misura();
    var t = progresso();
    if (!dati.cameraVerticale) {   // portrait screens may aim elsewhere (xv, yv, vv): the panels cover their bottom
      dati.cameraVerticale = dati.camera.map(function (k) {
        return { t: k.t, x: k.xv !== undefined ? k.xv : k.x, y: k.yv !== undefined ? k.yv : k.y,
                 vista: k.vv !== undefined ? k.vv : k.vista };
      });
    }
    var cam = interpola(altezza > larghezza ? dati.cameraVerticale : dati.camera, t);
    var z = zoomPer(larghezza, altezza, cam.vista, CELLA) * dpr;
    // never smaller than the map: a tall phone screen zooms in rather than showing bands past its edges
    z = Math.max(z, tela.width / (dati.larghezza * CELLA), tela.height / (dati.righe * CELLA));
    var fy = fuoco(larghezza, altezza);
    var dentro = limitaCamera(cam.x, cam.y, tela.width, tela.height, z, CELLA, dati.larghezza, dati.righe, fy);
    var tx = Math.round(tela.width / 2 - dentro.x * CELLA * z);
    var ty = Math.round(tela.height * fy - dentro.y * CELLA * z);
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.fillStyle = "#0b0d10";
    ctx.fillRect(0, 0, tela.width, tela.height);
    ctx.setTransform(z, 0, 0, z, tx, ty);
    ctx.imageSmoothingEnabled = false;
    ctx.drawImage(img.terreno, 0, 0);
    (dati.fissi || []).forEach(function (f) {   // there from the start: the Core, the market screen
      fotogramma(f.sprite, fotogrammaAnimato(f.sprite, tempo), 0, 0, 0, 0, f.x * CELLA, f.y * CELLA, f.w * CELLA, f.h * CELLA);
    });
    disegnaMercato(t, tempo);
    dati.costruzioni.forEach(function (c) { disegnaCostruzione(c, t, tempo); });
    disegnaOggetti(t, tempo);
    disegnaLinee(t);
    disegnaDrone(t, tempo, dt);
    disegnaNotte(t);
    disegnaNemici(t, tempo);
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    disegnaAllarme(t, tempo);
    disegnaBarre(t);
    dopo.forEach(function (el) {   // panel lines waiting for a moment of the story (the ice breaking)
      var visto = t >= el.__dopo;
      if (visto !== el.__visto) { el.classList.toggle("visto", visto); el.__visto = visto; }
    });
  }
  var dopo = Array.prototype.slice.call(racconto.querySelectorAll("[data-dopo]"));
  dopo.forEach(function (el) { el.__dopo = parseFloat(el.getAttribute("data-dopo")); });

  window.tIndustryRacconto = { drone: function () { return drone.schermo || null; }, progresso: progresso };

  var richiesta = null;
  function ciclo(ora) {
    richiesta = null;
    disegna(ora);
    // keep running while something moves by itself; a paused page redraws only on scroll and resize
    if (inVista && !doc.hidden && (!statico() || dolce.attivo)) richiesta = window.requestAnimationFrame(ciclo);
  }
  function sveglia() { if (richiesta === null && !daCaricare) richiesta = window.requestAnimationFrame(ciclo); }

  function pronto() {
    racconto.classList.add("pronto");   // stile.css hides the still poster
    sveglia();
    window.addEventListener("scroll", sveglia, { passive: true });
    window.addEventListener("resize", sveglia);
    doc.addEventListener("visibilitychange", sveglia);
    if (typeof MutationObserver === "function") {   // the Animazioni button switches html.statico
      new MutationObserver(sveglia).observe(radice, { attributes: true, attributeFilter: ["class"] });
    }
    if (typeof IntersectionObserver === "function") {
      new IntersectionObserver(function (voci) { inVista = voci[0].isIntersecting; sveglia(); }).observe(racconto);
    }
  }
})();
