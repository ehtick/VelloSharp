using System;

namespace SkiaSharp;

public sealed class SKRoundRect : IDisposable
{
    private SKRect _rect;
    private readonly SKPoint[] _radii = new SKPoint[4];
    private bool _disposed;

    public SKRoundRect()
    {
        ShimNotImplemented.Throw($"{nameof(SKRoundRect)}.ctor", "rounded rectangle geometry");
    }

    public bool IsEmpty => _rect.IsEmpty;

    public SKRect Rect => _rect;

    public void SetRectRadii(SKRect rect, SKPoint[] radii)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(radii);
        if (radii.Length < 4)
        {
            throw new ArgumentException("Expecting four corner radii.", nameof(radii));
        }

        ShimNotImplemented.Throw($"{nameof(SKRoundRect)}.{nameof(SetRectRadii)}");
        _rect = rect;
        Array.Copy(radii, _radii, 4);
    }

    public void SetEmpty()
    {
        EnsureNotDisposed();
        ShimNotImplemented.Throw($"{nameof(SKRoundRect)}.{nameof(SetEmpty)}");
        _rect = default;
        Array.Clear(_radii, 0, _radii.Length);
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
