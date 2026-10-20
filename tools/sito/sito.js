/*
 * tIndustry site script (tools/sito/sito.js, copied next to the pages by costruisci.py).
 *
 * Three jobs, each one skipped when its elements are missing (the sviluppo/ and asset/ pages have
 * neither scenes nor drone):
 *   1. adatta()  - scales every .scena (a 1152 px factory floor) with CSS zoom to fit its .sez and
 *                  writes the factor into --zoom on the .sez;
 *   2. scorri()  - writes the scroll progress 0..1 into the CSS variable --p of the pinned wrappers,
 *                  the sections and the revealed elements, and fills #avanza;
 *   3. drone     - the construction drone: it wanders over the factory and, on a plain click on a
 *                  link with data-costruisci, flies to the button, builds it with its beam for half
 *                  a second and then opens it;
 *   4. visore    - a click on a capture opens it in the #visore dialog over the page (close button,
 *                  Esc, click outside; arrows and buttons go through the captures); without the
 *                  script the link opens the PNG.
 * Motion policy (politica): everything runs by default, because the moving factory is the content;
 * the #animazioni button of the bar pauses it all (html.statico, remembered in localStorage) and
 * starts it again (html.animato); prefers-reduced-motion alone keeps the belts running but keeps
 * the drone on the ground and lets stile.css drop the flashes and the shake of «Sotto attacco».
 * Vanilla, no dependencies, nothing loaded from the network. The pure functions (intercetta,
 * avanzamento, bersaglio, politica) are exported for `node --test`: the file can be required
 * without a DOM.
 */
"use strict";

(function () {
  var LARGHEZZA_SCENA = 1152;      // the factory floor: 18 cells of 64 px
  var ALTEZZA_BARRA = 52;          // fallback when the page has no .barra
  var VELOCITA_DRONE = 384;        // px/s: 6 cells per second, as in the game
  var DURATA_CANTIERE = 500;       // ms the button spends under construction
  var MISURA_DRONE = 48;           // sprite drone-1 (24 px) shown at 2x
  var PAUSA_VAGA = [3000, 6000];   // ms between two wandering targets
  var CHIAVE_ANIMAZIONI = "tindustry-animazioni";   // "no": everything still; "si": everything on, whatever the system says
  var RIDUCI = "(prefers-reduced-motion: reduce)";
  var NS_SVG = "http://www.w3.org/2000/svg";

  // ------------------------------------------------------------------ pure functions (also for node)

  function limita(v) { return v < 0 ? 0 : v > 1 ? 1 : v; }

  function limitaTra(v, minimo, massimo) {
    if (massimo < minimo) return (minimo + massimo) / 2;
    return v < minimo ? minimo : v > massimo ? massimo : v;
  }

  function haCostruisci(link) {
    if (!link) return false;
    if (typeof link.hasAttribute === "function") return link.hasAttribute("data-costruisci");
    return !!(link.dataset && Object.prototype.hasOwnProperty.call(link.dataset, "costruisci"));
  }

  /* True when a click on `link` must be played by the drone instead of the browser: a plain primary
     click (a keyboard Enter arrives as a click without modifiers) on a link with data-costruisci,
     not already handled, and no request to reduce motion. `matchMedia` is optional (tests). */
  function intercetta(evento, link, matchMedia) {
    if (!evento || !link || evento.defaultPrevented) return false;
    if (evento.button !== undefined && evento.button !== null && evento.button !== 0) return false;
    if (evento.ctrlKey || evento.metaKey || evento.shiftKey || evento.altKey) return false;
    if (!haCostruisci(link)) return false;
    var media = matchMedia || (typeof window !== "undefined" && typeof window.matchMedia === "function"
      ? function (q) { return window.matchMedia(q); }
      : null);
    if (media && media(RIDUCI).matches) return false;
    return true;
  }

  /* The motion policy from the stored choice and the system request: "statico" (everything still)
     when the visitor paused the animations, "pieno" when they started them again or nothing asks to
     reduce motion, "ridotto" (belts on, no drone, no flashes) when only the system asks for less. */
  function politica(preferenza, riduceSistema) {
    if (preferenza === "no") return "statico";
    if (preferenza === "si") return "pieno";
    return riduceSistema ? "ridotto" : "pieno";
  }

  /* Index of the capture `passo` places away from `i` among `n`, wrapping around at both ends. */
  function vicino(i, passo, n) {
    if (!(n > 0)) return 0;
    return ((i + passo) % n + n) % n;
  }

  /* Scroll progress of the page in percent: 0 at the top, 100 when the bottom of the document is
     reached, 0 when the page does not scroll at all. */
  function avanzamento(scrollY, altezzaDocumento, vh) {
    var corsa = altezzaDocumento - vh;
    if (!(corsa > 0) || !(scrollY > 0)) return 0;
    return Math.min(100, 100 * scrollY / corsa);
  }

  /* Next wandering target (viewport px, centre of the drone) given the rectangles of the factory
     pieces: a point just above a visible piece, preferably outside the central half of the width
     where the text is read, or a point along the side margins when nothing is visible. `caso` is
     the random source (0..1), `margine` the top band to keep free (bar + half drone). */
  function bersaglio(rettangoli, vw, vh, caso, margine) {
    var alto = margine || ALTEZZA_BARRA + MISURA_DRONE / 2;
    var bordo = MISURA_DRONE / 2;
    var visibili = rettangoli.filter(function (r) {
      return r.width > 0 && r.bottom > alto && r.top < vh && r.right > 0 && r.left < vw;
    });
    var laterali = visibili.filter(function (r) {
      var cx = r.left + r.width / 2;
      return cx < vw * 0.25 || cx > vw * 0.75;
    });
    var lista = laterali.length ? laterali : visibili;
    var x, y;
    if (lista.length) {
      var r = lista[Math.min(lista.length - 1, Math.floor(caso() * lista.length))];
      x = r.left + r.width / 2 + (caso() - 0.5) * MISURA_DRONE * 1.5;
      y = r.top - MISURA_DRONE / 3 - caso() * MISURA_DRONE * 0.75;
    } else {
      var sinistra = caso() < 0.5;
      var banda = Math.max(bordo, vw * 0.15);
      x = sinistra ? bordo + caso() * banda : vw - bordo - caso() * banda;
      y = alto + caso() * Math.max(0, vh - alto - bordo);
    }
    return { x: limitaTra(x, bordo, vw - bordo), y: limitaTra(y, alto, vh - bordo) };
  }

  if (typeof module !== "undefined" && module.exports) {
    module.exports = { intercetta: intercetta, avanzamento: avanzamento, bersaglio: bersaglio, limita: limita, politica: politica, vicino: vicino };
  }
  if (typeof document === "undefined" || typeof window === "undefined") return;

  var doc = document;
  doc.documentElement.classList.add("js");   // stile.css: layouts that only make sense with the script
  function tutti(selettore, radice) { return Array.prototype.slice.call((radice || doc).querySelectorAll(selettore)); }

  // ------------------------------------------------------------------ 0. the motion policy

  var media = typeof window.matchMedia === "function" ? window.matchMedia(RIDUCI) : null;
  function preferenza() {
    try { return window.localStorage.getItem(CHIAVE_ANIMAZIONI); } catch (e) { return null; }
  }
  function ricorda(valore) {
    try { window.localStorage.setItem(CHIAVE_ANIMAZIONI, valore); } catch (e) { /* no storage: the choice lasts for this page */ }
  }
  function politicaAttuale() { return politica(preferenza(), !!(media && media.matches)); }
  function statico() { return doc.documentElement.classList.contains("statico"); }
  function applicaPolitica() {   // stile.css reads the two classes; applied before the first layout
    var stato = politicaAttuale();
    doc.documentElement.classList.toggle("statico", stato === "statico");
    doc.documentElement.classList.toggle("animato", stato === "pieno");
    tutti(".animazioni").forEach(function (bottone) {
      var testo = bottone.getAttribute(stato === "statico" ? "data-avvia" : "data-ferma");
      if (testo) { bottone.setAttribute("aria-label", testo); bottone.setAttribute("title", testo); }
    });
  }
  applicaPolitica();

  // ------------------------------------------------------------------ 1. adatta: zoom of the scenes

  // Desktop layouts (content laid over the floor) zoom the whole .pavimento, so text and scene shrink
  // together; stacked layouts (stile.css, max-width 863px) zoom only the .scena and keep the text as is.
  var IMPILATO = "(max-width: 863px)";
  function adatta() {
    var impilato = typeof window.matchMedia === "function" && window.matchMedia(IMPILATO).matches;
    tutti(".scena").forEach(function (scena) {
      var sez = (scena.closest && scena.closest(".sez")) || scena.parentElement;
      var pavimento = scena.parentElement && scena.parentElement.classList.contains("pavimento") ? scena.parentElement : null;
      if (!sez) return;
      var stile = window.getComputedStyle(sez);
      var disponibile = sez.clientWidth - (parseFloat(stile.paddingLeft) || 0) - (parseFloat(stile.paddingRight) || 0);
      var zoom = disponibile > 0 ? Math.min(1, disponibile / LARGHEZZA_SCENA) : 1;
      var valore = zoom < 1 ? zoom.toFixed(4) : "";
      var bersaglio = impilato || !pavimento ? scena : pavimento;
      var altro = bersaglio === scena ? pavimento : scena;
      if (bersaglio.style.zoom !== valore) bersaglio.style.zoom = valore;
      if (altro && altro.style.zoom) altro.style.zoom = "";
      sez.style.setProperty("--zoom", valore || "1");
    });
  }

  // ------------------------------------------------------------------ 2. scorri: --p and the progress bar

  var avanza = doc.getElementById("avanza");
  var pins = [], rivela = [];

  function raccogli() {
    pins = tutti(".pin");
    rivela = tutti(".sez").filter(function (el) { return !(el.closest && el.closest(".pin")); })
      .concat(tutti("[data-rivela]"));
  }

  function altezzaBarra() {
    var barra = doc.querySelector(".barra");
    return barra ? barra.getBoundingClientRect().height : ALTEZZA_BARRA;
  }

  function scriviP(el, v) {
    var testo = v.toFixed(3);
    if (el.__p === testo) return;   // one style write per real change
    el.__p = testo;
    el.style.setProperty("--p", testo);
  }

  function scorri() {
    var vh = window.innerHeight || doc.documentElement.clientHeight || 1;
    var hb = altezzaBarra();
    // All the reads first, then all the writes: one layout per frame instead of one per element.
    var letture = [];
    pins.forEach(function (pin) {
      var r = pin.getBoundingClientRect();
      var primo = pin.firstElementChild;
      var corsa = r.height - (primo ? primo.getBoundingClientRect().height : 0);
      var v = corsa > 0 ? limita((hb - r.top) / corsa) : (r.top <= hb ? 1 : 0);
      letture.push([pin, v]);
      if (primo) letture.push([primo, v]);   // the pinned section follows its wrapper
    });
    rivela.forEach(function (el) {
      letture.push([el, limita((vh - el.getBoundingClientRect().top) / (vh * 0.55))]);
    });
    letture.forEach(function (l) { scriviP(l[0], l[1]); });
    if (avanza) {
      var y = window.pageYOffset || doc.documentElement.scrollTop || 0;
      avanza.style.width = avanzamento(y, doc.documentElement.scrollHeight, vh).toFixed(2) + "%";
    }
  }

  var attesa = null, serveAdatta = false;
  function pianifica(conAdatta) {   // at most one computation per frame
    if (conAdatta) serveAdatta = true;
    if (attesa !== null) return;
    attesa = window.requestAnimationFrame(function () {
      attesa = null;
      if (serveAdatta) { serveAdatta = false; adatta(); }
      scorri();
    });
  }

  // ------------------------------------------------------------------ 3. the drone

  var drone = doc.getElementById("drone-cantiere");
  function riduci() { return politicaAttuale() !== "pieno"; }   // the drone flies only with the full policy

  // Seeded random source (mulberry32): the drone wanders the same way at every visit.
  var seme = 0x74494e44;
  function caso() {
    seme = (seme + 0x6D2B79F5) | 0;
    var t = seme;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  }

  var attivo = false;             // shown and wandering
  var pos = { x: 0, y: 0 };       // last commanded centre, viewport px
  var rot = 0;                    // heading in degrees, accumulated so the drone turns the short way
  var timerVaga = null, timerCantiere = null;
  var cantiere = null;            // { link } while a button is under construction

  function centroAttuale() {
    var r = drone.getBoundingClientRect();   // mid-flight this is the interpolated position
    return r.width ? { x: r.left + r.width / 2, y: r.top + r.height / 2 } : pos;
  }

  function trasla(p) {
    return "translate(" + (p.x - MISURA_DRONE / 2).toFixed(1) + "px," + (p.y - MISURA_DRONE / 2).toFixed(1) + "px)";
  }

  function orienta(dx, dy) {   // nose (East in the sprite) towards (dx, dy)
    if (Math.abs(dx) + Math.abs(dy) < 1) return;
    var voluto = Math.atan2(dy, dx) * 180 / Math.PI;
    var delta = ((voluto - rot) % 360 + 540) % 360 - 180;
    rot += delta;
    drone.style.setProperty("--rot", rot.toFixed(1) + "deg");
  }

  function posa(p) {   // place instantly
    drone.style.transition = "none";
    drone.style.transform = trasla(p);
    pos = p;
  }

  function volaVerso(meta) {   // linear flight at VELOCITA_DRONE; returns its duration in ms
    var da = centroAttuale();
    var dx = meta.x - da.x, dy = meta.y - da.y;
    var durata = Math.sqrt(dx * dx + dy * dy) / VELOCITA_DRONE;
    orienta(dx, dy);
    drone.style.transition = "none";
    drone.style.transform = trasla(da);
    void drone.offsetWidth;   // flush: the new transition starts from the current point
    drone.style.transition = "transform " + durata.toFixed(3) + "s linear";
    drone.style.transform = trasla(meta);
    pos = meta;
    return Math.round(durata * 1000);
  }

  function fermaVaga() { if (timerVaga !== null) { clearTimeout(timerVaga); timerVaga = null; } }
  function pianificaVaga(ms) { fermaVaga(); timerVaga = setTimeout(vaga, ms); }

  function vaga() {
    timerVaga = null;
    if (!attivo || cantiere || doc.hidden) return;
    var rettangoli = tutti(".scena .px").map(function (el) { return el.getBoundingClientRect(); });
    var meta = bersaglio(rettangoli, window.innerWidth, window.innerHeight, caso, altezzaBarra() + MISURA_DRONE / 2);
    var volo = volaVerso(meta);
    pianificaVaga(Math.max(volo + 800, PAUSA_VAGA[0] + caso() * (PAUSA_VAGA[1] - PAUSA_VAGA[0])));
  }

  function elementoRaggio(nome) {
    var raggio = drone.querySelector("svg.raggio");
    if (!raggio) {
      raggio = doc.createElementNS(NS_SVG, "svg");
      raggio.setAttribute("class", "raggio");
      drone.appendChild(raggio);
    }
    var el = raggio.querySelector(nome);
    if (!el) { el = doc.createElementNS(NS_SVG, nome); raggio.appendChild(el); }
    return el;
  }

  function accendiRaggio(centro) {   // from the nose of the drone to the centre of the button
    var dx = centro.x - pos.x, dy = centro.y - pos.y;
    orienta(dx, dy);
    var a = rot * Math.PI / 180, muso = MISURA_DRONE * 0.45;
    var linea = elementoRaggio("line"), punto = elementoRaggio("circle");
    linea.setAttribute("x1", (Math.cos(a) * muso).toFixed(1));
    linea.setAttribute("y1", (Math.sin(a) * muso).toFixed(1));
    linea.setAttribute("x2", dx.toFixed(1));
    linea.setAttribute("y2", dy.toFixed(1));
    punto.setAttribute("cx", dx.toFixed(1));
    punto.setAttribute("cy", dy.toFixed(1));
    punto.setAttribute("r", "4");
    drone.classList.add("costruisce");
  }

  function annullaCantiere() {
    if (timerCantiere !== null) { clearTimeout(timerCantiere); timerCantiere = null; }
    if (cantiere) { cantiere.link.classList.remove("cantiere"); cantiere = null; }
    drone.classList.remove("costruisce");
  }

  function metaSopraIlBottone(r) {   // above the button, to the left of its corner, inside the viewport
    var hb = altezzaBarra(), mezzo = MISURA_DRONE / 2;
    return {
      x: limitaTra(r.left - MISURA_DRONE * 0.3, mezzo, window.innerWidth - mezzo),
      y: limitaTra(r.top - MISURA_DRONE * 0.6, hb + mezzo, window.innerHeight - mezzo)
    };
  }

  function costruisci(link) {
    annullaCantiere();
    fermaVaga();
    var lavoro = { link: link };
    cantiere = lavoro;
    var volo = volaVerso(metaSopraIlBottone(link.getBoundingClientRect()));
    timerCantiere = setTimeout(function () {
      if (cantiere !== lavoro) return;
      var r = link.getBoundingClientRect();   // fresh: the page may have scrolled during the flight
      accendiRaggio({ x: r.left + r.width / 2, y: r.top + r.height / 2 });
      link.classList.add("cantiere");
      timerCantiere = setTimeout(function () { if (cantiere === lavoro) vai(link); }, DURATA_CANTIERE);
    }, volo + 80);
  }

  function vai(link) {
    var href = link.href, nuovaScheda = link.target === "_blank";
    annullaCantiere();
    if (nuovaScheda) {
      var finestra = window.open(href, "_blank");
      if (finestra) { try { finestra.opener = null; } catch (e) { /* cross-origin */ } }
      else window.location.href = href;   // popup blocked: open it here
    } else {
      window.location.href = href;
    }
    pianificaVaga(1500);   // same-page anchors keep the page: back to wandering
  }

  function impilato() { return typeof window.matchMedia === "function" && window.matchMedia(IMPILATO).matches; }

  function accendi() {
    if (attivo || riduci() || impilato()) return;
    attivo = true;
    drone.removeAttribute("hidden");
    posa({ x: window.innerWidth - MISURA_DRONE, y: altezzaBarra() + MISURA_DRONE });
    pianificaVaga(400);
  }

  function spegni() {
    attivo = false;
    annullaCantiere();
    fermaVaga();
    drone.setAttribute("hidden", "");
  }

  var secondoPreferenza = function () {};   // drone on or off after the policy or the layout changed

  function avviaDrone() {
    if (!drone) return;
    secondoPreferenza = function () {
      if (riduci() || impilato()) spegni(); else accendi();
    };
    secondoPreferenza();
    window.addEventListener("resize", function () {
      if (impilato() !== !attivo && !cantiere) secondoPreferenza();   // layout changed: drone on or off
      if (attivo && (pos.x > window.innerWidth || pos.y > window.innerHeight)) {
        posa({ x: window.innerWidth - MISURA_DRONE, y: altezzaBarra() + MISURA_DRONE });
      }
    });
    doc.addEventListener("visibilitychange", function () {
      if (doc.hidden) fermaVaga();
      else if (attivo && !cantiere) pianificaVaga(600);
    });
    window.addEventListener("pageshow", function (ev) {   // back from the bfcache: no stale site
      if (ev.persisted && attivo) { annullaCantiere(); pianificaVaga(600); }
    });
    doc.addEventListener("click", function (ev) {
      var t = ev.target;
      var link = t && typeof t.closest === "function" ? t.closest("a[data-costruisci]") : null;
      if (!link || !attivo || !intercetta(ev, link, function () { return { matches: riduci() }; })) return;
      ev.preventDefault();
      if (cantiere && cantiere.link === link) { vai(link); return; }   // second click: go now
      costruisci(link);
    });
  }

  // ------------------------------------------------------------------ 4. visore: the captures over the page

  function avviaVisore() {
    var visore = doc.getElementById("visore");
    if (!visore || typeof visore.showModal !== "function") return;   // no <dialog>: the links open the PNGs
    var img = visore.querySelector("img"), didascalia = visore.querySelector(".visore-didascalia");
    var conta = visore.querySelector(".visore-conta"), attuale = 0;
    function catture() { return tutti(".crt > a"); }
    function mostra(i) {
      var elenco = catture();
      if (!elenco.length) return;
      attuale = vicino(i, 0, elenco.length);
      var link = elenco[attuale], cap = link.parentElement.querySelector("figcaption");
      var testo = cap ? cap.textContent.trim() : "";
      img.src = link.getAttribute("href");
      img.alt = testo;
      didascalia.textContent = testo;
      conta.textContent = (attuale + 1) + " / " + elenco.length;
    }
    doc.addEventListener("click", function (ev) {
      var t = ev.target;
      var link = t && typeof t.closest === "function" ? t.closest(".crt > a") : null;
      if (!link || ev.defaultPrevented || ev.button !== 0 || ev.ctrlKey || ev.metaKey || ev.shiftKey || ev.altKey) return;
      ev.preventDefault();
      mostra(catture().indexOf(link));
      visore.showModal();
    });
    visore.addEventListener("click", function (ev) {
      var t = ev.target;
      if (t === visore) { visore.close(); return; }   // the backdrop: a click outside the picture
      if (t.closest(".visore-chiudi")) visore.close();
      else if (t.closest(".visore-prec")) mostra(attuale - 1);
      else if (t.closest(".visore-succ")) mostra(attuale + 1);
    });
    visore.addEventListener("keydown", function (ev) {
      if (ev.key === "ArrowLeft") { ev.preventDefault(); mostra(attuale - 1); }
      else if (ev.key === "ArrowRight") { ev.preventDefault(); mostra(attuale + 1); }
    });
    visore.addEventListener("close", function () { img.removeAttribute("src"); });
  }

  // ------------------------------------------------------------------ start

  // The Animazioni button of the bar: pause everything, or start everything (even when the system
  // asks for reduced motion: the visitor asked). The choice is kept for the next visits.
  function avviaBottoni() {
    tutti(".animazioni").forEach(function (bottone) {
      bottone.removeAttribute("hidden");
      bottone.addEventListener("click", function () {
        ricorda(statico() ? "si" : "no");
        applicaPolitica();
        secondoPreferenza();
        pianifica(true);   // the pinned hero and the strip change layout
      });
    });
    if (media && typeof media.addEventListener === "function") {
      media.addEventListener("change", function () { applicaPolitica(); secondoPreferenza(); pianifica(true); });
    }
  }

  function avvia() {
    raccogli();
    adatta();
    scorri();
    window.addEventListener("scroll", function () { pianifica(false); }, { passive: true });
    window.addEventListener("resize", function () { pianifica(true); });
    window.addEventListener("load", function () { raccogli(); pianifica(true); });
    avviaDrone();
    avviaBottoni();
    avviaVisore();
  }

  window.tIndustrySito = { intercetta: intercetta, adatta: adatta, scorri: scorri, avanzamento: avanzamento, politica: politica };
  if (doc.readyState === "loading") doc.addEventListener("DOMContentLoaded", avvia);
  else avvia();
})();
