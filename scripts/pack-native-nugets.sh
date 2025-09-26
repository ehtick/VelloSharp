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
for native_dir in "${RUNTIMES_ROOT}"/*/native; do
  [[ -d "${native_dir}" ]] || continue
  rid="$(basename "$(dirname "${native_dir}")")"
  if rid_seen "${rid}"; then
    continue
  fi
  seen_rids+=("${rid}")

  project="${ROOT}/packaging/VelloSharp.Native.${rid}/VelloSharp.Native.${rid}.csproj"
  if [[ ! -f "${project}" ]]; then
    echo "Skipping ${rid}: packaging project not found at ${project}."
    continue
  fi

  if ! find "${native_dir}" -type f -mindepth 1 -print -quit | grep -q .; then
    echo "Skipping ${rid}: no native assets found under '${native_dir}'."
    continue
  fi

  native_dir_abs="$(cd "${native_dir}" && pwd)"
  echo "Packing native package for ${rid}"
  dotnet pack "${project}" \
    -c Release \
    -p:NativeAssetsDirectory="${native_dir_abs}" \
    -p:PackageOutputPath="${OUTPUT_DIR_ABS}"
  processed=$((processed + 1))
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
