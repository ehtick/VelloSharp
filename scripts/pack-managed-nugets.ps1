[CmdletBinding()]
param(
    [string]$NuGetOutput,
    [string]$NativeFeed
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootPath = (Resolve-Path (Join-Path $scriptRoot '..')).Path

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

$commonArgs = @('-c', 'Release', "-p:PackageOutputPath=$NuGetOutput", '-p:EnableWindowsTargeting=true', '-p:VelloUseNativePackageDependencies=true')
if ($NativeFeed) {
    $commonArgs += "-p:RestoreAdditionalProjectSources=$NativeFeed"
}

if ($projects.Count -eq 0) {
    Write-Warning "No packable managed projects were found under 'bindings' or 'src'."
}

foreach ($project in @($projects)) {
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
