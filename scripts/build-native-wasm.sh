#!/usr/bin/env bash
set -euo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
TARGET=${1:-wasm32-unknown-unknown}
PROFILE=${2:-release}
RID=${3:-browser-wasm}
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
  LIB_NAME="${crate}.wasm"
  SRC="${ROOT}/target/${TARGET}/${PROFILE}/${LIB_NAME}"
  if [[ ! -f "${SRC}" ]]; then
    LIB_NAME="lib${crate}.a"
    SRC="${ROOT}/target/${TARGET}/${PROFILE}/${LIB_NAME}"
  fi
  if [[ ! -f "${SRC}" ]]; then
    echo "Native artifact for ${crate} not found (expected .wasm or .a)." >&2
    exit 1
  fi
  cp "${SRC}" "${DEST}/"
  echo "Copied ${SRC} -> ${DEST}/${LIB_NAME}"
done
