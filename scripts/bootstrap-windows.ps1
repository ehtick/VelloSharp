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

function Test-VsBuildTools {
    if (Test-Command -Name 'cl.exe') {
        return $true
    }

    $vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswherePath)) {
        return $false
    }

    $result = & $vswherePath -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2>$null
    return -not [string]::IsNullOrWhiteSpace($result)
}

$wingetAvailable = Test-Command -Name 'winget'
$chocoAvailable = Test-Command -Name 'choco'

function Install-WithWinget {
    param(
        [Parameter(Mandatory = $true)][string]$Id,
        [Parameter(Mandatory = $true)][string]$DisplayName,
        [string]$Override
    )

    if (-not $wingetAvailable) {
        return $false
    }

    Write-Host "Installing $DisplayName via winget..."
    $args = @('install', '--id', $Id, '--exact', '--source', 'winget', '--silent', '--accept-package-agreements', '--accept-source-agreements')
    if ($Override) {
        $args += @('--override', $Override)
    }
    winget @args | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "winget failed to install $DisplayName."
        return $false
    }
    return $true
}

function Install-WithChoco {
    param(
        [Parameter(Mandatory = $true)][string]$Id,
        [Parameter(Mandatory = $true)][string]$DisplayName,
        [string]$Params
    )

    if (-not $chocoAvailable) {
        return $false
    }

    Write-Host "Installing $DisplayName via Chocolatey..."
    $args = @('install', $Id, '-y')
    if ($Params) {
        $args += @('--params', $Params)
    }
    choco @args | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Chocolatey failed to install $DisplayName."
        return $false
    }
    return $true
}

function Ensure-Package {
    param(
        [Parameter(Mandatory = $true)][string]$DisplayName,
        [Parameter(Mandatory = $true)][scriptblock]$Check,
        [string]$WingetId,
        [string]$ChocoId,
        [string]$WingetOverride,
        [string]$ChocoParams
    )

    if (& $Check) {
        Write-Host "$DisplayName already present."
        return
    }

    if ($WingetId -and (Install-WithWinget -Id $WingetId -DisplayName $DisplayName -Override $WingetOverride)) {
        return
    }

    if ($ChocoId -and (Install-WithChoco -Id $ChocoId -DisplayName $DisplayName -Params $ChocoParams)) {
        return
    }

    Write-Warning "Unable to install $DisplayName automatically. Please install it manually."
}

Ensure-Package -DisplayName 'Visual Studio Build Tools (VC++)' -Check { Test-VsBuildTools } -WingetId 'Microsoft.VisualStudio.2022.BuildTools' -ChocoId 'visualstudio2022buildtools' -WingetOverride '--quiet --wait --norestart --nocache --add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Workload.NativeDesktop --includeRecommended --includeOptional' -ChocoParams '"--add Microsoft.VisualStudio.Workload.VCTools --includeRecommended --includeOptional"'

Ensure-Package -DisplayName 'CMake' -Check { Test-Command -Name 'cmake' } -WingetId 'Kitware.CMake' -ChocoId 'cmake'
Ensure-Package -DisplayName 'Ninja' -Check { Test-Command -Name 'ninja' } -WingetId 'Ninja-build.Ninja' -ChocoId 'ninja'
Ensure-Package -DisplayName 'Git' -Check { Test-Command -Name 'git' } -WingetId 'Git.Git' -ChocoId 'git'

function Ensure-Rustup {
    if (Test-Command -Name 'cargo' -or Test-Command -Name 'rustup') {
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
