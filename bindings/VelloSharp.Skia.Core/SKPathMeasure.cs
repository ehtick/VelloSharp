using System;
using System.Collections.Generic;
using System.Numerics;

namespace SkiaSharp;

public sealed class SKPathMeasure : IDisposable
{
    private const float SegmentEpsilon = 1e-4f;
    private const int BaseQuadraticSteps = 8;
    private const int BaseCubicSteps = 16;

    private readonly bool _forceClosed;
    private readonly float _resScale;
    private readonly List<Segment> _segments = new();
    private readonly List<ContourInfo> _contours = new();
    private readonly float _length;
    private bool _disposed;

    public SKPathMeasure(SKPath path, bool forceClosed = false, float resScale = 1f)
    {
        ArgumentNullException.ThrowIfNull(path);

        _forceClosed = forceClosed;
        _resScale = resScale <= 0f ? 1f : resScale;
        _length = BuildSegments(path);
    }

    public SKPathMeasure(SKPath path)
        : this(path, false, 1f)
    {
    }

    public bool ForceClosed => _forceClosed;

    public float ResScale => _resScale;

    public float Length
    {
        get
        {
            EnsureNotDisposed();
            return _length;
        }
    }

    public bool GetPosition(float distance, out SKPoint position)
    {
        EnsureNotDisposed();

        position = default;
        if (_segments.Count == 0)
        {
            return false;
        }

        if (distance < 0f || distance > _length)
        {
            if (MathF.Abs(distance - _length) > SegmentEpsilon)
            {
                return false;
            }

            distance = _length;
        }

        if (MathF.Abs(distance - _length) < SegmentEpsilon && _segments.Count > 0)
        {
            distance = MathF.Max(_length - SegmentEpsilon, 0f);
        }

        if (!TryFindSegment(distance, out var index, out var localT))
        {
            return false;
        }

        var segment = _segments[index];
        var point = segment.Lerp(localT);
        position = ToSKPoint(point);
        return true;
    }

    public bool GetPositionAndTangent(float distance, out SKPoint position, out SKPoint tangent)
    {
        EnsureNotDisposed();

        position = default;
        tangent = default;

        if (_segments.Count == 0)
        {
            return false;
        }

        if (distance < 0f || distance > _length)
        {
            if (MathF.Abs(distance - _length) > SegmentEpsilon)
            {
                return false;
            }

            distance = _length;
        }

        if (MathF.Abs(distance - _length) < SegmentEpsilon && _segments.Count > 0)
        {
            distance = MathF.Max(_length - SegmentEpsilon, 0f);
        }

        if (!TryFindSegment(distance, out var index, out var localT))
        {
            return false;
        }

        var segment = _segments[index];
        var point = segment.Lerp(localT);
        position = ToSKPoint(point);

        var direction = segment.Direction;
        if (direction == Vector2.Zero)
        {
            direction = FindDirection(index);
        }

        tangent = direction == Vector2.Zero ? default : new SKPoint(direction.X, direction.Y);
        return true;
    }

    public bool GetSegment(float startD, float stopD, SKPath destination, bool startWithMoveTo)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(destination);

        destination.Reset();

        if (_segments.Count == 0)
        {
            return false;
        }

        var start = MathF.Max(0f, startD);
        var stop = MathF.Max(0f, stopD);

        if (start >= stop || start >= _length)
        {
            return false;
        }

        start = MathF.Min(start, _length);
        stop = MathF.Min(stop, _length);

        if (!TryFindContour(start, out var contour))
        {
            return false;
        }

        if (start >= contour.EndDistance)
        {
            return false;
        }

        stop = MathF.Min(stop, contour.EndDistance);
        if (stop - start <= SegmentEpsilon)
        {
            return false;
        }

        if (!TryFindSegment(start, out var index, out var localT))
        {
            return false;
        }

        var segment = _segments[index];
        var currentPoint = segment.Lerp(localT);
        if (startWithMoveTo || destination.IsEmpty)
        {
            destination.MoveTo(ToSKPoint(currentPoint));
        }
        else
        {
            destination.LineTo(ToSKPoint(currentPoint));
        }

        var contourEndIndex = contour.StartSegment + contour.SegmentCount;
        while (index < contourEndIndex)
        {
            segment = _segments[index];

            if (segment.StartDistance >= stop)
            {
                break;
            }

            var segmentStart = MathF.Max(segment.StartDistance, start);
            var segmentEnd = MathF.Min(segment.EndDistance, stop);

            var startT = segment.Length <= SegmentEpsilon ? 0f : (segmentStart - segment.StartDistance) / segment.Length;
            var endT = segment.Length <= SegmentEpsilon ? 1f : (segmentEnd - segment.StartDistance) / segment.Length;

            startT = Math.Clamp(startT, 0f, 1f);
            endT = Math.Clamp(endT, 0f, 1f);

            var endPoint = segment.Lerp(endT);
            destination.LineTo(ToSKPoint(endPoint));

            if (segment.EndDistance >= stop - SegmentEpsilon)
            {
                break;
            }

            index++;
        }

        return true;
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private float BuildSegments(SKPath source)
    {
        var builder = source.ToPathBuilder();
        var elements = builder.AsSpan();

        if (elements.IsEmpty)
        {
            return 0f;
        }

        var totalLength = 0f;
        var contourStartDistance = 0f;
        var contourStartIndex = 0;
        var contourClosed = false;

        Vector2 current = default;
        Vector2 contourStartPoint = default;
        bool haveCurrent = false;
        bool haveContourStart = false;
        bool contourHasSegments = false;

        void FinalizeContour(bool closeRemaining)
        {
            if (closeRemaining && haveCurrent && haveContourStart && !contourClosed)
            {
                TryEmitSegment(current, contourStartPoint);
            }

            if (contourHasSegments)
            {
                _contours.Add(new ContourInfo(
                    contourStartDistance,
                    totalLength,
                    contourStartIndex,
                    _segments.Count - contourStartIndex));
            }

            contourStartDistance = totalLength;
            contourStartIndex = _segments.Count;
            contourHasSegments = false;
            contourClosed = false;
            haveCurrent = false;
            haveContourStart = false;
        }

        void TryEmitSegment(Vector2 startPoint, Vector2 endPoint)
        {
            current = endPoint;
            var length = Vector2.Distance(startPoint, endPoint);
            if (length <= SegmentEpsilon)
            {
                return;
            }

            _segments.Add(new Segment(startPoint, endPoint, totalLength, length));
            totalLength += length;
            contourHasSegments = true;
        }

        foreach (var element in elements)
        {
            switch (element.Verb)
            {
                case VelloSharp.PathVerb.MoveTo:
                    if (haveContourStart)
                    {
                        FinalizeContour(_forceClosed && !contourClosed);
                    }

                    current = new Vector2((float)element.X0, (float)element.Y0);
                    contourStartPoint = current;
                    haveCurrent = true;
                    haveContourStart = true;
                    contourClosed = false;
                    break;

                case VelloSharp.PathVerb.LineTo:
                    if (haveCurrent)
                    {
                        var target = new Vector2((float)element.X0, (float)element.Y0);
                        TryEmitSegment(current, target);
                    }
                    break;

                case VelloSharp.PathVerb.QuadTo:
                    if (haveCurrent)
                    {
                        var control = new Vector2((float)element.X0, (float)element.Y0);
                        var endPoint = new Vector2((float)element.X1, (float)element.Y1);
                        FlattenQuadratic(current, control, endPoint, GetQuadraticSteps(), TryEmitSegment);
                        current = endPoint;
                    }
                    break;

                case VelloSharp.PathVerb.CubicTo:
                    if (haveCurrent)
                    {
                        var control1 = new Vector2((float)element.X0, (float)element.Y0);
                        var control2 = new Vector2((float)element.X1, (float)element.Y1);
                        var endPoint = new Vector2((float)element.X2, (float)element.Y2);
                        FlattenCubic(current, control1, control2, endPoint, GetCubicSteps(), TryEmitSegment);
                        current = endPoint;
                    }
                    break;

                case VelloSharp.PathVerb.Close:
                    if (haveCurrent && haveContourStart)
                    {
                        TryEmitSegment(current, contourStartPoint);
                        contourClosed = true;
                    }
                    FinalizeContour(closeRemaining: false);
                    break;
            }
        }

        FinalizeContour(_forceClosed && !contourClosed);
        return totalLength;

        int GetQuadraticSteps()
        {
            var scaled = (int)MathF.Ceiling(BaseQuadraticSteps * _resScale);
            return Math.Max(1, scaled);
        }

        int GetCubicSteps()
        {
            var scaled = (int)MathF.Ceiling(BaseCubicSteps * _resScale);
            return Math.Max(1, scaled);
        }
    }

    private bool TryFindSegment(float distance, out int index, out float localT)
    {
        index = -1;
        localT = 0f;

        if (_segments.Count == 0)
        {
            return false;
        }

        if (distance <= 0f)
        {
            index = 0;
            localT = 0f;
            return true;
        }

        if (distance >= _length)
        {
            index = _segments.Count - 1;
            localT = 1f;
            return true;
        }

        var low = 0;
        var high = _segments.Count - 1;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            var segment = _segments[mid];
            if (distance < segment.StartDistance)
            {
                high = mid - 1;
            }
            else if (distance > segment.EndDistance)
            {
                low = mid + 1;
            }
            else
            {
                index = mid;
                localT = segment.Length <= SegmentEpsilon
                    ? 0f
                    : (distance - segment.StartDistance) / segment.Length;
                localT = Math.Clamp(localT, 0f, 1f);
                return true;
            }
        }

        index = Math.Clamp(low, 0, _segments.Count - 1);
        var fallback = _segments[index];
        localT = fallback.Length <= SegmentEpsilon
            ? 0f
            : (distance - fallback.StartDistance) / fallback.Length;
        localT = Math.Clamp(localT, 0f, 1f);
        return true;
    }

    private bool TryFindContour(float distance, out ContourInfo contour)
    {
        contour = default;
        if (_contours.Count == 0)
        {
            return false;
        }

        var low = 0;
        var high = _contours.Count - 1;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            var candidate = _contours[mid];
            if (distance < candidate.StartDistance)
            {
                high = mid - 1;
            }
            else if (distance > candidate.EndDistance)
            {
                low = mid + 1;
            }
            else
            {
                contour = candidate;
                return true;
            }
        }

        return false;
    }

    private Vector2 FindDirection(int index)
    {
        for (var i = index; i < _segments.Count; i++)
        {
            var dir = _segments[i].Direction;
            if (dir != Vector2.Zero)
            {
                return dir;
            }
        }

        for (var i = index - 1; i >= 0; i--)
        {
            var dir = _segments[i].Direction;
            if (dir != Vector2.Zero)
            {
                return dir;
            }
        }

        return Vector2.Zero;
    }

    private static void FlattenQuadratic(
        Vector2 start,
        Vector2 control,
        Vector2 end,
        int steps,
        Action<Vector2, Vector2> emit)
    {
        var previous = start;
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            var point = EvaluateQuadratic(start, control, end, t);
            emit(previous, point);
            previous = point;
        }
    }

    private static void FlattenCubic(
        Vector2 start,
        Vector2 control1,
        Vector2 control2,
        Vector2 end,
        int steps,
        Action<Vector2, Vector2> emit)
    {
        var previous = start;
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            var point = EvaluateCubic(start, control1, control2, end, t);
            emit(previous, point);
            previous = point;
        }
    }

    private static Vector2 EvaluateQuadratic(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        var mt = 1f - t;
        var mt2 = mt * mt;
        var t2 = t * t;
        return new Vector2(
            mt2 * p0.X + 2f * mt * t * p1.X + t2 * p2.X,
            mt2 * p0.Y + 2f * mt * t * p1.Y + t2 * p2.Y);
    }

    private static Vector2 EvaluateCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        var mt = 1f - t;
        var mt2 = mt * mt;
        var mt3 = mt2 * mt;
        var t2 = t * t;
        var t3 = t2 * t;

        return new Vector2(
            mt3 * p0.X + 3f * mt2 * t * p1.X + 3f * mt * t2 * p2.X + t3 * p3.X,
            mt3 * p0.Y + 3f * mt2 * t * p1.Y + 3f * mt * t2 * p2.Y + t3 * p3.Y);
    }

    private static SKPoint ToSKPoint(Vector2 point) => new(point.X, point.Y);

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKPathMeasure));
        }
    }

    private readonly struct Segment
    {
        public Segment(Vector2 start, Vector2 end, float startDistance, float length)
        {
            StartPoint = start;
            EndPoint = end;
            StartDistance = startDistance;
            Length = length;
        }

        public Vector2 StartPoint { get; }
        public Vector2 EndPoint { get; }
        public float StartDistance { get; }
        public float Length { get; }
        public float EndDistance => StartDistance + Length;
        public Vector2 Direction => Length <= SegmentEpsilon ? Vector2.Zero : Vector2.Normalize(EndPoint - StartPoint);

        public Vector2 Lerp(float t) => Vector2.Lerp(StartPoint, EndPoint, Math.Clamp(t, 0f, 1f));
    }

    private readonly struct ContourInfo
    {
        public ContourInfo(float startDistance, float endDistance, int startSegment, int segmentCount)
        {
            StartDistance = startDistance;
            EndDistance = endDistance;
            StartSegment = startSegment;
            SegmentCount = segmentCount;
        }

        public float StartDistance { get; }
        public float EndDistance { get; }
        public int StartSegment { get; }
        public int SegmentCount { get; }
    }
}
