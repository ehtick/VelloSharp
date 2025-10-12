param(
    [Parameter(Mandatory = $true)]
    [string]$BundlePath,

    [Parameter(Mandatory = $true)]
    [ValidateSet(''android'', ''ios'', ''iossimulator'', ''maccatalyst'', ''windows'')]
    [string]$Platform
)

if (-not (Test-Path $BundlePath)) {
    throw "Bundle path '$BundlePath' was not found."
}

$expected = @{
    android      = @(
        @{ Path = 'lib/arm64-v8a/libvello.so'; Description = 'Vello runtime (arm64)' },
        @{ Path = 'lib/arm64-v8a/libvellosparse.so'; Description = 'Vello sparse runtime (arm64)' },
        @{ Path = 'lib/arm64-v8a/libaccesskitwgpu.so'; Description = 'AccessKit bridge (arm64)' }
    )
    iossimulator = @(
        @{ Path = 'Frameworks/libvello.dylib'; Description = 'Vello runtime (simulator)' },
        @{ Path = 'Frameworks/libvellosparse.dylib'; Description = 'Vello sparse runtime (simulator)' }
    )
    ios          = @(
        @{ Path = 'Frameworks/libvello.dylib'; Description = 'Vello runtime (device)' },
        @{ Path = 'Frameworks/libvellosparse.dylib'; Description = 'Vello sparse runtime (device)' }
    )
    maccatalyst  = @(
        @{ Path = 'Frameworks/libvello.dylib'; Description = 'Vello runtime (MacCatalyst)' },
        @{ Path = 'Frameworks/libvellosparse.dylib'; Description = 'Vello sparse runtime (MacCatalyst)' }
    )
    windows      = @(
        @{ Path = 'runtimes/win-x64/native/vello.dll'; Description = 'Vello runtime (WinUI)' },
        @{ Path = 'runtimes/win-x64/native/vello_sparse.dll'; Description = 'Vello sparse runtime (WinUI)' }
    )
}

if (-not $expected.ContainsKey($Platform)) {
    throw "Unsupported platform '$Platform'."
}

$missing = @()
foreach ($entry in $expected[$Platform]) {
    $candidate = Join-Path $BundlePath $entry.Path
    if (-not (Test-Path $candidate)) {
        $missing += $entry
    }
}

if ($missing.Count -gt 0) {
    Write-Host "The following native assets are missing from '$BundlePath':" -ForegroundColor Red
    foreach ($item in $missing) {
        Write-Host "  - $($item.Path) ($($item.Description))" -ForegroundColor Red
    }
    exit 1
}

Write-Host "All expected VelloSharp native assets were found for '$Platform'." -ForegroundColor Green
