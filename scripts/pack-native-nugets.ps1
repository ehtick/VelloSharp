[CmdletBinding()]
param(
    [string]$RuntimesRoot,
    [string]$OutputDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootPath = (Resolve-Path (Join-Path $scriptRoot '..')).Path

if (-not $RuntimesRoot -or [string]::IsNullOrWhiteSpace($RuntimesRoot)) {
    $RuntimesRoot = Join-Path $rootPath 'artifacts/runtimes'
}
$RuntimesRoot = [System.IO.Path]::GetFullPath($RuntimesRoot)

if (-not (Test-Path $RuntimesRoot -PathType Container)) {
    Write-Error "No runtimes directory found at '$RuntimesRoot'."
    exit 1
}

if (-not $OutputDir -or [string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $rootPath 'artifacts/nuget'
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$outputDirAbs = (Resolve-Path $OutputDir).Path

$ffiProjects = @('AccessKit', 'ChartEngine', 'Composition', 'Editor', 'Gauges', 'Kurbo', 'Peniko', 'Scada', 'TreeDataGrid', 'Vello', 'VelloSparse', 'Winit')
$ffiWithAssets = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$seenRids = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$processed = 0

foreach ($ridDir in Get-ChildItem -Path $RuntimesRoot -Directory) {
    $nativeDir = Join-Path $ridDir.FullName 'native'
    if (-not (Test-Path $nativeDir -PathType Container)) {
        continue
    }

    $rid = $ridDir.Name
    if (-not $seenRids.Add($rid)) {
        continue
    }

    if (-not (Get-ChildItem -Path $nativeDir -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1)) {
        Write-Host ("Skipping {0}: no native assets found under '{1}'." -f $rid, $nativeDir)
        continue
    }

    $nativeDirAbs = (Resolve-Path $nativeDir).Path

    foreach ($ffi in $ffiProjects) {
        $project = Join-Path $rootPath ("packaging/VelloSharp.Native.{0}/VelloSharp.Native.{0}.{1}.csproj" -f $ffi, $rid)
        if (-not (Test-Path $project -PathType Leaf)) {
            Write-Host ("Skipping {0} for {1}: project not found at {2}." -f $ffi, $rid, $project)
            continue
        }

        Write-Host "Packing native package for $ffi ($rid)"
        dotnet pack $project -c Release -p:NativeAssetsDirectory="$nativeDirAbs" -p:PackageOutputPath="$outputDirAbs"
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet pack failed with exit code $LASTEXITCODE for $project."
        }
        $processed++
        $null = $ffiWithAssets.Add($ffi)
    }
}

if ($processed -eq 0) {
    Write-Error "No native runtime directories were processed under '$RuntimesRoot'."
    exit 1
}

foreach ($ffi in $ffiProjects) {
    if (-not $ffiWithAssets.Contains($ffi)) {
        Write-Host ("Skipping meta-package for {0}: no runtime-specific packages were produced." -f $ffi)
        continue
    }

    $metaProject = Join-Path $rootPath ("packaging/VelloSharp.Native.{0}/VelloSharp.Native.{0}.csproj" -f $ffi)
    if (-not (Test-Path $metaProject -PathType Leaf)) {
        Write-Host ("Skipping meta-package for {0}: project not found at {1}." -f $ffi, $metaProject)
        continue
    }

    Write-Host "Packing native meta-package for $ffi"
    dotnet pack $metaProject -c Release -p:PackageOutputPath="$outputDirAbs"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed with exit code $LASTEXITCODE for $metaProject."
    }
}

$webRuntimeDir = Join-Path $RuntimesRoot 'browser-wasm/native'
if (Test-Path $webRuntimeDir -PathType Container) {
    $webProject = Join-Path $rootPath 'packaging/VelloSharp.Native.Vello.Web/VelloSharp.Native.Vello.Web.csproj'
    if (Test-Path $webProject -PathType Leaf) {
        Write-Host "Packing WebAssembly WebGPU bundle"
        dotnet pack $webProject -c Release -p:NativeAssetsDirectory="$webRuntimeDir" -p:PackageOutputPath="$outputDirAbs"
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet pack failed with exit code $LASTEXITCODE for $webProject."
        }
    }
    else {
        Write-Host ("Skipping VelloSharp.Native.Vello.Web: project not found at {0}." -f $webProject)
    }
}

if (-not (Get-ChildItem -Path $outputDirAbs -Filter '*.nupkg' -File -ErrorAction SilentlyContinue | Select-Object -First 1)) {
    Write-Error "No native packages were produced."
    exit 1
}

Write-Host "Native packages created in '$OutputDir'."
