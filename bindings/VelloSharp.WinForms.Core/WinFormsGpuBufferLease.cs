using System;
using VelloSharp;

namespace VelloSharp.WinForms;

internal sealed class WinFormsGpuBufferLease : IDisposable
{
    private readonly WinFormsGpuResourcePool _pool;
    private readonly WinFormsGpuDiagnostics _diagnostics;
    private WgpuBuffer? _buffer;
    private readonly ulong _bucketSize;

    internal WinFormsGpuBufferLease(WinFormsGpuResourcePool pool, WgpuBuffer buffer, ulong bucketSize, WinFormsGpuDiagnostics diagnostics)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _bucketSize = bucketSize;
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public WgpuBuffer Buffer => _buffer ?? throw new ObjectDisposedException(nameof(WinFormsGpuBufferLease));

    public ulong BucketSize => _bucketSize;

    public void Dispose()
    {
        if (_buffer is { } buffer)
        {
            _pool.ReturnUploadBuffer(buffer, _bucketSize, _diagnostics);
            _buffer = null;
        }
    }
}
