#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Linux" ]]; then
  echo "This bootstrap script targets Linux; detected $(uname -s)." >&2
  exit 1
fi

has_command() {
  command -v "$1" >/dev/null 2>&1
}

SUDO=""
if [[ $EUID -ne 0 ]]; then
  if has_command sudo; then
    SUDO="sudo"
  else
    echo "This script requires elevated privileges (root or sudo)." >&2
    exit 1
  fi
fi

install_with_apt() {
  local packages=(
    build-essential
    pkg-config
    cmake
    ninja-build
    curl
    git
    libssl-dev
    libx11-dev
    libxcb1-dev
    libxrandr-dev
    libxinerama-dev
    libxi-dev
    libxcursor-dev
    libxkbcommon-dev
    libwayland-dev
    libudev-dev
    libgl1-mesa-dev
    libegl1-mesa-dev
    libgles2-mesa-dev
    libvulkan-dev
  )

  $SUDO apt-get update
  $SUDO apt-get install -y "${packages[@]}"
}

install_with_dnf() {
  local packages=(
    pkgconf-pkg-config
    cmake
    ninja-build
    curl
    git
    openssl-devel
    libX11-devel
    libxcb-devel
    libXrandr-devel
    libXinerama-devel
    libXi-devel
    libXcursor-devel
    libxkbcommon-devel
    wayland-devel
    systemd-devel
    mesa-libGL-devel
    mesa-libEGL-devel
    mesa-libGLES-devel
    vulkan-loader-devel
    vulkan-headers
  )

  $SUDO dnf -y groupinstall "Development Tools"
  $SUDO dnf -y install "${packages[@]}"
}

install_with_pacman() {
  local packages=(
    base-devel
    pkgconf
    cmake
    ninja
    curl
    git
    openssl
    libx11
    libxcb
    libxrandr
    libxinerama
    libxi
    libxcursor
    libxkbcommon
    wayland
    mesa
    vulkan-icd-loader
    vulkan-headers
  )

  $SUDO pacman -Sy --noconfirm --needed "${packages[@]}"
}

install_with_zypper() {
  local packages=(
    patterns-devel-base-devel_basis
    pkg-config
    cmake
    ninja
    curl
    git
    libopenssl-devel
    libX11-devel
    libxcb-devel
    libXrandr-devel
    libXinerama-devel
    libXi-devel
    libXcursor-devel
    libxkbcommon-devel
    wayland-devel
    libudev-devel
    Mesa-libGL-devel
    Mesa-libEGL-devel
    Mesa-libGLESv2-devel
    vulkan-loader-devel
    vulkan-headers
  )

  $SUDO zypper refresh
  $SUDO zypper --non-interactive install "${packages[@]}"
}

install_native_dependencies() {
  if has_command apt-get; then
    echo "Installing native dependencies via apt-get"
    install_with_apt
  elif has_command dnf; then
    echo "Installing native dependencies via dnf"
    install_with_dnf
  elif has_command pacman; then
    echo "Installing native dependencies via pacman"
    install_with_pacman
  elif has_command zypper; then
    echo "Installing native dependencies via zypper"
    install_with_zypper
  else
    echo "Unsupported package manager. Please install dependencies manually." >&2
    exit 1
  fi
}

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

install_native_dependencies
ensure_rustup

echo "Bootstrap complete. Ensure $HOME/.cargo/bin is in your PATH before building."
