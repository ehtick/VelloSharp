using System;
using System.Collections.Generic;
using System.Numerics;
using VelloSharp;

namespace SkiaSharp;

public sealed class SKPath : IDisposable
{
    private readonly List<PathCommand> _commands = new();

    public SKPath()
    {
    }

    public SKPath(SKPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        FillType = path.FillType;
        _commands.AddRange(path._commands);
    }

    public SKPathFillType FillType { get; set; } = SKPathFillType.Winding;

    public bool IsEmpty => _commands.Count == 0;

    public SKRect TightBounds
    {
        get
        {
            if (_commands.Count == 0)
            {
                return new SKRect(0, 0, 0, 0);
            }

            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;
            var current = new SKPoint(0, 0);
            var hasPoint = false;

            void Include(SKPoint point)
            {
                minX = MathF.Min(minX, point.X);
                minY = MathF.Min(minY, point.Y);
                maxX = MathF.Max(maxX, point.X);
                maxY = MathF.Max(maxY, point.Y);
                hasPoint = true;
            }

            foreach (var command in _commands)
            {
                switch (command.Verb)
                {
                    case PathVerb.MoveTo:
                        current = command.Point0;
                        Include(current);
                        break;
                    case PathVerb.LineTo:
                        Include(current);
                        Include(command.Point0);
                        current = command.Point0;
                        break;
                    case PathVerb.QuadTo:
                    case PathVerb.ConicTo:
                        Include(current);
                        Include(command.Point0);
                        Include(command.Point1);
                        current = command.Point1;
                        break;
                    case PathVerb.CubicTo:
                        Include(current);
                        Include(command.Point0);
                        Include(command.Point1);
                        Include(command.Point2);
                        current = command.Point2;
                        break;
                    case PathVerb.Close:
                        break;
                }
            }

            if (!hasPoint || float.IsInfinity(minX) || float.IsInfinity(minY) || float.IsInfinity(maxX) || float.IsInfinity(maxY))
            {
                return new SKRect(0, 0, 0, 0);
            }

            return new SKRect(minX, minY, maxX, maxY);
        }
    }

    public bool Contains(float x, float y)
    {
        ShimNotImplemented.Throw($"{nameof(SKPath)}.{nameof(Contains)}");
        return TightBounds.Contains(new SKPoint(x, y));
    }

    public void MoveTo(float x, float y) => MoveTo(new SKPoint(x, y));

    public void MoveTo(SKPoint point)
    {
        _commands.Add(new PathCommand(PathVerb.MoveTo, point));
    }

    public void LineTo(float x, float y) => LineTo(new SKPoint(x, y));

    public void LineTo(SKPoint point)
    {
        _commands.Add(new PathCommand(PathVerb.LineTo, point));
    }

    public void QuadTo(SKPoint control, SKPoint end)
    {
        _commands.Add(new PathCommand(PathVerb.QuadTo, control, end));
    }

    public void QuadTo(float x1, float y1, float x2, float y2) =>
        QuadTo(new SKPoint(x1, y1), new SKPoint(x2, y2));

    public void ConicTo(SKPoint control, SKPoint end, float weight)
    {
        _commands.Add(new PathCommand(PathVerb.ConicTo, control, end, default, weight));
    }

    public void ConicTo(float x1, float y1, float x2, float y2, float weight) =>
        ConicTo(new SKPoint(x1, y1), new SKPoint(x2, y2), weight);

    public void CubicTo(SKPoint control1, SKPoint control2, SKPoint end)
    {
        _commands.Add(new PathCommand(PathVerb.CubicTo, control1, control2, end));
    }

    public void CubicTo(float x1, float y1, float x2, float y2, float x3, float y3) =>
        CubicTo(new SKPoint(x1, y1), new SKPoint(x2, y2), new SKPoint(x3, y3));

    public void AddRect(SKRect rect) => AddRect(rect, SKPathDirection.Clockwise);

    public void AddRect(SKRect rect, SKPathDirection direction)
    {
        if (rect.IsEmpty)
        {
            return;
        }

        var topLeft = new SKPoint(rect.Left, rect.Top);
        var topRight = new SKPoint(rect.Right, rect.Top);
        var bottomRight = new SKPoint(rect.Right, rect.Bottom);
        var bottomLeft = new SKPoint(rect.Left, rect.Bottom);

        MoveTo(topLeft);
        if (direction == SKPathDirection.Clockwise)
        {
            LineTo(topRight);
            LineTo(bottomRight);
            LineTo(bottomLeft);
        }
        else
        {
            LineTo(bottomLeft);
            LineTo(bottomRight);
            LineTo(topRight);
        }

        Close();
    }

    public void AddOval(SKRect rect) => AddOval(rect, SKPathDirection.Clockwise);

    public void AddOval(SKRect rect, SKPathDirection direction)
    {
        if (rect.IsEmpty)
        {
            return;
        }

        var cx = (rect.Left + rect.Right) * 0.5f;
        var cy = (rect.Top + rect.Bottom) * 0.5f;
        var rx = rect.Width * 0.5f;
        var ry = rect.Height * 0.5f;
        if (rx == 0f || ry == 0f)
        {
            return;
        }

        const float kappa = 0.552284749831f;
        var ox = rx * kappa;
        var oy = ry * kappa;

        var start = new SKPoint(rect.Right, cy);
        MoveTo(start);

        if (direction == SKPathDirection.Clockwise)
        {
            CubicTo(
                new SKPoint(rect.Right, cy + oy),
                new SKPoint(cx + ox, rect.Bottom),
                new SKPoint(cx, rect.Bottom));
            CubicTo(
                new SKPoint(cx - ox, rect.Bottom),
                new SKPoint(rect.Left, cy + oy),
                new SKPoint(rect.Left, cy));
            CubicTo(
                new SKPoint(rect.Left, cy - oy),
                new SKPoint(cx - ox, rect.Top),
                new SKPoint(cx, rect.Top));
            CubicTo(
                new SKPoint(cx + ox, rect.Top),
                new SKPoint(rect.Right, cy - oy),
                start);
        }
        else
        {
            CubicTo(
                new SKPoint(rect.Right, cy - oy),
                new SKPoint(cx + ox, rect.Top),
                new SKPoint(cx, rect.Top));
            CubicTo(
                new SKPoint(cx - ox, rect.Top),
                new SKPoint(rect.Left, cy - oy),
                new SKPoint(rect.Left, cy));
            CubicTo(
                new SKPoint(rect.Left, cy + oy),
                new SKPoint(cx - ox, rect.Bottom),
                new SKPoint(cx, rect.Bottom));
            CubicTo(
                new SKPoint(cx + ox, rect.Bottom),
                new SKPoint(rect.Right, cy + oy),
                start);
        }

        Close();
    }

    public void AddPath(SKPath path)
    {
        AppendPath(path, null);
    }

    public void AddPath(SKPath path, float dx, float dy)
    {
        var transform = Matrix3x2.CreateTranslation(dx, dy);
        AppendPath(path, transform);
    }

    public void AddPath(SKPath path, SKMatrix matrix)
    {
        AppendPath(path, matrix.ToMatrix3x2());
    }

    public void ArcTo(float rx, float ry, float xAxisRotate, SKPathArcSize arcSize, SKPathDirection direction, float x, float y)
    {
        var endPoint = new SKPoint(x, y);
        if (!TryGetCurrentPoint(out var current))
        {
            MoveTo(endPoint);
            return;
        }

        if (Math.Abs(rx) < float.Epsilon || Math.Abs(ry) < float.Epsilon || Approximately(current, endPoint))
        {
            LineTo(endPoint);
            return;
        }

        var sweepPositive = direction == SKPathDirection.Clockwise;
        var largeArc = arcSize == SKPathArcSize.Large;

        var x0 = current.X;
        var y0 = current.Y;
        var x1 = endPoint.X;
        var y1 = endPoint.Y;

        var rxAbs = Math.Abs((double)rx);
        var ryAbs = Math.Abs((double)ry);

        var phi = DegreesToRadians(xAxisRotate);
        var cosPhi = Math.Cos(phi);
        var sinPhi = Math.Sin(phi);

        var dx2 = (x0 - x1) / 2.0;
        var dy2 = (y0 - y1) / 2.0;

        var x1Prime = cosPhi * dx2 + sinPhi * dy2;
        var y1Prime = -sinPhi * dx2 + cosPhi * dy2;

        if (Math.Abs(x1Prime) < 1e-12 && Math.Abs(y1Prime) < 1e-12)
        {
            LineTo(endPoint);
            return;
        }

        var rxSq = rxAbs * rxAbs;
        var rySq = ryAbs * ryAbs;
        var x1PrimeSq = x1Prime * x1Prime;
        var y1PrimeSq = y1Prime * y1Prime;

        var radiiCheck = x1PrimeSq / rxSq + y1PrimeSq / rySq;
        if (radiiCheck > 1)
        {
            var scale = Math.Sqrt(radiiCheck);
            rxAbs *= scale;
            ryAbs *= scale;
            rxSq = rxAbs * rxAbs;
            rySq = ryAbs * ryAbs;
        }

        var sign = (largeArc == sweepPositive) ? -1.0 : 1.0;
        var numerator = Math.Max(0.0, rxSq * rySq - rxSq * y1PrimeSq - rySq * x1PrimeSq);
        var denom = rxSq * y1PrimeSq + rySq * x1PrimeSq;
        var coef = denom == 0 ? 0 : sign * Math.Sqrt(numerator / denom);

        var cxPrime = coef * (rxAbs * y1Prime / ryAbs);
        var cyPrime = coef * (-ryAbs * x1Prime / rxAbs);

        var cx = cosPhi * cxPrime - sinPhi * cyPrime + (x0 + x1) / 2.0;
        var cy = sinPhi * cxPrime + cosPhi * cyPrime + (y0 + y1) / 2.0;

        var startVector = new Vector2d((x1Prime - cxPrime) / rxAbs, (y1Prime - cyPrime) / ryAbs);
        var endVector = new Vector2d((-x1Prime - cxPrime) / rxAbs, (-y1Prime - cyPrime) / ryAbs);

        var startAngle = AngleBetween(new Vector2d(1, 0), startVector);
        var sweepAngle = AngleBetween(startVector, endVector);

        if (!largeArc && Math.Abs(sweepAngle) > Math.PI)
        {
            sweepAngle += sweepAngle > 0 ? -2 * Math.PI : 2 * Math.PI;
        }
        else if (largeArc && Math.Abs(sweepAngle) < Math.PI)
        {
            sweepAngle += sweepAngle > 0 ? 2 * Math.PI : -2 * Math.PI;
        }

        if (!sweepPositive && sweepAngle > 0)
        {
            sweepAngle -= 2 * Math.PI;
        }
        else if (sweepPositive && sweepAngle < 0)
        {
            sweepAngle += 2 * Math.PI;
        }

        var segments = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweepAngle) / (Math.PI / 2.0)));
        var delta = sweepAngle / segments;
        var t = startAngle;

        for (var i = 0; i < segments; i++)
        {
            var t1 = t;
            var t2 = t1 + delta;
            AddArcSegment(cx, cy, rxAbs, ryAbs, cosPhi, sinPhi, t1, t2);
            t = t2;
        }
    }

    public void Reset() => _commands.Clear();

    public void Close() => _commands.Add(PathCommand.ClosePath);

    public void Dispose()
    {
        _commands.Clear();
    }

    public SKPath Clone() => new(this);

    public void Transform(SKMatrix matrix) => Transform(in matrix);

    public void Transform(in SKMatrix matrix)
    {
        var transform = matrix.ToMatrix3x2();
        for (var i = 0; i < _commands.Count; i++)
        {
            var command = _commands[i];
            _commands[i] = TransformCommand(command, transform);
        }
    }

    public Iterator CreateIterator(bool forceClose) => new(this, forceClose);

    public Iterator CreateIterator() => CreateIterator(false);

    public SKPath Op(SKPath other, SKPathOp operation)
    {
        ArgumentNullException.ThrowIfNull(other);
        ShimNotImplemented.Throw($"{nameof(SKPath)}.{nameof(Op)}", operation.ToString());
        return Clone();
    }

    internal PathBuilder ToPathBuilder()
    {
        var builder = new PathBuilder();
        foreach (var command in _commands)
        {
            switch (command.Verb)
            {
                case PathVerb.MoveTo:
                    builder.MoveTo(command.Point0.X, command.Point0.Y);
                    break;
                case PathVerb.LineTo:
                    builder.LineTo(command.Point0.X, command.Point0.Y);
                    break;
                case PathVerb.QuadTo:
                    builder.QuadraticTo(command.Point0.X, command.Point0.Y, command.Point1.X, command.Point1.Y);
                    break;
                case PathVerb.CubicTo:
                    builder.CubicTo(
                        command.Point0.X,
                        command.Point0.Y,
                        command.Point1.X,
                        command.Point1.Y,
                        command.Point2.X,
                        command.Point2.Y);
                    break;
                case PathVerb.Close:
                    builder.Close();
                    break;
            }
        }

        return builder;
    }

    public sealed class Iterator : IDisposable
    {
        private readonly SKPath _path;
        private readonly bool _forceClose;
        private int _index;
        private SKPoint _current;
        private SKPoint _first;
        private float _conicWeight;

        internal Iterator(SKPath path, bool forceClose)
        {
            _path = path;
            _forceClose = forceClose;
            _current = default;
            _first = default;
        }

        public SKPathVerb Next(SKPoint[] points)
        {
            ArgumentNullException.ThrowIfNull(points);
            if (points.Length < 4)
            {
                throw new ArgumentException("Iterator requires at least four points.", nameof(points));
            }

            if (_index >= _path._commands.Count)
            {
                return SKPathVerb.Done;
            }

            ShimNotImplemented.Throw($"{nameof(Iterator)}.{nameof(Next)}");

            var command = _path._commands[_index++];
            switch (command.Verb)
            {
                case PathVerb.MoveTo:
                    _current = command.Point0;
                    _first = _current;
                    points[0] = _current;
                    return SKPathVerb.Move;
                case PathVerb.LineTo:
                    points[0] = _current;
                    points[1] = command.Point0;
                    _current = command.Point0;
                    return SKPathVerb.Line;
                case PathVerb.QuadTo:
                    points[0] = _current;
                    points[1] = command.Point0;
                    points[2] = command.Point1;
                    _current = command.Point1;
                    return SKPathVerb.Quad;
                case PathVerb.ConicTo:
                    points[0] = _current;
                    points[1] = command.Point0;
                    points[2] = command.Point1;
                    _current = command.Point1;
                    _conicWeight = command.Weight;
                    return SKPathVerb.Conic;
                case PathVerb.CubicTo:
                    points[0] = _current;
                    points[1] = command.Point0;
                    points[2] = command.Point1;
                    points[3] = command.Point2;
                    _current = command.Point2;
                    return SKPathVerb.Cubic;
                case PathVerb.Close:
                    points[0] = _first;
                    if (_forceClose)
                    {
                        _current = _first;
                    }
                    return SKPathVerb.Close;
                default:
                    return SKPathVerb.Done;
            }
        }

        public float ConicWeight()
        {
            ShimNotImplemented.Throw($"{nameof(Iterator)}.{nameof(ConicWeight)}");
            return _conicWeight;
        }

        public void Dispose()
        {
        }
    }

    private readonly record struct PathCommand
    {
        public PathCommand(PathVerb verb, SKPoint point0)
            : this(verb, point0, default, default, 0f)
        {
        }

        public PathCommand(PathVerb verb, SKPoint point0, SKPoint point1)
            : this(verb, point0, point1, default, 0f)
        {
        }

        public PathCommand(PathVerb verb, SKPoint point0, SKPoint point1, SKPoint point2, float weight = 0f)
        {
            Verb = verb;
            Point0 = point0;
            Point1 = point1;
            Point2 = point2;
            Weight = weight;
        }

        public static PathCommand ClosePath => new(PathVerb.Close, default);

        public PathVerb Verb { get; }
        public SKPoint Point0 { get; }
        public SKPoint Point1 { get; }
        public SKPoint Point2 { get; }
        public float Weight { get; }
    }

    private void AppendPath(SKPath path, Matrix3x2? transform)
    {
        ArgumentNullException.ThrowIfNull(path);

        var source = ReferenceEquals(path, this)
            ? new List<PathCommand>(_commands)
            : path._commands;

        if (transform is Matrix3x2 matrix)
        {
            foreach (var command in source)
            {
                _commands.Add(TransformCommand(command, matrix));
            }
        }
        else
        {
            _commands.AddRange(source);
        }
    }

    private static PathCommand TransformCommand(PathCommand command, Matrix3x2 transform) => command.Verb switch
    {
        PathVerb.MoveTo => new PathCommand(PathVerb.MoveTo, TransformPoint(command.Point0, transform)),
        PathVerb.LineTo => new PathCommand(PathVerb.LineTo, TransformPoint(command.Point0, transform)),
        PathVerb.QuadTo => new PathCommand(PathVerb.QuadTo, TransformPoint(command.Point0, transform), TransformPoint(command.Point1, transform)),
        PathVerb.ConicTo => new PathCommand(PathVerb.ConicTo, TransformPoint(command.Point0, transform), TransformPoint(command.Point1, transform), default, command.Weight),
        PathVerb.CubicTo => new PathCommand(PathVerb.CubicTo, TransformPoint(command.Point0, transform), TransformPoint(command.Point1, transform), TransformPoint(command.Point2, transform)),
        PathVerb.Close => PathCommand.ClosePath,
        _ => command,
    };

    private static SKPoint TransformPoint(SKPoint point, Matrix3x2 transform)
    {
        var vector = Vector2.Transform(point.ToVector2(), transform);
        return new SKPoint(vector.X, vector.Y);
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180.0);

    private static bool Approximately(in SKPoint a, in SKPoint b)
        => Math.Abs(a.X - b.X) < 1e-4f && Math.Abs(a.Y - b.Y) < 1e-4f;

    private bool TryGetCurrentPoint(out SKPoint point)
    {
        point = default;
        if (_commands.Count == 0)
        {
            return false;
        }

        var current = new SKPoint(0, 0);
        var first = new SKPoint(0, 0);
        var haveCurrent = false;
        var haveFirst = false;

        foreach (var command in _commands)
        {
            switch (command.Verb)
            {
                case PathVerb.MoveTo:
                    current = command.Point0;
                    first = current;
                    haveCurrent = true;
                    haveFirst = true;
                    break;
                case PathVerb.LineTo:
                    if (!haveFirst)
                    {
                        first = current;
                        haveFirst = true;
                    }
                    current = command.Point0;
                    haveCurrent = true;
                    break;
                case PathVerb.QuadTo:
                case PathVerb.ConicTo:
                    if (!haveFirst)
                    {
                        first = current;
                        haveFirst = true;
                    }
                    current = command.Point1;
                    haveCurrent = true;
                    break;
                case PathVerb.CubicTo:
                    if (!haveFirst)
                    {
                        first = current;
                        haveFirst = true;
                    }
                    current = command.Point2;
                    haveCurrent = true;
                    break;
                case PathVerb.Close:
                    if (haveFirst)
                    {
                        current = first;
                        haveCurrent = true;
                    }
                    break;
            }
        }

        point = current;
        return haveCurrent;
    }

    private void AddArcSegment(double cx, double cy, double rx, double ry, double cosPhi, double sinPhi, double startAngle, double endAngle)
    {
        var delta = endAngle - startAngle;
        var alpha = (4.0 / 3.0) * Math.Tan(delta / 4.0);

        var cosStart = Math.Cos(startAngle);
        var sinStart = Math.Sin(startAngle);
        var cosEnd = Math.Cos(endAngle);
        var sinEnd = Math.Sin(endAngle);

        var p1x = cosStart;
        var p1y = sinStart;
        var p2x = cosEnd;
        var p2y = sinEnd;

        var cp1x = p1x - alpha * p1y;
        var cp1y = p1y + alpha * p1x;
        var cp2x = p2x + alpha * p2y;
        var cp2y = p2y - alpha * p2x;

        var cp1 = TransformArcPoint(cx, cy, rx, ry, cosPhi, sinPhi, cp1x, cp1y);
        var cp2 = TransformArcPoint(cx, cy, rx, ry, cosPhi, sinPhi, cp2x, cp2y);
        var end = TransformArcPoint(cx, cy, rx, ry, cosPhi, sinPhi, p2x, p2y);

        CubicTo(cp1, cp2, end);
    }

    private static SKPoint TransformArcPoint(double cx, double cy, double rx, double ry, double cosPhi, double sinPhi, double x, double y)
    {
        var xr = rx * x;
        var yr = ry * y;
        var xp = cosPhi * xr - sinPhi * yr;
        var yp = sinPhi * xr + cosPhi * yr;
        return new SKPoint((float)(xp + cx), (float)(yp + cy));
    }

    private static double AngleBetween(Vector2d u, Vector2d v)
    {
        var dot = u.X * v.X + u.Y * v.Y;
        var len = Math.Sqrt((u.X * u.X + u.Y * u.Y) * (v.X * v.X + v.Y * v.Y));
        if (len == 0)
        {
            return 0;
        }

        var cos = Math.Clamp(dot / len, -1.0, 1.0);
        var angle = Math.Acos(cos);
        var cross = u.X * v.Y - u.Y * v.X;
        return cross >= 0 ? angle : -angle;
    }

    private readonly struct Vector2d
    {
        public Vector2d(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }
    }

    private enum PathVerb
    {
        MoveTo,
        LineTo,
        QuadTo,
        ConicTo,
        CubicTo,
        Close,
    }
}
