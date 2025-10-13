[CmdletBinding()]
param(
    [Alias('OutputDir')]
    [string]$NuGetOutput,
    [string]$NativeFeed,
    [ValidateSet('linux', 'windows', 'all')]
    [string]$Profile = 'all',
    [string[]]$Include = @(),
    [string[]]$Exclude = @(),
    [switch]$PrintProjects
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootPath = (Resolve-Path (Join-Path $scriptRoot '..')).Path

$windowsSpecificProjects = @(
    'bindings/VelloSharp.Integration.WinForms/VelloSharp.Integration.WinForms.csproj',
    'bindings/VelloSharp.Integration.Wpf/VelloSharp.Integration.Wpf.csproj',
    'bindings/VelloSharp.Maui/VelloSharp.Maui.csproj',
    'bindings/VelloSharp.Uno/VelloSharp.Uno.csproj',
    'bindings/VelloSharp.Uwp/VelloSharp.Uwp.csproj',
    'bindings/VelloSharp.WinForms.Core/VelloSharp.WinForms.Core.csproj',
    'bindings/VelloSharp.WinUI/VelloSharp.WinUI.csproj',
    'bindings/VelloSharp.Windows.Core/VelloSharp.Windows.Core.csproj',
    'src/VelloSharp.Charting.WinForms/VelloSharp.Charting.WinForms.csproj',
    'src/VelloSharp.Charting.Wpf/VelloSharp.Charting.Wpf.csproj',
    'src/VelloSharp.ChartRuntime.Windows/VelloSharp.ChartRuntime.Windows.csproj',
    'src/VelloSharp.Maui.Core/VelloSharp.Maui.Core.csproj',
    'src/VelloSharp.Windows.Shared/VelloSharp.Windows.Shared.csproj'
)

function Test-IsWindowsSpecificProject {
    param([string]$Project)
    return $windowsSpecificProjects -contains $Project
}

if ([string]::IsNullOrWhiteSpace($NuGetOutput)) {
    $NuGetOutput = Join-Path $rootPath 'artifacts/nuget'
}
$NuGetOutput = [System.IO.Path]::GetFullPath($NuGetOutput)

if ([string]::IsNullOrWhiteSpace($NativeFeed)) {
    $NativeFeed = $NuGetOutput
}
$NativeFeed = [System.IO.Path]::GetFullPath($NativeFeed)

New-Item -ItemType Directory -Force -Path $NuGetOutput | Out-Null

function Get-NormalizedRelativePath {
    param(
        [string]$Root,
        [string]$FullPath
    )

    $relative = [System.IO.Path]::GetRelativePath($Root, $FullPath)
    return $relative.Replace([System.IO.Path]::DirectorySeparatorChar, [char]'/')
}

function Test-IsPackable {
    param([string]$ProjectFile)

    try {
        $content = Get-Content -LiteralPath $ProjectFile -Raw
        if ([string]::IsNullOrWhiteSpace($content)) {
            return $false
        }

        $xml = [xml]$content
    }
    catch {
        Write-Verbose "Failed to parse '$ProjectFile' as XML. $_"
        return $false
    }

    if ($null -eq $xml.Project) {
        return $false
    }

    foreach ($propertyGroup in @($xml.Project.PropertyGroup)) {
        if ($null -eq $propertyGroup) {
            continue
        }

        foreach ($child in @($propertyGroup.ChildNodes)) {
            if ($null -eq $child) {
                continue
            }

            if ($child.NodeType -ne [System.Xml.XmlNodeType]::Element) {
                continue
            }

            if ($child.Name -ne 'IsPackable') {
                continue
            }

            $value = $child.InnerText
            if (-not [string]::IsNullOrWhiteSpace($value) -and $value.Trim().Equals('true', [System.StringComparison]::OrdinalIgnoreCase)) {
                return $true
            }
        }
    }

    return $false
}

function Get-PackableProjects {
    param(
        [string]$Root,
        [string[]]$Directories
    )

    $result = [System.Collections.Generic.List[string]]::new()

    foreach ($directoryName in $Directories) {
        $directoryPath = Join-Path $Root $directoryName
        if (-not (Test-Path $directoryPath -PathType Container)) {
            continue
        }

        Get-ChildItem -Path $directoryPath -Filter '*.csproj' -Recurse | Sort-Object -Property FullName | ForEach-Object {
            $projectPath = $_.FullName
            if (Test-IsPackable -ProjectFile $projectPath) {
                $result.Add((Get-NormalizedRelativePath -Root $Root -FullPath $projectPath))
            }
        }
    }

    return $result
}

function Get-ProjectExtraArgs {
    param(
        [string]$Project,
        [string]$ProjectPath
    )

    $args = [System.Collections.Generic.List[string]]::new()
    $args.Add('-p:VelloSkipNativeBuild=true')

    try {
        $content = Get-Content -LiteralPath $ProjectPath -Raw
    }
    catch {
        Write-Verbose "Failed to read '$ProjectPath'. $_"
        $content = $null
    }

    if ($content) {
        if ($content -match 'VelloIncludeNativeAssets') {
            $args.Add('-p:VelloIncludeNativeAssets=false')
        }

        if ($content -match 'VelloRequireAllNativeAssets') {
            $args.Add('-p:VelloRequireAllNativeAssets=false')
        }
    }

    if ($Project -eq 'bindings/VelloSharp.Gpu/VelloSharp.Gpu.csproj') {
        if (-not $args.Contains('-p:VelloSkipNativeBuild=true')) {
            $args.Add('-p:VelloSkipNativeBuild=true')
        }
        if (-not $args.Contains('-p:VelloIncludeNativeAssets=false')) {
            $args.Add('-p:VelloIncludeNativeAssets=false')
        }
        if (-not $args.Contains('-p:VelloRequireAllNativeAssets=false')) {
            $args.Add('-p:VelloRequireAllNativeAssets=false')
        }
    }

    return ,$args.ToArray()
}

$projects = Get-PackableProjects -Root $rootPath -Directories @('bindings', 'src')
$projects = @($projects)

switch ($Profile) {
    'linux' {
        $projects = $projects | Where-Object { -not (Test-IsWindowsSpecificProject $_) }
    }
    'windows' {
        $projects = $projects | Where-Object { Test-IsWindowsSpecificProject $_ }
    }
}

if ($Include.Count -gt 0) {
    Write-Verbose ("Applying include filters: {0}" -f ($Include -join ', '))
    $includeFiltered = foreach ($project in $projects) {
        foreach ($pattern in $Include) {
            if ([string]::IsNullOrWhiteSpace($pattern)) {
                continue
            }
            if ($project -like $pattern) {
                $project
                break
            }
        }
    }
    $projects = @($includeFiltered | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

if ($Exclude.Count -gt 0) {
    Write-Verbose ("Applying exclude filters: {0}" -f ($Exclude -join ', '))
    $excludeFiltered = foreach ($project in $projects) {
        $skip = $false
        foreach ($pattern in $Exclude) {
            if ([string]::IsNullOrWhiteSpace($pattern)) {
                continue
            }
            if ($project -like $pattern) {
                $skip = $true
                break
            }
        }
        if (-not $skip) {
            $project
        }
    }
    $projects = @($excludeFiltered)
}

$commonArgs = @('-c', 'Release', "-p:PackageOutputPath=$NuGetOutput", '-p:EnableWindowsTargeting=true', '-p:VelloUseNativePackageDependencies=true', '-p:VelloNativePackagesAvailable=true')
if ($NativeFeed) {
    $commonArgs += "-p:RestoreAdditionalProjectSources=$NativeFeed"
}

if ($projects.Count -eq 0) {
    $message = "No packable managed projects matched the requested filters under 'bindings' or 'src'."
    if ($PrintProjects) {
        Write-Verbose $message
        return
    }
    Write-Warning $message
    return
}

if ($PrintProjects) {
    $projects | ForEach-Object { Write-Output $_ }
    return
}

foreach ($project in $projects) {
    $projectPath = Join-Path $rootPath $project
    if (-not (Test-Path $projectPath -PathType Leaf)) {
        Write-Host "Skipping missing project '$project'."
        continue
    }

    $args = [System.Collections.Generic.List[string]]::new()
    $args.AddRange([string[]]$commonArgs)

    $projectExtraArgs = Get-ProjectExtraArgs -Project $project -ProjectPath $projectPath
    if ($projectExtraArgs.Count -gt 0) {
        $args.AddRange([string[]]$projectExtraArgs)
    }

    Write-Host "Packing $project"
    & dotnet pack $projectPath @args
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed with exit code $LASTEXITCODE for $projectPath."
    }
}

Write-Host "Managed packages created in '$NuGetOutput'."
