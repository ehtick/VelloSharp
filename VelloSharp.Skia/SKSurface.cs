using System;
using VelloSharp;

namespace SkiaSharp;

public sealed class SKSurface : IDisposable
{
    private readonly Scene _scene;
    private readonly SKCanvas _canvas;
    private bool _disposed;

    private SKSurface(SKImageInfo info)
    {
        Info = info;
        _scene = new Scene();
        _canvas = new SKCanvas(_scene, info.Width, info.Height);
    }

    public SKImageInfo Info { get; }

    public SKCanvas Canvas
    {
        get
        {
            ThrowIfDisposed();
            return _canvas;
        }
    }

    public Scene Scene
    {
        get
        {
            ThrowIfDisposed();
            return _scene;
        }
    }

    public static SKSurface Create(SKImageInfo info) => new(info);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _scene.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKSurface));
        }
    }
}
