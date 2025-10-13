param(
    [Parameter(Mandatory = $true)]
    [string]$BundlePath,

    [Parameter(Mandatory = $true)]
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$RuntimeIdentifier,

    [switch]$AllowUnsigned
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $BundlePath)) {
    throw "Bundle path '$BundlePath' was not found."
}

$expected = @{
    'win-x64'  = @(
        @{ Path = 'runtimes/win-x64/native/accesskit_ffi.dll'; Description = 'AccessKit bridge (x64)' },
        @{ Path = 'runtimes/win-x64/native/kurbo_ffi.dll'; Description = 'Kurbo primitives (x64)' },
        @{ Path = 'runtimes/win-x64/native/peniko_ffi.dll'; Description = 'Peniko primitives (x64)' },
        @{ Path = 'runtimes/win-x64/native/vello_ffi.dll'; Description = 'Vello GPU runtime (x64)' },
        @{ Path = 'runtimes/win-x64/native/vello_sparse_ffi.dll'; Description = 'Vello sparse runtime (x64)' },
        @{ Path = 'runtimes/win-x64/native/vello.backends.json'; Description = 'GPU backend manifest (x64)'; SkipSignature = $true },
        @{ Path = 'runtimes/win-x64/native/vello_chart_engine.dll'; Description = 'Chart engine (x64)' },
        @{ Path = 'runtimes/win-x64/native/vello_composition.dll'; Description = 'Composition helpers (x64)' },
        @{ Path = 'runtimes/win-x64/native/vello_editor_core.dll'; Description = 'Editor core (x64)' },
        @{ Path = 'runtimes/win-x64/native/vello_gauges_core.dll'; Description = 'Gauges core (x64)' },
        @{ Path = 'runtimes/win-x64/native/vello_scada_runtime.dll'; Description = 'SCADA runtime (x64)' },
        @{ Path = 'runtimes/win-x64/native/vello_tree_datagrid.dll'; Description = 'TreeDataGrid runtime (x64)' },
        @{ Path = 'runtimes/win-x64/native/winit_ffi.dll'; Description = 'Winit interop (x64)' }
    )
    'win-arm64' = @(
        @{ Path = 'runtimes/win-arm64/native/accesskit_ffi.dll'; Description = 'AccessKit bridge (ARM64)' },
        @{ Path = 'runtimes/win-arm64/native/kurbo_ffi.dll'; Description = 'Kurbo primitives (ARM64)' },
        @{ Path = 'runtimes/win-arm64/native/peniko_ffi.dll'; Description = 'Peniko primitives (ARM64)' },
        @{ Path = 'runtimes/win-arm64/native/vello_ffi.dll'; Description = 'Vello GPU runtime (ARM64)' },
        @{ Path = 'runtimes/win-arm64/native/vello_sparse_ffi.dll'; Description = 'Vello sparse runtime (ARM64)' },
        @{ Path = 'runtimes/win-arm64/native/vello.backends.json'; Description = 'GPU backend manifest (ARM64)'; SkipSignature = $true },
        @{ Path = 'runtimes/win-arm64/native/vello_chart_engine.dll'; Description = 'Chart engine (ARM64)' },
        @{ Path = 'runtimes/win-arm64/native/vello_composition.dll'; Description = 'Composition helpers (ARM64)' },
        @{ Path = 'runtimes/win-arm64/native/vello_editor_core.dll'; Description = 'Editor core (ARM64)' },
        @{ Path = 'runtimes/win-arm64/native/vello_gauges_core.dll'; Description = 'Gauges core (ARM64)' },
        @{ Path = 'runtimes/win-arm64/native/vello_scada_runtime.dll'; Description = 'SCADA runtime (ARM64)' },
        @{ Path = 'runtimes/win-arm64/native/vello_tree_datagrid.dll'; Description = 'TreeDataGrid runtime (ARM64)' },
        @{ Path = 'runtimes/win-arm64/native/winit_ffi.dll'; Description = 'Winit interop (ARM64)' }
    )
}

if (-not $expected.ContainsKey($RuntimeIdentifier)) {
    throw "Unsupported runtime identifier '$RuntimeIdentifier'."
}

$missing = @()
$invalidSignatures = @()

foreach ($entry in $expected[$RuntimeIdentifier]) {
    $candidate = Join-Path $BundlePath $entry.Path
    if (-not (Test-Path $candidate)) {
        $missing += $entry
        continue
    }

    $requiresSignature = -not ($entry.ContainsKey('SkipSignature') -and $entry.SkipSignature)
    if (-not $AllowUnsigned -and $requiresSignature) {
        try {
            $signature = Get-AuthenticodeSignature -FilePath $candidate
        } catch {
            $invalidSignatures += @{ Path = $entry.Path; Description = $entry.Description; Reason = $_.Exception.Message }
            continue
        }

        if ($signature.Status -ne 'Valid') {
            $reason = if ($signature.StatusMessage) { $signature.StatusMessage } else { "signature status '$($signature.Status)'" }
            $invalidSignatures += @{ Path = $entry.Path; Description = $entry.Description; Reason = $reason }
        }
    }
}

if ($missing.Count -gt 0) {
    Write-Host "Missing native assets for '$RuntimeIdentifier':" -ForegroundColor Red
    foreach ($item in $missing) {
        Write-Host "  - $($item.Path) ($($item.Description))" -ForegroundColor Red
    }
    exit 1
}

if ($invalidSignatures.Count -gt 0) {
    Write-Host "The following native assets failed signature validation:" -ForegroundColor Red
    foreach ($item in $invalidSignatures) {
        Write-Host "  - $($item.Path) ($($item.Description)) – $($item.Reason)" -ForegroundColor Red
    }
    exit 2
}

Write-Host "All WinUI native assets were found for '$RuntimeIdentifier' and signatures validated." -ForegroundColor Green
