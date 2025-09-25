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

found=0
while IFS= read -r native_dir; do
  [[ -z "${native_dir}" ]] && continue

  # Skip if we are looking at the destination directory we're populating.
  if [[ "${native_dir}" -ef "${DEST_ROOT}" ]]; then
    continue
  fi

  rid_dir="$(dirname "${native_dir}")"
  rid_name="$(basename "${rid_dir}")"
  if [[ -z "${rid_name}" ]]; then
    continue
  fi

  found=1
  target="${DEST_ROOT}/${rid_name}/native"
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
