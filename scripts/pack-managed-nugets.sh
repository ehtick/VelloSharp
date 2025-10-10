#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NUGET_OUTPUT="${1:-${ROOT}/artifacts/nuget}"
NATIVE_FEED="${2:-${NUGET_OUTPUT}}"

mkdir -p "${NUGET_OUTPUT}"
NUGET_OUTPUT_ABS="$(cd "${NUGET_OUTPUT}" && pwd)"
NATIVE_FEED_ABS="$(cd "${NATIVE_FEED}" && pwd)"

declare -a PROJECT_PATHS=()

add_candidate_projects() {
  local search_root="$1"
  [[ -d "${search_root}" ]] || return 0

  while IFS= read -r -d '' candidate; do
    PROJECT_PATHS+=("${candidate}")
  done < <(find "${search_root}" -type f -name '*.csproj' -print0)
}

add_candidate_projects "${ROOT}/bindings"
add_candidate_projects "${ROOT}/src"

declare -a PROJECTS=()

for candidate in "${PROJECT_PATHS[@]}"; do
  [[ -f "${candidate}" ]] || continue
  if tr '[:upper:]' '[:lower:]' < "${candidate}" | tr -d '[:space:]' | grep -Fq "<ispackable>true</ispackable>"; then
    relative="${candidate#${ROOT}/}"
    relative="${relative//\\//}"
    PROJECTS+=("${relative}")
  fi
done

if ((${#PROJECTS[@]} == 0)); then
  echo "No packable managed projects found under 'bindings' or 'src'." >&2
fi

if ((${#PROJECTS[@]} > 0)); then
  IFS=$'\n' PROJECTS=($(printf '%s\n' "${PROJECTS[@]}" | sort))
  unset IFS
fi

add_extra_arg() {
  local value="$1"
  local array_name="$2"
  local existing

  eval "local current_values=(\"\${${array_name}[@]:-}\")"

  for existing in "${current_values[@]}"; do
    if [[ "${existing}" == "${value}" ]]; then
      return 0
    fi
  done

  eval "${array_name}+=(\"\${value}\")"
}

COMMON_ARGS=("-c" "Release" "-p:PackageOutputPath=${NUGET_OUTPUT_ABS}" "-p:EnableWindowsTargeting=true" "-p:VelloUseNativePackageDependencies=true")

if [[ -n "${NATIVE_FEED_ABS}" ]]; then
  COMMON_ARGS+=("-p:RestoreAdditionalProjectSources=${NATIVE_FEED_ABS}")
fi

for project in "${PROJECTS[@]}"; do
  full_path="${ROOT}/${project}"
  if [[ ! -f "${full_path}" ]]; then
    echo "Skipping missing project '${project}'."
    continue
  fi

  args=("${COMMON_ARGS[@]}")

  declare -a extra_args=()
  add_extra_arg "-p:VelloSkipNativeBuild=true" extra_args

  if [[ -f "${full_path}" ]]; then
    if grep -q 'VelloIncludeNativeAssets' "${full_path}"; then
      add_extra_arg "-p:VelloIncludeNativeAssets=false" extra_args
    fi

    if grep -q 'VelloRequireAllNativeAssets' "${full_path}"; then
      add_extra_arg "-p:VelloRequireAllNativeAssets=false" extra_args
    fi
  fi

  if [[ "${project}" == "bindings/VelloSharp.Gpu/VelloSharp.Gpu.csproj" ]]; then
    add_extra_arg "-p:VelloSkipNativeBuild=true" extra_args
    add_extra_arg "-p:VelloIncludeNativeAssets=false" extra_args
    add_extra_arg "-p:VelloRequireAllNativeAssets=false" extra_args
  fi

  if ((${#extra_args[@]} > 0)); then
    args+=("${extra_args[@]}")
  fi

  echo "Packing ${project}"
  dotnet pack "${full_path}" "${args[@]}"
done

echo "Managed packages created in '${NUGET_OUTPUT_ABS}'."
