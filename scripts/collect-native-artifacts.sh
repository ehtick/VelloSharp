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
while IFS= read -r runtime_dir; do
  if [[ -z "${runtime_dir}" ]]; then
    continue
  fi
  # Skip if this is already the destination directory
  if [[ "${runtime_dir}" -ef "${DEST_ROOT}" ]]; then
    continue
  fi
  found=1
  shopt -s nullglob dotglob
  for rid_dir in "${runtime_dir}"/*; do
    [[ -d "${rid_dir}" ]] || continue
    rid_name="$(basename "${rid_dir}")"
    target="${DEST_ROOT}/${rid_name}"
    mkdir -p "${target}"
    if command -v rsync >/dev/null 2>&1; then
      rsync -a --delete "${rid_dir}/" "${target}/"
    else
      shopt -s dotglob nullglob
      rm -rf "${target}"/*
      cp -a "${rid_dir}/." "${target}/" 2>/dev/null || true
      shopt -u dotglob nullglob
    fi
  done
  shopt -u nullglob dotglob
done < <(find "${SOURCE_ROOT}" -type d -name runtimes | sort)

if [[ ${found} -eq 0 ]]; then
  echo "No 'runtimes' directories found under '${SOURCE_ROOT}'." >&2
  exit 1
fi

echo "Collected runtimes into '${DEST_ROOT}'."
