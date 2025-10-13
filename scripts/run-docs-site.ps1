[CmdletBinding()]
param(
    [switch]$NoSync,
    [int]$Port = 3000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot '..')).Path
$websiteDir = Join-Path $repoRoot 'docs/website'
$previousSkip = $env:SKIP_DOTNET_API_SYNC

if (-not (Test-Path $websiteDir -PathType Container)) {
    throw "Docusaurus site not found at '$websiteDir'."
}

if ($NoSync) {
    $env:SKIP_DOTNET_API_SYNC = '1'
} else {
    Remove-Item Env:SKIP_DOTNET_API_SYNC -ErrorAction Ignore
}

Push-Location $websiteDir
try {
    Write-Host "Starting Docusaurus dev server on http://localhost:$Port ..."
    npm run start -- --port $Port
}
finally {
    Pop-Location
    if ($null -ne $previousSkip) {
        $env:SKIP_DOTNET_API_SYNC = $previousSkip
    } else {
        Remove-Item Env:SKIP_DOTNET_API_SYNC -ErrorAction Ignore
    }
}
