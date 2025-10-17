using System;

namespace SkiaSharp;

public sealed class SKRoundRect : IDisposable
{
    private readonly SKPoint[] _radii = new SKPoint[4];
    private SKRect _rect;
    private bool _disposed;

    public SKRoundRect()
    {
    }

    public SKRoundRect(SKRect rect)
    {
        _rect = rect;
    }

    public bool IsEmpty
    {
        get
        {
            EnsureNotDisposed();
            return _rect.IsEmpty;
        }
    }

    public SKRect Rect
    {
        get
        {
            EnsureNotDisposed();
            return _rect;
        }
    }

    public SKPoint[] Radii
    {
        get
        {
            EnsureNotDisposed();
            var copy = new SKPoint[4];
            Array.Copy(_radii, copy, 4);
            return copy;
        }
    }

    public void SetRect(SKRect rect)
    {
        EnsureNotDisposed();
        _rect = rect;
    }

    public void SetRectRadii(SKRect rect, SKPoint[] radii)
    {
        ArgumentNullException.ThrowIfNull(radii);
        SetRectRadii(rect, radii.AsSpan());
    }

    public void SetRectRadii(SKRect rect, ReadOnlySpan<SKPoint> radii)
    {
        EnsureNotDisposed();
        if (radii.Length < 4)
        {
            throw new ArgumentException("Expecting four corner radii.", nameof(radii));
        }

        _rect = rect;
        for (var i = 0; i < 4; i++)
        {
            _radii[i] = radii[i];
        }
    }

    public void SetEmpty()
    {
        EnsureNotDisposed();
        _rect = default;
        Array.Clear(_radii, 0, _radii.Length);
    }

    public void Offset(float dx, float dy)
    {
        EnsureNotDisposed();
        _rect.Offset(dx, dy);
    }

    public void Inflate(float dx, float dy)
    {
        EnsureNotDisposed();
        _rect.Inflate(dx, dy);
    }

    public void Deflate(float dx, float dy)
    {
        EnsureNotDisposed();
        _rect.Deflate(dx, dy);
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKRoundRect));
        }
    }
}
