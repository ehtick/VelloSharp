using System;
using VelloSharp;

namespace VelloSharp.Windows;

internal sealed class WindowsGpuBufferLease : IDisposable
{
    private readonly WindowsGpuResourcePool _pool;
    private readonly WindowsGpuDiagnostics _diagnostics;
    private WgpuBuffer? _buffer;
    private readonly ulong _bucketSize;

    internal WindowsGpuBufferLease(WindowsGpuResourcePool pool, WgpuBuffer buffer, ulong bucketSize, WindowsGpuDiagnostics diagnostics)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _bucketSize = bucketSize;
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public WgpuBuffer Buffer => _buffer ?? throw new ObjectDisposedException(nameof(WindowsGpuBufferLease));

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
