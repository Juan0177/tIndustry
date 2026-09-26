#!/usr/bin/env bash
# Install Godot 4.4.1 mono editor + export templates for CI / local export.
set -euo pipefail

VER="${GODOT_VERSION:-4.4.1}"
INSTALL_DIR="${GODOT_INSTALL_DIR:-$HOME/.local/share/godot-ci}"
TEMPLATES_DIR="${HOME}/.local/share/godot/export_templates/${VER}.stable.mono"
BIN_LINK="${GODOT_BIN_LINK:-$HOME/.local/bin/godot4}"

mkdir -p "$INSTALL_DIR" "$(dirname "$BIN_LINK")" "$HOME/.local/share/godot/export_templates"

EDITOR_ZIP="Godot_v${VER}-stable_mono_linux_x86_64.zip"
TEMPLATES_TPZ="Godot_v${VER}-stable_mono_export_templates.tpz"
BASE_URL="https://github.com/godotengine/godot/releases/download/${VER}-stable"

if [[ ! -x "${INSTALL_DIR}/Godot_v${VER}-stable_mono_linux.x86_64" ]] \
   && [[ ! -x "${INSTALL_DIR}/Godot_v${VER}-stable_mono_linux_x86_64/Godot_v${VER}-stable_mono_linux.x86_64" ]]; then
  echo "Downloading Godot ${VER} mono editor..."
  curl -fsSL -o "${INSTALL_DIR}/${EDITOR_ZIP}" "${BASE_URL}/${EDITOR_ZIP}"
  unzip -qo "${INSTALL_DIR}/${EDITOR_ZIP}" -d "${INSTALL_DIR}"
fi

EDITOR_BIN="$(find "$INSTALL_DIR" -type f -name 'Godot_v*-stable_mono_linux.x86_64' | head -1)"
test -n "$EDITOR_BIN"
chmod +x "$EDITOR_BIN"
ln -sfn "$EDITOR_BIN" "$BIN_LINK"

if [[ ! -f "${TEMPLATES_DIR}/version.txt" ]]; then
  echo "Downloading Godot ${VER} mono export templates..."
  curl -fsSL -o "${INSTALL_DIR}/${TEMPLATES_TPZ}" "${BASE_URL}/${TEMPLATES_TPZ}"
  rm -rf "${TEMPLATES_DIR}"
  mkdir -p "${INSTALL_DIR}/tpz-extract"
  unzip -qo "${INSTALL_DIR}/${TEMPLATES_TPZ}" -d "${INSTALL_DIR}/tpz-extract"
  mkdir -p "${TEMPLATES_DIR}"
  if [[ -d "${INSTALL_DIR}/tpz-extract/templates" ]]; then
    cp -a "${INSTALL_DIR}/tpz-extract/templates/." "${TEMPLATES_DIR}/"
  else
    cp -a "${INSTALL_DIR}/tpz-extract/." "${TEMPLATES_DIR}/"
  fi
fi

echo "Godot editor: $EDITOR_BIN"
echo "Templates: ${TEMPLATES_DIR} ($(cat "${TEMPLATES_DIR}/version.txt"))"
"$BIN_LINK" --version
