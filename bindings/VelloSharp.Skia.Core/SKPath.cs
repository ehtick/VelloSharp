using System;
using System.Collections.Generic;
using VelloSharp;

namespace SkiaSharp;

public sealed class SKPath : IDisposable
{
    private readonly List<PathCommand> _commands = new();

    public SKPathFillType FillType { get; set; } = SKPathFillType.Winding;

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

    public void ArcTo(float rx, float ry, float xAxisRotate, SKPathArcSize arcSize, SKPathDirection direction, float x, float y)
    {
        _ = rx;
        _ = ry;
        _ = xAxisRotate;
        _ = arcSize;
        _ = direction;
        ShimNotImplemented.Throw($"{nameof(SKPath)}.{nameof(ArcTo)}");
        LineTo(x, y);
    }

    public void Reset() => _commands.Clear();

    public void Close() => _commands.Add(PathCommand.ClosePath);

    public void Dispose()
    {
        _commands.Clear();
    }

    public SKPath Clone()
    {
        var clone = new SKPath();
        clone._commands.AddRange(_commands);
        return clone;
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
