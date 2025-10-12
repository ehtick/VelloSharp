#if !WINDOWS
using System;
using VelloSharp;

namespace VelloSharp.Windows;

/// <summary>
/// Lightweight graphics device used on non-Windows targets to satisfy shared presenter logic.
/// </summary>
public sealed class VelloGraphicsDevice : IDisposable
{
    private Scene _scene = new();
    private uint _width;
    private uint _height;
    private bool _disposed;

    public VelloGraphicsDevice(uint width, uint height, VelloGraphicsDeviceOptions? options = null)
    {
        if (width == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Surface width must be positive.");
        }
        if (height == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Surface height must be positive.");
        }

        _width = width;
        _height = height;
        Options = options ?? VelloGraphicsDeviceOptions.Default;
    }

    public VelloGraphicsDeviceOptions Options { get; }

    public (uint Width, uint Height) SurfaceSize => (_width, _height);

    public VelloGraphicsSession BeginSession(uint width, uint height)
    {
        ThrowIfDisposed();
        if (width == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Surface width must be positive.");
        }
        if (height == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Surface height must be positive.");
        }

        _scene.Dispose();
        _scene = new Scene();
        _width = width;
        _height = height;

        return new VelloGraphicsSession(_scene, width, height);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scene.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VelloGraphicsDevice));
        }
    }
}
#endif
