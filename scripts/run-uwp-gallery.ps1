param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "..\samples\UwpVelloGallery\UwpVelloGallery.csproj"
$projectPath = (Resolve-Path $projectPath).Path

Write-Host "Building UwpVelloGallery ($Configuration)..."
dotnet build $projectPath -c $Configuration | Out-Host

$output = Join-Path $PSScriptRoot "..\samples\UwpVelloGallery\bin\$Configuration\net8.0-windows10.0.19041\UwpVelloGallery.exe"
$output = (Resolve-Path $output -ErrorAction SilentlyContinue)?.Path

if (-not $output) {
    Write-Warning "Executable not found. Ensure build succeeded."
    return
}

Write-Host "Launching $output"
Start-Process $output
