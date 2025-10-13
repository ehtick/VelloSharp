[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$websiteDir = (Resolve-Path (Join-Path $scriptRoot '..\docs\website')).Path
$nodeScript = Join-Path $websiteDir 'scripts/sync-dotnet-api-docs.mjs'

if (-not (Test-Path $nodeScript -PathType Leaf)) {
    throw "Sync script not found at '$nodeScript'."
}

Push-Location $websiteDir
try {
    node $nodeScript
}
finally {
    Pop-Location
}
