[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot '..')).Path
$docfxDir = Join-Path $repoRoot 'docs/docfx'
$docfxJson = Join-Path $docfxDir 'docfx.json'
$websiteDir = Join-Path $repoRoot 'docs/website'
$generatedDir = Join-Path $websiteDir 'generated/dotnet-api'
$docfxOutputDir = Join-Path $docfxDir 'obj/api'
$apiIndex = Join-Path $docfxDir 'api/index.md'

if (-not (Test-Path $docfxJson -PathType Leaf)) {
    throw "DocFX configuration not found at '$docfxJson'."
}

dotnet tool restore | Out-Null

try {
    Write-Host 'Generating DocFX metadata as Markdown...'
    dotnet tool run docfx metadata $docfxJson | Out-Null

    if (Test-Path $generatedDir) {
        Remove-Item -Recurse -Force $generatedDir
    }

    New-Item -ItemType Directory -Path $generatedDir | Out-Null

    Write-Host 'Copying Markdown artifacts into docs/website/generated/dotnet-api...'
    Get-ChildItem -Path $docfxOutputDir -Filter '*.md' -Recurse | ForEach-Object {
        $relativePath = $_.FullName.Substring($docfxOutputDir.Length + 1)
        $targetPath = Join-Path $generatedDir $relativePath
        $targetDir = Split-Path -Parent $targetPath
        if (-not (Test-Path $targetDir)) {
            New-Item -ItemType Directory -Path $targetDir | Out-Null
        }
        Copy-Item -Path $_.FullName -Destination $targetPath -Force
    }

    if (Test-Path $apiIndex -PathType Leaf) {
        Copy-Item -Path $apiIndex -Destination (Join-Path $generatedDir 'index.md') -Force
    }

    Write-Host 'Dotnet API docs synced.'
}
finally {
    if ($null -ne $previousMsbuildArgs) {
        $env:DOCFX_MSBUILD_ARGS = $previousMsbuildArgs
    } else {
        Remove-Item Env:DOCFX_MSBUILD_ARGS -ErrorAction Ignore
    }
}
$previousMsbuildArgs = $env:DOCFX_MSBUILD_ARGS
$env:DOCFX_MSBUILD_ARGS = '/p:EnableWindowsTargeting=true'
