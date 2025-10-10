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

function Get-HostArchitecture {
    switch ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture) {
        'X64' { return 'x64' }
        'Arm64' { return 'arm64' }
        default { return $null }
    }
}

function Test-NativeArchitectureSupport {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DirectoryName,
        [string]$HostArchitecture
    )

    if ([string]::IsNullOrWhiteSpace($HostArchitecture)) {
        return $true
    }

    $name = $DirectoryName.ToLowerInvariant()
    switch ($HostArchitecture.ToLowerInvariant()) {
        'x64' { return ($name -like '*x64*' -or $name -like '*x86_64*' -or $name -like '*amd64*') }
        'arm64' { return ($name -like '*arm64*' -or $name -like '*aarch64*') }
        default { return $true }
    }
}

function Add-PathValue {
    param(
        [string]$Existing,
        [string[]]$NewPaths,
        [char]$Separator
    )

    $normalizedNew = @()
    foreach ($path in $NewPaths) {
        if (-not [string]::IsNullOrWhiteSpace($path)) {
            $normalizedNew += [System.IO.Path]::GetFullPath($path)
        }
    }

    $result = @()
    foreach ($path in $normalizedNew) {
        if ($result -notcontains $path) {
            $result += $path
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($Existing)) {
        $existingParts = $Existing.Split($Separator, [System.StringSplitOptions]::RemoveEmptyEntries)
        foreach ($part in $existingParts) {
            if ($result -notcontains $part) {
                $result += $part
            }
        }
    }

    return [string]::Join($Separator, $result)
}

function Initialize-NativeLibrarySearchPath {
    param(
        [string]$RootPath,
        [string]$Platform,
        [string]$HostArchitecture
    )

    $runtimeRoot = Join-Path $RootPath 'artifacts/runtimes'
    if (-not (Test-Path $runtimeRoot -PathType Container)) {
        return
    }

    $ridPrefix = switch ($Platform) {
        'windows' { 'win' }
        'linux' { 'linux' }
        'macos' { 'osx' }
        default { $null }
    }

    if (-not $ridPrefix) {
        return
    }

    $searchPaths = @()
    if (-not [string]::IsNullOrWhiteSpace($HostArchitecture)) {
        $candidate = Join-Path $runtimeRoot "$ridPrefix-$HostArchitecture"
        $nativePath = Join-Path $candidate 'native'
        if (Test-Path $nativePath -PathType Container) {
            $searchPaths += $nativePath
        }
    }

    if ($searchPaths.Count -eq 0) {
        return
    }

    $separator = [System.IO.Path]::PathSeparator
    switch ($Platform) {
        'windows' {
            $env:PATH = Add-PathValue -Existing $env:PATH -NewPaths $searchPaths -Separator $separator
        }
        'linux' {
            $env:LD_LIBRARY_PATH = Add-PathValue -Existing $env:LD_LIBRARY_PATH -NewPaths $searchPaths -Separator $separator
        }
        'macos' {
            $env:DYLD_LIBRARY_PATH = Add-PathValue -Existing $env:DYLD_LIBRARY_PATH -NewPaths $searchPaths -Separator $separator
        }
    }

    Write-Host "Using native runtime search paths:"
    foreach ($path in $searchPaths) {
        Write-Host "  $path"
    }
}

function Test-UnoIntegrationPrerequisites {
    if (-not $IsWindows) {
        return $true
    }

    try {
        $dotnet = Get-Command dotnet -ErrorAction Stop
        $dotnetRoot = Split-Path -Parent $dotnet.Path
        $sdkRoot = Join-Path $dotnetRoot 'sdk'
        if (-not (Test-Path $sdkRoot -PathType Container)) {
            return $false
        }

        $sdkDirs = Get-ChildItem -Path $sdkRoot -Directory | Sort-Object Name -Descending
        foreach ($sdkDir in $sdkDirs) {
            $taskPath = Join-Path $sdkDir.FullName 'Microsoft\VisualStudio\v17.0\AppxPackage\Microsoft.Build.Packaging.Pri.Tasks.dll'
            if (Test-Path $taskPath -PathType Leaf) {
                return $true
            }
        }
    } catch {
        return $false
    }

    return $false
}

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

function Get-SupportedIntegrationPlatforms([string]$ProjectPath) {
    try {
        $xml = [xml](Get-Content -Path $ProjectPath -Raw)
    } catch {
        return $null
    }

    if (-not $xml.Project) {
        return $null
    }

    $documentElement = $xml.DocumentElement
    if ($null -eq $documentElement) {
        return $null
    }

    if ([string]::IsNullOrWhiteSpace($documentElement.NamespaceURI)) {
        $node = $xml.SelectSingleNode('//SupportedIntegrationPlatforms')
    } else {
        $nsManager = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
        $nsManager.AddNamespace('msb', $documentElement.NamespaceURI)
        $node = $xml.SelectSingleNode('//msb:SupportedIntegrationPlatforms', $nsManager)
    }

    if ($null -eq $node) {
        return $null
    }

    $text = $node.InnerText
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    return $text
}

function Test-IntegrationPlatformSupport([string]$ProjectPath, [string]$Platform) {
    $raw = Get-SupportedIntegrationPlatforms -ProjectPath $ProjectPath
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return $true
    }

    $tokens = $raw -split '[,;\s]+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    foreach ($token in $tokens) {
        $normalized = $token.ToLowerInvariant()
        if ($normalized -eq 'all' -or $normalized -eq $Platform) {
            return $true
        }
    }

    return $false
}

function Get-NativeProjects([string]$RootPath, [string]$PlatformFilter, [string]$HostArchitecture, [bool]$RestrictByArch) {
    $nativePath = Join-Path $RootPath 'integration/native'
    if (-not (Test-Path $nativePath)) {
        return @()
    }

    $files = Get-ChildItem -Path $nativePath -Filter *.csproj -Recurse | Sort-Object FullName
    $selected = @()
    foreach ($file in $files) {
        $directory = Split-Path -Leaf $file.DirectoryName
        $directoryLower = $directory.ToLowerInvariant()
        $matchesPlatform =
            if ($PlatformFilter -eq 'linux') {
                $directoryLower -like 'linux*'
            } elseif ($PlatformFilter -eq 'macos') {
                ($directoryLower -like 'osx*') -or ($directoryLower -like 'ios*')
            } elseif ($PlatformFilter -eq 'windows') {
                $directoryLower -like 'win*'
            } else {
                $true
            }
        if (-not $matchesPlatform) {
            continue
        }
        if ($RestrictByArch -and -not (Test-NativeArchitectureSupport -DirectoryName $directoryLower -HostArchitecture $HostArchitecture)) {
            $relative = [System.IO.Path]::GetRelativePath($RootPath, $file.FullName)
            Write-Host "Skipping native integration project '$relative' (host architecture $HostArchitecture not supported)."
            continue
        }
        $selected += $file.FullName
    }
    return ,$selected
}

$inCi = ([bool]$env:CI) -or ([bool]$env:TF_BUILD) -or ([bool]$env:GITHUB_ACTIONS)
$hostArchitecture = Get-HostArchitecture
Initialize-NativeLibrarySearchPath -RootPath $root -Platform $Platform -HostArchitecture $hostArchitecture
$unoPrerequisitesAvailable = Test-UnoIntegrationPrerequisites

function Invoke-IntegrationProject([string]$ProjectPath, [string]$RootPath, [string]$Configuration, [string]$Framework, [string[]]$AdditionalMsBuildArguments) {
    $relative = [System.IO.Path]::GetRelativePath($RootPath, $ProjectPath)
    Write-Host "Running integration project: $relative"
    $arguments = @('run', '--project', $ProjectPath, '-c', $Configuration)
    if (-not [string]::IsNullOrWhiteSpace($Framework)) {
        $arguments += @('-f', $Framework)
    }
    if ($AdditionalMsBuildArguments -and $AdditionalMsBuildArguments.Count -gt 0) {
        $arguments += $AdditionalMsBuildArguments
    }
    dotnet @arguments
}

Write-Host "Executing integration tests for platform '$Platform' (configuration: $Configuration)"

if ($runManaged) {
    $managed = Get-ManagedProjects -RootPath $root -PlatformFilter $Platform
    $windowsIntegrationRoot = Join-Path 'integration' 'windows'
    $skipUnoIntegration = ($inCi -or -not $unoPrerequisitesAvailable)
    if ($skipUnoIntegration -and $managed.Count -gt 0) {
        $filtered = @()
        foreach ($project in $managed) {
            if ($project -like '*VelloSharp.Uno.Integration*') {
                $relative = [System.IO.Path]::GetRelativePath($root, $project)
                if ($inCi) {
                    Write-Host "Skipping integration project '$relative' (temporarily disabled on CI)."
                } else {
                    Write-Host "Skipping integration project '$relative' (required Windows App SDK tooling not found)."
                }
                continue
            }
            $filtered += $project
        }
        $managed = $filtered
    }
    if ($managed.Count -eq 0) {
        Write-Warning "No managed integration projects found."
    } else {
        foreach ($project in $managed) {
            if (-not (Test-IntegrationPlatformSupport -ProjectPath $project -Platform $Platform)) {
                $relative = [System.IO.Path]::GetRelativePath($root, $project)
                Write-Host "Skipping integration project '$relative' (unsupported on $Platform)."
                continue
            }
            $relative = [System.IO.Path]::GetRelativePath($root, $project)
            $additionalArgs = @()
            if ($relative.StartsWith($windowsIntegrationRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                $additionalArgs += '-p:ManagePackageVersionsCentrally=false'
            }
            Invoke-IntegrationProject -ProjectPath $project -RootPath $root -Configuration $Configuration -Framework $Framework -AdditionalMsBuildArguments $additionalArgs
        }
    }
}

if ($runNative) {
    $native = Get-NativeProjects -RootPath $root -PlatformFilter $Platform -HostArchitecture $hostArchitecture -RestrictByArch:$true
    if ($native.Count -eq 0) {
        Write-Warning "No native integration projects matched the '$Platform' filter."
    } else {
        foreach ($project in $native) {
            Invoke-IntegrationProject -ProjectPath $project -RootPath $root -Configuration $Configuration -Framework $Framework
        }
    }
}

Write-Host "Integration test run completed."
