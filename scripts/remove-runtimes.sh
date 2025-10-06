#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ "$#" -gt 0 ]]; then
  TARGETS=("$@")
else
  TARGETS=(
    "VelloSharp"
    "VelloSharp.Integration"
    "samples/AvaloniaVelloExamples"
    "samples/AvaloniaVelloWinitDemo"
    "samples/AvaloniaVelloX11Demo"
    "samples/AvaloniaVelloWin32Demo"
    "samples/AvaloniaVelloNativeDemo"
    "samples/VelloSharp.WithWinit"
    "samples/VelloSharp.Uno.WinAppSdkSample"
  )
fi

declare -a CONFIGURATIONS
if [[ -n "${REMOVE_RUNTIMES_CONFIGURATIONS:-}" ]]; then
  read -r -a CONFIGURATIONS <<< "${REMOVE_RUNTIMES_CONFIGURATIONS}"
else
  CONFIGURATIONS=(Debug Release)
fi

declare -a TARGET_FRAMEWORKS
if [[ -n "${REMOVE_RUNTIMES_TARGET_FRAMEWORKS:-}" ]]; then
  read -r -a TARGET_FRAMEWORKS <<< "${REMOVE_RUNTIMES_TARGET_FRAMEWORKS}"
else
  TARGET_FRAMEWORKS=(net8.0 net8.0-windows net8.0-windows10.0.19041.0)
fi

delete_directory() {
  local path="$1"

  if [[ -d "${path}" ]]; then
    rm -rf "${path}"
    echo "Removed '${path}'"
  fi
}

echo "Removing runtime payloads"
for target in "${TARGETS[@]}"; do
  target_root="${ROOT}/${target}"
  if [[ ! -d "${target_root}" ]]; then
    echo "Skipping '${target}' (directory not found)."
    continue
  fi

  delete_directory "${target_root}/runtimes"

  for configuration in "${CONFIGURATIONS[@]}"; do
    for framework in "${TARGET_FRAMEWORKS[@]}"; do
      output_base="${target_root}/bin/${configuration}/${framework}"
      delete_directory "${output_base}/runtimes"
    done
  done

done
