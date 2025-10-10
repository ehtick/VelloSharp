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
IS_CI=0

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
    if [[ ${IS_CI} -eq 1 && -n "${HOST_ARCH}" ]]; then
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
