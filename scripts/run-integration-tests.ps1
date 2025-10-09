#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework,
    [string]$Platform,
    [switch]$ManagedOnly,
    [switch]$NativeOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($ManagedOnly -and $NativeOnly) {
    throw "Cannot combine --managed-only and --native-only."
}

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).ProviderPath

if ([string]::IsNullOrWhiteSpace($Platform)) {
    if ($IsWindows) {
        $Platform = 'windows'
    } elseif ($IsLinux) {
        $Platform = 'linux'
    } elseif ($IsMacOS) {
        $Platform = 'macos'
    } else {
        throw "Unsupported host platform."
    }
} else {
    $Platform = $Platform.ToLowerInvariant()
}

switch ($Platform) {
    'linux' { }
    'macos' { }
    'windows' { }
    default { throw "Unsupported platform '$Platform'. Expected linux, macos, or windows." }
}

$runManaged = -not $NativeOnly
$runNative = -not $ManagedOnly

function Get-ManagedProjects([string]$RootPath, [string]$PlatformFilter) {
    $results = @()
    $managedPath = Join-Path $RootPath 'integration/managed'
    if (Test-Path $managedPath) {
        $results += Get-ChildItem -Path $managedPath -Filter *.csproj -Recurse | Sort-Object FullName
    }
    if ($PlatformFilter -eq 'windows') {
        $windowsPath = Join-Path $RootPath 'integration/windows'
        if (Test-Path $windowsPath) {
            $results += Get-ChildItem -Path $windowsPath -Filter *.csproj -Recurse | Sort-Object FullName
        }
    }
    if ($results.Count -eq 0) {
        return @()
    }
    return ($results | ForEach-Object { $_.FullName })
}

function Get-NativeProjects([string]$RootPath, [string]$PlatformFilter) {
    $nativePath = Join-Path $RootPath 'integration/native'
    if (-not (Test-Path $nativePath)) {
        return @()
    }

    $files = Get-ChildItem -Path $nativePath -Filter *.csproj -Recurse | Sort-Object FullName
    $selected = @()
    foreach ($file in $files) {
        $name = $file.Name
        switch ($PlatformFilter) {
            'linux' {
                if ($name -match '(?i)linux') { $selected += $file.FullName }
            }
            'macos' {
                if ($name -match '(?i)(osx|mac|ios)') { $selected += $file.FullName }
            }
            'windows' {
                if ($name -match '(?i)win') { $selected += $file.FullName }
            }
        }
    }
    return $selected
}

function Invoke-IntegrationProject([string]$ProjectPath, [string]$RootPath, [string]$Configuration, [string]$Framework) {
    $relative = [System.IO.Path]::GetRelativePath($RootPath, $ProjectPath)
    Write-Host "Running integration project: $relative"
    $arguments = @('run', '--project', $ProjectPath, '-c', $Configuration)
    if (-not [string]::IsNullOrWhiteSpace($Framework)) {
        $arguments += @('-f', $Framework)
    }
    dotnet @arguments
}

Write-Host "Executing integration tests for platform '$Platform' (configuration: $Configuration)"

if ($runManaged) {
    $managed = Get-ManagedProjects -RootPath $root -PlatformFilter $Platform
    if ($managed.Count -eq 0) {
        Write-Warning "No managed integration projects found."
    } else {
        foreach ($project in $managed) {
            Invoke-IntegrationProject -ProjectPath $project -RootPath $root -Configuration $Configuration -Framework $Framework
        }
    }
}

if ($runNative) {
    $native = Get-NativeProjects -RootPath $root -PlatformFilter $Platform
    if ($native.Count -eq 0) {
        Write-Warning "No native integration projects matched the '$Platform' filter."
    } else {
        foreach ($project in $native) {
            Invoke-IntegrationProject -ProjectPath $project -RootPath $root -Configuration $Configuration -Framework $Framework
        }
    }
}

Write-Host "Integration test run completed."
