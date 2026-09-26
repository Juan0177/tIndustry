#!/usr/bin/env bash
# Export official Godot 4 .NET client (Windows + Linux) into dist/.
# Requires: Godot 4.4.1 mono, matching export templates, .NET 8 SDK.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
GODOT_BIN="${GODOT_BIN:-godot4}"
VER="${GODOT_VERSION:-4.4.1}"

cd "$ROOT"

mkdir -p godot/data
cp -f data/content.json data/campaign.json godot/data/

# Ensure classic .sln exists (Godot mono export requires it).
if [[ ! -f godot/TIndustry.Godot.sln ]]; then
  (cd godot && dotnet new sln -n TIndustry.Godot --format sln --force)
  (cd godot && dotnet sln TIndustry.Godot.sln add TIndustry.Godot.csproj)
  (cd godot && dotnet sln TIndustry.Godot.sln add ../src/TIndustry.Shared/TIndustry.Shared.csproj)
fi

dotnet build src/TIndustry.Shared/TIndustry.Shared.csproj -c ExportRelease
dotnet build godot/TIndustry.Godot.csproj -c ExportRelease

rm -rf dist/godot-win dist/godot-linux
mkdir -p dist/godot-win dist/godot-linux

# Import once so .godot cache is warm (ignore exit if already imported).
"${GODOT_BIN}" --headless --path godot --import || true

"${GODOT_BIN}" --headless --path godot \
  --export-release "Windows Desktop" "${ROOT}/dist/godot-win/tIndustry.exe"

"${GODOT_BIN}" --headless --path godot \
  --export-release "Linux" "${ROOT}/dist/godot-linux/tIndustry.x86_64"

test -f dist/godot-win/tIndustry.exe
test -f dist/godot-linux/tIndustry.x86_64
chmod +x dist/godot-linux/tIndustry.x86_64

mkdir -p dist
(cd dist/godot-win && zip -qr ../tIndustry-win-x64.zip .)
(cd dist/godot-linux && zip -qr ../tIndustry-linux-x64.zip .)

# Also publish clearly named godot aliases (same content).
cp -f dist/tIndustry-win-x64.zip dist/tIndustry-godot-win-x64.zip
cp -f dist/tIndustry-linux-x64.zip dist/tIndustry-godot-linux-x64.zip

echo "Godot export OK (${VER}):"
ls -lh dist/tIndustry-*-x64.zip
