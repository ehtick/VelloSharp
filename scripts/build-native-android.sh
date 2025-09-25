#!/usr/bin/env bash
set -euo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
TARGET=${1:-aarch64-linux-android}
PROFILE=${2:-release}
RID=${3:-android-arm64}
LIB_NAME=libvello_ffi.so
OUT_DIR="${ROOT}/artifacts/runtimes"

if [[ -z "${ANDROID_NDK_HOME:-}" ]]; then
  echo "ANDROID_NDK_HOME must be set" >&2
  exit 1
fi

# Ensure toolchains on PATH
export PATH="${ANDROID_NDK_HOME}/toolchains/llvm/prebuilt/linux-x86_64/bin:${PATH}"
export AR_aarch64_linux_android="${ANDROID_NDK_HOME}/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-ar"

echo "Building vello_ffi for ${TARGET} (${PROFILE})"
cargo build -p vello_ffi --target "${TARGET}" --${PROFILE}

SRC="${ROOT}/target/${TARGET}/${PROFILE}/${LIB_NAME}"
DEST="${OUT_DIR}/${RID}/native"
mkdir -p "${DEST}"
cp "${SRC}" "${DEST}/"
echo "Copied ${SRC} -> ${DEST}/${LIB_NAME}"
