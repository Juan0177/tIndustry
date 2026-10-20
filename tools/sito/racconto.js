/*
 * tIndustry site: the scroll-driven story (tools/sito/racconto.js, copied next to the pages by costruisci.py).
 *
 * The page has a sticky full-screen <canvas> behind the chapter panels. The scroll progress through the story
 * (0..1) is the timeline: it places the camera (key frames, smoothstep between them) and decides how far every
 * construction has risen, so scrolling back takes the factory apart again (scrub). Wall-clock time only drives
 * what moves by itself: belt treads, items, working blocks, the drone's hover and its beam. The story runs
 * through the seven chapters: deposit and belts, smelter and plates, power, night and the attack (enemies only at
 * night: the survivors flee at dawn, the drone wears its helmet meanwhile), the world, the Core.
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

  /* Exponential approach of `attuale` to `meta` after dt seconds (frame-rate independent easing). */
  function smorza(attuale, meta, dt, k) {
    return meta + (attuale - meta) * Math.exp(-k * dt);
  }

  /* Where the drone hovers at t (cells) and which construction it works on (-1: flying or idle). It waits at
     `partenza` until `ingresso`, flies to each site in the last VOLO before its t0 (eased), stays over it until t1
     and waits there until it is time to leave for the next one. */
  var VOLO = 0.04;   // at most this much progress to fly to the next site: long pauses are spent waiting on guard
  function posizioneDrone(costruzioni, partenza, ingresso, t) {
    var tappe = [{ t: ingresso, x: partenza[0], y: partenza[1], lavora: -1 }];
    costruzioni.forEach(function (c, i) {
      var sopra = { x: c.x + c.w / 2 - 0.35, y: c.y + c.h / 2 - 0.9 };
      var prima = tappe[tappe.length - 1];
      if (c.t0 - VOLO > prima.t) tappe.push({ t: c.t0 - VOLO, x: prima.x, y: prima.y, lavora: -1 });
      tappe.push({ t: c.t0, x: sopra.x, y: sopra.y, lavora: -1 });
      tappe.push({ t: c.t1, x: sopra.x, y: sopra.y, lavora: i });
    });
    if (t <= tappe[0].t) return { x: tappe[0].x, y: tappe[0].y, lavora: -1 };
    for (var k = 0; k < tappe.length - 1; k++) {
      var a = tappe[k], b = tappe[k + 1];
      if (t < b.t) {
        var u = b.t > a.t ? (t - a.t) / (b.t - a.t) : 1;
        if (b.lavora >= 0) return { x: b.x, y: b.y, lavora: b.lavora };   // over its site, building
        u = liscia(u);
        return { x: a.x + (b.x - a.x) * u, y: a.y + (b.y - a.y) * u, lavora: -1 };
      }
    }
    var z = tappe[tappe.length - 1];
    return { x: z.x, y: z.y, lavora: -1 };
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
      limitaCamera: limitaCamera };
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
     built so far (in flow order): a finished chain carries them into its block, or out of the map. */
  function disegnaOggetti(t, tempo) {
    var fase = (tempo * dati.velocita) % 1;
    dati.flussi.forEach(function (f) {
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
      for (var d = fase; d <= limite; d += 1) {
        var q = puntoSulPercorso(f.percorso, d);
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
    var dove = posizioneDrone(dati.costruzioni, D.partenza, D.ingresso, t);
    // Flight direction from the timeline itself (a little before and after t), not from frame deltas: the same
    // pose at the same scroll position, however fast the visitor scrolls.
    var prima = posizioneDrone(dati.costruzioni, D.partenza, D.ingresso, t - 0.003);
    var dopo = posizioneDrone(dati.costruzioni, D.partenza, D.ingresso, t + 0.003);
    var mx = dopo.x - prima.x, my = dopo.y - prima.y, passo = Math.sqrt(mx * mx + my * my);
    var vola = dove.lavora < 0 && passo > 0.002;
    var meta = drone.ang;
    if (dove.lavora >= 0) {
      var c = dati.costruzioni[dove.lavora];
      meta = Math.atan2(c.y + c.h / 2 - dove.y, c.x + c.w / 2 - dove.x);
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
    var cx = dove.x * CELLA, cy = dove.y * CELLA - bob;
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
    (dati.fissi || []).forEach(function (f) {   // there from the start: the Core
      fotogramma(f.sprite, fotogrammaAnimato(f.sprite, tempo), 0, 0, 0, 0, f.x * CELLA, f.y * CELLA, f.w * CELLA, f.h * CELLA);
    });
    dati.costruzioni.forEach(function (c) { disegnaCostruzione(c, t, tempo); });
    disegnaOggetti(t, tempo);
    disegnaLinee(t);
    disegnaDrone(t, tempo, dt);
    disegnaNotte(t);
    disegnaNemici(t, tempo);
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    disegnaAllarme(t, tempo);
  }

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
