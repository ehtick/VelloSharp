#!/usr/bin/env pwsh
Param(
    [string]$Target = "wasm32-unknown-unknown",
    [string]$Profile = "release",
    [string]$Crate = "vello_webgpu_ffi",
    [string]$OutDir,
    [string]$SampleAssetDir
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) {
    $OutDir = Join-Path $root "artifacts"
    $OutDir = Join-Path $OutDir "browser"
    $OutDir = Join-Path $OutDir "native"
}

if (-not $SampleAssetDir) {
    $SampleAssetDir = Join-Path $root "samples"
    $SampleAssetDir = Join-Path $SampleAssetDir "AvaloniaVelloBrowserDemo"
    $SampleAssetDir = Join-Path $SampleAssetDir "AvaloniaVelloBrowserDemo.Browser"
    $SampleAssetDir = Join-Path $SampleAssetDir "wwwroot"
    $SampleAssetDir = Join-Path $SampleAssetDir "native"
}

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

function Resolve-Tool([string]$name) {
    $command = Get-Command $name -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "$name CLI not found on PATH. Install it and retry."
    }
    return $command.Source
}

$wasmBindgen = Resolve-Tool "wasm-bindgen"
$wasmOpt = $null
try {
    $wasmOpt = Resolve-Tool "wasm-opt"
} catch {
    Write-Warning "wasm-opt not found on PATH. Skipping Binaryen optimizations."
}

$buildArgs = @("--target", $Target, "-p", $Crate)
switch -Regex ($Profile) {
    "^(?i)release$" {
        $profileDir = "release"
        $buildArgs += "--release"
    }
    "^(?i)debug$" {
        $profileDir = "debug"
    }
    default {
        $profileDir = $Profile
        $buildArgs += @("--profile", $Profile)
    }
}

Write-Host "Building $Crate for $Target ($Profile)"
& cargo build @buildArgs

$wasmPath = Join-Path $root "target"
$wasmPath = Join-Path $wasmPath $Target
$wasmPath = Join-Path $wasmPath $profileDir
$wasmPath = Join-Path $wasmPath "$Crate.wasm"
if (-not (Test-Path $wasmPath -PathType Leaf)) {
    throw "Expected wasm artifact not found at $wasmPath"
}

$rawOut = Join-Path $OutDir "$Crate.wasm"
Copy-Item -Path $wasmPath -Destination $rawOut -Force
Write-Host "Copied raw artifact to $rawOut"

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("vello-webgpu-bindgen-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    $bindgenArgs = @(
        $wasmPath,
        "--target", "web",
        "--reference-types",
        "--no-typescript",
        "--out-dir", $tempDir
    )
    Write-Host "Running wasm-bindgen"
    & $wasmBindgen @bindgenArgs

    $bindgenWasm = Join-Path $tempDir ("${Crate}_bg.wasm")
    if (-not (Test-Path $bindgenWasm -PathType Leaf)) {
        throw "wasm-bindgen output not found at $bindgenWasm"
    }

    $destWasm = Join-Path $OutDir ("${Crate}_bg.wasm")
    Copy-Item -Path $bindgenWasm -Destination $destWasm -Force

    $bindgenJs = Join-Path $tempDir ("${Crate}.js")
    if (Test-Path $bindgenJs -PathType Leaf) {
        Copy-Item -Path $bindgenJs -Destination (Join-Path $OutDir ("${Crate}.js")) -Force
    }

    $packageJson = Join-Path $tempDir "package.json"
    if (Test-Path $packageJson -PathType Leaf) {
        Copy-Item -Path $packageJson -Destination (Join-Path $OutDir "package.json") -Force
    }

    $snippetsDir = Join-Path $tempDir "snippets"
    if (Test-Path $snippetsDir -PathType Container) {
        $destSnippets = Join-Path $OutDir "snippets"
        if (Test-Path $destSnippets) {
            Remove-Item -Path $destSnippets -Recurse -Force
        }
        Copy-Item -Path $snippetsDir -Destination $destSnippets -Recurse -Force
    }
}
finally {
    if (Test-Path $tempDir) {
        Remove-Item -Path $tempDir -Recurse -Force
    }
}

if ($wasmOpt) {
    $destWasm = Join-Path $OutDir ("${Crate}_bg.wasm")
    $optimizedPath = Join-Path ([System.IO.Path]::GetTempPath()) ("vello-webgpu-opt-" + [System.Guid]::NewGuid().ToString("N") + ".wasm")
    Write-Host "Optimizing $destWasm with wasm-opt"
    & $wasmOpt "-O2" "--strip-debug" "-o" $optimizedPath $destWasm
    Move-Item -Path $optimizedPath -Destination $destWasm -Force
    Write-Host "Optimized artifact written to $destWasm"
} else {
    Write-Warning "Binaryen optimizations skipped because wasm-opt is unavailable."
}

if ($SampleAssetDir) {
    try {
        New-Item -ItemType Directory -Path $SampleAssetDir -Force | Out-Null
        $destination = Join-Path $SampleAssetDir "$Crate.wasm"
        Copy-Item -Path (Join-Path $OutDir "$Crate.wasm") -Destination $destination -Force

        $bindgenCandidate = Join-Path $OutDir ("${Crate}_bg.wasm")
        if (Test-Path $bindgenCandidate -PathType Leaf) {
            Copy-Item -Path $bindgenCandidate -Destination (Join-Path $SampleAssetDir ("${Crate}_bg.wasm")) -Force
        }

        $jsCandidate = Join-Path $OutDir ("${Crate}.js")
        if (Test-Path $jsCandidate -PathType Leaf) {
            Copy-Item -Path $jsCandidate -Destination (Join-Path $SampleAssetDir ("${Crate}.js")) -Force
        }

        Write-Host "Sample assets mirrored to $SampleAssetDir"
    }
    catch {
        Write-Warning "Failed to mirror artifacts to sample directory '$SampleAssetDir'. $_"
    }
}

$runtimeNativeDir = Join-Path $root "artifacts"
$runtimeNativeDir = Join-Path $runtimeNativeDir "runtimes"
$runtimeNativeDir = Join-Path $runtimeNativeDir "browser-wasm"
$runtimeNativeDir = Join-Path $runtimeNativeDir "native"

try {
    New-Item -ItemType Directory -Path $runtimeNativeDir -Force | Out-Null

    foreach ($item in Get-ChildItem -Path $OutDir) {
        $target = Join-Path $runtimeNativeDir $item.Name

        if ($item.PSIsContainer) {
            if (Test-Path $target) {
                Remove-Item -Path $target -Recurse -Force
            }

            Copy-Item -Path $item.FullName -Destination $target -Recurse -Force
        }
        else {
            Copy-Item -Path $item.FullName -Destination $target -Force
        }
    }
}
catch {
    Write-Warning "Failed to mirror artifacts to runtime directory '$runtimeNativeDir'. $_"
}
