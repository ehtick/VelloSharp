#!/usr/bin/env pwsh
Param(
    [string]$Target = "aarch64-linux-android",
    [string]$Profile = "release",
    [string]$Rid = "android-arm64"
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
        "vello_tree_datagrid"
    )
}

if (-not $env:ANDROID_NDK_HOME) {
    throw "ANDROID_NDK_HOME must be set."
}

$ndkHome = (Resolve-Path $env:ANDROID_NDK_HOME).Path

$osArch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
if ($IsWindows) {
    $hostTag = if ($osArch -eq [System.Runtime.InteropServices.Architecture]::Arm64) { "windows-arm64" } else { "windows-x86_64" }
} elseif ($IsLinux) {
    $hostTag = if ($osArch -eq [System.Runtime.InteropServices.Architecture]::Arm64) { "linux-arm64" } else { "linux-x86_64" }
} elseif ($IsMacOS) {
    $hostTag = if ($osArch -eq [System.Runtime.InteropServices.Architecture]::Arm64) { "darwin-arm64" } else { "darwin-x86_64" }
} else {
    throw "Unsupported host platform for Android builds."
}

$llvmBin = Join-Path $ndkHome ("toolchains/llvm/prebuilt/{0}/bin" -f $hostTag)
if (-not (Test-Path $llvmBin -PathType Container)) {
    throw "Unable to locate LLVM toolchain under '$llvmBin'. Verify ANDROID_NDK_HOME."
}

$env:PATH = "$llvmBin$([IO.Path]::PathSeparator)$($env:PATH)"

$arTool = Join-Path $llvmBin ("llvm-ar" + ($(if ($IsWindows) { ".exe" } else { "" })))
$arEnvName = "AR_" + ($Target -replace "-", "_")
[Environment]::SetEnvironmentVariable($arEnvName, $arTool, "Process")

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
        throw "Native library '$libName' not found at '$src'."
    }

    $destPath = Join-Path $outDir $libName
    Copy-Item -Path $src -Destination $destPath -Force
    Write-Host "Copied $src -> $destPath"
}
