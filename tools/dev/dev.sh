#!/usr/bin/env bash
# All-in-one dev launcher: checks tools, builds, imports assets when needed, then starts the game.
# Run from anywhere: tools/dev/dev.sh [options]   (Windows: play.ps1 from the repo root)
set -u

usage() {
  cat <<'EOF'
Uso: tools/dev/dev.sh [opzioni]        (Windows: .\play.ps1 [opzioni])

Senza opzioni: controlla gli strumenti, compila, importa gli asset se serve e avvia il gioco.

  --editor          apre l'editor Godot invece del gioco
  --nuova           salta la Home e parte da una nuova partita (TINDUSTRY_FRESH=1)
  --profilo-temp    profilo dati temporaneo: impostazioni e salvataggi reali non vengono toccati
  --profilo DIR     usa DIR come profilo dati (persistente tra un avvio e l'altro)
  --test            esegue anche i test xUnit prima di avviare
  --playtest NOME   esegue il playtest NOME (ui | tutorial | agenti) invece di avviare il gioco
  --solo-build      compila (e importa se serve) senza avviare
  --import          forza la reimportazione degli asset Godot
  -h, --help        mostra questo aiuto

Variabili: GODOT=<comando Godot 4.4 mono> (default: godot4).
EOF
}

EDITOR=0 FRESH=0 TEMP_PROFILE=0 PROFILE="" RUN_TESTS=0 PLAYTEST="" BUILD_ONLY=0 FORCE_IMPORT=0
while [ $# -gt 0 ]; do
  case "$1" in
    --editor) EDITOR=1 ;;
    --nuova) FRESH=1 ;;
    --profilo-temp) TEMP_PROFILE=1 ;;
    --profilo) PROFILE="${2:?--profilo richiede una cartella}"; shift ;;
    --test) RUN_TESTS=1 ;;
    --playtest) PLAYTEST="${2:?--playtest richiede ui, tutorial o agenti}"; shift ;;
    --solo-build) BUILD_ONLY=1 ;;
    --import) FORCE_IMPORT=1 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Opzione sconosciuta: $1" >&2; usage >&2; exit 2 ;;
  esac
  shift
done
if [ -n "$PLAYTEST" ] && [ "$PLAYTEST" != ui ] && [ "$PLAYTEST" != tutorial ] && [ "$PLAYTEST" != agenti ]; then
  echo "--playtest accetta solo ui, tutorial o agenti (non '$PLAYTEST')" >&2; exit 2
fi
if [ "$TEMP_PROFILE" = 1 ] && [ -n "$PROFILE" ]; then
  echo "Usa --profilo-temp oppure --profilo DIR, non entrambi" >&2; exit 2
fi

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT" || exit 2
GODOT="${GODOT:-godot4}"
win() { if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else echo "$1"; fi; }
step() { printf '\n\033[1;33m==> %s\033[0m\n' "$*"; }
fail() { printf '\033[1;31m%s\033[0m\n' "$*" >&2; exit 1; }

step "Strumenti"
command -v dotnet >/dev/null 2>&1 || fail "dotnet non trovato: installa il .NET 8 SDK (https://dotnet.microsoft.com/download)."
dotnet --list-sdks | grep -q '^8\.' || fail ".NET 8 SDK mancante (trovati: $(dotnet --list-sdks | cut -d' ' -f1 | tr '\n' ' '))."
command -v "$GODOT" >/dev/null 2>&1 || fail "Godot non trovato come '$GODOT'. Installa Godot 4.4.1 .NET (mono) e mettilo nel PATH come godot4, oppure imposta GODOT=<percorso>."
GODOT_VERSION="$("$GODOT" --version 2>/dev/null | tail -1)"
case "$GODOT_VERSION" in
  4.4.*mono*) ;;
  *) fail "Serve Godot 4.4 .NET (mono): '$GODOT' è '$GODOT_VERSION'." ;;
esac
echo "dotnet $(dotnet --version) · Godot $GODOT_VERSION"

step "Compilazione (Shared, test, client Godot)"
dotnet build tIndustry.sln -nologo -v q "-clp:ErrorsOnly;NoSummary" || fail "Compilazione fallita."

if [ "$RUN_TESTS" = 1 ]; then
  step "Test"
  dotnet test tests/TIndustry.Shared.Tests --no-build -nologo -v q || fail "Test falliti."
fi

STAMP=godot/.godot/tindustry-import.stamp
# Prints the res:// paths listed in dest_files of every .import sidecar (outside .godot/) that are missing on disk.
import_missing() {
  find godot -path godot/.godot -prune -o -path godot/.mono -prune -o -name '*.import' -type f -print0 |
    xargs -0 -r sed -n 's/^dest_files=\[\(.*\)\]$/\1/p' | grep -o 'res://[^"]*' | sed 's|^res://||' |
    while IFS= read -r d; do [ -e "godot/$d" ] || echo "$d"; done
}
# True when the Godot import must run: forced, or the cache is absent, stale or incomplete.
import_needed() {
  [ "$FORCE_IMPORT" = 1 ] && return 0
  [ -d godot/.godot/imported ] || return 0
  [ -f "$STAMP" ] || return 0
  [ -n "$(import_missing | head -1)" ] && return 0
  # A source changed after the last import, or a source was never imported (no .import sidecar).
  [ -n "$(find godot -path godot/.godot -prune -o -path godot/.mono -prune -o -type f \( -name '*.import' -o -name '*.csv' \) -newer "$STAMP" -print -quit)" ] && return 0
  local f
  while IFS= read -r f; do
    [ -e "$f.import" ] || return 0
    [ "$f" -nt "$STAMP" ] && return 0
  done < <(find godot \( -path godot/.godot -o -path godot/.mono -o -path 'godot/*/bin' -o -path 'godot/*/obj' \) -prune -o -type f \( -name '*.png' -o -name '*.fnt' -o -name '*.csv' -o -name '*.wav' -o -name '*.ogg' -o -name '*.svg' \) -print)
  return 1
}

if import_needed; then
  step "Importazione asset Godot"
  # The importer runs the editor, which builds user:// from %APPDATA%: give it a throwaway one so the
  # player's real app_userdata (.recovery_mode_lock, caches) is never touched.
  IMPORT_APPDATA="$(mktemp -d)"
  APPDATA="$(win "$IMPORT_APPDATA")" "$GODOT" --headless --path godot --import >/dev/null 2>&1
  IMPORT_CODE=$?
  rm -rf "$IMPORT_APPDATA"
  # The headless import sometimes crashes at exit (139) after finishing: what counts is that every
  # output listed by the .import files exists.
  MISSING="$(import_missing)"
  if [ -n "$MISSING" ]; then
    fail "Importazione incompleta (codice $IMPORT_CODE): mancano $(echo "$MISSING" | wc -l | tr -d ' ') file importati, per esempio $(echo "$MISSING" | head -3 | tr '\n' ' '). Riprova con: $GODOT --headless --path godot --import"
  fi
  # Godot rewrites line endings of tracked .import files: drop changes that are only that.
  if command -v git >/dev/null 2>&1; then
    for f in $(git ls-files -m godot/assets 2>/dev/null); do
      git diff --ignore-cr-at-eol --quiet -- "$f" 2>/dev/null && git checkout -q -- "$f"
    done
  fi
  # Stamp last: the restore above touches .import files and must not look like a change.
  date -u +%Y-%m-%dT%H:%M:%SZ > "$STAMP"
fi

if [ "$BUILD_ONLY" = 1 ]; then
  step "Pronto (solo build)"
  exit 0
fi

if [ -n "$PLAYTEST" ]; then
  OUT="$ROOT/.dev/playtest-$PLAYTEST"
  step "Playtest $PLAYTEST (screenshot in $OUT)"
  GODOT="$GODOT" exec tools/dev/playtest.sh "$PLAYTEST" "$OUT"
fi

if [ "$TEMP_PROFILE" = 1 ]; then
  PROFILE="$(mktemp -d)"
  trap 'rm -rf "$PROFILE"' EXIT
fi
if [ -n "$PROFILE" ]; then
  mkdir -p "$PROFILE"
  export TINDUSTRY_DATA_DIR="$(win "$(cd "$PROFILE" && pwd)")"
  echo "Profilo dati: $TINDUSTRY_DATA_DIR"
else
  echo "Profilo dati: quello reale del giocatore (usa --profilo-temp per non toccarlo)"
fi
[ "$FRESH" = 1 ] && export TINDUSTRY_FRESH=1
# With a custom profile the engine log goes there too, never into the player's real folder.
LOG_ARGS=()
[ -n "$PROFILE" ] && LOG_ARGS=(--log-file "$TINDUSTRY_DATA_DIR/godot.log")

if [ "$EDITOR" = 1 ]; then
  step "Editor Godot"
  "$GODOT" --path godot "${LOG_ARGS[@]}" -e
else
  step "Avvio del gioco"
  # With a custom profile, user:// (shader_cache, vulkan, .recovery_mode_lock) goes there too: Godot builds it
  # from %APPDATA% on Windows. Not for the editor, which keeps its own settings under the real %APPDATA%.
  if [ -n "$PROFILE" ]; then
    APPDATA="$TINDUSTRY_DATA_DIR" "$GODOT" --path godot "${LOG_ARGS[@]}"
  else
    "$GODOT" --path godot "${LOG_ARGS[@]}"
  fi
fi
