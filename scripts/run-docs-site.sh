#!/usr/bin/env bash

set -euo pipefail

port=3000
no_sync=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --no-sync)
      no_sync=1
      shift
      ;;
    --port)
      port="$2"
      shift 2
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
website_dir="$repo_root/docs/website"

if [[ ! -d "$website_dir" ]]; then
  echo "Docusaurus site not found at $website_dir" >&2
  exit 1
fi

cd "$website_dir"
echo "Starting Docusaurus dev server on http://localhost:${port}..."

if [[ $no_sync -eq 1 ]]; then
  SKIP_DOTNET_API_SYNC=1 npm run start -- --port "$port"
else
  npm run start -- --port "$port"
fi
