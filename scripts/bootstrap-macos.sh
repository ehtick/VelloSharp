#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This bootstrap script targets macOS; detected $(uname -s)." >&2
  exit 1
fi

has_command() {
  command -v "$1" >/dev/null 2>&1
}

run_as_root() {
  if [[ $EUID -eq 0 ]]; then
    "$@"
  elif has_command sudo; then
    sudo "$@"
  else
    echo "Unable to run '$*' without elevated privileges. Re-run this script with sudo or install dependencies manually." >&2
    exit 1
  fi
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

ensure_command_line_tools() {
  if xcode-select -p >/dev/null 2>&1; then
    echo "Command Line Tools for Xcode detected."
    return
  fi

  if ! has_command softwareupdate; then
    echo "Command Line Tools for Xcode are required but 'softwareupdate' is unavailable. Install them manually (e.g. 'sudo xcode-select --install')." >&2
    exit 1
  fi

  echo "Command Line Tools for Xcode not detected. Installing via softwareupdate..."

  local label
  label="$(softwareupdate --list 2>/dev/null | grep -E 'Label: Command Line Tools' | head -n1 | sed 's/^[[:space:]]*\* Label: //')"

  if [[ -z "$label" ]]; then
    echo "Unable to determine the Command Line Tools package name. Run 'softwareupdate --list' manually to identify the label, then install it with 'sudo softwareupdate --install <label>'." >&2
    exit 1
  fi

  run_as_root softwareupdate --install "$label"
  run_as_root xcode-select --switch /Library/Developer/CommandLineTools

  if xcode-select -p >/dev/null 2>&1; then
    echo "Command Line Tools for Xcode installed."
  else
    echo "Command Line Tools installation attempted, but they are still not detected. Verify the installation manually." >&2
    exit 1
  fi
}

ensure_command_line_tools

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
