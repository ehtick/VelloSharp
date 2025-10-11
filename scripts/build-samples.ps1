[CmdletBinding()]
param(
    [string]$Configuration = $env:CONFIGURATION,
    [string]$Framework,
    [string[]]$DotNetArgument
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $Configuration -or [string]::IsNullOrWhiteSpace($Configuration)) {
    $Configuration = 'Release'
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootPath = (Resolve-Path (Join-Path $scriptRoot '..')).Path

function Get-Platform {
    if ($IsWindows) { return 'windows' }
    if ($IsLinux) { return 'linux' }
    if ($IsMacOS) { return 'macos' }
    return 'unknown'
}

$platform = Get-Platform

function Supports-Sample {
    param([string]$Path, [string]$Platform)

    $relative = [System.IO.Path]::GetRelativePath($rootPath, $Path).Replace('\', '/')
    switch -Wildcard ($relative) {
        'samples/*WinForms*' { return $Platform -eq 'windows' }
        'samples/*Wpf*'      { return $Platform -eq 'windows' }
        'samples/*WinAppSdk*' { return $Platform -eq 'windows' }
        'samples/*Win32*'    { return $Platform -eq 'windows' }
        'samples/*X11Demo*'  { return $Platform -eq 'linux' }
        default              { return $true }
    }
}

$projectPaths = Get-ChildItem -Path (Join-Path $rootPath 'samples') -Recurse -Filter '*.csproj' -File | Sort-Object FullName
if (-not $projectPaths) {
    Write-Error "No sample projects found under samples/."
    exit 1
}

Write-Host "Building sample projects (configuration: $Configuration)"

foreach ($project in $projectPaths) {
    if (-not (Supports-Sample -Path $project.FullName -Platform $platform)) {
        $rel = [System.IO.Path]::GetRelativePath($rootPath, $project.FullName).Replace('\', '/')
        Write-Host "Skipping $rel (unsupported on $platform)."
        continue
    }

    $relPath = [System.IO.Path]::GetRelativePath($rootPath, $project.FullName).Replace('\', '/')
    Write-Host "Building $relPath"

    $args = @('build', $project.FullName, '-c', $Configuration)
    if ($Framework) {
        $args += @('-f', $Framework)
    }
    if ($DotNetArgument) {
        $args += $DotNetArgument
    }

    dotnet @args
}

Write-Host 'Sample builds completed.'
