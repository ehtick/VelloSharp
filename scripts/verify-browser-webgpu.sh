#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${CONFIGURATION:-Release}"
FRAMEWORK="${FRAMEWORK:-net8.0-browserwasm}"
SKIP_BUILD="${SKIP_BUILD:-0}"

SCRIPT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_ROOT}/.." && pwd)"
TEMP_ROOT="${REPO_ROOT}/artifacts/tmp"
mkdir -p "${TEMP_ROOT}"

invoke_step() {
  local message="$1"
  shift
  echo "==> ${message}"
  "$@"
}

if [[ "${SKIP_BUILD}" != "1" ]]; then
  invoke_step "Building browser WebGPU runtime" \
    "${SCRIPT_ROOT}/build-wasm-vello.sh"
fi

invoke_step "Running wasm-bindgen tests (wasm32-unknown-unknown)" \
  bash -c 'WASM_BINDGEN_TEST_TIMEOUT=120 cargo test -p vello_webgpu_ffi --target wasm32-unknown-unknown --release --tests'

BROWSER_PROJECT="${REPO_ROOT}/samples/AvaloniaVelloBrowserDemo/AvaloniaVelloBrowserDemo.Browser/AvaloniaVelloBrowserDemo.Browser.csproj"
PUBLISH_DIR="${TEMP_ROOT}/browser-publish"
rm -rf "${PUBLISH_DIR}"

invoke_step "Publishing Avalonia browser host" \
  dotnet publish "${BROWSER_PROJECT}" \
    -c "${CONFIGURATION}" \
    -f "${FRAMEWORK}" \
    -o "${PUBLISH_DIR}" \
    /bl:"${TEMP_ROOT}/browser-publish.binlog" \
    /p:RunAOTCompilation=false \
    /p:WasmGenerateAppBundle=true

invoke_step "Checking published artefacts" bash -c '
  native_dir="$0/wwwroot/native"
  if [[ ! -d "${native_dir}" ]]; then
    echo "Native asset directory not found at ${native_dir}" >&2
    exit 1
  fi

  required=("vello_webgpu_ffi.wasm" "vello_webgpu_ffi_bg.wasm")
  for name in "${required[@]}"; do
    if [[ ! -f "${native_dir}/${name}" ]]; then
      echo "Expected asset ${name} was not produced." >&2
      exit 1
    fi
  done

  echo "Native asset bundle verified."
' "${PUBLISH_DIR}"

invoke_step "Running Playwright smoke test" bash -c '
  root="$0"
  temp_root="$1"
  script_root="$2"

  if ! command -v node >/dev/null 2>&1 || ! command -v npm >/dev/null 2>&1 || ! command -v npx >/dev/null 2>&1; then
    echo "Node.js tooling not found on PATH. Skipping Playwright smoke test." >&2
    exit 0
  fi

  web_root="${root}/wwwroot"
  if [[ ! -d "${web_root}" ]]; then
    echo "Published wwwroot directory not found at ${web_root}" >&2
    exit 1
  fi

  play_dir="${temp_root}/playwright-smoke"
  mkdir -p "${play_dir}"
  pushd "${play_dir}" >/dev/null

  trap "popd >/dev/null" EXIT

  if [[ ! -f package.json ]]; then
    npm init -y >/dev/null
  fi

  if [[ ! -d node_modules/playwright ]]; then
    npm install --no-save --no-audit --no-fund playwright@1.46.1 >/dev/null
  fi

  npx playwright install chromium >/dev/null

  server_script="${script_root}/playwright/server.mjs"
  smoke_script="${script_root}/playwright/smoke.mjs"

  if [[ ! -f "${server_script}" || ! -f "${smoke_script}" ]]; then
    echo "Playwright helper scripts not found." >&2
    exit 1
  fi

  port=$((4000 + RANDOM % 2000))
  node "${server_script}" "${web_root}" "${port}" \
    >"${temp_root}/playwright-server.log" \
    2>"${temp_root}/playwright-server.err" &
  server_pid=$!

  trap "kill ${server_pid} 2>/dev/null || true; popd >/dev/null" EXIT

  for _ in {1..200}; do
    if [[ -f "${temp_root}/playwright-server.log" ]] && grep -q "READY" "${temp_root}/playwright-server.log"; then
      break
    fi
    if ! kill -0 "${server_pid}" 2>/dev/null; then
      echo "Static server exited unexpectedly."
      cat "${temp_root}/playwright-server.err"
      exit 1
    fi
    sleep 0.1
  done

  if ! grep -q "READY" "${temp_root}/playwright-server.log"; then
    echo "Static server did not signal readiness." >&2
    exit 1
  fi

  screenshot="${temp_root}/playwright-smoke.png"
  if ! node "${smoke_script}" "http://127.0.0.1:${port}/" "${screenshot}"; then
    echo "Playwright smoke test failed." >&2
    exit 1
  fi

  kill "${server_pid}" 2>/dev/null || true
  wait "${server_pid}" 2>/dev/null || true
' "${PUBLISH_DIR}" "${TEMP_ROOT}" "${SCRIPT_ROOT}"

echo "Browser smoke verification succeeded."
