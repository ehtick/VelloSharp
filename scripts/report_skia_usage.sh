#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
AVALONIA_DIR="$ROOT_DIR/extern/Avalonia"
OUTPUT_FILE="${1:-}"

if [[ ! -d "$AVALONIA_DIR" ]]; then
  echo "Avalonia submodule directory not found at $AVALONIA_DIR" >&2
  exit 1
fi

SEARCH_DIRS=(
  "$AVALONIA_DIR/src/Skia"
  "$AVALONIA_DIR/tests/Avalonia.Skia.UnitTests"
  "$AVALONIA_DIR/tests/Avalonia.Skia.RenderTests"
)

TOKENS_FILE=$(mktemp)
rg --no-heading --no-line-number --no-filename --color=never \
  --glob '*.cs' --glob '*.fs' --glob '*.csproj' \
  -o 'SK[A-Za-z0-9_]+' "${SEARCH_DIRS[@]}" \
  | sort | uniq > "$TOKENS_FILE"

tmp=$(mktemp)
{
  echo "SkiaApiToken,Occurrences,SampleLocation"
  while IFS= read -r token; do
    [[ -z "$token" ]] && continue
    count=$(rg --glob '*.cs' --glob '*.fs' --glob '*.csproj' -c --fixed-strings "$token" "${SEARCH_DIRS[@]}" | awk -F: '{s+=$2} END{print s+0}')
    sample=$(rg --glob '*.cs' --glob '*.fs' --glob '*.csproj' -n --fixed-strings "$token" "${SEARCH_DIRS[@]}" | head -n1)
    sample_file=$(echo "$sample" | cut -d: -f1)
    echo "$token,$count,$sample_file"
  done < "$TOKENS_FILE"
} > "$tmp"

rm "$TOKENS_FILE"

if [[ -n "$OUTPUT_FILE" ]]; then
  mv "$tmp" "$OUTPUT_FILE"
  echo "Skia API usage report written to $OUTPUT_FILE"
else
  cat "$tmp"
  rm "$tmp"
fi
