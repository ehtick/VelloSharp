#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
website_dir="$(cd "$script_dir/../docs/website" && pwd)"
node_script="$website_dir/scripts/sync-dotnet-api-docs.mjs"

if [[ ! -f "$node_script" ]]; then
  echo "Sync script not found at $node_script" >&2
  exit 1
fi

pushd "$website_dir" >/dev/null
node "$node_script"
popd >/dev/null
