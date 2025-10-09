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
        Write-Host "Skipping $rid: no native assets found under '$nativeDir'."
        continue
    }

    $nativeDirAbs = (Resolve-Path $nativeDir).Path

    foreach ($ffi in $ffiProjects) {
        $project = Join-Path $rootPath ("packaging/VelloSharp.Native.{0}/VelloSharp.Native.{0}.{1}.csproj" -f $ffi, $rid)
        if (-not (Test-Path $project -PathType Leaf)) {
            Write-Host "Skipping $ffi for $rid: project not found at $project."
            continue
        }

        Write-Host "Packing native package for $ffi ($rid)"
        dotnet pack $project -c Release -p:NativeAssetsDirectory="$nativeDirAbs" -p:PackageOutputPath="$outputDirAbs"
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet pack failed with exit code $LASTEXITCODE for $project."
        }
        $processed++
    }
}

if ($processed -eq 0) {
    Write-Error "No native runtime directories were processed under '$RuntimesRoot'."
    exit 1
}

if (-not (Get-ChildItem -Path $outputDirAbs -Filter '*.nupkg' -File -ErrorAction SilentlyContinue | Select-Object -First 1)) {
    Write-Error "No native packages were produced."
    exit 1
}

Write-Host "Native packages created in '$OutputDir'."
