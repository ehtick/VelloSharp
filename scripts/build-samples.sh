#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
FRAMEWORK=""
DOTNET_ARGS=()

usage() {
  cat <<'EOF'
Usage: build-samples.sh [options] [-- additional dotnet args]

Options:
  -c, --configuration <CONFIG>   Build configuration (defaults to Release or $CONFIGURATION)
  -f, --framework <TFM>          Optional target framework passed to dotnet build
  -h, --help                     Show this help text

All remaining arguments after `--` are forwarded to `dotnet build`.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -c|--configuration)
      [[ $# -lt 2 ]] && { echo "Missing value for $1" >&2; exit 1; }
      CONFIGURATION="$2"
      shift 2
      ;;
    -f|--framework)
      [[ $# -lt 2 ]] && { echo "Missing value for $1" >&2; exit 1; }
      FRAMEWORK="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    --)
      shift
      DOTNET_ARGS+=("$@")
      break
      ;;
    *)
      DOTNET_ARGS+=("$1")
      shift
      ;;
  esac
done

platform="unknown"
uname_s="$(uname -s 2>/dev/null || echo "")"
case "${uname_s,,}" in
  linux*)
    platform="linux"
    ;;
  darwin*)
    platform="macos"
    ;;
  mingw*|msys*|cygwin*|windows_nt)
    platform="windows"
    ;;
esac

supports_sample() {
  local project_path="$1"
  local rel="${project_path#"${ROOT}/"}"

  case "${rel}" in
    samples/*WinForms*|samples/*Wpf*|samples/*WinAppSdk*|samples/*Win32*|samples/WinUIVelloGallery/*|samples/UwpVelloGallery/*)
      [[ "${platform}" == "windows" ]] || return 1
      ;;
    samples/MauiVelloGallery/*)
      [[ "${platform}" == "windows" ]] || return 1
      ;;
    samples/*X11Demo*)
      [[ "${platform}" == "linux" ]] || return 1
      ;;
  esac

  return 0
}

projects=()
while IFS= read -r -d '' project; do
  projects+=("${project}")
done < <(find "${ROOT}/samples" -type f -name '*.csproj' -print0)

if [[ "${#projects[@]}" -eq 0 ]]; then
  echo "No sample projects found under samples/." >&2
  exit 1
fi

echo "Building sample projects (configuration: ${CONFIGURATION})"

for project in "${projects[@]}"; do
  supports_sample "${project}" || {
    rel="${project#"${ROOT}/"}"
    echo "Skipping ${rel} (unsupported on ${platform})."
    continue
  }

  rel="${project#"${ROOT}/"}"
  echo "Building ${rel}"
  args=(dotnet build "${project}" -c "${CONFIGURATION}")
  if [[ -n "${FRAMEWORK}" ]]; then
    args+=(-f "${FRAMEWORK}")
  elif [[ "${platform}" == "windows" && "${rel}" == "samples/MauiVelloGallery/MauiVelloGallery.csproj" ]]; then
    args+=(-f "net8.0-windows10.0.19041")
  fi
  args+=("${DOTNET_ARGS[@]}")
  "${args[@]}"
done

echo "Sample builds completed."



