param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "..\samples\WinUIVelloGallery\WinUIVelloGallery.csproj"
$projectPath = (Resolve-Path $projectPath).Path

Write-Host "Building WinUIVelloGallery ($Configuration)..."
dotnet build $projectPath -c $Configuration | Out-Host

$output = Join-Path $PSScriptRoot "..\samples\WinUIVelloGallery\bin\$Configuration\net8.0-windows10.0.19041\WinUIVelloGallery.exe"
$output = (Resolve-Path $output -ErrorAction SilentlyContinue)?.Path

if (-not $output) {
    Write-Warning "Executable not found. Ensure build succeeded."
    return
}

Write-Host "Launching $output"
Start-Process $output
