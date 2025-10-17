using System;
using VelloSharp;

namespace SkiaSharp;

public sealed class SKSurface : IDisposable
{
    private readonly Scene _scene;
    private readonly SKCanvas _canvas;
    private readonly GRContext? _context;
    private bool _disposed;

    private SKSurface(SKImageInfo info, GRContext? context = null)
    {
        Info = info;
        _context = context;
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

    public static SKSurface Create(SKImageInfo info, SKSurfaceProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ShimNotImplemented.Throw($"{nameof(SKSurface)}.{nameof(Create)}", "surface properties");
        return Create(info);
    }

    public static SKSurface Create(GRContext context, bool budgeted, SKImageInfo info)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = budgeted;
        return new SKSurface(info, context);
    }

    public static SKSurface Create(GRContext context, bool budgeted, SKImageInfo info, SKSurfaceProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return Create(context, budgeted, info);
    }

    public static SKSurface Create(GRContext context, GRBackendRenderTarget renderTarget, GRSurfaceOrigin origin, SKColorType colorType)
        => Create(context, renderTarget, origin, colorType, default(SKSurfaceProperties?));

    public static SKSurface Create(GRContext context, GRBackendRenderTarget renderTarget, GRSurfaceOrigin origin, SKColorType colorType, SKSurfaceProperties? props)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(renderTarget);
        _ = origin;
        var info = new SKImageInfo(renderTarget.Width, renderTarget.Height, colorType, SKAlphaType.Premul);
        return new SKSurface(info, context);
    }

    public static SKSurface Create(GRContext context, GRBackendRenderTarget renderTarget, GRSurfaceOrigin origin, SKColorType colorType, SKColorSpace? colorspace)
        => Create(context, renderTarget, origin, colorType, colorspace, default(SKSurfaceProperties?));

    public static SKSurface Create(GRContext context, GRBackendRenderTarget renderTarget, GRSurfaceOrigin origin, SKColorType colorType, SKColorSpace? colorspace, SKSurfaceProperties? props)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(renderTarget);
        _ = origin;
        _ = props;
        _ = colorspace;
        var info = new SKImageInfo(renderTarget.Width, renderTarget.Height, colorType, SKAlphaType.Premul);
        return new SKSurface(info, context);
    }

    public static SKSurface Create(GRContext context, GRBackendTexture texture, GRSurfaceOrigin origin, SKColorType colorType)
        => Create(context, texture, origin, colorType, default(SKSurfaceProperties?));

    public static SKSurface Create(GRContext context, GRBackendTexture texture, GRSurfaceOrigin origin, SKColorType colorType, SKSurfaceProperties? props)
        => Create(context, texture, origin, 0, colorType, default(SKColorSpace?), props);

    public static SKSurface Create(GRContext context, GRBackendTexture texture, GRSurfaceOrigin origin, int sampleCount, SKColorType colorType)
        => Create(context, texture, origin, sampleCount, colorType, default(SKColorSpace?), default(SKSurfaceProperties?));

    public static SKSurface Create(GRContext context, GRBackendTexture texture, GRSurfaceOrigin origin, int sampleCount, SKColorType colorType, SKColorSpace? colorspace)
        => Create(context, texture, origin, sampleCount, colorType, colorspace, default(SKSurfaceProperties?));

    public static SKSurface Create(GRContext context, GRBackendTexture texture, GRSurfaceOrigin origin, int sampleCount, SKColorType colorType, SKColorSpace? colorspace, SKSurfaceProperties? props)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(texture);
        _ = origin;
        _ = sampleCount;
        _ = props;
        _ = colorspace;
        var info = new SKImageInfo(texture.Width, texture.Height, colorType, SKAlphaType.Premul);
        return new SKSurface(info, context);
    }

    public static SKSurface Create(SKImageInfo info, IntPtr pixels, int rowBytes, SKSurfaceProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        _ = pixels;
        _ = rowBytes;
        ShimNotImplemented.Throw($"{nameof(SKSurface)}.{nameof(Create)}", "CPU surface from pixel buffer");
        return Create(info);
    }

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

    public void Flush() => Flush(true);

    public void Flush(bool submit, bool synchronous = false)
    {
        ThrowIfDisposed();
        _canvas.Flush();
        _context?.Flush(submit, synchronous);
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
