#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOURCE_ROOT="${1:-${ROOT}/artifacts}"
DEST_ROOT="${2:-${ROOT}/artifacts/runtimes}"

if [[ ! -d "${SOURCE_ROOT}" ]]; then
  echo "Source directory '${SOURCE_ROOT}' does not exist." >&2
  exit 1
fi

mkdir -p "${DEST_ROOT}"

map_candidate_to_rid() {
  local candidate="$1"
  case "${candidate}" in
    native-windows-x64) printf '%s' 'win-x64' ;;
    native-windows-arm64) printf '%s' 'win-arm64' ;;
    native-macos-x64) printf '%s' 'osx-x64' ;;
    native-macos-arm64) printf '%s' 'osx-arm64' ;;
    native-linux-x64) printf '%s' 'linux-x64' ;;
    native-linux-arm64) printf '%s' 'linux-arm64' ;;
    native-android-arm64) printf '%s' 'android-arm64' ;;
    native-ios-arm64) printf '%s' 'ios-arm64' ;;
    native-iossimulator-x64) printf '%s' 'iossimulator-x64' ;;
    native-wasm) printf '%s' 'browser-wasm' ;;
    native-*) printf '%s' "${candidate#native-}" ;;
    *) printf '%s' "${candidate}" ;;
  esac
}

seen_rids=""
found=0
while IFS= read -r native_dir; do
  [[ -z "${native_dir}" ]] && continue

  # Skip if we are looking at the destination directory we're populating.
  if [[ "${native_dir}" == "${DEST_ROOT}" || "${native_dir}" == "${DEST_ROOT}"/* ]]; then
    continue
  fi

  rid_dir="$(dirname "${native_dir}")"
  rid_name="$(basename "${rid_dir}")"

  if [[ "${rid_name}" == "artifacts" || "${rid_name}" == "runtimes" || "${rid_name}" == "native" ]]; then
    candidate="$(basename "$(dirname "${rid_dir}")")"
    if [[ -n "${candidate}" ]]; then
      rid_name="$(map_candidate_to_rid "${candidate}")"
    fi
  fi

  if [[ -z "${rid_name}" || "${rid_name}" == "artifacts" || "${rid_name}" == "runtimes" || "${rid_name}" == "native" ]]; then
    echo "Skipping native directory '${native_dir}' – could not determine runtime identifier." >&2
    continue
  fi

  found=1
  target="${DEST_ROOT}/${rid_name}/native"
  if [[ " ${seen_rids} " == *" ${rid_name} "* ]]; then
    echo "Updating existing runtime '${rid_name}'."
  else
    seen_rids="${seen_rids} ${rid_name}"
  fi
  mkdir -p "${target}"
  if command -v rsync >/dev/null 2>&1; then
    rsync -a --delete "${native_dir}/" "${target}/"
  else
    shopt -s dotglob nullglob
    rm -rf "${target}"/*
    cp -a "${native_dir}/." "${target}/" 2>/dev/null || true
    shopt -u dotglob nullglob
  fi
done < <(find "${SOURCE_ROOT}" -type d -name native | sort)

if [[ ${found} -eq 0 ]]; then
  echo "No native runtime directories found under '${SOURCE_ROOT}'." >&2
  exit 1
fi

echo "Collected runtimes into '${DEST_ROOT}'."
