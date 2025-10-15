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

ensure_maui_workload() {
  echo "Restoring .NET MAUI workloads..."
  if ! dotnet workload restore maui; then
    echo "dotnet workload restore maui failed. Install the MAUI workloads via Visual Studio or rerun once prerequisites are installed." >&2
  fi
}

ensure_maui_workload

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

ensure_wasm_bindgen() {
  if has_command wasm-bindgen; then
    echo "wasm-bindgen CLI detected."
    return
  fi

  if ! has_command cargo; then
    echo "Cargo is not on PATH; skip automatic wasm-bindgen installation. Install manually with 'cargo install wasm-bindgen-cli' once cargo is available." >&2
    return
  fi

  echo "Installing wasm-bindgen-cli via cargo..."
  cargo install wasm-bindgen-cli --force
  hash -r

  if has_command wasm-bindgen; then
    echo "wasm-bindgen CLI installed."
  else
    echo "Attempted to install wasm-bindgen-cli but the command is still missing. Verify your PATH or reinstall manually." >&2
  fi
}

ensure_binaryen() {
  if has_command wasm-opt; then
    echo "wasm-opt (Binaryen) detected."
    return
  fi

  echo "wasm-opt not detected. Attempting to install Binaryen..."

  if has_command brew; then
    brew install binaryen
  elif has_command port; then
    run_as_root port install binaryen
  else
    echo "Homebrew/MacPorts not detected. Install Binaryen manually (e.g., brew install binaryen or download from https://github.com/WebAssembly/binaryen/releases)." >&2
  fi

  hash -r

  if has_command wasm-opt; then
    echo "Binaryen (wasm-opt) installed."
  else
    echo "Attempted to install Binaryen, but wasm-opt is still not detected. Verify the installation manually." >&2
  fi
}

ensure_wasm_bindgen
ensure_binaryen

echo "Bootstrap complete. Ensure $HOME/.cargo/bin is in your PATH before building."
