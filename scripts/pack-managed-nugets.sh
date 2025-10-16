#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

log() {
  echo "$@" >&2
}

declare -a INCLUDE_FILTERS=()
declare -a EXCLUDE_FILTERS=()
declare -a POSITIONAL=()
PROFILE="all"
PRINT_PROJECTS="false"

declare -a WINDOWS_SPECIFIC_PROJECTS=(
  "bindings/VelloSharp.Integration.WinForms/VelloSharp.Integration.WinForms.csproj"
  "bindings/VelloSharp.Integration.Wpf/VelloSharp.Integration.Wpf.csproj"
  "bindings/VelloSharp.Maui/VelloSharp.Maui.csproj"
  "bindings/VelloSharp.Uno/VelloSharp.Uno.csproj"
  "bindings/VelloSharp.Uwp/VelloSharp.Uwp.csproj"
  "bindings/VelloSharp.WinForms.Core/VelloSharp.WinForms.Core.csproj"
  "bindings/VelloSharp.WinUI/VelloSharp.WinUI.csproj"
  "bindings/VelloSharp.Windows.Core/VelloSharp.Windows.Core.csproj"
  "src/VelloSharp.Charting.WinForms/VelloSharp.Charting.WinForms.csproj"
  "src/VelloSharp.Charting.Wpf/VelloSharp.Charting.Wpf.csproj"
  "src/VelloSharp.ChartRuntime.Windows/VelloSharp.ChartRuntime.Windows.csproj"
  "src/VelloSharp.Maui.Core/VelloSharp.Maui.Core.csproj"
  "src/VelloSharp.Windows.Shared/VelloSharp.Windows.Shared.csproj"
)

is_windows_specific() {
  local candidate="$1"
  local project
  for project in "${WINDOWS_SPECIFIC_PROJECTS[@]}"; do
    if [[ "${candidate}" == "${project}" ]]; then
      return 0
    fi
  done
  return 1
}

while (($# > 0)); do
  case "$1" in
    --include)
      shift
      if (($# == 0)); then
        echo "Missing value for --include option." >&2
        exit 1
      fi
      INCLUDE_FILTERS+=("$1")
      ;;
    --exclude)
      shift
      if (($# == 0)); then
        echo "Missing value for --exclude option." >&2
        exit 1
      fi
      EXCLUDE_FILTERS+=("$1")
      ;;
    --profile)
      shift
      if (($# == 0)); then
        echo "Missing value for --profile option." >&2
        exit 1
      fi
      case "$1" in
        linux|windows|all)
          PROFILE="$1"
          ;;
        *)
          echo "Unknown profile '$1'. Expected one of: linux, windows, all." >&2
          exit 1
          ;;
      esac
      ;;
    --print-projects)
      PRINT_PROJECTS="true"
      ;;
    --help)
      cat <<'EOF'
Usage: pack-managed-nugets.sh [nuget-output] [native-feed]
       [--profile linux|windows|all] [--print-projects]
       [--include pattern]... [--exclude pattern]...
EOF
      exit 0
      ;;
    --*)
      echo "Unknown option '$1'." >&2
      exit 1
      ;;
    *)
      POSITIONAL+=("$1")
      ;;
  esac
  shift
done

set -- "${POSITIONAL[@]:-}"

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

if ((${#PROJECTS[@]} > 0)); then
  IFS=$'\n' PROJECTS=($(printf '%s\n' "${PROJECTS[@]}" | sort))
  unset IFS
fi

if [[ "${PROFILE}" == "linux" ]]; then
  declare -a filtered=()
  for project in "${PROJECTS[@]}"; do
    if ! is_windows_specific "${project}"; then
      filtered+=("${project}")
    fi
  done
  PROJECTS=("${filtered[@]}")
elif [[ "${PROFILE}" == "windows" ]]; then
  declare -a filtered=()
  for project in "${PROJECTS[@]}"; do
    if is_windows_specific "${project}"; then
      filtered+=("${project}")
    fi
  done
  PROJECTS=("${filtered[@]}")
fi

if ((${#INCLUDE_FILTERS[@]} > 0)); then
  log "Applying include filters: ${INCLUDE_FILTERS[*]}"
  declare -a filtered=()
  for project in "${PROJECTS[@]}"; do
    for pattern in "${INCLUDE_FILTERS[@]}"; do
      if [[ "${project}" == ${pattern} ]]; then
        filtered+=("${project}")
        break
      fi
    done
  done
  PROJECTS=("${filtered[@]}")
fi

if ((${#EXCLUDE_FILTERS[@]} > 0)); then
  log "Applying exclude filters: ${EXCLUDE_FILTERS[*]}"
  declare -a filtered=()
  for project in "${PROJECTS[@]}"; do
    skip=false
    for pattern in "${EXCLUDE_FILTERS[@]}"; do
      if [[ "${project}" == ${pattern} ]]; then
        skip=true
        break
      fi
    done
    if [[ "${skip}" == "false" ]]; then
      filtered+=("${project}")
    fi
  done
  PROJECTS=("${filtered[@]}")
fi

if ((${#PROJECTS[@]} == 0)); then
  log "No packable managed projects matched the provided filters under 'bindings' or 'src'."
  if [[ "${PRINT_PROJECTS}" == "true" ]]; then
    exit 0
  fi
fi

if [[ "${PRINT_PROJECTS}" == "true" ]]; then
  if ((${#PROJECTS[@]} > 0)); then
    printf '%s\n' "${PROJECTS[@]}"
  fi
  exit 0
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

COMMON_ARGS=("-c" "Release" "-p:PackageOutputPath=${NUGET_OUTPUT_ABS}" "-p:VelloUseNativePackageDependencies=true" "-p:VelloNativePackagesAvailable=true")

if [[ -n "${NATIVE_FEED_ABS}" ]]; then
  COMMON_ARGS+=("-p:RestoreAdditionalProjectSources=${NATIVE_FEED_ABS}")
fi

maui_workload_restored="false"
browser_workload_restored="false"

restore_browser_workload_if_needed() {
  local project_path="$1"
  if [[ "${browser_workload_restored}" == "true" ]]; then
    return 0
  fi

  if grep -q "net8.0-browser" "${project_path}"; then
    if dotnet workload list | grep -q "wasm-tools-net8"; then
      log "Detected wasm-tools-net8 workload; skipping restore."
      browser_workload_restored="true"
      return 0
    fi

    log "Restoring workloads required for browser TFM (net8.0-browser)."
    if DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 \
         dotnet workload restore "${project_path}" --skip-manifest-update; then
      browser_workload_restored="true"
    else
      log "Warning: Failed to restore workloads automatically. Ensure 'wasm-tools-net8' is installed."
    fi
  fi
}

restore_maui_workload_if_needed() {
  local project_path="$1"
  if [[ "${maui_workload_restored}" == "true" ]]; then
    return 0
  fi

  if grep -q "<UseMaui" "${project_path}" || grep -q "<UseMauiCore" "${project_path}"; then
    log "Restoring workloads required for MAUI target frameworks."
    if DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 \
         dotnet workload restore "${project_path}" --skip-manifest-update; then
      maui_workload_restored="true"
    else
      log "Warning: Failed to restore MAUI workloads automatically. Ensure the required workloads are installed."
    fi
  fi
}

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

  if is_windows_specific "${project}"; then
    add_extra_arg "-p:EnableWindowsTargeting=true" extra_args
  fi

  restore_browser_workload_if_needed "${full_path}"
  restore_maui_workload_if_needed "${full_path}"

  if [[ "${project}" == "bindings/VelloSharp.Gpu/VelloSharp.Gpu.csproj" ]]; then
    add_extra_arg "-p:VelloSkipNativeBuild=true" extra_args
    add_extra_arg "-p:VelloIncludeNativeAssets=false" extra_args
    add_extra_arg "-p:VelloRequireAllNativeAssets=false" extra_args
  fi

  if ((${#extra_args[@]} > 0)); then
    args+=("${extra_args[@]}")
  fi

  log "Packing ${project}"
  dotnet pack "${full_path}" "${args[@]}"
done

log "Managed packages created in '${NUGET_OUTPUT_ABS}'."
