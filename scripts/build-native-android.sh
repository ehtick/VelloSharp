#!/usr/bin/env bash
set -euo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
TARGET=${1:-aarch64-linux-android}
PROFILE=${2:-release}
RID=${3:-android-arm64}
LIBS=()
FFI_DIR="${ROOT}/ffi"
if [[ -d "${FFI_DIR}" ]]; then
  while IFS= read -r dir; do
    if [[ -f "${dir}/Cargo.toml" ]]; then
      LIBS+=("$(basename "${dir}")")
    fi
  done < <(find "${FFI_DIR}" -mindepth 1 -maxdepth 1 -type d | sort)
fi
if [[ ${#LIBS[@]} -eq 0 ]]; then
  LIBS=(accesskit_ffi vello_ffi kurbo_ffi peniko_ffi winit_ffi)
fi
OUT_DIR="${ROOT}/artifacts/runtimes"

if [[ -z "${ANDROID_NDK_HOME:-}" ]]; then
  echo "ANDROID_NDK_HOME must be set" >&2
  exit 1
fi

# Ensure toolchains on PATH
export PATH="${ANDROID_NDK_HOME}/toolchains/llvm/prebuilt/linux-x86_64/bin:${PATH}"
export AR_aarch64_linux_android="${ANDROID_NDK_HOME}/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-ar"

build_flags=("--target" "${TARGET}")
if [[ "${PROFILE}" == "release" ]]; then
  build_flags+=("--release")
elif [[ "${PROFILE}" != "debug" ]]; then
  build_flags+=("--profile" "${PROFILE}")
fi

for crate in "${LIBS[@]}"; do
  echo "Building ${crate} for ${TARGET} (${PROFILE})"
  cargo build -p "${crate}" "${build_flags[@]}"
done

DEST="${OUT_DIR}/${RID}/native"
mkdir -p "${DEST}"

for crate in "${LIBS[@]}"; do
  LIB_NAME="lib${crate}.so"
  SRC="${ROOT}/target/${TARGET}/${PROFILE}/${LIB_NAME}"
  if [[ ! -f "${SRC}" ]]; then
    echo "Native library ${LIB_NAME} not found for ${crate}." >&2
    exit 1
  fi
  cp "${SRC}" "${DEST}/"
  echo "Copied ${SRC} -> ${DEST}/${LIB_NAME}"
done
