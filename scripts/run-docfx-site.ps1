[CmdletBinding()]
param(
    [int]$Port = 3000,
    [switch]$NoSync,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArgs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$runDocsScript = Join-Path $scriptRoot 'run-docs-site.ps1'

if ($RemainingArgs) {
    Write-Warning 'Extra DocFX arguments are no longer supported and will be ignored.'
}

Write-Warning 'DocFX preview has been replaced by the Docusaurus site. Redirecting to run-docs-site.ps1.'

& $runDocsScript -Port $Port -NoSync:$NoSync
