#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Linux" ]]; then
  echo "This bootstrap script targets Linux; detected $(uname -s)." >&2
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
    echo "Unable to run '$*' without elevated privileges. Re-run this script as root or install dependencies manually." >&2
    exit 1
  fi
}

ensure_build_toolchain() {
  if has_command cc || has_command gcc || has_command clang; then
    echo "C compiler detected."
    return
  fi

  echo "C compiler not detected. Attempting to install build tools..."

  if has_command apt-get; then
    run_as_root apt-get update
    run_as_root apt-get install -y build-essential clang pkg-config
  elif has_command dnf; then
    run_as_root dnf -y groupinstall "Development Tools"
    run_as_root dnf install -y clang pkg-config
  elif has_command yum; then
    run_as_root yum -y groupinstall "Development Tools"
    run_as_root yum install -y clang pkg-config
  elif has_command zypper; then
    run_as_root zypper install -y -t pattern devel_basis
    run_as_root zypper install -y clang pkg-config
  elif has_command pacman; then
    run_as_root pacman -Sy --needed --noconfirm base-devel clang pkgconf
  else
    echo "No supported package manager detected. Install gcc/clang and related build tools manually." >&2
    exit 1
  fi

  hash -r

  if has_command cc || has_command gcc || has_command clang; then
    echo "C compiler installed."
  else
    echo "Failed to detect a C compiler after installation. Verify your toolchain manually." >&2
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

if ! has_command curl; then
  echo "curl is required to install the Rust toolchain. Please install curl and rerun this script." >&2
  exit 1
fi

ensure_dotnet
ensure_build_toolchain

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
    echo "Cargo is not on PATH; skipping automatic wasm-bindgen installation. Install manually with 'cargo install wasm-bindgen-cli' once cargo is available." >&2
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

  if has_command apt-get; then
    run_as_root apt-get update
    run_as_root apt-get install -y binaryen
  elif has_command dnf; then
    run_as_root dnf install -y binaryen
  elif has_command yum; then
    run_as_root yum install -y binaryen
  elif has_command zypper; then
    run_as_root zypper install -y binaryen
  elif has_command pacman; then
    run_as_root pacman -Sy --needed --noconfirm binaryen
  elif has_command brew; then
    brew install binaryen
  elif has_command nix; then
    nix-env -iA nixpkgs.binaryen
  elif has_command npm; then
    run_as_root npm install -g binaryen
  else
    echo "No supported package manager detected for installing Binaryen. Install it manually from https://github.com/WebAssembly/binaryen/releases." >&2
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
