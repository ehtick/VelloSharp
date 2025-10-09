#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

DOCFX_JSON="${ROOT}/docs/docfx/docfx.json"

if [[ ! -f "${DOCFX_JSON}" ]]; then
  echo "DocFX configuration not found at ${DOCFX_JSON}" >&2
  exit 1
fi

export DOCFX_MSBUILD_ARGS="/p:EnableWindowsTargeting=true"

dotnet tool restore
dotnet tool run docfx "${DOCFX_JSON}" --property EnableWindowsTargeting=true "$@"
