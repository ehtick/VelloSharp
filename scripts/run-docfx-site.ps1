[CmdletBinding()]
param(
    [int]$Port = 8080,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$DocFxArgs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootPath = (Resolve-Path (Join-Path $scriptRoot '..')).Path
$docfxJson = Join-Path $rootPath 'docs/docfx/docfx.json'

if (-not (Test-Path $docfxJson -PathType Leaf)) {
    throw "DocFX configuration not found at '$docfxJson'."
}

$env:DOCFX_MSBUILD_ARGS = '/p:EnableWindowsTargeting=true'

dotnet tool restore | Out-Null

$serveArgs = @('--serve', '--port', $Port.ToString())
if ($DocFxArgs) {
    $serveArgs += $DocFxArgs
}

Write-Host ("Starting DocFX preview on http://localhost:{0} ..." -f $Port)
dotnet tool run docfx $docfxJson @serveArgs
