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

function Supports-Project {
    param([string]$Path, [string]$Platform)

    $relative = [System.IO.Path]::GetRelativePath($rootPath, $Path)
    switch -Wildcard ($relative.Replace('\', '/')) {
        'integration/native/linux-*' { return $Platform -eq 'linux' }
        'integration/native/osx-*'   { return $Platform -eq 'macos' }
        'integration/native/win-*'   { return $Platform -eq 'windows' }
        'integration/windows/*'      { return $Platform -eq 'windows' }
        default                      { return $true }
    }
}

$projectPaths = Get-ChildItem -Path (Join-Path $rootPath 'integration') -Recurse -Filter '*.csproj' -File | Sort-Object FullName
if (-not $projectPaths) {
    Write-Error "No integration projects found under integration/."
    exit 1
}

Write-Host "Building integration projects (configuration: $Configuration)"

foreach ($project in $projectPaths) {
    if (-not (Supports-Project -Path $project.FullName -Platform $platform)) {
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

Write-Host 'Integration builds completed.'
