"use strict";
// Script of the tIndustry website, copied as is into the site by tools/sito/costruisci.py.
// Without it every page still reads and every link works.

const LATO_OGGETTO = 20; // px: an item enters fully hidden and leaves fully hidden

// Belt segments: each item crosses its segment at the belt speed (--v-<tier>, px/s, written by the page),
// so the duration follows the measured length. The long side is the travel direction: the short one is
// always one cell, and on narrow screens horizontal segments turn vertical.
function durataTratti() {
  const radice = getComputedStyle(document.documentElement);
  for (const tratto of document.querySelectorAll(".tratto")) {
    const v = parseFloat(radice.getPropertyValue("--v-" + tratto.dataset.tier));
    if (!(v > 0)) continue;
    const lunghezza = Math.max(tratto.offsetWidth, tratto.offsetHeight) + LATO_OGGETTO;
    tratto.style.setProperty("--durata", (lunghezza / v).toFixed(3) + "s");
  }
}

let attesaResize = 0;
document.addEventListener("DOMContentLoaded", durataTratti);
window.addEventListener("resize", () => {
  clearTimeout(attesaResize);
  attesaResize = setTimeout(durataTratti, 150);
});
