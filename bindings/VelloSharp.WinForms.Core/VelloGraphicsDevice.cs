using System;
using VelloSharp;

namespace VelloSharp.WinForms;

public sealed class VelloGraphicsDevice : IDisposable
{
    private readonly VelloGraphicsDeviceOptions _options;
    private Renderer? _renderer;
    private Scene? _scene;
    private uint _width;
    private uint _height;
    private bool _disposed;

    public VelloGraphicsDevice(uint width, uint height, VelloGraphicsDeviceOptions? options = null)
    {
        if (width == 0 || height == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Surface dimensions must be greater than zero.");
        }

        _options = options ?? VelloGraphicsDeviceOptions.Default;
        _renderer = CreateRenderer(width, height, _options);
        _scene = new Scene();
        _width = width;
        _height = height;
    }

    public VelloGraphicsDeviceOptions Options => _options;

    public (uint Width, uint Height) SurfaceSize => (_width, _height);

    public VelloGraphicsSession BeginSession(uint width, uint height)
    {
        EnsureNotDisposed();

        if (width == 0 || height == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Surface dimensions must be greater than zero.");
        }

        if (_renderer == null || _scene == null)
        {
            throw new ObjectDisposedException(nameof(VelloGraphicsDevice));
        }

        if (_width != width || _height != height)
        {
            _renderer.Resize(width, height);
            _width = width;
            _height = height;
        }

        _scene.Reset();
        return new VelloGraphicsSession(this, _scene, width, height);
    }

    internal void Render(Scene scene, Span<byte> target, int strideBytes, uint width, uint height)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (_renderer == null)
        {
            throw new ObjectDisposedException(nameof(VelloGraphicsDevice));
        }

        if (strideBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(strideBytes), "Stride must be positive.");
        }

        if (target.IsEmpty)
        {
            throw new ArgumentException("Target buffer must not be empty.", nameof(target));
        }

        var requiredSize = checked((long)strideBytes * height);
        if (target.Length < requiredSize)
        {
            throw new ArgumentException($"Target buffer must contain at least {requiredSize} bytes.", nameof(target));
        }

        var antialiasing = _options.GetAntialiasingMode();
        var parameters = new RenderParams(width, height, _options.BackgroundColor, antialiasing, _options.Format);
        _renderer.Render(scene, parameters, target, strideBytes);
    }

    private static Renderer CreateRenderer(uint width, uint height, VelloGraphicsDeviceOptions options)
    {
        return options.RendererOptions is { } rendererOptions
            ? new Renderer(width, height, rendererOptions)
            : new Renderer(width, height);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VelloGraphicsDevice));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scene?.Dispose();
        _scene = null;
        _renderer?.Dispose();
        _renderer = null;
    }
}
