#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Linux" ]]; then
  echo "This bootstrap script targets Linux; detected $(uname -s)." >&2
  exit 1
fi

has_command() {
  command -v "$1" >/dev/null 2>&1
}

if has_command dotnet; then
  echo ".NET SDK detected."
else
  echo ".NET SDK not detected. Please install the .NET SDK before continuing." >&2
fi

if ! has_command curl; then
  echo "curl is required to install the Rust toolchain. Please install curl and rerun this script." >&2
  exit 1
fi

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
