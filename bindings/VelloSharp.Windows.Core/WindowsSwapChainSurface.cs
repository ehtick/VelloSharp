using System;
using VelloSharp;

namespace VelloSharp.Windows;

public sealed class WindowsSwapChainSurface : IDisposable
{
    private readonly WindowsGpuContext _context;
    private readonly WgpuSurface _surface;
    private readonly SurfaceHandle _surfaceHandle;
    private readonly WgpuTextureFormat _format;
    private readonly PresentMode _presentMode;
    private uint _width;
    private uint _height;
    private bool _disposed;

    internal WindowsSwapChainSurface(WindowsGpuContext context, WgpuSurface surface, SurfaceHandle surfaceHandle, WgpuTextureFormat format, PresentMode presentMode, uint width, uint height)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        _surfaceHandle = surfaceHandle;
        _format = format;
        _presentMode = presentMode;
        Configure(width, height);
    }

    public WgpuTextureFormat Format => _format;

    public PresentMode PresentMode => _presentMode;

    public uint Width => _width;

    public uint Height => _height;

    public SurfaceHandle SurfaceHandle => _surfaceHandle;

    public void Configure(uint width, uint height)
    {
        EnsureNotDisposed();
        var calibratedWidth = Math.Max(width, 1);
        var calibratedHeight = Math.Max(height, 1);
        _context.ConfigureSurface(_surface, calibratedWidth, calibratedHeight, _presentMode, _format);
        _width = calibratedWidth;
        _height = calibratedHeight;
    }

    public WgpuSurfaceTexture AcquireNextTexture()
    {
        EnsureNotDisposed();
        return _surface.AcquireNextTexture();
    }

    internal WgpuSurface Surface
    {
        get
        {
            EnsureNotDisposed();
            return _surface;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _surface.Dispose();
        _disposed = true;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WindowsSwapChainSurface));
        }
    }
}
