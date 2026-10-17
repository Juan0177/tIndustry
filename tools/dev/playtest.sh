#!/usr/bin/env bash
# Usage: tools/dev/playtest.sh <ui|tutorial> <outdir>
# Runs a Godot playtest with an isolated temp data profile; screenshots + log.txt go to <outdir>.
# Exit 0 only if the log contains "PLAYTEST OK" and no "ERROR" lines (Godot's own exit code is
# ignored: a rare shutdown crash can happen after PLAYTEST OK).
set -u
NAME="${1:?usage: playtest.sh <ui|tutorial> <outdir>}"
OUT="${2:?usage: playtest.sh <ui|tutorial> <outdir>}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
mkdir -p "$OUT"
rm -f "$OUT"/*.png "$OUT/log.txt" "$OUT/godot.log"  # no stale shots from a previous run
DATA="$(mktemp -d)"
trap 'rm -rf "$DATA"' EXIT
win() { if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else echo "$1"; fi; }
LOG="$OUT/log.txt"
GODOT="${GODOT:-godot4}"
cd "$ROOT" || exit 2
# Godot puts user:// (shader_cache, vulkan, .recovery_mode_lock) under %APPDATA% on Windows: point it at the
# temp profile so the run leaves the player's real app_userdata untouched. .NET/LOCALAPPDATA paths are unaffected.
APPDATA="$(win "$DATA")" TINDUSTRY_DATA_DIR="$(win "$DATA")" TINDUSTRY_CAPTURE_DIR="$(win "$(cd "$OUT" && pwd)")" \
  TINDUSTRY_PLAYTEST="$NAME" timeout 150 "$GODOT" --path godot --resolution 1280x720   --log-file "$(win "$DATA")/godot.log" > "$LOG" 2>&1  # engine log in the temp profile, not in %APPDATA%
[ -f "$DATA/godot.log" ] && cp "$DATA/godot.log" "$OUT/godot.log"
if ! grep -q "PLAYTEST OK" "$LOG"; then echo "playtest $NAME: no 'PLAYTEST OK' in $LOG" >&2; exit 1; fi
if grep -q "ERROR" "$LOG"; then echo "playtest $NAME: ERROR lines in $LOG" >&2; grep "ERROR" "$LOG" | head -5 >&2; exit 1; fi
echo "playtest $NAME: OK"
