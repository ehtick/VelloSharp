#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NUGET_OUTPUT="${1:-${ROOT}/artifacts/nuget}"
NATIVE_FEED="${2:-${NUGET_OUTPUT}}"

mkdir -p "${NUGET_OUTPUT}"
NUGET_OUTPUT_ABS="$(cd "${NUGET_OUTPUT}" && pwd)"
NATIVE_FEED_ABS="$(cd "${NATIVE_FEED}" && pwd)"

declare -a PROJECTS=(
  "bindings/VelloSharp.Core/VelloSharp.Core.csproj"
  "bindings/VelloSharp.Ffi.Core/VelloSharp.Ffi.Core.csproj"
  "bindings/VelloSharp.Ffi.Gpu/VelloSharp.Ffi.Gpu.csproj"
  "bindings/VelloSharp.Ffi.Sparse/VelloSharp.Ffi.Sparse.csproj"
  "bindings/VelloSharp.Text/VelloSharp.Text.csproj"
  "bindings/VelloSharp.Skia.Core/VelloSharp.Skia.Core.csproj"
  "bindings/VelloSharp.Skia.Gpu/VelloSharp.Skia.Gpu.csproj"
  "bindings/VelloSharp.Skia.Cpu/VelloSharp.Skia.Cpu.csproj"
  "bindings/VelloSharp.Gpu/VelloSharp.Gpu.csproj"
  "bindings/VelloSharp.Integration.Skia/VelloSharp.Integration.Skia.csproj"
  "bindings/VelloSharp/VelloSharp.csproj"
  "src/VelloSharp.Composition/VelloSharp.Composition.csproj"
  "src/VelloSharp.ChartData/VelloSharp.ChartData.csproj"
  "src/VelloSharp.ChartDiagnostics/VelloSharp.ChartDiagnostics.csproj"
  "src/VelloSharp.ChartRuntime/VelloSharp.ChartRuntime.csproj"
  "src/VelloSharp.ChartRuntime.Windows/VelloSharp.ChartRuntime.Windows.csproj"
  "src/VelloSharp.ChartEngine/VelloSharp.ChartEngine.csproj"
  "src/VelloSharp.Charting/VelloSharp.Charting.csproj"
  "src/VelloSharp.Charting.Avalonia/VelloSharp.Charting.Avalonia.csproj"
  "src/VelloSharp.Charting.WinForms/VelloSharp.Charting.WinForms.csproj"
  "src/VelloSharp.Charting.Wpf/VelloSharp.Charting.Wpf.csproj"
  "src/VelloSharp.Gauges/VelloSharp.Gauges.csproj"
  "src/VelloSharp.TreeDataGrid/VelloSharp.TreeDataGrid.csproj"
  "src/VelloSharp.Editor/VelloSharp.Editor.csproj"
  "src/VelloSharp.Scada/VelloSharp.Scada.csproj"
)

declare -A EXTRA_ARGS=()
EXTRA_ARGS["bindings/VelloSharp.Text/VelloSharp.Text.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["bindings/VelloSharp.Skia.Core/VelloSharp.Skia.Core.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["bindings/VelloSharp.Skia.Gpu/VelloSharp.Skia.Gpu.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["bindings/VelloSharp.Skia.Cpu/VelloSharp.Skia.Cpu.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["bindings/VelloSharp.Integration.Skia/VelloSharp.Integration.Skia.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["src/VelloSharp.Composition/VelloSharp.Composition.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["src/VelloSharp.ChartData/VelloSharp.ChartData.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["src/VelloSharp.ChartDiagnostics/VelloSharp.ChartDiagnostics.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["src/VelloSharp.ChartRuntime/VelloSharp.ChartRuntime.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["src/VelloSharp.ChartRuntime.Windows/VelloSharp.ChartRuntime.Windows.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["src/VelloSharp.ChartEngine/VelloSharp.ChartEngine.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["src/VelloSharp.Charting/VelloSharp.Charting.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["src/VelloSharp.Charting.Avalonia/VelloSharp.Charting.Avalonia.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["src/VelloSharp.Charting.WinForms/VelloSharp.Charting.WinForms.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["src/VelloSharp.Charting.Wpf/VelloSharp.Charting.Wpf.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["src/VelloSharp.Gauges/VelloSharp.Gauges.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["src/VelloSharp.TreeDataGrid/VelloSharp.TreeDataGrid.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["src/VelloSharp.Editor/VelloSharp.Editor.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["src/VelloSharp.Scada/VelloSharp.Scada.csproj"]="-p:VelloSkipNativeBuild=true"
EXTRA_ARGS["bindings/VelloSharp.Gpu/VelloSharp.Gpu.csproj"]="-p:VelloSkipNativeBuild=true -p:VelloIncludeNativeAssets=false -p:VelloRequireAllNativeAssets=false"
EXTRA_ARGS["bindings/VelloSharp/VelloSharp.csproj"]="-p:VelloSkipNativeBuild=true -p:VelloIncludeNativeAssets=false -p:VelloRequireAllNativeAssets=false"

COMMON_ARGS=("-c" "Release" "-p:PackageOutputPath=${NUGET_OUTPUT_ABS}" "-p:EnableWindowsTargeting=true")

for project in "${PROJECTS[@]}"; do
  full_path="${ROOT}/${project}"
  if [[ ! -f "${full_path}" ]]; then
    echo "Skipping missing project '${project}'."
    continue
  }

  args=("${COMMON_ARGS[@]}")

  extra="${EXTRA_ARGS[${project}]-}"
  if [[ -n "${extra}" ]]; then
    read -r -a extra_array <<< "${extra}"
    args+=("${extra_array[@]}")
  fi

  echo "Packing ${project}"
  dotnet pack "${full_path}" "${args[@]}"
done

echo "Managed packages created in '${NUGET_OUTPUT_ABS}'."
