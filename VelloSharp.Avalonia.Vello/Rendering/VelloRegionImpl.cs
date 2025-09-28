using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Platform;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class VelloRegionImpl : IPlatformRenderInterfaceRegion
{
    private readonly List<LtrbPixelRect> _rects = new();
    private bool _disposed;
    private LtrbPixelRect _bounds;
    private bool _hasBounds;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _rects.Clear();
        _disposed = true;
        _hasBounds = false;
        _bounds = default;
    }

    public void AddRect(LtrbPixelRect rect)
    {
        EnsureNotDisposed();

        if (rect.Left >= rect.Right || rect.Top >= rect.Bottom)
        {
            return;
        }

        _rects.Add(rect);
        if (!_hasBounds)
        {
            _bounds = rect;
            _hasBounds = true;
        }
        else
        {
            _bounds.Left = Math.Min(_bounds.Left, rect.Left);
            _bounds.Top = Math.Min(_bounds.Top, rect.Top);
            _bounds.Right = Math.Max(_bounds.Right, rect.Right);
            _bounds.Bottom = Math.Max(_bounds.Bottom, rect.Bottom);
        }
    }

    public void Reset()
    {
        EnsureNotDisposed();
        _rects.Clear();
        _bounds = default;
        _hasBounds = false;
    }

    public bool IsEmpty => !_hasBounds;

    public LtrbPixelRect Bounds => _hasBounds ? _bounds : default;

    public IList<LtrbPixelRect> Rects
    {
        get
        {
            EnsureNotDisposed();
            return _rects;
        }
    }

    public bool Intersects(LtrbRect rect)
    {
        EnsureNotDisposed();
        return _rects.Count > 0;
    }

    public bool Contains(Point pt)
    {
        EnsureNotDisposed();
        return _rects.Count > 0;
    }

    private static LtrbPixelRect ToPixelRect(LtrbRect rect)
    {
        var left = (int)Math.Floor(rect.Left);
        var top = (int)Math.Floor(rect.Top);
        var right = (int)Math.Ceiling(rect.Right);
        var bottom = (int)Math.Ceiling(rect.Bottom);

        return new LtrbPixelRect
        {
            Left = left,
            Top = top,
            Right = right,
            Bottom = bottom,
        };
    }

    private static bool RectanglesIntersect(LtrbPixelRect a, LtrbPixelRect b)
    {
        return a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VelloRegionImpl));
        }
    }
}
