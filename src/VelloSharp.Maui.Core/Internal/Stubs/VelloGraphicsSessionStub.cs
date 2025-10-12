#if !WINDOWS
using System;
using VelloSharp;

namespace VelloSharp.Windows;

/// <summary>
/// Lightweight graphics session used by MAUI presenters on non-Windows platforms.
/// </summary>
public sealed class VelloGraphicsSession : IDisposable
{
    private readonly Scene _scene;
    private readonly uint _width;
    private readonly uint _height;
    private bool _submitted;
    private bool _disposed;

    internal VelloGraphicsSession(Scene scene, uint width, uint height)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _width = width;
        _height = height;
    }

    public Scene Scene
    {
        get
        {
            ThrowIfDisposed();
            return _scene;
        }
    }

    public uint Width => _width;

    public uint Height => _height;

    public bool IsSubmitted => _submitted;

    public void Submit(Span<byte> targetBuffer, int strideBytes)
        => throw new NotSupportedException("GPU frame readback is not supported on this platform.");

    public void Complete()
    {
        ThrowIfDisposed();
        if (_submitted)
        {
            throw new InvalidOperationException("Frame already submitted.");
        }

        _submitted = true;
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VelloGraphicsSession));
        }
    }
}
#endif
