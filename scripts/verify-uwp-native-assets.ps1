param(
    [Parameter(Mandatory = $true)]
    [string]$BundlePath,

    [Parameter(Mandatory = $true)]
    [ValidateSet('win10-x64', 'win10-arm64')]
    [string]$RuntimeIdentifier,

    [switch]$AllowUnsigned
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $BundlePath)) {
    throw "Bundle path '$BundlePath' was not found."
}

function Get-AssetPath {
    param(
        [string]$RelativePath,
        [string]$FallbackRid
    )

    $primary = Join-Path $BundlePath $RelativePath
    if (Test-Path $primary) {
        return $primary
    }

    if ($FallbackRid) {
        $fallbackPath = $RelativePath -replace '^runtimes/[^/]+/', "runtimes/$FallbackRid/"
        $candidate = Join-Path $BundlePath $fallbackPath
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

$fallbackMap = @{
    'win10-x64'  = 'win-x64'
    'win10-arm64' = 'win-arm64'
}

$expected = @(
    @{ Path = "runtimes/$RuntimeIdentifier/native/accesskit_ffi.dll"; Description = 'AccessKit bridge' },
    @{ Path = "runtimes/$RuntimeIdentifier/native/kurbo_ffi.dll"; Description = 'Kurbo primitives' },
    @{ Path = "runtimes/$RuntimeIdentifier/native/peniko_ffi.dll"; Description = 'Peniko primitives' },
    @{ Path = "runtimes/$RuntimeIdentifier/native/vello_ffi.dll"; Description = 'Vello GPU runtime' },
    @{ Path = "runtimes/$RuntimeIdentifier/native/vello_sparse_ffi.dll"; Description = 'Vello sparse runtime' },
    @{ Path = "runtimes/$RuntimeIdentifier/native/vello.backends.json"; Description = 'GPU backend manifest'; SkipSignature = $true },
    @{ Path = "runtimes/$RuntimeIdentifier/native/vello_chart_engine.dll"; Description = 'Chart engine' },
    @{ Path = "runtimes/$RuntimeIdentifier/native/vello_composition.dll"; Description = 'Composition helpers' },
    @{ Path = "runtimes/$RuntimeIdentifier/native/vello_editor_core.dll"; Description = 'Editor core' },
    @{ Path = "runtimes/$RuntimeIdentifier/native/vello_gauges_core.dll"; Description = 'Gauges core' },
    @{ Path = "runtimes/$RuntimeIdentifier/native/vello_scada_runtime.dll"; Description = 'SCADA runtime' },
    @{ Path = "runtimes/$RuntimeIdentifier/native/vello_tree_datagrid.dll"; Description = 'TreeDataGrid runtime' },
    @{ Path = "runtimes/$RuntimeIdentifier/native/winit_ffi.dll"; Description = 'Winit interop' }
)

$missing = @()
$invalidSignatures = @()
$fallbackRid = $fallbackMap[$RuntimeIdentifier]

foreach ($entry in $expected) {
    $candidate = Get-AssetPath -RelativePath $entry.Path -FallbackRid $fallbackRid
    if (-not $candidate) {
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

Write-Host "All UWP native assets were found for '$RuntimeIdentifier' and signatures validated." -ForegroundColor Green
