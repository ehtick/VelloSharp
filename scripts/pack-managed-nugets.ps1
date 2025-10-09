[CmdletBinding()]
param(
    [string]$NuGetOutput,
    [string]$NativeFeed
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootPath = (Resolve-Path (Join-Path $scriptRoot '..')).Path

if ([string]::IsNullOrWhiteSpace($NuGetOutput)) {
    $NuGetOutput = Join-Path $rootPath 'artifacts/nuget'
}
$NuGetOutput = [System.IO.Path]::GetFullPath($NuGetOutput)

if ([string]::IsNullOrWhiteSpace($NativeFeed)) {
    $NativeFeed = $NuGetOutput
}
$NativeFeed = [System.IO.Path]::GetFullPath($NativeFeed)

New-Item -ItemType Directory -Force -Path $NuGetOutput | Out-Null

$projects = @(
    'bindings/VelloSharp.Core/VelloSharp.Core.csproj',
    'bindings/VelloSharp.Ffi.Core/VelloSharp.Ffi.Core.csproj',
    'bindings/VelloSharp.Ffi.Gpu/VelloSharp.Ffi.Gpu.csproj',
    'bindings/VelloSharp.Ffi.Sparse/VelloSharp.Ffi.Sparse.csproj',
    'bindings/VelloSharp.Text/VelloSharp.Text.csproj',
    'bindings/VelloSharp.Skia.Core/VelloSharp.Skia.Core.csproj',
    'bindings/VelloSharp.Skia.Gpu/VelloSharp.Skia.Gpu.csproj',
    'bindings/VelloSharp.Skia.Cpu/VelloSharp.Skia.Cpu.csproj',
    'bindings/VelloSharp.Gpu/VelloSharp.Gpu.csproj',
    'bindings/VelloSharp.Integration.Skia/VelloSharp.Integration.Skia.csproj',
    'bindings/VelloSharp/VelloSharp.csproj',
    'src/VelloSharp.Composition/VelloSharp.Composition.csproj',
    'src/VelloSharp.ChartData/VelloSharp.ChartData.csproj',
    'src/VelloSharp.ChartDiagnostics/VelloSharp.ChartDiagnostics.csproj',
    'src/VelloSharp.ChartRuntime/VelloSharp.ChartRuntime.csproj',
    'src/VelloSharp.ChartRuntime.Windows/VelloSharp.ChartRuntime.Windows.csproj',
    'src/VelloSharp.ChartEngine/VelloSharp.ChartEngine.csproj',
    'src/VelloSharp.Charting/VelloSharp.Charting.csproj',
    'src/VelloSharp.Charting.Avalonia/VelloSharp.Charting.Avalonia.csproj',
    'src/VelloSharp.Charting.WinForms/VelloSharp.Charting.WinForms.csproj',
    'src/VelloSharp.Charting.Wpf/VelloSharp.Charting.Wpf.csproj',
    'src/VelloSharp.Gauges/VelloSharp.Gauges.csproj',
    'src/VelloSharp.TreeDataGrid/VelloSharp.TreeDataGrid.csproj',
    'src/VelloSharp.Editor/VelloSharp.Editor.csproj',
    'src/VelloSharp.Scada/VelloSharp.Scada.csproj'
)

$extraArgs = @{
    'bindings/VelloSharp.Text/VelloSharp.Text.csproj'             = @('-p:VelloSkipNativeBuild=true')
    'bindings/VelloSharp.Skia.Core/VelloSharp.Skia.Core.csproj'   = @('-p:VelloSkipNativeBuild=true')
    'bindings/VelloSharp.Skia.Gpu/VelloSharp.Skia.Gpu.csproj'     = @('-p:VelloSkipNativeBuild=true')
    'bindings/VelloSharp.Skia.Cpu/VelloSharp.Skia.Cpu.csproj'     = @('-p:VelloSkipNativeBuild=true')
    'bindings/VelloSharp.Integration.Skia/VelloSharp.Integration.Skia.csproj' = @('-p:VelloSkipNativeBuild=true')
    'src/VelloSharp.Composition/VelloSharp.Composition.csproj'    = @('-p:VelloSkipNativeBuild=true')
    'src/VelloSharp.ChartData/VelloSharp.ChartData.csproj'        = @('-p:VelloSkipNativeBuild=true')
    'src/VelloSharp.ChartDiagnostics/VelloSharp.ChartDiagnostics.csproj' = @('-p:VelloSkipNativeBuild=true')
    'src/VelloSharp.ChartRuntime/VelloSharp.ChartRuntime.csproj'  = @('-p:VelloSkipNativeBuild=true')
    'src/VelloSharp.ChartRuntime.Windows/VelloSharp.ChartRuntime.Windows.csproj' = @('-p:VelloSkipNativeBuild=true')
    'src/VelloSharp.ChartEngine/VelloSharp.ChartEngine.csproj'    = @('-p:VelloSkipNativeBuild=true')
    'src/VelloSharp.Charting/VelloSharp.Charting.csproj'          = @('-p:VelloSkipNativeBuild=true')
    'src/VelloSharp.Charting.Avalonia/VelloSharp.Charting.Avalonia.csproj' = @('-p:VelloSkipNativeBuild=true')
    'src/VelloSharp.Charting.WinForms/VelloSharp.Charting.WinForms.csproj' = @('-p:VelloSkipNativeBuild=true')
    'src/VelloSharp.Charting.Wpf/VelloSharp.Charting.Wpf.csproj'  = @('-p:VelloSkipNativeBuild=true')
    'src/VelloSharp.Gauges/VelloSharp.Gauges.csproj'               = @('-p:VelloSkipNativeBuild=true')
    'src/VelloSharp.TreeDataGrid/VelloSharp.TreeDataGrid.csproj'   = @('-p:VelloSkipNativeBuild=true')
    'src/VelloSharp.Editor/VelloSharp.Editor.csproj'               = @('-p:VelloSkipNativeBuild=true')
    'src/VelloSharp.Scada/VelloSharp.Scada.csproj'                 = @('-p:VelloSkipNativeBuild=true')
    'bindings/VelloSharp.Gpu/VelloSharp.Gpu.csproj'                = @('-p:VelloSkipNativeBuild=true', '-p:VelloIncludeNativeAssets=false', '-p:VelloRequireAllNativeAssets=false')
    'bindings/VelloSharp/VelloSharp.csproj'                        = @('-p:VelloSkipNativeBuild=true', '-p:VelloIncludeNativeAssets=false', '-p:VelloRequireAllNativeAssets=false')
}

$commonArgs = @('-c', 'Release', "-p:PackageOutputPath=$NuGetOutput", '-p:EnableWindowsTargeting=true')

foreach ($project in $projects) {
    $projectPath = Join-Path $rootPath $project
    if (-not (Test-Path $projectPath -PathType Leaf)) {
        Write-Host "Skipping missing project '$project'."
        continue
    }

    $args = [System.Collections.Generic.List[string]]::new()
    $args.AddRange([string[]]$commonArgs)

    if ($extraArgs.ContainsKey($project)) {
        $args.AddRange([string[]]$extraArgs[$project])
    }

    Write-Host "Packing $project"
    & dotnet pack $projectPath @args
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed with exit code $LASTEXITCODE for $projectPath."
    }
}

Write-Host "Managed packages created in '$NuGetOutput'."
