using System;
using System.Collections.Generic;
using Avalonia;
using VelloSharp;

namespace VelloSharp.Avalonia.Vello.Geometry;

internal sealed class VelloPathData
{
    private readonly List<PathCommand> _commands = new();
    private double _minX = double.PositiveInfinity;
    private double _minY = double.PositiveInfinity;
    private double _maxX = double.NegativeInfinity;
    private double _maxY = double.NegativeInfinity;
    private Point _currentPoint;
    private Point _figureStart;
    private bool _hasCurrentPoint;
    private double _contourLength;

    public IReadOnlyList<PathCommand> Commands => _commands;

    public Rect Bounds => _commands.Count == 0
        ? default
        : new Rect(new Point(_minX, _minY), new Point(_maxX, _maxY));

    public double ContourLength => _contourLength;

    public void MoveTo(double x, double y)
    {
        var point = new Point(x, y);
        _commands.Add(PathCommand.MoveTo(point));
        _currentPoint = point;
        _figureStart = point;
        _hasCurrentPoint = true;
        UpdateBounds(point);
    }

    public void LineTo(double x, double y)
    {
        EnsureCurrentPoint();
        var point = new Point(x, y);
        _commands.Add(PathCommand.LineTo(point));
        _contourLength += Distance(_currentPoint, point);
        _currentPoint = point;
        UpdateBounds(point);
    }

    public void QuadraticTo(double cx, double cy, double x, double y)
    {
        EnsureCurrentPoint();
        var control = new Point(cx, cy);
        var end = new Point(x, y);
        _commands.Add(PathCommand.QuadTo(control, end));
        UpdateBounds(control);
        UpdateBounds(end);
        _contourLength += EstimateQuadraticLength(_currentPoint, control, end);
        _currentPoint = end;
    }

    public void CubicTo(double c1x, double c1y, double c2x, double c2y, double x, double y)
    {
        EnsureCurrentPoint();
        var c1 = new Point(c1x, c1y);
        var c2 = new Point(c2x, c2y);
        var end = new Point(x, y);
        _commands.Add(PathCommand.CubicTo(c1, c2, end));
        UpdateBounds(c1);
        UpdateBounds(c2);
        UpdateBounds(end);
        _contourLength += EstimateCubicLength(_currentPoint, c1, c2, end);
        _currentPoint = end;
    }

    public void Close()
    {
        if (!_hasCurrentPoint)
        {
            return;
        }

        _commands.Add(PathCommand.Close());
        _contourLength += Distance(_currentPoint, _figureStart);
        _currentPoint = _figureStart;
    }

    public PathBuilder ToPathBuilder()
    {
        var builder = new PathBuilder();
        foreach (var command in _commands)
        {
            switch (command.Verb)
            {
                case VelloPathVerb.MoveTo:
                    builder.MoveTo(command.X0, command.Y0);
                    break;
                case VelloPathVerb.LineTo:
                    builder.LineTo(command.X0, command.Y0);
                    break;
                case VelloPathVerb.QuadTo:
                    builder.QuadraticTo(command.X0, command.Y0, command.X1, command.Y1);
                    break;
                case VelloPathVerb.CubicTo:
                    builder.CubicTo(command.X0, command.Y0, command.X1, command.Y1, command.X2, command.Y2);
                    break;
                case VelloPathVerb.Close:
                    builder.Close();
                    break;
            }
        }
        return builder;
    }

    public VelloPathData Transform(Matrix transform)
    {
        var result = new VelloPathData();
        foreach (var command in _commands)
        {
            switch (command.Verb)
            {
                case VelloPathVerb.MoveTo:
                {
                    var pt = transform.Transform(command.Point0);
                    result.MoveTo(pt.X, pt.Y);
                    break;
                }
                case VelloPathVerb.LineTo:
                {
                    var pt = transform.Transform(command.Point0);
                    result.LineTo(pt.X, pt.Y);
                    break;
                }
                case VelloPathVerb.QuadTo:
                {
                    var c = transform.Transform(command.Point0);
                    var end = transform.Transform(command.Point1);
                    result.QuadraticTo(c.X, c.Y, end.X, end.Y);
                    break;
                }
                case VelloPathVerb.CubicTo:
                {
                    var c1 = transform.Transform(command.Point0);
                    var c2 = transform.Transform(command.Point1);
                    var end = transform.Transform(command.Point2);
                    result.CubicTo(c1.X, c1.Y, c2.X, c2.Y, end.X, end.Y);
                    break;
                }
                case VelloPathVerb.Close:
                    result.Close();
                    break;
            }
        }

        return result;
    }

    public void Append(ReadOnlySpan<VelloPathElement> elements)
    {
        foreach (ref readonly var element in elements)
        {
            switch (element.Verb)
            {
                case VelloPathVerb.MoveTo:
                    MoveTo(element.X0, element.Y0);
                    break;
                case VelloPathVerb.LineTo:
                    LineTo(element.X0, element.Y0);
                    break;
                case VelloPathVerb.QuadTo:
                    QuadraticTo(element.X0, element.Y0, element.X1, element.Y1);
                    break;
                case VelloPathVerb.CubicTo:
                    CubicTo(element.X0, element.Y0, element.X1, element.Y1, element.X2, element.Y2);
                    break;
                case VelloPathVerb.Close:
                    Close();
                    break;
            }
        }
    }

    public VelloPathData Clone()
    {
        var clone = new VelloPathData();
        foreach (var command in _commands)
        {
            switch (command.Verb)
            {
                case VelloPathVerb.MoveTo:
                    clone.MoveTo(command.X0, command.Y0);
                    break;
                case VelloPathVerb.LineTo:
                    clone.LineTo(command.X0, command.Y0);
                    break;
                case VelloPathVerb.QuadTo:
                    clone.QuadraticTo(command.X0, command.Y0, command.X1, command.Y1);
                    break;
                case VelloPathVerb.CubicTo:
                    clone.CubicTo(command.X0, command.Y0, command.X1, command.Y1, command.X2, command.Y2);
                    break;
                case VelloPathVerb.Close:
                    clone.Close();
                    break;
            }
        }

        return clone;
    }

    private void UpdateBounds(Point point)
    {
        if (point.X < _minX)
        {
            _minX = point.X;
        }

        if (point.Y < _minY)
        {
            _minY = point.Y;
        }

        if (point.X > _maxX)
        {
            _maxX = point.X;
        }

        if (point.Y > _maxY)
        {
            _maxY = point.Y;
        }
    }

    private void EnsureCurrentPoint()
    {
        if (!_hasCurrentPoint)
        {
            throw new InvalidOperationException("Path must start with MoveTo.");
        }
    }

    private static double EstimateQuadraticLength(Point p0, Point p1, Point p2)
    {
        // Approximate with polygonal chain.
        var chord = Distance(p0, p2);
        var segment1 = Distance(p0, p1);
        var segment2 = Distance(p1, p2);
        var contNet = segment1 + segment2;
        return (contNet + chord) / 2;
    }

    private static double EstimateCubicLength(Point p0, Point p1, Point p2, Point p3)
    {
        var chord = Distance(p0, p3);
        var first = Distance(p0, p1);
        var second = Distance(p1, p2);
        var third = Distance(p2, p3);
        var contNet = first + second + third;
        return (contNet + chord) / 2;
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    internal readonly struct PathCommand
    {
        private PathCommand(VelloPathVerb verb, Point p0, Point p1, Point p2)
        {
            Verb = verb;
            Point0 = p0;
            Point1 = p1;
            Point2 = p2;
        }

        public VelloPathVerb Verb { get; }
        public Point Point0 { get; }
        public Point Point1 { get; }
        public Point Point2 { get; }

        public double X0 => Point0.X;
        public double Y0 => Point0.Y;
        public double X1 => Point1.X;
        public double Y1 => Point1.Y;
        public double X2 => Point2.X;
        public double Y2 => Point2.Y;

        public static PathCommand MoveTo(Point point) => new(VelloPathVerb.MoveTo, point, default, default);
        public static PathCommand LineTo(Point point) => new(VelloPathVerb.LineTo, point, default, default);
        public static PathCommand QuadTo(Point control, Point end) => new(VelloPathVerb.QuadTo, control, end, default);
        public static PathCommand CubicTo(Point c1, Point c2, Point end) => new(VelloPathVerb.CubicTo, c1, c2, end);
        public static PathCommand Close() => new(VelloPathVerb.Close, default, default, default);
    }
}
