#!/usr/bin/env bash
set -euo pipefail

PORT=8080
DOCFX_ARGS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    -p|--port)
      if [[ $# -lt 2 ]]; then
        echo "Error: --port expects a value." >&2
        exit 1
      fi
      PORT="$2"
      shift 2
      ;;
    *)
      DOCFX_ARGS+=("$1")
      shift
      ;;
  esac
done

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOCFX_JSON="${ROOT}/docs/docfx/docfx.json"

if [[ ! -f "${DOCFX_JSON}" ]]; then
  echo "DocFX configuration not found at ${DOCFX_JSON}" >&2
  exit 1
fi

export DOCFX_MSBUILD_ARGS="/p:EnableWindowsTargeting=true"

dotnet tool restore >/dev/null

echo "Starting DocFX preview on http://localhost:${PORT} ..."
if [[ ${#DOCFX_ARGS[@]} -gt 0 ]]; then
  dotnet tool run docfx "${DOCFX_JSON}" --serve --port "${PORT}" "${DOCFX_ARGS[@]}"
else
  dotnet tool run docfx "${DOCFX_JSON}" --serve --port "${PORT}"
fi
