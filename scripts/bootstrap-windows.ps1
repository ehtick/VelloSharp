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

function Test-VcTools {
    $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    if ([string]::IsNullOrWhiteSpace($programFilesX86)) {
        $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles')
    }

    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $vswherePath = Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'
        if (Test-Path -Path $vswherePath) {
            $installationPath = & $vswherePath -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2>$null
            if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($installationPath)) {
                return $true
            }
        }
    }

    return (Test-Command -Name 'cl.exe')
}

function Ensure-DotNet {
    if (Test-Command -Name 'dotnet') {
       & dotnet workload restore maui '.NET SDK detected.'
        return
    }

   & dotnet workload restore maui '.NET SDK not detected. Installing latest LTS using dotnet-install...'
    $installScript = Join-Path $env:TEMP 'dotnet-install.ps1'
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installScript

    $installDir = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.dotnet'
    & $installScript -Channel 'LTS' -InstallDir $installDir | Out-Null
    Remove-Item $installScript -Force

    $env:PATH = "$installDir;$installDir\tools;$env:PATH"

    if (Test-Command -Name 'dotnet') {
       & dotnet workload restore maui ".NET SDK installed to $installDir."
    } else {
        Write-Warning "Installed .NET SDK to $installDir. Add this directory (and its 'tools' subdirectory) to your PATH."
    }
}

function Ensure-CppBuildTools {
    if (Test-VcTools) {
       & dotnet workload restore maui 'MSVC build tools detected.'
        return
    }

    $bootstrapperPath = Join-Path $env:TEMP 'vs_BuildTools.exe'
   & dotnet workload restore maui 'MSVC build tools not detected. Downloading Visual Studio Build Tools bootstrapper...'
    Invoke-WebRequest -Uri 'https://aka.ms/vs/17/release/vs_BuildTools.exe' -OutFile $bootstrapperPath

    $arguments = '--quiet --wait --norestart --nocache --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended --includeOptional'
   & dotnet workload restore maui 'Installing Visual Studio Build Tools (this might take several minutes)...'
    Start-Process -FilePath $bootstrapperPath -ArgumentList $arguments -Wait
    Remove-Item $bootstrapperPath -Force

    if (Test-VcTools) {
       & dotnet workload restore maui 'MSVC build tools installed.'
    } else {
        Write-Warning 'Attempted to install MSVC build tools, but they were not detected. Verify the installation manually.'
    }
}

Ensure-DotNet
Ensure-CppBuildTools

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

function Ensure-MauiWorkloads {
    Write-Host 'Restoring .NET MAUI workloads...'
    & dotnet workload restore maui
    if ($LASTEXITCODE -ne 0) {
        Write-Warning 'dotnet workload restore maui reported a failure. Validate your installation or install the workloads manually via Visual Studio.'
    }
}

Ensure-MauiWorkloads

Write-Host 'Bootstrap complete. If this is the first install, restart your shell so the updated PATH is loaded.'

