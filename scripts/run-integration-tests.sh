#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="Release"
FRAMEWORK=""
PLATFORM=""
RUN_MANAGED=1
RUN_NATIVE=1
python_exec=""
HOST_ARCH=""
MANAGED_RUNTIME_IDENTIFIER=""
IS_CI=0

if [[ -z "${NUGET_PACKAGES:-}" ]]; then
  export NUGET_PACKAGES="${ROOT}/artifacts/packages"
fi
mkdir -p "${NUGET_PACKAGES}"

if [[ -z "${DOTNET_RESTORE_SOURCES:-}" ]]; then
  export DOTNET_RESTORE_SOURCES="${ROOT}/artifacts/nuget;https://api.nuget.org/v3/index.json"
fi

usage() {
  cat <<'EOF'
Usage: scripts/run-integration-tests.sh [options]

Options:
  -c, --configuration <value>  Build configuration passed to dotnet (default: Release)
  -f, --framework <value>      Target framework to run (passed to dotnet -f)
  -p, --platform <value>       Platform filter: linux, macos, or windows (defaults to host OS)
      --managed-only           Run managed integration projects only
      --native-only            Run native integration projects only
  -h, --help                   Show this help text
EOF
}

to_lower() {
  printf '%s' "$1" | tr '[:upper:]' '[:lower:]'
}

detect_host_architecture() {
  local machine
  machine="$(uname -m 2>/dev/null || echo "")"
  machine="$(to_lower "${machine}")"
  if [[ -z "${machine}" && -n "${PROCESSOR_ARCHITECTURE:-}" ]]; then
    machine="$(to_lower "${PROCESSOR_ARCHITECTURE}")"
  fi
  case "${machine}" in
    x86_64|amd64)
      echo "x64"
      ;;
    arm64|aarch64)
      echo "arm64"
      ;;
    *)
      echo ""
      ;;
  esac
}

is_ci_environment() {
  [[ -n "${CI:-}" || -n "${TF_BUILD:-}" || -n "${GITHUB_ACTIONS:-}" ]]
}

latest_matching_file() {
  local pattern="$1"
  ensure_python
  local result
  if ! result="$("${python_exec}" - "$pattern" <<'PY'
import glob
import os
import sys

pattern = sys.argv[1]
matches = glob.glob(pattern)
if not matches:
    sys.exit(1)
latest = max(matches, key=os.path.getmtime)
print(latest)
PY
  )"; then
    return 1
  fi
  printf '%s\n' "${result}"
}

resolve_runtime_identifier() {
  local platform="$1"
  local arch="$2"
  case "${platform}" in
    linux)
      if [[ "${arch}" == "arm64" ]]; then
        echo "linux-arm64"
      else
        echo "linux-x64"
      fi
      ;;
    macos)
      if [[ "${arch}" == "arm64" ]]; then
        echo "osx-arm64"
      else
        echo "osx-x64"
      fi
      ;;
    windows)
      if [[ "${arch}" == "arm64" ]]; then
        echo "win-arm64"
      else
        echo "win-x64"
      fi
      ;;
    *)
      echo ""
      ;;
  esac
}

ensure_native_payload_for_rid() {
  local rid="$1"
  if [[ -z "${rid}" ]]; then
    return
  fi

  local target=""
  local native_filename=""
  local build_script=""
  local -a build_args=()

  case "${rid}" in
    linux-x64)
      target="x86_64-unknown-linux-gnu"
      native_filename="libvello_gauges_core.so"
      build_script="${ROOT}/scripts/build-native-linux.sh"
      build_args=("${target}" "release" "${rid}")
      ;;
    linux-arm64)
      target="aarch64-unknown-linux-gnu"
      native_filename="libvello_gauges_core.so"
      build_script="${ROOT}/scripts/build-native-linux.sh"
      build_args=("${target}" "release" "${rid}")
      ;;
    osx-x64)
      target="x86_64-apple-darwin"
      native_filename="libvello_gauges_core.dylib"
      build_script="${ROOT}/scripts/build-native-macos.sh"
      build_args=("${target}" "release" "" "${rid}")
      ;;
    osx-arm64)
      target="aarch64-apple-darwin"
      native_filename="libvello_gauges_core.dylib"
      build_script="${ROOT}/scripts/build-native-macos.sh"
      build_args=("${target}" "release" "" "${rid}")
      ;;
    *)
      native_filename=""
      ;;
  esac

  if [[ -z "${native_filename}" ]]; then
    return
  fi

  local runtime_dir="${ROOT}/artifacts/runtimes/${rid}/native"
  local native_lib="${runtime_dir}/${native_filename}"
  local built_native=0

  if [[ ! -f "${native_lib}" ]]; then
    echo "Native gauges payload not found at '${native_lib}'. Building native assets for ${rid}."
    if [[ -z "${build_script}" || ! -x "${build_script}" ]]; then
      echo "Required build script '${build_script}' is missing or not executable." >&2
      exit 1
    fi
    "${build_script}" "${build_args[@]}"
    built_native=1
  fi

  local native_pattern="${ROOT}/artifacts/nuget/VelloSharp.Native.Gauges.${rid}."*
  local native_package=""
  if native_package=$(latest_matching_file "${native_pattern}"); then
    :
  else
    native_package=""
  fi

  local should_pack_native=0
  if [[ -z "${native_package}" || ${built_native} -eq 1 ]]; then
    should_pack_native=1
  fi

  if (( should_pack_native )); then
    if [[ ! -x "${ROOT}/scripts/pack-native-nugets.sh" ]]; then
      echo "Native packaging script '${ROOT}/scripts/pack-native-nugets.sh' is missing or not executable." >&2
      exit 1
    fi
    echo "Packing native NuGet payloads (RID: ${rid})."
    "${ROOT}/scripts/pack-native-nugets.sh"
    if native_package=$(latest_matching_file "${native_pattern}"); then
      :
    else
      echo "Failed to produce native packages for RID '${rid}'." >&2
      exit 1
    fi
  fi

  printf '%s\n' "${native_package}"
}

run_pack_managed_packages() {
  if [[ ! -x "${ROOT}/scripts/pack-managed-nugets.sh" ]]; then
    echo "Managed packaging script '${ROOT}/scripts/pack-managed-nugets.sh' is missing or not executable." >&2
    exit 1
  fi
  echo "Packing managed NuGet payloads to include updated native dependencies."
  "${ROOT}/scripts/pack-managed-nugets.sh"

  if [[ -n "${NUGET_PACKAGES:-}" && -d "${NUGET_PACKAGES}" ]]; then
    echo "Clearing cached VelloSharp packages from '${NUGET_PACKAGES}' to avoid stale payloads."
    shopt -s nullglob
    for cache_dir in "${NUGET_PACKAGES}"/vellosharp* "${NUGET_PACKAGES}"/VelloSharp*; do
      [[ -d "${cache_dir}" ]] || continue
      rm -rf "${cache_dir}"
    done
    shopt -u nullglob
  fi
}

ensure_native_payloads() {
  local -a required_rids=()
  local rid=""

  if [[ ${RUN_MANAGED} -eq 1 ]]; then
    rid="$(resolve_runtime_identifier "${PLATFORM}" "${HOST_ARCH}")"
    if [[ -n "${rid}" ]]; then
      MANAGED_RUNTIME_IDENTIFIER="${rid}"
      required_rids+=("${rid}")
    fi
  fi

  if [[ ${RUN_NATIVE} -eq 1 && -d "${ROOT}/integration/native" ]]; then
    shopt -s nullglob
    for native_dir in "${ROOT}/integration/native"/*; do
      [[ -d "${native_dir}" ]] || continue
      local native_name
      native_name="$(basename "${native_dir}")"
      local native_lower
      native_lower="$(to_lower "${native_name}")"
      if [[ -n "${HOST_ARCH}" ]]; then
        if ! native_project_matches_arch "${native_lower}" "${HOST_ARCH}"; then
          continue
        fi
      fi
      case "${PLATFORM}" in
        linux)
          [[ "${native_lower}" == linux* ]] || continue
          ;;
        macos)
          [[ "${native_lower}" == osx* || "${native_lower}" == ios* ]] || continue
          ;;
        windows)
          [[ "${native_lower}" == win* ]] || continue
          ;;
        *)
          continue
          ;;
      esac
      required_rids+=("${native_lower}")
    done
    shopt -u nullglob
  fi

  if [[ ${#required_rids[@]} -eq 0 ]]; then
    return
  fi

  local seen_rids=""
  local latest_native_package=""
  local latest_native_time=0
  for rid in "${required_rids[@]}"; do
    if [[ " ${seen_rids} " == *" ${rid} "* ]]; then
      continue
    fi
    seen_rids+=" ${rid}"
    local native_package
    native_package="$(ensure_native_payload_for_rid "${rid}")"
    if [[ -n "${native_package}" && -f "${native_package}" ]]; then
      ensure_python
      local pkg_time
      pkg_time=$("${python_exec}" -c 'import os, sys; print(int(os.path.getmtime(sys.argv[1])));' "${native_package}")
      if (( pkg_time > latest_native_time )); then
        latest_native_time=${pkg_time}
        latest_native_package="${native_package}"
      fi
    fi
  done

  if [[ ${RUN_MANAGED} -ne 1 ]]; then
    return
  fi

  local managed_pattern="${ROOT}/artifacts/nuget/VelloSharp.Gauges."*
  local managed_package=""
  if managed_package=$(latest_matching_file "${managed_pattern}"); then
    :
  else
    managed_package=""
  fi

  local should_pack_managed=0
  if [[ -z "${managed_package}" ]]; then
    should_pack_managed=1
  elif [[ -n "${latest_native_package}" && -n "${managed_package}" ]]; then
    ensure_python
    local native_time managed_time
    native_time=$("${python_exec}" -c 'import os, sys; print(int(os.path.getmtime(sys.argv[1])));' "${latest_native_package}")
    managed_time=$("${python_exec}" -c 'import os, sys; print(int(os.path.getmtime(sys.argv[1])));' "${managed_package}")
    if (( native_time > managed_time )); then
      should_pack_managed=1
    fi
  fi

  if (( should_pack_managed )); then
    run_pack_managed_packages
  fi
}

ensure_required_managed_packages() {
  if [[ ${RUN_MANAGED} -ne 1 || ${#managed_projects[@]} -eq 0 ]]; then
    return
  fi
  ensure_python
  local required_output
  required_output="$("${python_exec}" - "${managed_projects[@]}" <<'PY'
import sys
import xml.etree.ElementTree as ET

packages = set()
for path in sys.argv[1:]:
    try:
        tree = ET.parse(path)
    except Exception:
        continue
    root = tree.getroot()
    for element in root.findall(".//PackageReference"):
        include = element.attrib.get("Include")
        if not include:
            continue
        if include.startswith("VelloSharp.Native"):
            continue
        if include.startswith("VelloSharp"):
            packages.add(include)

for name in sorted(packages):
    print(name)
PY
)"

  local -a required_packages=()
  if [[ -n "${required_output}" ]]; then
    while IFS= read -r line; do
      [[ -z "${line}" ]] && continue
      required_packages+=("${line}")
    done <<< "${required_output}"
  fi

  if [[ ${#required_packages[@]} -eq 0 ]]; then
    return
  fi

  local -a missing_packages=()
  local pkg=""
  for pkg in "${required_packages[@]}"; do
    local pattern="${ROOT}/artifacts/nuget/${pkg}."*
    if ! latest_matching_file "${pattern}" >/dev/null 2>&1; then
      missing_packages+=("${pkg}")
    fi
  done

  if [[ ${#missing_packages[@]} -eq 0 ]]; then
    return
  fi

  run_pack_managed_packages

  local -a still_missing=()
  for pkg in "${required_packages[@]}"; do
    local pattern="${ROOT}/artifacts/nuget/${pkg}."*
    if ! latest_matching_file "${pattern}" >/dev/null 2>&1; then
      still_missing+=("${pkg}")
    fi
  done

  if [[ ${#still_missing[@]} -ne 0 ]]; then
    echo "Failed to produce required managed packages: ${still_missing[*]}" >&2
    exit 1
  fi
}

native_project_matches_arch() {
  local name
  local arch
  name="$(to_lower "$1")"
  arch="$2"
  case "${arch}" in
    x64)
      [[ "${name}" == *x64* || "${name}" == *x86_64* || "${name}" == *amd64* ]]
      ;;
    arm64)
      [[ "${name}" == *arm64* || "${name}" == *aarch64* ]]
      ;;
    *)
      return 0
      ;;
  esac
}

ensure_python() {
  if [[ -n "${python_exec}" ]]; then
    return
  fi
  if command -v python3 >/dev/null 2>&1; then
    python_exec="python3"
  elif command -v python >/dev/null 2>&1; then
    python_exec="python"
  else
    echo "Python 3 is required to enumerate integration projects." >&2
    exit 1
  fi
}

collect_projects() {
  ensure_python
  local search_dir="$1"
  "${python_exec}" - "$search_dir" <<'PY'
import os
import sys

root = sys.argv[1]
if not os.path.isdir(root):
    sys.exit(0)

projects = []
for dirpath, _, filenames in os.walk(root):
    for name in filenames:
        if name.endswith(".csproj"):
            projects.append(os.path.abspath(os.path.join(dirpath, name)))

for path in sorted(projects):
    print(path)
PY
}

project_supports_platform() {
  ensure_python
  local project="$1"
  local platform="$2"
  local value
  value="$("${python_exec}" - "${project}" <<'PY'
import sys
import xml.etree.ElementTree as ET

path = sys.argv[1]
try:
    tree = ET.parse(path)
except Exception:
    sys.exit(0)

root = tree.getroot()
value = None
for group in root.findall('PropertyGroup'):
    element = group.find('SupportedIntegrationPlatforms')
    if element is not None and element.text and element.text.strip():
        value = element.text.strip()
        break

if value:
    print(value)
PY
)"

  if [[ -z "${value}" ]]; then
    return 0
  fi

  local normalized
  normalized="${value//,/;}"
  normalized="${normalized// /}"

  IFS=';' read -ra tokens <<< "${normalized}"
  for token in "${tokens[@]}"; do
    [[ -z "${token}" ]] && continue
    local lower
    lower="$(to_lower "${token}")"
    if [[ "${lower}" == "all" || "${lower}" == "${platform}" ]]; then
      return 0
    fi
  done

  return 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -c|--configuration)
      [[ $# -ge 2 ]] || { echo "Missing value for $1" >&2; exit 1; }
      CONFIGURATION="$2"
      shift 2
      ;;
    -f|--framework)
      [[ $# -ge 2 ]] || { echo "Missing value for $1" >&2; exit 1; }
      FRAMEWORK="$2"
      shift 2
      ;;
    -p|--platform)
      [[ $# -ge 2 ]] || { echo "Missing value for $1" >&2; exit 1; }
      PLATFORM="$(to_lower "$2")"
      shift 2
      ;;
    --managed-only)
      RUN_NATIVE=0
      shift
      ;;
    --native-only)
      RUN_MANAGED=0
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if [[ ${RUN_MANAGED} -eq 0 && ${RUN_NATIVE} -eq 0 ]]; then
  echo "Both managed and native test execution disabled. Nothing to do." >&2
  exit 1
fi

if [[ -z "${PLATFORM}" ]]; then
  case "$(uname -s)" in
    Linux) PLATFORM="linux" ;;
    Darwin) PLATFORM="macos" ;;
    MINGW*|MSYS*|CYGWIN*|Windows_NT) PLATFORM="windows" ;;
    *) echo "Unsupported host platform: $(uname -s)" >&2; exit 1 ;;
  esac
fi

case "${PLATFORM}" in
  linux|macos|windows) ;;
  *)
    echo "Unsupported platform '${PLATFORM}'. Expected linux, macos, or windows." >&2
    exit 1
    ;;
esac

managed_projects=()
HOST_ARCH="$(detect_host_architecture)"
ensure_native_payloads
if is_ci_environment; then
  IS_CI=1
fi

if [[ -d "${ROOT}/integration/managed" ]]; then
  while IFS= read -r project; do
    [[ -n "${project}" ]] && managed_projects+=("${project}")
  done < <(collect_projects "${ROOT}/integration/managed")
fi

if [[ "${PLATFORM}" == "windows" && -d "${ROOT}/integration/windows" ]]; then
  while IFS= read -r project; do
    [[ -n "${project}" ]] && managed_projects+=("${project}")
  done < <(collect_projects "${ROOT}/integration/windows")
fi

if [[ ${IS_CI} -eq 1 && ${#managed_projects[@]} -gt 0 ]]; then
  filtered_managed=()
  for project in "${managed_projects[@]}"; do
    if [[ "${project}" == *"VelloSharp.Uno.Integration"* ]]; then
      rel="${project#"${ROOT}/"}"
      echo "Skipping integration project '${rel}' (temporarily disabled on CI)."
      continue
    fi
    filtered_managed+=("${project}")
  done
  managed_projects=("${filtered_managed[@]}")
fi

ensure_required_managed_packages

native_projects=()
if [[ -d "${ROOT}/integration/native" ]]; then
  while IFS= read -r project; do
    [[ -z "${project}" ]] && continue
    dir="$(basename "$(dirname "${project}")")"
    dir_lower="$(to_lower "${dir}")"
    case "${PLATFORM}" in
      linux)
        if [[ "${dir_lower}" != linux* ]]; then
          continue
        fi
        ;;
      macos)
        if [[ "${dir_lower}" != osx* && "${dir_lower}" != ios* ]]; then
          continue
        fi
        ;;
      windows)
        if [[ "${dir_lower}" != win* ]]; then
          continue
        fi
        ;;
    esac
    if [[ -n "${HOST_ARCH}" ]]; then
      if ! native_project_matches_arch "${dir_lower}" "${HOST_ARCH}"; then
        rel="${project#"${ROOT}/"}"
        echo "Skipping native integration project '${rel}' (host architecture ${HOST_ARCH} not supported)."
        continue
      fi
    fi
    native_projects+=("${project}")
  done < <(collect_projects "${ROOT}/integration/native")
fi

run_project() {
  local project="$1"
  local rel="${project#"${ROOT}/"}"
  echo "Running integration project: ${rel}"
  local args=(dotnet run --project "${project}" -c "${CONFIGURATION}")
  if [[ -n "${FRAMEWORK}" ]]; then
    args+=(-f "${FRAMEWORK}")
  fi
  if [[ -n "${MANAGED_RUNTIME_IDENTIFIER}" ]]; then
    args+=(-p:RuntimeIdentifier="${MANAGED_RUNTIME_IDENTIFIER}")
  fi
  "${args[@]}"
}

echo "Executing integration tests for platform '${PLATFORM}' (configuration: ${CONFIGURATION})"

if [[ ${RUN_MANAGED} -eq 1 ]]; then
  if [[ ${#managed_projects[@]} -eq 0 ]]; then
    echo "No managed integration projects found under integration/managed." >&2
  else
    for project in "${managed_projects[@]}"; do
      if ! project_supports_platform "${project}" "${PLATFORM}"; then
        rel="${project#"${ROOT}/"}"
        echo "Skipping integration project '${rel}' (unsupported on ${PLATFORM})."
        continue
      fi
      run_project "${project}"
    done
  fi
fi

if [[ ${RUN_NATIVE} -eq 1 ]]; then
  if [[ ${#native_projects[@]} -eq 0 ]]; then
    echo "No native integration projects matched the '${PLATFORM}' filter." >&2
  else
    for project in "${native_projects[@]}"; do
      run_project "${project}"
    done
  fi
fi

echo "Integration test run completed."
