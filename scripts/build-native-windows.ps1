#!/usr/bin/env pwsh
Param(
    [string]$Target = "x86_64-pc-windows-msvc",
    [string]$Profile = "release",
    [string]$Rid
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$profileFlag = if ($Profile -ieq "release") { "--release" } else { "" }
$profileDir = if ($Profile -ieq "release") { "release" } else { "debug" }

Write-Host "Building vello_ffi for $Target ($Profile)"
cargo build -p vello_ffi --target $Target $profileFlag

if ([string]::IsNullOrWhiteSpace($Rid)) {
    switch ($Target) {
        "x86_64-pc-windows-msvc" { $Rid = "win-x64" }
        "aarch64-pc-windows-msvc" { $Rid = "win-arm64" }
        default {
            throw "Unknown target '$Target'. Provide the runtime identifier as the third argument."
        }
    }
}

$libName = "vello_ffi.dll"
$src = Join-Path $root "target"
$src = Join-Path $src $Target
$src = Join-Path $src $profileDir
$src = Join-Path $src $libName

if (-not (Test-Path $src)) {
    throw "Native library not found at '$src'."
}

$dest = Join-Path $root "artifacts"
$dest = Join-Path $dest "runtimes"
$dest = Join-Path $dest $Rid
$dest = Join-Path $dest "native"
New-Item -ItemType Directory -Path $dest -Force | Out-Null

Copy-Item -Path $src -Destination (Join-Path $dest $libName) -Force
Write-Host "Copied $src -> $(Join-Path $dest $libName)"
