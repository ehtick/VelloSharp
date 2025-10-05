using System;
using VelloSharp;

namespace VelloSharp.WinForms;

public sealed class VelloGraphicsSession : IDisposable
{
    private readonly VelloGraphicsDevice _device;
    private readonly Scene _scene;
    private readonly uint _width;
    private readonly uint _height;
    private bool _submitted;
    private bool _disposed;

    internal VelloGraphicsSession(VelloGraphicsDevice device, Scene scene, uint width, uint height)
    {
        _device = device;
        _scene = scene;
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
    {
        ThrowIfDisposed();
        if (_submitted)
        {
            throw new InvalidOperationException("Frame already submitted.");
        }

        _device.Render(_scene, targetBuffer, strideBytes, _width, _height);
        _submitted = true;
    }

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
        if (_disposed)
        {
            return;
        }

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
