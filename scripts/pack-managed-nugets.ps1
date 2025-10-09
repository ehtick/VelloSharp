[CmdletBinding()]
param(
    [string]$NuGetOutput,
    [string]$NativeFeed
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootPath = (Resolve-Path (Join-Path $scriptRoot '..')).Path

if (-not $NuGetOutput -or [string]::IsNullOrWhiteSpace($NuGetOutput)) {
    $NuGetOutput = Join-Path $rootPath 'artifacts/nuget'
}
$NuGetOutput = [System.IO.Path]::GetFullPath($NuGetOutput)

if (-not $NativeFeed -or [string]::IsNullOrWhiteSpace($NativeFeed)) {
    $NativeFeed = $NuGetOutput
}
$NativeFeed = [System.IO.Path]::GetFullPath($NativeFeed)

New-Item -ItemType Directory -Force -Path $NuGetOutput | Out-Null

$buildArgs = @('-c', 'Release', '-p:VelloSkipNativeBuild=true')
& dotnet build (Join-Path $rootPath 'VelloSharp.sln') @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

$sources = & dotnet nuget list source
if ($LASTEXITCODE -ne 0) {
    throw "dotnet nuget list source failed with exit code $LASTEXITCODE."
}

if ($sources -match 'VelloNativeLocal') {
    & dotnet nuget remove source VelloNativeLocal | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet nuget remove source VelloNativeLocal failed with exit code $LASTEXITCODE."
    }
}

& dotnet nuget add source $NativeFeed --name VelloNativeLocal | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "dotnet nuget add source failed with exit code $LASTEXITCODE."
}

try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
} catch {
}

function Get-NativePackageIds {
    param([string]$FeedPath)

    $set = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)

    if (-not (Test-Path $FeedPath -PathType Container)) {
        return $set
    }

    foreach ($package in Get-ChildItem -Path $FeedPath -Filter 'VelloSharp.Native.*.nupkg' -File -ErrorAction SilentlyContinue) {
        try {
            $zip = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
        } catch {
            continue
        }

        try {
            $entry = $zip.Entries | Where-Object { $_.FullName -like '*.nuspec' } | Select-Object -First 1
            if (-not $entry) {
                continue
            }

            $stream = $entry.Open()
            try {
                $reader = New-Object System.IO.StreamReader($stream)
                $content = $reader.ReadToEnd()
            } finally {
                if ($reader) { $reader.Dispose() }
                $stream.Dispose()
            }

            $xml = New-Object System.Xml.XmlDocument
            try {
                $xml.LoadXml($content)
            } catch {
                continue
            }

            $idNode = $xml.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]/*[local-name()="id"]')
            if ($idNode -and $idNode.InnerText) {
                $null = $set.Add($idNode.InnerText.Trim())
            }
        } finally {
            $zip.Dispose()
        }
    }

    return $set
}

$idSet = Get-NativePackageIds -FeedPath $NativeFeed
if ($idSet.Count -eq 0) {
    Write-Error "No VelloSharp.Native packages found in '$NativeFeed'. Run scripts/pack-native-nugets.sh first or disable native package dependencies."
    exit 1
}

$nativeIds = [string]::Join(';', ($idSet | Sort-Object))

$commonPackArgs = @('-c', 'Release', "-p:PackageOutputPath=$NuGetOutput")

$packProjects = @(
    @{ Path = 'bindings/VelloSharp.Core/VelloSharp.Core.csproj'; Extra = @() },
    @{ Path = 'bindings/VelloSharp.Ffi.Core/VelloSharp.Ffi.Core.csproj'; Extra = @() },
    @{ Path = 'bindings/VelloSharp.Ffi.Gpu/VelloSharp.Ffi.Gpu.csproj'; Extra = @() },
    @{ Path = 'bindings/VelloSharp.Ffi.Sparse/VelloSharp.Ffi.Sparse.csproj'; Extra = @() },
    @{ Path = 'bindings/VelloSharp.Text/VelloSharp.Text.csproj'; Extra = @('-p:VelloSkipNativeBuild=true') },
    @{ Path = 'bindings/VelloSharp.Skia.Core/VelloSharp.Skia.Core.csproj'; Extra = @('-p:VelloSkipNativeBuild=true') },
    @{ Path = 'bindings/VelloSharp.Skia.Gpu/VelloSharp.Skia.Gpu.csproj'; Extra = @('-p:VelloSkipNativeBuild=true') },
    @{ Path = 'bindings/VelloSharp.Skia.Cpu/VelloSharp.Skia.Cpu.csproj'; Extra = @('-p:VelloSkipNativeBuild=true') },
    @{ Path = 'bindings/VelloSharp.Gpu/VelloSharp.Gpu.csproj'; Extra = @(
        '-p:VelloSkipNativeBuild=true',
        '-p:VelloIncludeNativeAssets=false',
        '-p:VelloUseNativePackageDependencies=true',
        '-p:VelloRequireAllNativeAssets=false',
        "-p:VelloNativePackageIds=$nativeIds",
        "-p:RestoreAdditionalProjectSources=$NativeFeed"
    ) },
    @{ Path = 'bindings/VelloSharp.Integration.Skia/VelloSharp.Integration.Skia.csproj'; Extra = @('-p:VelloSkipNativeBuild=true') },
    @{ Path = 'bindings/VelloSharp/VelloSharp.csproj'; Extra = @(
        '-p:VelloSkipNativeBuild=true',
        '-p:VelloIncludeNativeAssets=false',
        '-p:VelloUseNativePackageDependencies=true',
        '-p:VelloRequireAllNativeAssets=false',
        "-p:VelloNativePackageIds=$nativeIds",
        "-p:RestoreAdditionalProjectSources=$NativeFeed"
    ) },
    @{ Path = 'src/VelloSharp.Composition/VelloSharp.Composition.csproj'; Extra = @('-p:VelloSkipNativeBuild=true') },
    @{ Path = 'src/VelloSharp.ChartData/VelloSharp.ChartData.csproj'; Extra = @('-p:VelloSkipNativeBuild=true') },
    @{ Path = 'src/VelloSharp.ChartDiagnostics/VelloSharp.ChartDiagnostics.csproj'; Extra = @('-p:VelloSkipNativeBuild=true') },
    @{ Path = 'src/VelloSharp.ChartRuntime/VelloSharp.ChartRuntime.csproj'; Extra = @('-p:VelloSkipNativeBuild=true') },
    @{ Path = 'src/VelloSharp.ChartRuntime.Windows/VelloSharp.ChartRuntime.Windows.csproj'; Extra = @('-p:VelloSkipNativeBuild=true') },
    @{ Path = 'src/VelloSharp.ChartEngine/VelloSharp.ChartEngine.csproj'; Extra = @('-p:VelloSkipNativeBuild=true') },
    @{ Path = 'src/VelloSharp.Charting/VelloSharp.Charting.csproj'; Extra = @('-p:VelloSkipNativeBuild=true') },
    @{ Path = 'src/VelloSharp.Charting.Avalonia/VelloSharp.Charting.Avalonia.csproj'; Extra = @('-p:VelloSkipNativeBuild=true') },
    @{ Path = 'src/VelloSharp.Charting.WinForms/VelloSharp.Charting.WinForms.csproj'; Extra = @('-p:VelloSkipNativeBuild=true') },
    @{ Path = 'src/VelloSharp.Charting.Wpf/VelloSharp.Charting.Wpf.csproj'; Extra = @('-p:VelloSkipNativeBuild=true') },
    @{ Path = 'src/VelloSharp.Gauges/VelloSharp.Gauges.csproj'; Extra = @(
        '-p:VelloSkipNativeBuild=true',
        '-p:VelloUseNativePackageDependencies=true',
        "-p:RestoreAdditionalProjectSources=$NativeFeed"
    ) },
    @{ Path = 'src/VelloSharp.TreeDataGrid/VelloSharp.TreeDataGrid.csproj'; Extra = @(
        '-p:VelloSkipNativeBuild=true',
        '-p:VelloUseNativePackageDependencies=true',
        "-p:RestoreAdditionalProjectSources=$NativeFeed"
    ) },
    @{ Path = 'src/VelloSharp.Editor/VelloSharp.Editor.csproj'; Extra = @(
        '-p:VelloSkipNativeBuild=true',
        '-p:VelloUseNativePackageDependencies=true',
        "-p:RestoreAdditionalProjectSources=$NativeFeed"
    ) },
    @{ Path = 'src/VelloSharp.Scada/VelloSharp.Scada.csproj'; Extra = @(
        '-p:VelloSkipNativeBuild=true',
        '-p:VelloUseNativePackageDependencies=true',
        "-p:RestoreAdditionalProjectSources=$NativeFeed"
    ) }
)

foreach ($project in $packProjects) {
    $projectPath = Join-Path $rootPath $project.Path
    $args = @($projectPath) + $commonPackArgs + $project.Extra
    & dotnet pack @args
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed with exit code $LASTEXITCODE for $projectPath."
    }
}

Write-Host "Managed packages created in '$NuGetOutput'."
