#!/usr/bin/env bash
set -euo pipefail

PORT=3000
NO_SYNC=""
EXTRA_ARGS=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --no-sync)
      NO_SYNC="--no-sync"
      shift
      ;;
    -p|--port)
      if [[ $# -lt 2 ]]; then
        echo "Error: --port expects a value." >&2
        exit 1
      fi
      PORT="$2"
      shift 2
      ;;
    *)
      EXTRA_ARGS=1
      shift
      ;;
  esac
done

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUN_DOCS_SCRIPT="${ROOT}/scripts/run-docs-site.sh"

[[ -z "${PORT:-}" ]] && PORT=3000
[[ -z "${NO_SYNC:-}" ]] && NO_SYNC=""

if [[ -n "${EXTRA_ARGS:-}" ]]; then
  echo "Warning: extra DocFX arguments are no longer supported and will be ignored." >&2
fi

echo "DocFX preview has been replaced by the Docusaurus site. Redirecting to run-docs-site.sh" >&2
"${RUN_DOCS_SCRIPT}" ${NO_SYNC} --port "${PORT}"
