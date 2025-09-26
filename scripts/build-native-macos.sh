#!/usr/bin/env bash
set -euo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
TARGET=${1:-x86_64-apple-darwin}
PROFILE=${2:-release}
SDK=${3:-}
LIBS=(vello_ffi kurbo_ffi peniko_ffi winit_ffi)
OUT_DIR="${ROOT}/artifacts/runtimes"

if [[ -n "${SDK}" ]]; then
  export SDKROOT=$(xcrun --sdk "${SDK}" --show-sdk-path)
  echo "Using SDKROOT=${SDKROOT}"
fi

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
for crate in "${LIBS[@]}"; do
  LIB_NAME="lib${crate}.dylib"
  SRC="${ROOT}/target/${TARGET}/${PROFILE_DIR}/${LIB_NAME}"

  if [[ ! -f "${SRC}" && "${TARGET}" == *"apple-ios"* ]]; then
    LIB_NAME="lib${crate}.a"
    SRC="${ROOT}/target/${TARGET}/${PROFILE_DIR}/${LIB_NAME}"
  fi

  if [[ ! -f "${SRC}" ]]; then
    echo "Native library ${LIB_NAME} not found for crate ${crate}." >&2
    exit 1
  fi

  DEST="${OUT_DIR}/${RID}/native"
  mkdir -p "${DEST}"
  cp "${SRC}" "${DEST}/"
  echo "Copied ${SRC} -> ${DEST}/${LIB_NAME}"
done
