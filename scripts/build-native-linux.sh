#!/usr/bin/env bash
set -euo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
TARGET=${1:-x86_64-unknown-linux-gnu}
PROFILE=${2:-release}
LIB_NAME=libvello_ffi.so
OUT_DIR="${ROOT}/artifacts/runtimes"

echo "Building vello_ffi for ${TARGET} (${PROFILE})"
cargo build -p vello_ffi --target "${TARGET}" --${PROFILE}

RID=${3:-}
if [[ -z "${RID}" ]]; then
  case "${TARGET}" in
    x86_64-unknown-linux-gnu) RID=linux-x64 ;;
    aarch64-unknown-linux-gnu) RID=linux-arm64 ;;
    *) echo "Unknown target ${TARGET}; please provide RID as third argument" && exit 1 ;;
  esac
fi

SRC="${ROOT}/target/${TARGET}/${PROFILE}/${LIB_NAME}"
DEST="${OUT_DIR}/${RID}/native"
mkdir -p "${DEST}"
cp "${SRC}" "${DEST}/"
echo "Copied ${SRC} -> ${DEST}/${LIB_NAME}"
