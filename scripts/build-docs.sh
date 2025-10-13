#!/usr/bin/env bash
set -euo pipefail

NO_SYNC=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --no-sync)
      NO_SYNC=1
      shift
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WEBSITE_DIR="${ROOT}/docs/website"

if [[ ! -d "${WEBSITE_DIR}" ]]; then
  echo "Docusaurus site not found at ${WEBSITE_DIR}" >&2
  exit 1
fi

cd "${WEBSITE_DIR}"
if [[ ${NO_SYNC} -eq 1 ]]; then
  SKIP_DOTNET_API_SYNC=1 npm run build
else
  npm run build
fi
