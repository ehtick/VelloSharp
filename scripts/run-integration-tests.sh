#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="Release"
FRAMEWORK=""
PLATFORM=""
RUN_MANAGED=1
RUN_NATIVE=1
python_exec=""

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

native_projects=()
if [[ -d "${ROOT}/integration/native" ]]; then
  while IFS= read -r project; do
    [[ -z "${project}" ]] && continue
    file="$(basename "${project}")"
    case "${PLATFORM}" in
      linux)
        if [[ "${file}" == *Linux* || "${file}" == *linux* ]]; then
          native_projects+=("${project}")
        fi
        ;;
      macos)
        if [[ "${file}" == *Osx* || "${file}" == *OSX* || "${file}" == *Mac* || "${file}" == *Ios* || "${file}" == *IOS* ]]; then
          native_projects+=("${project}")
        fi
        ;;
      windows)
        if [[ "${file}" == *Win* || "${file}" == *Windows* ]]; then
          native_projects+=("${project}")
        fi
        ;;
    esac
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
