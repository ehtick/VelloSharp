#!/usr/bin/env bash
set -euo pipefail

TARGET="${TARGET:-wasm32-unknown-unknown}"
PROFILE="${PROFILE:-release}"
CRATE="${CRATE:-vello_webgpu_ffi}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
OUT_DIR="${OUT_DIR:-${ROOT}/artifacts/browser/native}"
SAMPLE_ASSET_DIR="${SAMPLE_ASSET_DIR:-${ROOT}/samples/AvaloniaVelloBrowserDemo/AvaloniaVelloBrowserDemo.Browser/wwwroot/native}"

mkdir -p "${OUT_DIR}"

if ! command -v wasm-bindgen >/dev/null 2>&1; then
  echo "wasm-bindgen CLI not found on PATH. Install it (cargo install wasm-bindgen-cli) and retry." >&2
  exit 1
fi

if ! command -v wasm-opt >/dev/null 2>&1; then
  echo "wasm-opt not found on PATH. Binaryen optimizations will be skipped." >&2
  HAS_WASM_OPT=0
else
  HAS_WASM_OPT=1
fi

build_args=(--target "${TARGET}" -p "${CRATE}")
case "${PROFILE}" in
  release|Release)
    profile_dir="release"
    build_args+=(--release)
    ;;
  debug|Debug)
    profile_dir="debug"
    ;;
  *)
    profile_dir="${PROFILE}"
    build_args+=(--profile "${PROFILE}")
    ;;
esac

echo "Building ${CRATE} for ${TARGET} (${PROFILE})"
cargo build "${build_args[@]}"

wasm_path="${ROOT}/target/${TARGET}/${profile_dir}/${CRATE}.wasm"
if [[ ! -f "${wasm_path}" ]]; then
  echo "Expected wasm artifact not found at ${wasm_path}" >&2
  exit 1
fi

raw_out="${OUT_DIR}/${CRATE}.wasm"
cp "${wasm_path}" "${raw_out}"
echo "Copied raw artifact to ${raw_out}"

temp_dir="$(mktemp -d)"
opt_temp="$(mktemp)"
cleanup() {
  rm -rf "${temp_dir}"
  rm -f "${opt_temp}"
}
trap cleanup EXIT

echo "Running wasm-bindgen"
wasm-bindgen "${wasm_path}" \
  --target web \
  --reference-types \
  --no-typescript \
  --out-dir "${temp_dir}"

bindgen_wasm="${temp_dir}/${CRATE}_bg.wasm"
if [[ ! -f "${bindgen_wasm}" ]]; then
  echo "wasm-bindgen output not found at ${bindgen_wasm}" >&2
  exit 1
fi

cp "${bindgen_wasm}" "${OUT_DIR}/${CRATE}_bg.wasm"

if [[ -f "${temp_dir}/${CRATE}.js" ]]; then
  cp "${temp_dir}/${CRATE}.js" "${OUT_DIR}/${CRATE}.js"
fi

if [[ -f "${temp_dir}/package.json" ]]; then
  cp "${temp_dir}/package.json" "${OUT_DIR}/package.json"
fi

if [[ -d "${temp_dir}/snippets" ]]; then
  rm -rf "${OUT_DIR}/snippets"
  cp -R "${temp_dir}/snippets" "${OUT_DIR}/snippets"
fi

if [[ "${HAS_WASM_OPT}" -eq 1 ]]; then
  echo "Optimizing ${CRATE}_bg.wasm with wasm-opt"
  wasm-opt -O2 --strip-debug -o "${opt_temp}" "${OUT_DIR}/${CRATE}_bg.wasm"
  mv "${opt_temp}" "${OUT_DIR}/${CRATE}_bg.wasm"
  echo "Optimized artifact written to ${OUT_DIR}/${CRATE}_bg.wasm"
else
  echo "Skipping wasm-opt optimization step."
fi

if [[ -n "${SAMPLE_ASSET_DIR}" ]]; then
  mkdir -p "${SAMPLE_ASSET_DIR}"
  cp "${OUT_DIR}/${CRATE}.wasm" "${SAMPLE_ASSET_DIR}/${CRATE}.wasm"
  if [[ -f "${OUT_DIR}/${CRATE}_bg.wasm" ]]; then
    cp "${OUT_DIR}/${CRATE}_bg.wasm" "${SAMPLE_ASSET_DIR}/${CRATE}_bg.wasm"
  fi
  if [[ -f "${OUT_DIR}/${CRATE}.js" ]]; then
    cp "${OUT_DIR}/${CRATE}.js" "${SAMPLE_ASSET_DIR}/${CRATE}.js"
  fi
  echo "Sample assets mirrored to ${SAMPLE_ASSET_DIR}"
fi

runtime_native_dir="${ROOT}/artifacts/runtimes/browser-wasm/native"
mkdir -p "${runtime_native_dir}"

cp "${OUT_DIR}/${CRATE}.wasm" "${runtime_native_dir}/${CRATE}.wasm"
if [[ -f "${OUT_DIR}/${CRATE}_bg.wasm" ]]; then
  cp "${OUT_DIR}/${CRATE}_bg.wasm" "${runtime_native_dir}/${CRATE}_bg.wasm"
fi
if [[ -f "${OUT_DIR}/${CRATE}.js" ]]; then
  cp "${OUT_DIR}/${CRATE}.js" "${runtime_native_dir}/${CRATE}.js"
fi
if [[ -f "${OUT_DIR}/package.json" ]]; then
  cp "${OUT_DIR}/package.json" "${runtime_native_dir}/package.json"
fi
if [[ -d "${OUT_DIR}/snippets" ]]; then
  rm -rf "${runtime_native_dir}/snippets"
  cp -R "${OUT_DIR}/snippets" "${runtime_native_dir}/snippets"
fi
