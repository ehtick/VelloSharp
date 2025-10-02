#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NUGET_OUTPUT="${1:-${ROOT}/artifacts/nuget}"
NATIVE_FEED="${2:-${NUGET_OUTPUT}}"

mkdir -p "${NUGET_OUTPUT}"

dotnet build "${ROOT}/VelloSharp.sln" -c Release -p:VelloSkipNativeBuild=true

# Ensure the local feed is available
if dotnet nuget list source | grep -q "VelloNativeLocal"; then
  dotnet nuget remove source VelloNativeLocal >/dev/null 2>&1 || true
fi
dotnet nuget add source "${NATIVE_FEED}" --name VelloNativeLocal >/dev/null

dotnet pack "${ROOT}/bindings/VelloSharp/VelloSharp.csproj" \
  -c Release \
  -p:VelloSkipNativeBuild=true \
  -p:VelloIncludeNativeAssets=false \
  -p:VelloUseNativePackageDependencies=true \
  -p:VelloRequireAllNativeAssets=false \
  -p:RestoreAdditionalProjectSources="${NATIVE_FEED}" \
  -p:PackageOutputPath="${NUGET_OUTPUT}"

echo "Managed packages created in '${NUGET_OUTPUT}'."
