[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$isWindowsPlatform = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)
if (-not $isWindowsPlatform) {
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

function Add-CargoBinToPath {
    $userProfile = [Environment]::GetFolderPath('UserProfile')
    if ([string]::IsNullOrWhiteSpace($userProfile)) {
        return
    }

    $cargoBin = Join-Path $userProfile '.cargo\bin'
    if ((Test-Path -Path $cargoBin) -and -not ($env:PATH.Split(';') -contains $cargoBin)) {
        $env:PATH = "$cargoBin;$env:PATH"
    }
}

function Add-PathEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Entry
    )

    if ([string]::IsNullOrWhiteSpace($Entry)) {
        return
    }

    $normalizedEntry = $Entry
    try {
        if (Test-Path -LiteralPath $Entry) {
            $normalizedEntry = (Get-Item -LiteralPath $Entry).FullName
        }
    } catch {
        $normalizedEntry = $Entry
    }

    $trimmed = $normalizedEntry.TrimEnd('\')
    $segments = $env:PATH -split ';'
    if (-not ($segments | Where-Object { $_.TrimEnd('\') -ieq $trimmed })) {
        $env:PATH = if ([string]::IsNullOrWhiteSpace($env:PATH)) {
            $normalizedEntry
        } else {
            "$normalizedEntry;$env:PATH"
        }
    }

    $userPath = [Environment]::GetEnvironmentVariable('PATH', 'User')
    $userSegments = if ([string]::IsNullOrWhiteSpace($userPath)) {
        @()
    } else {
        $userPath -split ';'
    }

    if (-not ($userSegments | Where-Object { $_.TrimEnd('\') -ieq $trimmed })) {
        $newUserPath = if ([string]::IsNullOrWhiteSpace($userPath)) {
            $normalizedEntry
        } else {
            "$normalizedEntry;$userPath"
        }
        [Environment]::SetEnvironmentVariable('PATH', $newUserPath, 'User')
    }
}

function Add-BinaryenToPath {
    param(
        [string]$InstallRoot
    )

    $candidateRoots = @()
    if (-not [string]::IsNullOrWhiteSpace($InstallRoot)) {
        $candidateRoots += $InstallRoot
    }

    $programFiles = [Environment]::GetFolderPath('ProgramFiles')
    if (-not [string]::IsNullOrWhiteSpace($programFiles)) {
        $candidateRoots += (Join-Path $programFiles 'Binaryen')
    }
    $localAppData = [Environment]::GetFolderPath('LocalApplicationData')
    if (-not [string]::IsNullOrWhiteSpace($localAppData)) {
        $candidateRoots += (Join-Path $localAppData 'Binaryen')
    }

    foreach ($root in $candidateRoots) {
        if ([string]::IsNullOrWhiteSpace($root)) {
            continue
        }

        $binPath = Join-Path $root 'bin'
        if (Test-Path -Path $binPath) {
            Add-PathEntry -Entry $binPath
        }
    }
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
        Write-Host '.NET SDK detected.'
        return
    }

    Write-Host '.NET SDK not detected. Installing latest LTS using dotnet-install...'
    $installScript = Join-Path $env:TEMP 'dotnet-install.ps1'
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installScript

    $installDir = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.dotnet'
    & $installScript -Channel 'LTS' -InstallDir $installDir | Out-Null
    Remove-Item $installScript -Force

    $env:PATH = "$installDir;$installDir\tools;$env:PATH"

    if (Test-Command -Name 'dotnet') {
        Write-Host ".NET SDK installed to $installDir."
    } else {
        Write-Warning "Installed .NET SDK to $installDir. Add this directory (and its 'tools' subdirectory) to your PATH."
    }
}

function Ensure-CppBuildTools {
    if (Test-VcTools) {
        Write-Host 'MSVC build tools detected.'
        return
    }

    $bootstrapperPath = Join-Path $env:TEMP 'vs_BuildTools.exe'
    Write-Host 'MSVC build tools not detected. Downloading Visual Studio Build Tools bootstrapper...'
    Invoke-WebRequest -Uri 'https://aka.ms/vs/17/release/vs_BuildTools.exe' -OutFile $bootstrapperPath

    $arguments = '--quiet --wait --norestart --nocache --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended --includeOptional'
    Write-Host 'Installing Visual Studio Build Tools (this might take several minutes)...'
    Start-Process -FilePath $bootstrapperPath -ArgumentList $arguments -Wait
    Remove-Item $bootstrapperPath -Force

    if (Test-VcTools) {
        Write-Host 'MSVC build tools installed.'
    } else {
        Write-Warning 'Attempted to install MSVC build tools, but they were not detected. Verify the installation manually.'
    }
}

Ensure-DotNet
Ensure-CppBuildTools

function Ensure-Rustup {
    Add-CargoBinToPath
    if ((Test-Command -Name 'cargo') -or (Test-Command -Name 'rustup')) {
        Write-Host 'Rust toolchain already detected.'
        return
    }

    Write-Host 'Installing Rust toolchain via rustup...'
    $rustupPath = Join-Path $env:TEMP 'rustup-init.exe'
    Invoke-WebRequest -Uri 'https://win.rustup.rs/x86_64' -OutFile $rustupPath
    & $rustupPath -y --profile minimal | Out-Null
    Remove-Item $rustupPath -Force
    Add-CargoBinToPath
}

Ensure-Rustup

function Ensure-WasmBindgenCli {
    Add-CargoBinToPath
    if (Test-Command -Name 'wasm-bindgen') {
        Write-Host 'wasm-bindgen CLI detected.'
        return
    }

    if (-not (Test-Command -Name 'cargo')) {
        Write-Warning 'Cargo not detected in PATH; skipping automatic wasm-bindgen-cli installation.'
        return
    }

    Write-Host 'Installing wasm-bindgen-cli via cargo...'
    cargo install wasm-bindgen-cli --force
    if (Test-Command -Name 'wasm-bindgen') {
        Write-Host 'wasm-bindgen CLI installed.'
    } else {
        Write-Warning 'Attempted to install wasm-bindgen-cli but the command is still not detected. Verify your PATH or install manually with `cargo install wasm-bindgen-cli`.'
    }
}

function Install-BinaryenFromGitHub {
    try {
        Write-Host 'Attempting to download Binaryen release from GitHub...'
        $headers = @{ 'User-Agent' = 'VelloSharp-Bootstrap' }
        $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/WebAssembly/binaryen/releases/latest' -Headers $headers -ErrorAction Stop
        if ($null -eq $release -or $null -eq $release.assets) {
            Write-Warning 'GitHub release metadata not available. Please install Binaryen manually.'
            return $false
        }

        $asset = $release.assets | Where-Object { $_.name -match 'x86_64-windows\.(zip|tar\.gz|tgz)$' } | Select-Object -First 1
        if ($null -eq $asset) {
            Write-Warning 'No Windows Binaryen asset found on the latest GitHub release.'
            return $false
        }

        $downloadUri = $asset.browser_download_url
        $archiveName = $asset.name
        $tempArchive = Join-Path $env:TEMP $archiveName
        Invoke-WebRequest -Uri $downloadUri -OutFile $tempArchive -ErrorAction Stop

        $extractRoot = Join-Path $env:TEMP ("binaryen-" + [System.Guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null

        if ($archiveName -match '\.zip$') {
            Expand-Archive -Path $tempArchive -DestinationPath $extractRoot -Force
        } elseif ($archiveName -match '\.(tar\.gz|tgz)$') {
            if (-not (Test-Command -Name 'tar')) {
                Write-Warning "Downloaded Binaryen archive '$archiveName' requires tar, but the 'tar' command was not found."
                Remove-Item -Path $tempArchive -Force
                Remove-Item -Path $extractRoot -Recurse -Force
                return $false
            }
            & tar -xf $tempArchive -C $extractRoot
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Failed to extract Binaryen archive '$archiveName' with tar."
                Remove-Item -Path $tempArchive -Force
                Remove-Item -Path $extractRoot -Recurse -Force
                return $false
            }
        } else {
            Write-Warning "Unrecognized Binaryen archive format: $archiveName"
            Remove-Item -Path $tempArchive -Force
            Remove-Item -Path $extractRoot -Recurse -Force
            return $false
        }

        $sourceDir = Get-ChildItem -Path $extractRoot -Directory | Select-Object -First 1
        if ($null -eq $sourceDir) {
            Write-Verbose "Unexpected archive layout for Binaryen asset $archiveName."
            Remove-Item -Path $tempArchive -Force
            Remove-Item -Path $extractRoot -Recurse -Force
            return $false
        }

        $installTargets = @()
        $programFiles = [Environment]::GetFolderPath('ProgramFiles')
        if (-not [string]::IsNullOrWhiteSpace($programFiles)) {
            $installTargets += (Join-Path $programFiles 'Binaryen')
        }
        $localAppData = [Environment]::GetFolderPath('LocalApplicationData')
        if (-not [string]::IsNullOrWhiteSpace($localAppData)) {
            $installTargets += (Join-Path $localAppData 'Binaryen')
        }

        foreach ($installDir in $installTargets) {
            try {
                if (Test-Path -Path $installDir) {
                    Remove-Item -Path $installDir -Recurse -Force
                }

                New-Item -Path $installDir -ItemType Directory -Force | Out-Null
                Copy-Item -Path (Join-Path $sourceDir.FullName '*') -Destination $installDir -Recurse -Force

                Add-BinaryenToPath -InstallRoot $installDir
                if (Test-Command -Name 'wasm-opt') {
                    Remove-Item -Path $tempArchive -Force
                    Remove-Item -Path $extractRoot -Recurse -Force
                    return $true
                }
            } catch {
                Write-Verbose "Binaryen installation attempt to $installDir failed: $_"
            }
        }

        Remove-Item -Path $tempArchive -Force
        Remove-Item -Path $extractRoot -Recurse -Force
        Write-Warning 'Binaryen files downloaded but wasm-opt still not detected. Add the installation directory to PATH manually.'
        return $false
    } catch {
        Write-Warning "Binaryen GitHub installation failed: $_"
        return $false
    }
}

function Ensure-Binaryen {
    Add-BinaryenToPath
    if (Test-Command -Name 'wasm-opt') {
        Write-Host 'wasm-opt (Binaryen) detected.'
        return
    }

    Write-Host 'wasm-opt not detected. Downloading Binaryen release...'
    if (-not (Install-BinaryenFromGitHub)) {
        Write-Warning 'Binaryen installation failed. Install it manually from https://github.com/WebAssembly/binaryen/releases.'
        return
    }

    Add-BinaryenToPath
    if (Test-Command -Name 'wasm-opt') {
        Write-Host 'Binaryen (wasm-opt) installed.'
    } else {
        Write-Warning 'Binaryen files installed but wasm-opt is still missing. Ensure the Binaryen bin directory is on PATH.'
    }
}

function Ensure-MauiWorkloads {
    Write-Host 'Installing .NET MAUI workloads...'
    & dotnet workload install maui
    if ($LASTEXITCODE -ne 0) {
        Write-Warning 'dotnet workload install maui reported a failure. Validate your installation or install the workloads manually via Visual Studio.'
    }
}

Ensure-MauiWorkloads
Ensure-WasmBindgenCli
Ensure-Binaryen

Write-Host 'Bootstrap complete. If this is the first install, restart your shell so the updated PATH is loaded.'
