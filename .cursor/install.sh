#!/usr/bin/env bash
# Idempotent Cloud Agent bootstrap for the tIndustry .NET 10 / Raylib project.
set -euo pipefail

DOTNET_CHANNEL="10.0"
DOTNET_INSTALL_DIR="/usr/lib/dotnet"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

echo "==> Installing system libraries (Raylib native deps + headless X server)"
sudo apt-get update -qq
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq --no-install-recommends \
  xvfb \
  libgl1 \
  libgl1-mesa-dri \
  libglx-mesa0 \
  libx11-6 \
  libxcursor1 \
  libxrandr2 \
  libxinerama1 \
  libxi6 \
  libxext6

echo "==> Ensuring .NET SDK ${DOTNET_CHANNEL} is installed at ${DOTNET_INSTALL_DIR}"
if [ ! -x "${DOTNET_INSTALL_DIR}/dotnet" ]; then
  tmp_script="$(mktemp)"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "${tmp_script}"
  chmod +x "${tmp_script}"
  sudo "${tmp_script}" --channel "${DOTNET_CHANNEL}" --install-dir "${DOTNET_INSTALL_DIR}"
  rm -f "${tmp_script}"
fi
sudo ln -sf "${DOTNET_INSTALL_DIR}/dotnet" /usr/local/bin/dotnet
# dnx runs NuGet MCP / other .NET tools without a permanent global install
if [ -x "${DOTNET_INSTALL_DIR}/dnx" ]; then
  sudo ln -sf "${DOTNET_INSTALL_DIR}/dnx" /usr/local/bin/dnx
fi
# NuGet.Mcp.Server (and other apphost tools) need DOTNET_ROOT when SDK is not in /usr/share/dotnet
export DOTNET_ROOT="${DOTNET_INSTALL_DIR}"
export DOTNET_ROOT_X64="${DOTNET_INSTALL_DIR}"
# Persist for interactive / MCP shells on this VM (idempotent)
if [ -f /etc/environment ]; then
  grep -q 'DOTNET_ROOT=' /etc/environment \
    || echo "DOTNET_ROOT=${DOTNET_INSTALL_DIR}" | sudo tee -a /etc/environment >/dev/null
  grep -q 'DOTNET_ROOT_X64=' /etc/environment \
    || echo "DOTNET_ROOT_X64=${DOTNET_INSTALL_DIR}" | sudo tee -a /etc/environment >/dev/null
fi

echo "==> dotnet version: $(dotnet --version)"
echo "==> dnx available: $(command -v dnx || echo missing)"

echo "==> Restoring and building the solution"
cd "$(dirname "$0")/.."
dotnet restore
dotnet build -c Release

echo "==> Install complete."
