using System;
using System.Collections.Generic;

namespace SkiaSharp;

public sealed class SKRegion : IDisposable
{
    private readonly List<SKRectI> _rects = new();
    private bool _disposed;

    public SKRegion()
    {
        ShimNotImplemented.Throw($"{nameof(SKRegion)}.ctor", "region boolean operations");
    }

    public bool IsEmpty
    {
        get
        {
            EnsureNotDisposed();
            return _rects.Count == 0;
        }
    }

    public SKRectI Bounds
    {
        get
        {
            EnsureNotDisposed();
            if (_rects.Count == 0)
            {
                return new SKRectI(0, 0, 0, 0);
            }

            var minLeft = int.MaxValue;
            var minTop = int.MaxValue;
            var maxRight = int.MinValue;
            var maxBottom = int.MinValue;

            foreach (var rect in _rects)
            {
                minLeft = Math.Min(minLeft, rect.Left);
                minTop = Math.Min(minTop, rect.Top);
                maxRight = Math.Max(maxRight, rect.Right);
                maxBottom = Math.Max(maxBottom, rect.Bottom);
            }

            return new SKRectI(minLeft, minTop, maxRight, maxBottom);
        }
    }

    public void SetEmpty()
    {
        EnsureNotDisposed();
        ShimNotImplemented.Throw($"{nameof(SKRegion)}.{nameof(SetEmpty)}");
        _rects.Clear();
    }

    public bool Contains(int x, int y)
    {
        EnsureNotDisposed();
        ShimNotImplemented.Throw($"{nameof(SKRegion)}.{nameof(Contains)}");
        foreach (var rect in _rects)
        {
            if (x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom)
            {
                return true;
            }
        }

        return false;
    }

    public bool Intersects(SKRectI rect)
    {
        EnsureNotDisposed();
        ShimNotImplemented.Throw($"{nameof(SKRegion)}.{nameof(Intersects)}");
        foreach (var existing in _rects)
        {
            if (existing.Left < rect.Right &&
                existing.Right > rect.Left &&
                existing.Top < rect.Bottom &&
                existing.Bottom > rect.Top)
            {
                return true;
            }
        }

        return false;
    }

    public void Op(int left, int top, int right, int bottom, SKRegionOperation operation)
    {
        EnsureNotDisposed();
        var rect = new SKRectI(left, top, right, bottom);
        Op(rect, operation);
    }

    public void Op(SKRectI rect, SKRegionOperation operation)
    {
        EnsureNotDisposed();
        ShimNotImplemented.Throw($"{nameof(SKRegion)}.{nameof(Op)}", operation.ToString());
        if (operation != SKRegionOperation.Union)
        {
            return;
        }

        _rects.Add(rect);
    }

    public SKRegionRectIterator CreateRectIterator()
    {
        EnsureNotDisposed();
        ShimNotImplemented.Throw($"{nameof(SKRegion)}.{nameof(CreateRectIterator)}");
        return new SKRegionRectIterator(_rects);
    }

    public void Dispose()
    {
        _rects.Clear();
        _disposed = true;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKRegion));
        }
    }
}

public sealed class SKRegionRectIterator : IDisposable
{
    private readonly IEnumerator<SKRectI> _enumerator;

    internal SKRegionRectIterator(IEnumerable<SKRectI> source)
    {
        _enumerator = source.GetEnumerator();
    }

    public bool Next(out SKRectI rect)
    {
        if (_enumerator.MoveNext())
        {
            rect = _enumerator.Current;
            return true;
        }

        rect = default;
        return false;
    }

    public void Dispose()
    {
        _enumerator.Dispose();
    }
}
