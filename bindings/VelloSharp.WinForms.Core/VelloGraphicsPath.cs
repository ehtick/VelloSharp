using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using VelloSharp;

namespace VelloSharp.WinForms;

public sealed class VelloGraphicsPath : IDisposable
{
    private readonly List<PathElement> _elements = new();
    private bool _figureOpen;
    private bool _hasCurrentPoint;
    private PointF _currentPoint;
    private bool _disposed;

    public VelloGraphicsPath()
        : this(FillMode.Alternate)
    {
    }

    public VelloGraphicsPath(FillMode fillMode)
    {
        FillMode = fillMode;
    }

    public FillMode FillMode { get; set; }

    public bool IsEmpty => _elements.Count == 0;

    public void StartFigure()
    {
        EnsureNotDisposed();
        _figureOpen = false;
        _hasCurrentPoint = false;
    }

    public void CloseFigure()
    {
        EnsureNotDisposed();
        if (_figureOpen)
        {
            _elements.Add(new PathElement(PathVerb.Close));
            _figureOpen = false;
            _hasCurrentPoint = false;
        }
    }

    public void Reset()
    {
        EnsureNotDisposed();
        _elements.Clear();
        _figureOpen = false;
        _hasCurrentPoint = false;
    }

    public void AddLine(PointF pt1, PointF pt2)
    {
        EnsureNotDisposed();
        EnsureFigureStarted(pt1, connect: false);
        AddLineTo(pt2);
    }

    public void AddLine(float x1, float y1, float x2, float y2)
        => AddLine(new PointF(x1, y1), new PointF(x2, y2));

    public void AddLines(ReadOnlySpan<PointF> points)
    {
        EnsureNotDisposed();
        if (points.Length < 2)
        {
            throw new ArgumentException("At least two points are required.", nameof(points));
        }

        EnsureFigureStarted(points[0], connect: false);
        for (var i = 1; i < points.Length; i++)
        {
            AddLineTo(points[i]);
        }
    }

    public void AddRectangle(RectangleF rectangle)
    {
        EnsureNotDisposed();
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return;
        }

        AppendRectangle(rectangle);
    }

    public void AddRectangles(ReadOnlySpan<RectangleF> rectangles)
    {
        EnsureNotDisposed();
        for (var i = 0; i < rectangles.Length; i++)
        {
            AddRectangle(rectangles[i]);
        }
    }

    public void AddPolygon(ReadOnlySpan<PointF> points)
    {
        EnsureNotDisposed();
        if (points.Length < 3)
        {
            throw new ArgumentException("At least three points are required.", nameof(points));
        }

        _elements.Add(new PathElement(PathVerb.MoveTo, points[0].X, points[0].Y));
        for (var i = 1; i < points.Length; i++)
        {
            var point = points[i];
            _elements.Add(new PathElement(PathVerb.LineTo, point.X, point.Y));
        }

        _elements.Add(new PathElement(PathVerb.Close));
        _currentPoint = points[points.Length - 1];
        _figureOpen = false;
        _hasCurrentPoint = false;
    }

    public void AddEllipse(RectangleF rectangle)
    {
        EnsureNotDisposed();
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return;
        }

        AppendEllipse(rectangle);
    }
    public void AddBezier(PointF pt1, PointF pt2, PointF pt3, PointF pt4)
    {
        EnsureNotDisposed();
        EnsureFigureStarted(pt1, connect: false);
        _elements.Add(new PathElement(PathVerb.CubicTo, pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y));
        _currentPoint = pt4;
        _hasCurrentPoint = true;
    }

    public void AddBezier(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
        => AddBezier(new PointF(x1, y1), new PointF(x2, y2), new PointF(x3, y3), new PointF(x4, y4));

    public void AddBeziers(ReadOnlySpan<PointF> points)
    {
        EnsureNotDisposed();
        if (points.Length < 4 || ((points.Length - 1) % 3) != 0)
        {
            throw new ArgumentException("Point sequence must be 4 + 3n elements (start plus control triplets).", nameof(points));
        }

        EnsureFigureStarted(points[0], connect: false);
        for (var i = 1; i < points.Length; i += 3)
        {
            var c1 = points[i];
            var c2 = points[i + 1];
            var end = points[i + 2];
            _elements.Add(new PathElement(PathVerb.CubicTo, c1.X, c1.Y, c2.X, c2.Y, end.X, end.Y));
            _currentPoint = end;
            _hasCurrentPoint = true;
        }
    }

    public void AddQuadratic(PointF pt1, PointF control, PointF pt2)
    {
        EnsureNotDisposed();
        EnsureFigureStarted(pt1, connect: false);
        _elements.Add(new PathElement(PathVerb.QuadTo, control.X, control.Y, pt2.X, pt2.Y));
        _currentPoint = pt2;
        _hasCurrentPoint = true;
    }

    public void AddPath(VelloGraphicsPath path, bool connect)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(path);
        path.EnsureNotDisposed();

        if (path._elements.Count == 0)
        {
            return;
        }

        var first = true;
        foreach (var element in path._elements)
        {
            switch (element.Verb)
            {
                case PathVerb.MoveTo:
                    if (first && connect && _hasCurrentPoint)
                    {
                        _elements.Add(new PathElement(PathVerb.LineTo, element.X0, element.Y0));
                        _currentPoint = new PointF((float)element.X0, (float)element.Y0);
                        _hasCurrentPoint = true;
                        _figureOpen = true;
                    }
                    else
                    {
                        _elements.Add(element);
                        _currentPoint = new PointF((float)element.X0, (float)element.Y0);
                        _hasCurrentPoint = true;
                        _figureOpen = true;
                    }
                    break;
                case PathVerb.LineTo:
                    _elements.Add(element);
                    _currentPoint = new PointF((float)element.X0, (float)element.Y0);
                    _hasCurrentPoint = true;
                    _figureOpen = true;
                    break;
                case PathVerb.QuadTo:
                    _elements.Add(element);
                    _currentPoint = new PointF((float)element.X1, (float)element.Y1);
                    _hasCurrentPoint = true;
                    _figureOpen = true;
                    break;
                case PathVerb.CubicTo:
                    _elements.Add(element);
                    _currentPoint = new PointF((float)element.X2, (float)element.Y2);
                    _hasCurrentPoint = true;
                    _figureOpen = true;
                    break;
                case PathVerb.Close:
                    _elements.Add(element);
                    _figureOpen = false;
                    _hasCurrentPoint = false;
                    break;
            }

            first = false;
        }
    }

    public void Transform(Matrix3x2 matrix)
    {
        EnsureNotDisposed();
        for (var i = 0; i < _elements.Count; i++)
        {
            var element = _elements[i];
            switch (element.Verb)
            {
                case PathVerb.MoveTo:
                {
                    var p = TransformPoint(element.X0, element.Y0, matrix);
                    element = new PathElement(PathVerb.MoveTo, p.X, p.Y);
                    break;
                }
                case PathVerb.LineTo:
                {
                    var p = TransformPoint(element.X0, element.Y0, matrix);
                    element = new PathElement(PathVerb.LineTo, p.X, p.Y);
                    break;
                }
                case PathVerb.QuadTo:
                {
                    var c = TransformPoint(element.X0, element.Y0, matrix);
                    var end = TransformPoint(element.X1, element.Y1, matrix);
                    element = new PathElement(PathVerb.QuadTo, c.X, c.Y, end.X, end.Y);
                    break;
                }
                case PathVerb.CubicTo:
                {
                    var c1 = TransformPoint(element.X0, element.Y0, matrix);
                    var c2 = TransformPoint(element.X1, element.Y1, matrix);
                    var end = TransformPoint(element.X2, element.Y2, matrix);
                    element = new PathElement(PathVerb.CubicTo, c1.X, c1.Y, c2.X, c2.Y, end.X, end.Y);
                    break;
                }
            }

            _elements[i] = element;
        }
    }

    internal PathBuilder ToPathBuilder()
    {
        EnsureNotDisposed();
        var builder = new PathBuilder();
        foreach (var element in _elements)
        {
            switch (element.Verb)
            {
                case PathVerb.MoveTo:
                    builder.MoveTo(element.X0, element.Y0);
                    break;
                case PathVerb.LineTo:
                    builder.LineTo(element.X0, element.Y0);
                    break;
                case PathVerb.QuadTo:
                    builder.QuadraticTo(element.X0, element.Y0, element.X1, element.Y1);
                    break;
                case PathVerb.CubicTo:
                    builder.CubicTo(element.X0, element.Y0, element.X1, element.Y1, element.X2, element.Y2);
                    break;
                case PathVerb.Close:
                    builder.Close();
                    break;
            }
        }

        return builder;
    }

    internal PathElement[] SnapshotElements()
    {
        EnsureNotDisposed();
        return _elements.ToArray();
    }

    public void Dispose()
    {
        _disposed = true;
        _elements.Clear();
    }

    private void EnsureFigureStarted(PointF start, bool connect)
    {
        if (_figureOpen)
        {
            if (!connect)
            {
                _elements.Add(new PathElement(PathVerb.MoveTo, start.X, start.Y));
            }
            else if (!_hasCurrentPoint || !ApproximatelyEquals(_currentPoint, start))
            {
                _elements.Add(new PathElement(PathVerb.LineTo, start.X, start.Y));
            }
        }
        else
        {
            _elements.Add(new PathElement(PathVerb.MoveTo, start.X, start.Y));
            _figureOpen = true;
        }

        _currentPoint = start;
        _hasCurrentPoint = true;
    }

    private void AddLineTo(PointF point)
    {
        _elements.Add(new PathElement(PathVerb.LineTo, point.X, point.Y));
        _currentPoint = point;
        _hasCurrentPoint = true;
    }

    private void AppendRectangle(RectangleF rectangle)
    {
        _elements.Add(new PathElement(PathVerb.MoveTo, rectangle.Left, rectangle.Top));
        _elements.Add(new PathElement(PathVerb.LineTo, rectangle.Right, rectangle.Top));
        _elements.Add(new PathElement(PathVerb.LineTo, rectangle.Right, rectangle.Bottom));
        _elements.Add(new PathElement(PathVerb.LineTo, rectangle.Left, rectangle.Bottom));
        _elements.Add(new PathElement(PathVerb.Close));
        _currentPoint = new PointF(rectangle.Left, rectangle.Top);
        _figureOpen = false;
        _hasCurrentPoint = false;
    }

    private void AppendEllipse(RectangleF rectangle)
    {
        const double kappa = 0.55228474983079363;
        var cx = rectangle.Left + rectangle.Width / 2.0;
        var cy = rectangle.Top + rectangle.Height / 2.0;
        var rx = rectangle.Width / 2.0;
        var ry = rectangle.Height / 2.0;
        var ox = rx * kappa;
        var oy = ry * kappa;
        var top = cy - ry;
        var bottom = cy + ry;
        var left = rectangle.Left;
        var right = rectangle.Right;

        _elements.Add(new PathElement(PathVerb.MoveTo, cx, top));
        _elements.Add(new PathElement(PathVerb.CubicTo, cx + ox, top, right, cy - oy, right, cy));
        _elements.Add(new PathElement(PathVerb.CubicTo, right, cy + oy, cx + ox, bottom, cx, bottom));
        _elements.Add(new PathElement(PathVerb.CubicTo, cx - ox, bottom, left, cy + oy, left, cy));
        _elements.Add(new PathElement(PathVerb.CubicTo, left, cy - oy, cx - ox, top, cx, top));
        _elements.Add(new PathElement(PathVerb.Close));
        _currentPoint = new PointF((float)cx, (float)top);
        _figureOpen = false;
        _hasCurrentPoint = false;
    }
    private static (double X, double Y) TransformPoint(double x, double y, Matrix3x2 matrix)
    {
        var transformed = Vector2.Transform(new Vector2((float)x, (float)y), matrix);
        return (transformed.X, transformed.Y);
    }

    private static bool ApproximatelyEquals(PointF a, PointF b)
        => Math.Abs(a.X - b.X) < 0.0001f && Math.Abs(a.Y - b.Y) < 0.0001f;

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VelloGraphicsPath));
        }
    }
}


