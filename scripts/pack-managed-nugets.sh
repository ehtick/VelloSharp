#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NUGET_OUTPUT="${1:-${ROOT}/artifacts/nuget}"
NATIVE_FEED="${2:-${NUGET_OUTPUT}}"
export NATIVE_FEED

mkdir -p "${NUGET_OUTPUT}"

dotnet build "${ROOT}/VelloSharp.sln" -c Release -p:VelloSkipNativeBuild=true

# Ensure the local feed is available
if dotnet nuget list source | grep -q "VelloNativeLocal"; then
  dotnet nuget remove source VelloNativeLocal >/dev/null 2>&1 || true
fi
dotnet nuget add source "${NATIVE_FEED}" --name VelloNativeLocal >/dev/null

native_ids=$(python3 - <<'PY'
import glob
import os
import zipfile
import xml.etree.ElementTree as ET

feed = os.environ.get("NATIVE_FEED")
ids = set()
if feed:
    for path in glob.glob(os.path.join(feed, "VelloSharp.Native.*.nupkg")):
        try:
            with zipfile.ZipFile(path, 'r') as zf:
                for name in zf.namelist():
                    if name.endswith('.nuspec'):
                        root = ET.fromstring(zf.read(name))
                        elem = root.find('.//{*}id')
                        if elem is not None and elem.text:
                            ids.add(elem.text.strip())
                        break
        except zipfile.BadZipFile:
            continue
print(';'.join(sorted(ids)))
PY
)

if [[ -z "${native_ids}" ]]; then
  echo "No VelloSharp.Native packages found in '${NATIVE_FEED}'. Run scripts/pack-native-nugets.sh first or disable native package dependencies." >&2
  exit 1
fi

COMMON_PACK_ARGS=("-c" "Release" "-p:PackageOutputPath=${NUGET_OUTPUT}")

PACK_PROJECTS=(
  "bindings/VelloSharp.Core/VelloSharp.Core.csproj|"
  "bindings/VelloSharp.Ffi.Core/VelloSharp.Ffi.Core.csproj|"
  "bindings/VelloSharp.Ffi.Gpu/VelloSharp.Ffi.Gpu.csproj|"
  "bindings/VelloSharp.Ffi.Sparse/VelloSharp.Ffi.Sparse.csproj|"
  "bindings/VelloSharp.Text/VelloSharp.Text.csproj|-p:VelloSkipNativeBuild=true"
  "bindings/VelloSharp.Skia.Core/VelloSharp.Skia.Core.csproj|-p:VelloSkipNativeBuild=true"
  "bindings/VelloSharp.Skia.Gpu/VelloSharp.Skia.Gpu.csproj|-p:VelloSkipNativeBuild=true"
  "bindings/VelloSharp.Skia.Cpu/VelloSharp.Skia.Cpu.csproj|-p:VelloSkipNativeBuild=true"
  "bindings/VelloSharp.Gpu/VelloSharp.Gpu.csproj|-p:VelloSkipNativeBuild=true -p:VelloIncludeNativeAssets=false -p:VelloUseNativePackageDependencies=true -p:VelloRequireAllNativeAssets=false"
  "bindings/VelloSharp.Integration.Skia/VelloSharp.Integration.Skia.csproj|-p:VelloSkipNativeBuild=true"
  "bindings/VelloSharp/VelloSharp.csproj|-p:VelloSkipNativeBuild=true -p:VelloIncludeNativeAssets=false -p:VelloUseNativePackageDependencies=true -p:VelloRequireAllNativeAssets=false"
)

for entry in "${PACK_PROJECTS[@]}"; do
  IFS='|' read -r relpath extra <<<"${entry}"
  extra_args=()
  if [[ -n "${extra}" ]]; then
    read -r -a extra_args <<<"${extra}"
  fi

  if [[ "${relpath}" == "bindings/VelloSharp/VelloSharp.csproj" || "${relpath}" == "bindings/VelloSharp.Gpu/VelloSharp.Gpu.csproj" ]]; then
    extra_args+=("-p:VelloNativePackageIds=\"${native_ids}\"")
    extra_args+=("-p:RestoreAdditionalProjectSources=${NATIVE_FEED}")
  fi

  if (( ${#extra_args[@]} )); then
    dotnet pack "${ROOT}/${relpath}" "${COMMON_PACK_ARGS[@]}" "${extra_args[@]}"
  else
    dotnet pack "${ROOT}/${relpath}" "${COMMON_PACK_ARGS[@]}"
  fi
done

echo "Managed packages created in '${NUGET_OUTPUT}'."
