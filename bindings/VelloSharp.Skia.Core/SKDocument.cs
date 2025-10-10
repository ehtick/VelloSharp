using System;
using System.Collections.Generic;

namespace SkiaSharp;

public sealed class SKDocument : IDisposable
{
    public const float DefaultRasterDpi = 72f;

    private readonly List<SKSurface> _pages = new();
    private bool _disposed;

    private SKDocument()
    {
    }

    public static SKDocument CreatePdf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        ShimNotImplemented.Throw($"{nameof(SKDocument)}.{nameof(CreatePdf)}", "PDF export");
        return new SKDocument();
    }

    public static SKDocument CreatePdf(System.IO.Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ShimNotImplemented.Throw($"{nameof(SKDocument)}.{nameof(CreatePdf)}", "PDF export");
        return new SKDocument();
    }

    public SKCanvas BeginPage(float width, float height)
    {
        EnsureNotDisposed();
        var w = Math.Max(1, (int)MathF.Ceiling(width));
        var h = Math.Max(1, (int)MathF.Ceiling(height));
        var surface = SKSurface.Create(new SKImageInfo(w, h));
        _pages.Add(surface);
        ShimNotImplemented.Throw($"{nameof(SKDocument)}.{nameof(BeginPage)}", "document paging");
        return surface.Canvas;
    }

    public SKCanvas BeginPage(float width, float height, SKRect content)
    {
        _ = content;
        return BeginPage(width, height);
    }

    public void EndPage()
    {
        EnsureNotDisposed();
        ShimNotImplemented.Throw($"{nameof(SKDocument)}.{nameof(EndPage)}");
    }

    public void Close()
    {
        Dispose();
    }

    public void Abort()
    {
        EnsureNotDisposed();
        ShimNotImplemented.Throw($"{nameof(SKDocument)}.{nameof(Abort)}");
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var surface in _pages)
        {
            surface.Dispose();
        }

        _pages.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKDocument));
        }
    }
}
