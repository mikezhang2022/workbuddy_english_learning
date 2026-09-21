#!/usr/bin/env bash
# 可重复安装 factory-report-starter 开发依赖（不含长驻服务）。
set -euo pipefail

CHANNEL="${DOTNET_CHANNEL:-10.0}"
INSTALL_DIR="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"

if ! command -v curl >/dev/null 2>&1; then
  echo "curl is required" >&2
  exit 1
fi

if [[ ! -x "$INSTALL_DIR/dotnet" ]]; then
  echo "Installing .NET SDK channel $CHANNEL into $INSTALL_DIR ..."
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel "$CHANNEL" --install-dir "$INSTALL_DIR"
fi

export DOTNET_ROOT="$INSTALL_DIR"
export PATH="$INSTALL_DIR:$PATH"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

dotnet --info
dotnet restore FactoryReport.sln
dotnet build FactoryReport.sln --no-restore

echo "Bootstrap complete. Start services separately (see docs/development.md)."
