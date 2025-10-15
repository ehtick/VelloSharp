#!/usr/bin/env pwsh
Param(
    [string]$Configuration = "Release",
    [string]$Framework = "net8.0-browserwasm",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$tempRoot = Join-Path $repoRoot "artifacts/tmp"
if (-not (Test-Path $tempRoot)) {
    New-Item -ItemType Directory -Path $tempRoot | Out-Null
}

function Invoke-Step([string]$Message, [scriptblock]$Action) {
    Write-Host "==> $Message"
    & $Action
}

if (-not $SkipBuild) {
    Invoke-Step "Building browser WebGPU runtime" {
        & (Join-Path $scriptRoot "build-wasm-vello.ps1")
    }
}

Invoke-Step "Running wasm-bindgen tests (wasm32-unknown-unknown)" {
    $env:WASM_BINDGEN_TEST_TIMEOUT = "120"
    $args = @("test", "-p", "vello_webgpu_ffi", "--target", "wasm32-unknown-unknown", "--release", "--tests")
    & cargo @args
}

$browserProject = Join-Path $repoRoot "samples/AvaloniaVelloBrowserDemo/AvaloniaVelloBrowserDemo.Browser/AvaloniaVelloBrowserDemo.Browser.csproj"
$publishDir = Join-Path $tempRoot "browser-publish"

Invoke-Step "Publishing Avalonia browser host" {
    if (Test-Path $publishDir) {
        Remove-Item -Path $publishDir -Recurse -Force
    }

    & dotnet publish $browserProject `
        -c $Configuration `
        -f $Framework `
        -o $publishDir `
        /bl:$(Join-Path $tempRoot "browser-publish.binlog") `
        /p:RunAOTCompilation=false `
        /p:WasmGenerateAppBundle=true
}

Invoke-Step "Checking published artefacts" {
    $nativeDir = Join-Path $publishDir "wwwroot/native"
    if (-not (Test-Path $nativeDir -PathType Container)) {
        throw "Native asset directory not found at $nativeDir"
    }

    $required = @(
        "vello_webgpu_ffi.wasm",
        "vello_webgpu_ffi_bg.wasm"
    )

    foreach ($name in $required) {
        $candidate = Join-Path $nativeDir $name
        if (-not (Test-Path $candidate -PathType Leaf)) {
            throw "Expected asset '$name' was not produced (checked $candidate)."
        }
    }

    Write-Host "Native asset bundle verified."
}

Invoke-Step "Running Playwright smoke test" {
    $node = Get-Command node -ErrorAction SilentlyContinue
    if (-not $node) {
        Write-Warning "Node.js executable not found on PATH. Skipping Playwright smoke test."
        return
    }

    $npm = Get-Command npm -ErrorAction SilentlyContinue
    $npx = Get-Command npx -ErrorAction SilentlyContinue
    if (-not $npm -or -not $npx) {
        Write-Warning "npm/npx executables not available on PATH. Skipping Playwright smoke test."
        return
    }

    $playwrightDir = Join-Path $tempRoot "playwright-smoke"
    New-Item -ItemType Directory -Force -Path $playwrightDir | Out-Null
    Push-Location $playwrightDir

    $webRoot = Join-Path $publishDir "wwwroot"
    if (-not (Test-Path $webRoot -PathType Container)) {
        throw "Published wwwroot directory not found at $webRoot"
    }

    $serverProcess = $null
    try {
        if (-not (Test-Path (Join-Path $playwrightDir "package.json"))) {
            & $npm.Source init -y | Out-Null
        }

        if (-not (Test-Path (Join-Path $playwrightDir "node_modules/playwright"))) {
            & $npm.Source install --no-save --no-fund --no-audit playwright@1.46.1 | Out-Null
        }

        & $npx.Source playwright install chromium | Out-Null

        $serverScript = Join-Path $scriptRoot "playwright/server.mjs"
        $smokeScript = Join-Path $scriptRoot "playwright/smoke.mjs"

        if (-not (Test-Path $serverScript -PathType Leaf)) {
            throw "Server script not found at $serverScript"
        }

        if (-not (Test-Path $smokeScript -PathType Leaf)) {
            throw "Playwright smoke script not found at $smokeScript"
        }

        $port = Get-Random -Minimum 4000 -Maximum 9000
        $serverArgs = "`"$serverScript`" `"$webRoot`" $port"
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo($node.Source, $serverArgs)
        $startInfo.WorkingDirectory = $playwrightDir
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.CreateNoWindow = $true

        $serverProcess = [System.Diagnostics.Process]::Start($startInfo)
        if ($serverProcess -eq $null) {
            throw "Failed to start Node.js server process."
        }

        $deadline = [DateTime]::UtcNow.AddSeconds(20)
        $readyLine = $null

        while ([DateTime]::UtcNow -lt $deadline) {
            if ($serverProcess.HasExited) {
                $errorOutput = $serverProcess.StandardError.ReadToEnd()
                throw "Static server exited unexpectedly.`n$errorOutput"
            }

            $readyLine = $serverProcess.StandardOutput.ReadLine()
            if ($readyLine -match "READY") {
                break
            }

            Start-Sleep -Milliseconds 100
        }

        if (-not $readyLine -or $readyLine -notmatch "READY") {
            throw "Static server did not signal readiness within the timeout window."
        }

        $screenshot = Join-Path $tempRoot "playwright-smoke.png"
        $smokeArgs = "`"$smokeScript`" http://127.0.0.1:$port/ `"$screenshot`""
        $smokeInfo = New-Object System.Diagnostics.ProcessStartInfo($node.Source, $smokeArgs)
        $smokeInfo.WorkingDirectory = $playwrightDir
        $smokeInfo.UseShellExecute = $false
        $smokeInfo.RedirectStandardOutput = $true
        $smokeInfo.RedirectStandardError = $true
        $smokeInfo.CreateNoWindow = $true

        $smokeProcess = [System.Diagnostics.Process]::Start($smokeInfo)
        if ($smokeProcess -eq $null) {
            throw "Failed to start Playwright smoke process."
        }

        $smokeProcess.WaitForExit()
        if ($smokeProcess.ExitCode -ne 0) {
            $stdout = $smokeProcess.StandardOutput.ReadToEnd()
            $stderr = $smokeProcess.StandardError.ReadToEnd()
            throw "Playwright smoke test failed with exit code $($smokeProcess.ExitCode).`n$stdout`n$stderr"
        }

        Write-Host "Playwright smoke test completed. Screenshot: $screenshot"
    }
    finally {
        if ($serverProcess -and -not $serverProcess.HasExited) {
            $serverProcess.Kill(true)
            $serverProcess.WaitForExit()
        }

        $serverProcess?.Dispose()
        Pop-Location
    }
}

Write-Host "Browser smoke verification succeeded."
