#!/usr/bin/env bash
set -euo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
TARGET=${1:-x86_64-unknown-linux-gnu}
PROFILE=${2:-release}
LIBS=(accesskit_ffi vello_ffi kurbo_ffi peniko_ffi winit_ffi)
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

RID=${3:-}
if [[ -z "${RID}" ]]; then
  case "${TARGET}" in
    x86_64-unknown-linux-gnu) RID=linux-x64 ;;
    aarch64-unknown-linux-gnu) RID=linux-arm64 ;;
    *) echo "Unknown target ${TARGET}; please provide RID as third argument" && exit 1 ;;
  esac
fi

for crate in "${LIBS[@]}"; do
  LIB_NAME="lib${crate}.so"
  SRC="${ROOT}/target/${TARGET}/${PROFILE}/${LIB_NAME}"
  if [[ ! -f "${SRC}" ]]; then
    SRC="${ROOT}/target/${TARGET}/${PROFILE}/${crate}.so"
  fi
  DEST="${OUT_DIR}/${RID}/native"
  mkdir -p "${DEST}"
  cp "${SRC}" "${DEST}/"
  echo "Copied ${SRC} -> ${DEST}/${LIB_NAME}"
done
