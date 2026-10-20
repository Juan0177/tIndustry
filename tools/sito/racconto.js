/*
 * tIndustry site: the scroll-driven story (tools/sito/racconto.js, copied next to the pages by costruisci.py).
 *
 * The page has a sticky full-screen <canvas> behind the chapter panels. The scroll progress through the story
 * (0..1) is the timeline: it places the camera (key frames, smoothstep between them) and decides how far every
 * construction has risen, so scrolling back takes the factory apart again (scrub). Wall-clock time only drives
 * what moves by itself: belt treads, items, the working miner, the drone's hover and its beam.
 *
 * The data (map, camera, constructions in chain order, item path, sprite list) comes from racconto.py as JSON in
 * <script id="racconto-dati">. Drawing follows the game: art scaled with nearest-neighbour, plans as the construct
 * shader at progress 0 (dark silhouette, amber outline), sites rising from the bottom with an amber scan line,
 * the drone (drone-1) in 2.5D as stacked slices over its 35 % shadow, enlarged x4 and drawn smaller with
 * filtering like DroneVisual, with the build beam to its site.
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

  /* Exponential approach of `attuale` to `meta` after dt seconds (frame-rate independent easing). */
  function smorza(attuale, meta, dt, k) {
    return meta + (attuale - meta) * Math.exp(-k * dt);
  }

  /* Where the drone hovers at t (cells) and which construction it works on (-1: flying or idle). It waits at
     `partenza` until `ingresso`, flies to each site before its t0 (eased), stays over it until t1. */
  function posizioneDrone(costruzioni, partenza, ingresso, t) {
    var tappe = [{ t: ingresso, x: partenza[0], y: partenza[1], lavora: -1 }];
    costruzioni.forEach(function (c, i) {
      var sopra = { x: c.x + c.w / 2 - 0.35, y: c.y + c.h / 2 - 0.9 };
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
      posizioneDrone: posizioneDrone, puntoSulPercorso: puntoSulPercorso, angoloVerso: angoloVerso, fuoco: fuoco };
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
    var x = c.x * CELLA, y = c.y * CELLA, w = c.w * CELLA, h = c.h * CELLA;
    if (t < c.piano) return;
    var p = avanzamento(t, c.t0, c.t1);
    var pieno = c.sprite === "minatore" ? (p >= 1 ? "minatore" : "minatore-fermo") : c.sprite;
    var k = p >= 1 ? fotogrammaAnimato(pieno, tempo) : 0;
    if (p < 1) girato(c.sprite === "minatore" ? "minatore-piano" : c.sprite + "-piano", 0, x, y, w, h, c.rot, c.specchio);
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

  function disegnaOggetti(t, tempo) {
    var minatore = dati.costruzioni[0];
    if (avanzamento(t, minatore.t0, minatore.t1) < 1) return;
    var finiti = 0;   // belt cells built, in chain order: items run only where the belt is finished
    for (var i = 1; i < dati.costruzioni.length; i++) {
      if (avanzamento(t, dati.costruzioni[i].t0, dati.costruzioni[i].t1) >= 1) finiti++; else break;
    }
    if (!finiti) return;
    var s = dati.sprite.oggetto, fase = (tempo * dati.velocita) % 1;
    var limite = finiti === dati.costruzioni.length - 1 ? finiti + 0.5 : finiti;   // the last half cell enters the plan
    for (var k = 0; k < limite + 1; k++) {
      var d = k + fase;
      if (d > limite) break;
      var q = puntoSulPercorso(dati.percorso, d);
      fotogramma("oggetto", 0, 0, 0, 0, 0, q.x * CELLA - s.w / 2, q.y * CELLA - s.h / 2);
    }
  }

  var drone = { x: dati.drone.partenza[0], y: dati.drone.partenza[1], ang: Math.PI * 0.75 };
  function disegnaDrone(t, tempo, dt) {
    var dove = posizioneDrone(dati.costruzioni, dati.drone.partenza, dati.drone.ingresso, t);
    var vx = dove.x - drone.x, vy = dove.y - drone.y;
    var meta = drone.ang;
    if (dove.lavora >= 0) {
      var c = dati.costruzioni[dove.lavora];
      meta = Math.atan2(c.y + c.h / 2 - dove.y, c.x + c.w / 2 - dove.x);
    } else if (Math.abs(vx) + Math.abs(vy) > 0.004) {
      meta = Math.atan2(vy, vx);
    }
    drone.ang = angoloVerso(drone.ang, meta, 1 - Math.exp(-dt * 8));
    drone.x = dove.x; drone.y = dove.y;
    var s = dati.sprite.drone, lato = s.lato;
    var bob = Math.sin(tempo * 2.6) * 1.6;   // DroneVisual: ±0.05 tile
    var cx = dove.x * CELLA, cy = dove.y * CELLA - bob;
    ctx.imageSmoothingEnabled = true;   // enlarged x4 art drawn smaller with filtering: crisp at any heading
    ctx.save();
    ctx.translate(dove.x * CELLA + 4, dove.y * CELLA + 6);
    ctx.rotate(drone.ang);
    fotogramma("drone-ombra", 0, 0, 0, 0, 0, -lato / 2, -lato / 2, lato, lato);
    ctx.restore();
    for (var k = 0; k < s.fotogrammi; k++) {
      ctx.save();
      ctx.translate(cx, cy - k);
      ctx.rotate(drone.ang);
      fotogramma("drone", k, 0, 0, 0, 0, -lato / 2, -lato / 2, lato, lato);
      ctx.restore();
    }
    ctx.imageSmoothingEnabled = false;
    var top = cy - (s.fotogrammi - 1);
    // engine glow at the tail, pulsing
    var coda = 12, r = 2.6 + 0.6 * Math.sin(tempo * 9);
    ctx.fillStyle = "rgba(255,179,71,0.85)";
    ctx.beginPath();
    ctx.arc(cx - Math.cos(drone.ang) * coda, top - Math.sin(drone.ang) * coda, r, 0, Math.PI * 2);
    ctx.fill();
    if (dove.lavora >= 0) {   // the build beam to a point wandering over the site
      var b = dati.costruzioni[dove.lavora];
      var px = (b.x + b.w / 2) * CELLA + Math.sin(tempo * 3.1) * b.w * CELLA * 0.3;
      var py = (b.y + b.h / 2) * CELLA + Math.cos(tempo * 2.3) * b.h * CELLA * 0.3;
      var nx = cx + Math.cos(drone.ang) * 10, ny = top + Math.sin(drone.ang) * 10;
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
    var cam = interpola(dati.camera, t);
    var z = zoomPer(larghezza, altezza, cam.vista, CELLA) * dpr;
    var tx = Math.round(tela.width / 2 - cam.x * CELLA * z);
    var ty = Math.round(tela.height * fuoco(larghezza, altezza) - cam.y * CELLA * z);
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.fillStyle = "#0b0d10";
    ctx.fillRect(0, 0, tela.width, tela.height);
    ctx.setTransform(z, 0, 0, z, tx, ty);
    ctx.imageSmoothingEnabled = false;
    ctx.drawImage(img.terreno, 0, 0);
    var piano = dati.piano;
    if (t >= piano.t) {
      ctx.globalAlpha = Math.min(1, (t - piano.t) / 0.04) * (0.75 + 0.25 * Math.sin(tempo * 3));
      fotogramma("forno-piano", 0, 0, 0, 0, 0, piano.x * CELLA, piano.y * CELLA, piano.w * CELLA, piano.h * CELLA);
      ctx.globalAlpha = 1;
    }
    dati.costruzioni.forEach(function (c) { disegnaCostruzione(c, t, tempo); });
    disegnaOggetti(t, tempo);
    disegnaDrone(t, tempo, dt);
  }

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
