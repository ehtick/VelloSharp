#!/usr/bin/env bash
set -euo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
TARGET=${1:-x86_64-apple-darwin}
PROFILE=${2:-release}
SDK=${3:-}
LIB_EXT="dylib"
LIB_NAME="libvello_ffi.${LIB_EXT}"
OUT_DIR="${ROOT}/artifacts/runtimes"

if [[ -n "${SDK}" ]]; then
  export SDKROOT=$(xcrun --sdk "${SDK}" --show-sdk-path)
  echo "Using SDKROOT=${SDKROOT}"
fi

echo "Building vello_ffi for ${TARGET} (${PROFILE})"
cargo build -p vello_ffi --target "${TARGET}" --${PROFILE}

RID=${4:-}
if [[ -z "${RID}" ]]; then
  case "${TARGET}" in
    x86_64-apple-darwin) RID=osx-x64 ;;
    aarch64-apple-darwin) RID=osx-arm64 ;;
    aarch64-apple-ios) RID=ios-arm64 ;;
    x86_64-apple-ios) RID=iossimulator-x64 ;;
    *) echo "Unknown target ${TARGET}; please provide RID as fourth argument" && exit 1 ;;
  esac
fi

PROFILE_DIR=${PROFILE}
SRC="${ROOT}/target/${TARGET}/${PROFILE_DIR}/${LIB_NAME}"

if [[ ! -f "${SRC}" && "${TARGET}" == *"apple-ios"* ]]; then
  # static lib for iOS
  LIB_NAME="libvello_ffi.a"
  SRC="${ROOT}/target/${TARGET}/${PROFILE_DIR}/${LIB_NAME}"
fi

DEST="${OUT_DIR}/${RID}/native"
mkdir -p "${DEST}"
cp "${SRC}" "${DEST}/"
echo "Copied ${SRC} -> ${DEST}/${LIB_NAME}"
