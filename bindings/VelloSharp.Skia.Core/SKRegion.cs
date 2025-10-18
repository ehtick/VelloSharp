using System;
using System.Collections.Generic;

namespace SkiaSharp;

public sealed class SKRegion : IDisposable
{
    private readonly List<SKRectI> _rects = new();
    private bool _disposed;

    public SKRegion()
    {
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
        _rects.Clear();
    }

    public bool Contains(int x, int y)
    {
        EnsureNotDisposed();
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
        rect = NormalizeRect(rect);
        foreach (var existing in _rects)
        {
            if (TryIntersect(existing, rect, out _))
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
        rect = NormalizeRect(rect);

        if (IsRectDegenerate(rect))
        {
            if (operation == SKRegionOperation.Replace)
            {
                _rects.Clear();
            }
            return;
        }

        switch (operation)
        {
            case SKRegionOperation.Union:
                AddRect(rect);
                break;
            case SKRegionOperation.Replace:
                _rects.Clear();
                AddRect(rect);
                break;
            case SKRegionOperation.Intersect:
                ApplyIntersect(rect);
                break;
            case SKRegionOperation.Difference:
                ApplyDifference(rect);
                break;
            case SKRegionOperation.ReverseDifference:
                ApplyReverseDifference(rect);
                break;
            case SKRegionOperation.Xor:
                ApplyXor(rect);
                break;
        }
    }

    public SKRegionRectIterator CreateRectIterator()
    {
        EnsureNotDisposed();
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

    private void AddRect(SKRectI rect)
    {
        if (IsRectDegenerate(rect))
        {
            return;
        }

        if (!_rects.Contains(rect))
        {
            _rects.Add(rect);
        }
    }

    private void ApplyIntersect(SKRectI rect)
    {
        var intersections = new List<SKRectI>();
        foreach (var existing in _rects)
        {
            if (TryIntersect(existing, rect, out var intersection))
            {
                intersections.Add(intersection);
            }
        }

        _rects.Clear();
        foreach (var candidate in intersections)
        {
            if (!IsRectDegenerate(candidate))
            {
                _rects.Add(candidate);
            }
        }
    }

    private void ApplyDifference(SKRectI rect)
    {
        var result = new List<SKRectI>();
        foreach (var existing in _rects)
        {
            result.AddRange(SubtractRect(existing, rect));
        }

        _rects.Clear();
        foreach (var candidate in result)
        {
            if (!IsRectDegenerate(candidate))
            {
                _rects.Add(candidate);
            }
        }
    }

    private void ApplyReverseDifference(SKRectI rect)
    {
        var seeds = new List<SKRectI> { rect };
        foreach (var existing in _rects)
        {
            var next = new List<SKRectI>();
            foreach (var candidate in seeds)
            {
                next.AddRange(SubtractRect(candidate, existing));
            }

            seeds = next;
            if (seeds.Count == 0)
            {
                break;
            }
        }

        _rects.Clear();
        foreach (var candidate in seeds)
        {
            if (!IsRectDegenerate(candidate))
            {
                _rects.Add(candidate);
            }
        }
    }

    private void ApplyXor(SKRectI rect)
    {
        var existingMinusRect = new List<SKRectI>();
        foreach (var existing in _rects)
        {
            existingMinusRect.AddRange(SubtractRect(existing, rect));
        }

        var rectMinusExisting = new List<SKRectI> { rect };
        foreach (var existing in _rects)
        {
            var next = new List<SKRectI>();
            foreach (var candidate in rectMinusExisting)
            {
                next.AddRange(SubtractRect(candidate, existing));
            }

            rectMinusExisting = next;
            if (rectMinusExisting.Count == 0)
            {
                break;
            }
        }

        _rects.Clear();
        foreach (var candidate in existingMinusRect)
        {
            if (!IsRectDegenerate(candidate))
            {
                _rects.Add(candidate);
            }
        }

        foreach (var candidate in rectMinusExisting)
        {
            if (!IsRectDegenerate(candidate))
            {
                AddRect(candidate);
            }
        }
    }

    private static IEnumerable<SKRectI> SubtractRect(SKRectI source, SKRectI subtractor)
    {
        source = NormalizeRect(source);
        subtractor = NormalizeRect(subtractor);

        if (!TryIntersect(source, subtractor, out var intersection))
        {
            yield return source;
            yield break;
        }

        if (RectEquals(intersection, source))
        {
            yield break;
        }

        if (source.Top < intersection.Top)
        {
            yield return new SKRectI(source.Left, source.Top, source.Right, intersection.Top);
        }

        if (intersection.Bottom < source.Bottom)
        {
            yield return new SKRectI(source.Left, intersection.Bottom, source.Right, source.Bottom);
        }

        if (source.Left < intersection.Left)
        {
            yield return new SKRectI(source.Left, intersection.Top, intersection.Left, intersection.Bottom);
        }

        if (intersection.Right < source.Right)
        {
            yield return new SKRectI(intersection.Right, intersection.Top, source.Right, intersection.Bottom);
        }
    }

    private static bool TryIntersect(SKRectI a, SKRectI b, out SKRectI intersection)
    {
        var left = Math.Max(a.Left, b.Left);
        var top = Math.Max(a.Top, b.Top);
        var right = Math.Min(a.Right, b.Right);
        var bottom = Math.Min(a.Bottom, b.Bottom);

        if (left < right && top < bottom)
        {
            intersection = new SKRectI(left, top, right, bottom);
            return true;
        }

        intersection = default;
        return false;
    }

    private static SKRectI NormalizeRect(SKRectI rect)
    {
        var left = Math.Min(rect.Left, rect.Right);
        var right = Math.Max(rect.Left, rect.Right);
        var top = Math.Min(rect.Top, rect.Bottom);
        var bottom = Math.Max(rect.Top, rect.Bottom);
        return new SKRectI(left, top, right, bottom);
    }

    private static bool RectEquals(SKRectI a, SKRectI b) =>
        a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;

    private static bool IsRectDegenerate(SKRectI rect)
    {
        rect = NormalizeRect(rect);
        return rect.Right <= rect.Left || rect.Bottom <= rect.Top;
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
