#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACTS_DIR="${1:-${ROOT}/artifacts/runtimes}"
shift || true

if [[ ! -d "${ARTIFACTS_DIR}" ]]; then
  echo "No runtimes directory found at '${ARTIFACTS_DIR}'." >&2
  exit 1
fi

declare -a TARGETS=("VelloSharp" "VelloSharp.Integration" "samples/AvaloniaVelloExamples" "samples/AvaloniaVelloDemo")
if [[ "$#" -gt 0 ]]; then
  TARGETS=("$@")
fi

if [[ -n "${COPY_CONFIGURATIONS:-}" ]]; then
  read -r -a CONFIGURATIONS <<< "${COPY_CONFIGURATIONS}"
else
  CONFIGURATIONS=(Debug Release)
fi

if [[ -n "${COPY_TARGET_FRAMEWORKS:-}" ]]; then
  read -r -a TARGET_FRAMEWORKS <<< "${COPY_TARGET_FRAMEWORKS}"
else
  TARGET_FRAMEWORKS=(net8.0)
fi

copy_payload() {
  local destination="$1"
  local delete_flag="${2:-false}"

  mkdir -p "${destination}"
  if command -v rsync >/dev/null 2>&1; then
    if [[ "${delete_flag}" == "true" ]]; then
      rsync -a --delete "${ARTIFACTS_DIR}/" "${destination}/"
    else
      rsync -a "${ARTIFACTS_DIR}/" "${destination}/"
    fi
  else
    shopt -s dotglob nullglob
    if [[ "${delete_flag}" == "true" ]]; then
      rm -rf "${destination}"/*
    fi
    cp -a "${ARTIFACTS_DIR}/"* "${destination}/" 2>/dev/null || true
    shopt -u dotglob
    shopt -u nullglob
  fi
}

echo "Copying runtimes from '${ARTIFACTS_DIR}'"
for target in "${TARGETS[@]}"; do
  target_root="${ROOT}/${target}"
  if [[ ! -d "${target_root}" ]]; then
    echo "Skipping '${target}' (directory not found)."
    continue
  fi

  # Keep a project-level copy in case it is useful for packaging.
  copy_payload "${target_root}/runtimes" true
  echo "Copied runtimes to '${target_root}/runtimes'"

  for configuration in "${CONFIGURATIONS[@]}"; do
    for framework in "${TARGET_FRAMEWORKS[@]}"; do
      output_base="${target_root}/bin/${configuration}/${framework}"
      if [[ ! -d "${output_base}" ]]; then
        echo "Skipping '${target}' (${configuration}|${framework}) – build output not found."
        continue
      fi

      copy_payload "${output_base}/runtimes" false
      echo "Copied runtimes to '${output_base}/runtimes'"
    done
  done
done
