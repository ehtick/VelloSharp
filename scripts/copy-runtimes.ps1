[CmdletBinding()]
param(
    [string]$ArtifactsDir,
    [string[]]$Targets
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootPath = (Resolve-Path (Join-Path $scriptRoot '..')).Path

if (-not $ArtifactsDir -or [string]::IsNullOrWhiteSpace($ArtifactsDir)) {
    $ArtifactsDir = Join-Path $rootPath 'artifacts/runtimes'
}
$ArtifactsDir = [System.IO.Path]::GetFullPath($ArtifactsDir)

if (-not (Test-Path $ArtifactsDir -PathType Container)) {
    Write-Error "No runtimes directory found at '$ArtifactsDir'."
    exit 1
}

if (-not $Targets -or $Targets.Count -eq 0) {
    $defaultTargets = @(
        'VelloSharp',
        'VelloSharp.Integration',
        'samples/AvaloniaVelloExamples',
        'samples/AvaloniaVelloPlayground',
        'samples/AvaloniaVelloWinitDemo',
        'samples/AvaloniaVelloX11Demo',
        'samples/AvaloniaVelloWin32Demo',
        'samples/AvaloniaVelloNativeDemo',
        'samples/AvaloniaVelloControlsSample',
        'samples/AvaloniaVelloHarfBuzzSample',
        'samples/AvaloniaVelloSkiaSharpSample',
        'samples/VelloSharp.Charting.AvaloniaSample',
        'samples/VelloSharp.TreeDataGrid.CompositionSample',
        'samples/VelloSharp.WithWinit',
        'samples/VelloSharp.WpfSample',
        'samples/VelloSharp.Uno.WinAppSdkSample',
        'samples/MauiVelloGallery',
        'samples/WinFormsMotionMarkShim'
    )

    $targetSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($item in $defaultTargets) {
        $null = $targetSet.Add($item)
    }

    $integrationRoot = Join-Path $rootPath 'integration'
    if (Test-Path $integrationRoot -PathType Container) {
        foreach ($project in Get-ChildItem -Path $integrationRoot -Recurse -Filter '*.csproj' -File) {
            $relative = [System.IO.Path]::GetRelativePath($rootPath, $project.Directory.FullName)
            if (-not [string]::IsNullOrWhiteSpace($relative)) {
                $relative = $relative -replace '\\', '/'
                if ($targetSet.Add($relative)) {
                    $defaultTargets += $relative
                }
            }
        }
    }

    $Targets = $defaultTargets
}

if ($env:COPY_CONFIGURATIONS) {
    $configurations = ($env:COPY_CONFIGURATIONS -split '\s+') | Where-Object { $_ }
} else {
    $configurations = @('Debug', 'Release')
}

if ($env:COPY_TARGET_FRAMEWORKS) {
    $targetFrameworks = ($env:COPY_TARGET_FRAMEWORKS -split '\s+') | Where-Object { $_ }
} else {
    $targetFrameworks = @('net8.0', 'net8.0-windows', 'net8.0-windows10.0.19041.0')
}

function Copy-Payload {
    param(
        [string]$Destination,
        [bool]$Delete = $false,
        [string]$Source
    )

    if (-not $Source) {
        $Source = $ArtifactsDir
    }

    if (-not (Test-Path $Source -PathType Container)) {
        Write-Host "Skipping copy into '$Destination' (source '$Source' not found)."
        return
    }

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null

    $robocopy = Get-Command robocopy -ErrorAction SilentlyContinue
    if ($robocopy) {
        if ($Delete) {
            $args = @($Source, $Destination, '*', '/MIR', '/NFL', '/NDL', '/NJH', '/NJS', '/NP', '/R:3', '/W:5')
        } else {
            $args = @($Source, $Destination, '*', '/E', '/NFL', '/NDL', '/NJH', '/NJS', '/NP', '/R:3', '/W:5')
        }
        & $robocopy @args | Out-Null
        $code = $LASTEXITCODE
        if ($code -ge 8) {
            throw "robocopy failed with exit code $code while copying '$Source' to '$Destination'."
        }
    } else {
        if ($Delete -and (Test-Path $Destination -PathType Container)) {
            Get-ChildItem -Path $Destination -Force | Remove-Item -Recurse -Force
        }
        Copy-Item -Path (Join-Path $Source '*') -Destination $Destination -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Copying runtimes from '$ArtifactsDir'"
foreach ($target in $Targets) {
    $targetRoot = Join-Path $rootPath $target
    if (-not (Test-Path $targetRoot -PathType Container)) {
        Write-Host "Skipping '$target' (directory not found)."
        continue
    }

    foreach ($configuration in $configurations) {
        $configurationRoot = Join-Path (Join-Path $targetRoot 'bin') $configuration
        $frameworkList = [System.Collections.Generic.List[string]]::new()
        $frameworkSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

        foreach ($framework in $targetFrameworks) {
            if (-not [string]::IsNullOrWhiteSpace($framework) -and $frameworkSet.Add($framework)) {
                [void]$frameworkList.Add($framework)
            }
        }

        if (Test-Path $configurationRoot -PathType Container) {
            foreach ($frameworkDir in Get-ChildItem -Path $configurationRoot -Directory -ErrorAction SilentlyContinue) {
                $frameworkName = $frameworkDir.Name
                if ([string]::IsNullOrWhiteSpace($frameworkName)) {
                    continue
                }
                if ($frameworkSet.Add($frameworkName)) {
                    [void]$frameworkList.Add($frameworkName)
                }
            }
        }

        foreach ($framework in $frameworkList) {
            $outputBase = Join-Path $configurationRoot $framework
            if (-not (Test-Path $outputBase -PathType Container)) {
                Write-Host "Skipping '$target' ($configuration|$framework) – build output not found."
                continue
            }

            $dest = Join-Path $outputBase 'runtimes'
            Copy-Payload -Destination $dest
            Write-Host "Copied runtimes to '$dest'"
        }
    }
}

Write-Host "Synchronizing runtimes into packaging projects"
$packagingRoot = Join-Path $rootPath 'packaging'
foreach ($ffiDir in Get-ChildItem -Path $packagingRoot -Directory -Filter 'VelloSharp.Native.*') {
    if ($ffiDir.FullName -eq (Join-Path $packagingRoot 'VelloSharp.Native')) {
        continue
    }

    foreach ($project in Get-ChildItem -Path $ffiDir.FullName -Filter '*.csproj' -File) {
        $segments = $project.BaseName.Split('.')
        if ($segments.Length -lt 4) {
            continue
        }

        $rid = $segments[-1]
        $sourceDir = Join-Path (Join-Path $ArtifactsDir $rid) 'native'
        if (-not (Test-Path $sourceDir -PathType Container)) {
            Write-Host "Skipping packaging '$($ffiDir.Name)' for '$rid' (no artifacts)."
            continue
        }

        $destDir = Join-Path (Join-Path (Join-Path $ffiDir.FullName 'runtimes') $rid) 'native'
        Copy-Payload -Destination $destDir -Delete $true -Source $sourceDir
        if (Test-Path $destDir -PathType Container) {
            Write-Host "Copied runtimes to '$destDir'"
        }
    }
}



