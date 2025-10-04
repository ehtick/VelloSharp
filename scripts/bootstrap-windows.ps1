[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

if (-not $IsWindows) {
    Write-Error 'This bootstrap script targets Windows.'
    exit 1
}

function Test-IsAdmin {
    $current = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($current)
    return $principal.IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
}

if (-not (Test-IsAdmin)) {
    Write-Error 'Administrator privileges are required. Please re-run this script from an elevated PowerShell session.'
    exit 1
}

function Test-Command {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    return (Get-Command -Name $Name -ErrorAction SilentlyContinue) -ne $null
}

if (Test-Command -Name 'dotnet') {
    Write-Host '.NET SDK detected.'
} else {
    Write-Warning '.NET SDK not detected. Please install the .NET SDK before continuing.'
}

function Ensure-Rustup {
    if ((Test-Command -Name 'cargo') -or (Test-Command -Name 'rustup')) {
        Write-Host 'Rust toolchain already detected.'
        return
    }

    Write-Host 'Installing Rust toolchain via rustup...'
    $rustupPath = Join-Path $env:TEMP 'rustup-init.exe'
    Invoke-WebRequest -Uri 'https://win.rustup.rs/x86_64' -OutFile $rustupPath
    & $rustupPath -y --profile minimal | Out-Null
    Remove-Item $rustupPath -Force
}

Ensure-Rustup

Write-Host 'Bootstrap complete. If this is the first install, restart your shell so the updated PATH is loaded.'
