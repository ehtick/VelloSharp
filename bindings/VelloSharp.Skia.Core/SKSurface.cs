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

    public SKImage Snapshot()
    {
        ThrowIfDisposed();
        var width = Info.Width;
        var height = Info.Height;
        if (width == 0 || height == 0)
        {
            throw new InvalidOperationException("Cannot snapshot an empty surface.");
        }

        using var renderer = new Renderer((uint)width, (uint)height);
        var stride = Info.RowBytes;
        if (stride <= 0)
        {
            stride = width * 4;
        }

        var buffer = new byte[stride * height];
        var renderParams = new RenderParams(
            (uint)width,
            (uint)height,
            RgbaColor.FromBytes(0, 0, 0, 0),
            AntialiasingMode.Area,
            RenderFormat.Bgra8);
        renderer.Render(_scene, renderParams, buffer, stride);

        var imageInfo = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul);
        return SKImage.FromPixels(imageInfo, buffer, stride);
    }

    public void Draw(SKCanvas canvas, float x, float y, SKPaint? paint)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(canvas);
        using var image = Snapshot();
        var dest = SKRect.Create(x, y, image.Width, image.Height);
        canvas.DrawImage(image, dest);
    }

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
