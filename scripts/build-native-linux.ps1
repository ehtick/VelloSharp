#!/usr/bin/env pwsh
Param(
    [string]$Target = "x86_64-unknown-linux-gnu",
    [string]$Profile = "release",
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
        "x86_64-unknown-linux-gnu" { $Rid = "linux-x64" }
        "aarch64-unknown-linux-gnu" { $Rid = "linux-arm64" }
        Default {
            throw "Unknown target '$Target'; please provide the RID as the third parameter."
        }
    }
}

$outDir = Join-Path $root "artifacts"
$outDir = Join-Path $outDir "runtimes"
$outDir = Join-Path $outDir $Rid
$outDir = Join-Path $outDir "native"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

foreach ($crate in $libs) {
    $libName = "lib$crate.so"
    $src = Join-Path $root "target"
    $src = Join-Path $src $Target
    $src = Join-Path $src $profileDir
    $src = Join-Path $src $libName

    if (-not (Test-Path $src -PathType Leaf)) {
        $libName = "$crate.so"
        $src = Join-Path $root "target"
        $src = Join-Path $src $Target
        $src = Join-Path $src $profileDir
        $src = Join-Path $src $libName
    }

    if (-not (Test-Path $src -PathType Leaf)) {
        throw "Native library '$crate' not found (checked for lib$crate.so and $crate.so)."
    }

    $destPath = Join-Path $outDir $libName
    Copy-Item -Path $src -Destination $destPath -Force
    Write-Host "Copied $src -> $destPath"
}
