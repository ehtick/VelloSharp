#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This bootstrap script targets macOS; detected $(uname -s)." >&2
  exit 1
fi

has_command() {
  command -v "$1" >/dev/null 2>&1
}

ensure_dotnet() {
  if has_command dotnet; then
    echo ".NET SDK detected."
    return
  fi

  echo ".NET SDK not detected. Installing latest LTS using dotnet-install..."
  local install_script
  install_script="$(mktemp)"
  curl -sSL https://dot.net/v1/dotnet-install.sh -o "$install_script"
  bash "$install_script" --channel LTS --install-dir "$HOME/.dotnet"
  rm -f "$install_script"
  export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
  hash -r

  if has_command dotnet; then
    echo ".NET SDK installed to $HOME/.dotnet."
  else
    echo ".NET SDK was installed to $HOME/.dotnet but is not yet on PATH. Add '$HOME/.dotnet' and '$HOME/.dotnet/tools' to your PATH." >&2
  fi
}

if ! xcode-select -p >/dev/null 2>&1; then
  echo "Command Line Tools for Xcode are required. Triggering install prompt..."
  xcode-select --install >/dev/null 2>&1 || true
  echo "Please complete the Command Line Tools installation, then rerun this script." >&2
  exit 0
fi

if ! has_command curl; then
  echo "curl is required to install the Rust toolchain. Please install curl and rerun this script." >&2
  exit 1
fi

ensure_dotnet

ensure_rustup() {
  if has_command cargo || has_command rustup; then
    echo "Rust toolchain already detected."
    return
  fi

  echo "Installing Rust toolchain via rustup..."
  curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y --profile minimal
  # shellcheck disable=SC1090
  source "$HOME/.cargo/env"
}

ensure_rustup

echo "Bootstrap complete. Ensure $HOME/.cargo/bin is in your PATH before building."
