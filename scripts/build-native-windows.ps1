#!/usr/bin/env pwsh
Param(
    [string]$Target = "x86_64-pc-windows-msvc",
    [string]$Profile = "release",
    [string]$Rid
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$ffiDir = Join-Path $root "ffi"
$libs = @()
function Get-PackageName([string]$cargoToml) {
    if (-not (Test-Path $cargoToml -PathType Leaf)) {
        return $null
    }

    foreach ($line in Get-Content -Path $cargoToml) {
        if ($line -match '^\s*name\s*=\s*"([^"]+)"') {
            return $Matches[1]
        }
    }

    return $null
}

function Has-NativeLibrary([string]$cargoToml) {
    if (-not (Test-Path $cargoToml -PathType Leaf)) {
        return $false
    }

    $content = Get-Content -Path $cargoToml -Raw
    return $content -match 'crate-type\s*=\s*\[[^\]]*"(cdylib|staticlib)"'
}

if (Test-Path $ffiDir) {
    $libs = Get-ChildItem -Path $ffiDir -Directory |
        Where-Object {
            $cargoToml = Join-Path $_.FullName "Cargo.toml"
            (Test-Path $cargoToml) -and (Has-NativeLibrary $cargoToml)
        } |
        Sort-Object Name |
        ForEach-Object {
            Get-PackageName (Join-Path $_.FullName "Cargo.toml")
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
$profileArgs = @()
switch -Regex ($Profile) {
    "^(?i)release$" {
        $profileDir = "release"
        $profileArgs += "--release"
    }
    "^(?i)debug$" {
        $profileDir = "debug"
    }
    Default {
        $profileDir = $Profile
        $profileArgs += "--profile"
        $profileArgs += $Profile
    }
}

foreach ($crate in $libs) {
    Write-Host "Building $crate for $Target ($Profile)"
    cargo build -p $crate --target $Target @profileArgs
}

if ([string]::IsNullOrWhiteSpace($Rid)) {
    switch ($Target) {
        "x86_64-pc-windows-msvc" { $Rid = "win-x64" }
        "aarch64-pc-windows-msvc" { $Rid = "win-arm64" }
        default {
            throw "Unknown target '$Target'. Provide the runtime identifier as the third argument."
        }
    }
}

foreach ($crate in $libs) {
    $libName = "$crate.dll"
    $src = Join-Path $root "target"
    $src = Join-Path $src $Target
    $src = Join-Path $src $profileDir
    $src = Join-Path $src $libName

    if (-not (Test-Path $src)) {
        throw "Native library '$libName' not found at '$src'."
    }

    $dest = Join-Path $root "artifacts"
    $dest = Join-Path $dest "runtimes"
    $dest = Join-Path $dest $Rid
    $dest = Join-Path $dest "native"
    New-Item -ItemType Directory -Path $dest -Force | Out-Null

    Copy-Item -Path $src -Destination (Join-Path $dest $libName) -Force
    Write-Host "Copied $src -> $(Join-Path $dest $libName)"
}
