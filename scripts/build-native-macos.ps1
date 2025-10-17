#!/usr/bin/env pwsh
Param(
    [string]$Target = "aarch64-apple-darwin",
    [string]$Profile = "release",
    [string]$Sdk,
    [string]$Rid
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Get-PackageName([string]$CargoToml) {
    if (-not (Test-Path $CargoToml -PathType Leaf)) {
        return $null
    }

    foreach ($line in Get-Content -Path $CargoToml) {
        if ($line -match '^\s*name\s*=\s*"([^"]+)"') {
            return $Matches[1]
        }
    }

    return $null
}

function Has-NativeLibrary([string]$CargoToml) {
    if (-not (Test-Path $CargoToml -PathType Leaf)) {
        return $false
    }

    $content = Get-Content -Path $CargoToml -Raw
    return $content -match 'crate-type\s*=\s*\[[^\]]*"(cdylib|staticlib)"'
}

$ffiDir = Join-Path $root "ffi"
$libs = @()

if (Test-Path $ffiDir) {
    $libs = Get-ChildItem -Path $ffiDir -Directory |
        Sort-Object Name |
        ForEach-Object {
            $cargoToml = Join-Path $_.FullName "Cargo.toml"
            if ((Test-Path $cargoToml) -and (Has-NativeLibrary $cargoToml)) {
                Get-PackageName $cargoToml
            }
        } |
        Where-Object { $_ }
}

if ($libs.Count -eq 0) {
    $libs = @(
        "accesskit_ffi",
        "vello_ffi",
        "kurbo_ffi",
        "peniko_ffi",
        "winit_ffi",
        "vello_sparse_ffi",
        "vello_composition",
        "vello_chart_engine",
        "vello_tree_datagrid",
        "vello_editor_core",
        "vello_gauges_core",
        "vello_scada_runtime"
    )
}

if (-not [string]::IsNullOrEmpty($Sdk)) {
    $sdkRoot = (& xcrun --sdk $Sdk --show-sdk-path).Trim()
    if (-not $sdkRoot) {
        throw "Failed to resolve SDK path for '$Sdk'."
    }
    $env:SDKROOT = $sdkRoot
    Write-Host "Using SDKROOT=$sdkRoot"
}

$buildArgs = @("--target", $Target)

switch -Regex ($Profile) {
    "^(?i)release$" {
        $profileDir = "release"
        $buildArgs += "--release"
    }
    "^(?i)debug$" {
        $profileDir = "debug"
    }
    Default {
        $profileDir = $Profile
        $buildArgs += @("--profile", $Profile)
    }
}

foreach ($crate in $libs) {
    Write-Host "Building $crate for $Target ($Profile)"
    cargo build -p $crate @buildArgs
}

if ([string]::IsNullOrEmpty($Rid)) {
    switch ($Target) {
        "x86_64-apple-darwin" { $Rid = "osx-x64" }
        "aarch64-apple-darwin" { $Rid = "osx-arm64" }
        "aarch64-apple-ios" { $Rid = "ios-arm64" }
        "x86_64-apple-ios" { $Rid = "iossimulator-x64" }
        Default {
            throw "Unknown target '$Target'; please provide the RID as the fourth parameter."
        }
    }
}

$outDir = Join-Path $root "artifacts"
$outDir = Join-Path $outDir "runtimes"
$outDir = Join-Path $outDir $Rid
$outDir = Join-Path $outDir "native"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

foreach ($crate in $libs) {
    $libName = "lib$crate.dylib"
    $src = Join-Path $root "target"
    $src = Join-Path $src $Target
    $src = Join-Path $src $profileDir
    $src = Join-Path $src $libName

    if (-not (Test-Path $src -PathType Leaf) -and ($Target -like "*apple-ios*")) {
        $libName = "lib$crate.a"
        $src = Join-Path $root "target"
        $src = Join-Path $src $Target
        $src = Join-Path $src $profileDir
        $src = Join-Path $src $libName
    }

    if (-not (Test-Path $src -PathType Leaf)) {
        throw "Native library '$crate' not found for target '$Target'."
    }

    $destPath = Join-Path $outDir $libName
    Copy-Item -Path $src -Destination $destPath -Force
    Write-Host "Copied $src -> $destPath"
}
