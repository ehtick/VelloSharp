#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This bootstrap script targets macOS; detected $(uname -s)." >&2
  exit 1
fi

has_command() {
  command -v "$1" >/dev/null 2>&1
}

if ! xcode-select -p >/dev/null 2>&1; then
  echo "Command Line Tools for Xcode are required. Triggering install prompt..."
  xcode-select --install >/dev/null 2>&1 || true
  echo "Please complete the Command Line Tools installation, then rerun this script." >&2
  exit 0
fi

if ! has_command brew; then
  echo "Homebrew not detected. Installing..."
  /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
  if [[ -x /opt/homebrew/bin/brew ]]; then
    eval "$(/opt/homebrew/bin/brew shellenv)"
  elif [[ -x /usr/local/bin/brew ]]; then
    eval "$(/usr/local/bin/brew shellenv)"
  fi
fi

if ! has_command brew; then
  echo "Homebrew installation failed or brew not on PATH." >&2
  exit 1
fi

brew update

BREW_PACKAGES=(cmake ninja pkg-config llvm python@3.12 git)
for pkg in "${BREW_PACKAGES[@]}"; do
  if brew list "$pkg" >/dev/null 2>&1 || brew list --cask "$pkg" >/dev/null 2>&1; then
    echo "brew package $pkg already installed"
  else
    brew install "$pkg"
  fi
done

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
