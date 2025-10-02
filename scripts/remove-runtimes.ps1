[CmdletBinding()]
param(
    [string[]]$Targets
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootPath = (Resolve-Path (Join-Path $scriptRoot '..')).Path

if (-not $Targets -or $Targets.Count -eq 0) {
    $Targets = @('VelloSharp', 'VelloSharp.Integration', 'samples/AvaloniaVelloExamples', 'samples/AvaloniaVelloDemo', 'samples/VelloSharp.WithWinit')
}

if ($env:REMOVE_RUNTIMES_CONFIGURATIONS) {
    $configurations = ($env:REMOVE_RUNTIMES_CONFIGURATIONS -split '\s+') | Where-Object { $_ }
} else {
    $configurations = @('Debug', 'Release')
}

if ($env:REMOVE_RUNTIMES_TARGET_FRAMEWORKS) {
    $targetFrameworks = ($env:REMOVE_RUNTIMES_TARGET_FRAMEWORKS -split '\s+') | Where-Object { $_ }
} else {
    $targetFrameworks = @('net8.0')
}

function Remove-Directory {
    param([string]$Path)

    if (Test-Path $Path -PathType Container) {
        Remove-Item -Path $Path -Recurse -Force
        Write-Host "Removed '$Path'"
    }
}

Write-Host "Removing runtime payloads"
foreach ($target in $Targets) {
    $targetRoot = Join-Path $rootPath $target
    if (-not (Test-Path $targetRoot -PathType Container)) {
        Write-Host "Skipping '$target' (directory not found)."
        continue
    }

    Remove-Directory -Path (Join-Path $targetRoot 'runtimes')

    foreach ($configuration in $configurations) {
        foreach ($framework in $targetFrameworks) {
            $outputBase = Join-Path (Join-Path (Join-Path $targetRoot 'bin') $configuration) $framework
            Remove-Directory -Path (Join-Path $outputBase 'runtimes')
        }
    }
}
