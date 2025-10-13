#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
docfx_dir="$repo_root/docs/docfx"
docfx_json="$docfx_dir/docfx.json"
website_dir="$repo_root/docs/website"
generated_dir="$website_dir/generated/dotnet-api"
docfx_output_dir="$docfx_dir/obj/api"
api_index="$docfx_dir/api/index.md"

if [[ ! -f "$docfx_json" ]]; then
  echo "DocFX configuration not found at $docfx_json" >&2
  exit 1
fi

dotnet tool restore >/dev/null

echo "Generating DocFX metadata as Markdown..."
export DOCFX_MSBUILD_ARGS="/p:EnableWindowsTargeting=true"
dotnet tool run docfx metadata "$docfx_json" >/dev/null

rm -rf "$generated_dir"
mkdir -p "$generated_dir"

echo "Copying Markdown artifacts into docs/website/generated/dotnet-api..."
while IFS= read -r file; do
  rel_path="${file#$docfx_output_dir/}"
  dest_path="$generated_dir/$rel_path"
  mkdir -p "$(dirname "$dest_path")"
  cp "$file" "$dest_path"
done < <(find "$docfx_output_dir" -type f -name '*.md')

if [[ -f "$api_index" ]]; then
  cp "$api_index" "$generated_dir/index.md"
fi

echo "Dotnet API docs synced."
unset DOCFX_MSBUILD_ARGS
