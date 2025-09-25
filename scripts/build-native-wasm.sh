#!/usr/bin/env bash
set -euo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
TARGET=${1:-wasm32-unknown-unknown}
PROFILE=${2:-release}
RID=${3:-browser-wasm}
LIB_NAME=vello_ffi.wasm
OUT_DIR="${ROOT}/artifacts/runtimes"

echo "Building vello_ffi for ${TARGET} (${PROFILE})"
cargo build -p vello_ffi --target "${TARGET}" --${PROFILE}

SRC="${ROOT}/target/${TARGET}/${PROFILE}/${LIB_NAME}"
DEST="${OUT_DIR}/${RID}/native"
mkdir -p "${DEST}"
cp "${SRC}" "${DEST}/"
echo "Copied ${SRC} -> ${DEST}/${LIB_NAME}"
