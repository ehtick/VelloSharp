#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUNTIMES_ROOT="${1:-${ROOT}/artifacts/runtimes}"
OUTPUT_DIR="${2:-${ROOT}/artifacts/nuget}"

if [[ ! -d "${RUNTIMES_ROOT}" ]]; then
  echo "No runtimes directory found at '${RUNTIMES_ROOT}'." >&2
  exit 1
fi

mkdir -p "${OUTPUT_DIR}"
OUTPUT_DIR_ABS="$(cd "${OUTPUT_DIR}" && pwd)"

DOTNET_CLI="${DOTNET_CLI:-dotnet}"
if ! command -v "${DOTNET_CLI}" >/dev/null 2>&1; then
  if [[ -x "/mnt/c/Program Files/dotnet/dotnet" ]]; then
    DOTNET_CLI="/mnt/c/Program Files/dotnet/dotnet"
  elif [[ -x "/mnt/c/Program Files (x86)/dotnet/dotnet" ]]; then
    DOTNET_CLI="/mnt/c/Program Files (x86)/dotnet/dotnet"
  else
    echo "dotnet CLI not found on PATH. Set DOTNET_CLI to the dotnet executable." >&2
    exit 1
  fi
fi

shopt -s nullglob
seen_rids=()

rid_seen() {
  local rid="$1"
  for existing in "${seen_rids[@]:-}"; do
    if [[ "${existing}" == "${rid}" ]]; then
      return 0
    fi
  done
  return 1
}

processed=0
ffi_projects=(AccessKit ChartEngine Composition Editor Gauges Kurbo Peniko Scada TreeDataGrid Vello VelloSparse Winit)
for native_dir in "${RUNTIMES_ROOT}"/*/native; do
  [[ -d "${native_dir}" ]] || continue
  rid="$(basename "$(dirname "${native_dir}")")"
  if rid_seen "${rid}"; then
    continue
  fi
  seen_rids+=("${rid}")

  if ! find "${native_dir}" -mindepth 1 -type f -print -quit | grep -q .; then
    echo "Skipping ${rid}: no native assets found under '${native_dir}'."
    continue
  fi

  native_dir_abs="$(cd "${native_dir}" && pwd)"
  for ffi in "${ffi_projects[@]}"; do
    project="${ROOT}/packaging/VelloSharp.Native.${ffi}/VelloSharp.Native.${ffi}.${rid}.csproj"
    if [[ ! -f "${project}" ]]; then
      echo "Skipping ${ffi} for ${rid}: project not found at ${project}."
      continue
    fi

    echo "Packing native package for ${ffi} (${rid})"
    "${DOTNET_CLI}" pack "${project}" \
      -c Release \
      -p:NativeAssetsDirectory="${native_dir_abs}" \
      -p:PackageOutputPath="${OUTPUT_DIR_ABS}"
    processed=$((processed + 1))
  done
done

if [[ ${processed} -eq 0 ]]; then
  echo "No native runtime directories were processed under '${RUNTIMES_ROOT}'." >&2
  exit 1
fi

if compgen -G "${OUTPUT_DIR_ABS}"'/*.nupkg' > /dev/null; then
  echo "Native packages created in '${OUTPUT_DIR}'."
else
  echo "No native packages were produced." >&2
  exit 1
fi
