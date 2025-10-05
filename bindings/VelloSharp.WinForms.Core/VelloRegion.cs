using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using VelloSharp;

namespace VelloSharp.WinForms;

public sealed class VelloRegion
{
    private readonly List<PathElement> _elements = new();

    public VelloRegion()
    {
    }

    public VelloRegion(RectangleF rectangle)
    {
        SetRectangle(rectangle);
    }

    public VelloRegion(VelloGraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        FillMode = path.FillMode;
        _elements.AddRange(path.SnapshotElements());
    }

    public FillMode FillMode { get; set; } = FillMode.Alternate;

    public bool IsEmpty => _elements.Count == 0;

    public void MakeEmpty() => _elements.Clear();

    public void SetRectangle(RectangleF rectangle)
    {
        _elements.Clear();
        _elements.Add(new PathElement(PathVerb.MoveTo, rectangle.Left, rectangle.Top));
        _elements.Add(new PathElement(PathVerb.LineTo, rectangle.Right, rectangle.Top));
        _elements.Add(new PathElement(PathVerb.LineTo, rectangle.Right, rectangle.Bottom));
        _elements.Add(new PathElement(PathVerb.LineTo, rectangle.Left, rectangle.Bottom));
        _elements.Add(new PathElement(PathVerb.Close));
    }

    public void SetPath(VelloGraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        _elements.Clear();
        _elements.AddRange(path.SnapshotElements());
        FillMode = path.FillMode;
    }

        public VelloRegion Clone()
    {
        var clone = new VelloRegion
        {
            FillMode = FillMode,
        };
        clone._elements.AddRange(_elements);
        return clone;
    }

    public RectangleF GetBounds()
    {
        if (_elements.Count == 0)
        {
            return RectangleF.Empty;
        }

        double minX = double.PositiveInfinity;
        double minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity;
        double maxY = double.NegativeInfinity;

        void Accumulate(double x, double y)
        {
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
            {
                return;
            }

            if (x < minX)
            {
                minX = x;
            }

            if (x > maxX)
            {
                maxX = x;
            }

            if (y < minY)
            {
                minY = y;
            }

            if (y > maxY)
            {
                maxY = y;
            }
        }

        foreach (var element in _elements)
        {
            switch (element.Verb)
            {
                case PathVerb.MoveTo:
                case PathVerb.LineTo:
                    Accumulate(element.X0, element.Y0);
                    break;
                case PathVerb.QuadTo:
                    Accumulate(element.X0, element.Y0);
                    Accumulate(element.X1, element.Y1);
                    break;
                case PathVerb.CubicTo:
                    Accumulate(element.X0, element.Y0);
                    Accumulate(element.X1, element.Y1);
                    Accumulate(element.X2, element.Y2);
                    break;
            }
        }

        if (minX == double.PositiveInfinity || minY == double.PositiveInfinity)
        {
            return RectangleF.Empty;
        }

        return RectangleF.FromLTRB((float)minX, (float)minY, (float)maxX, (float)maxY);
    }

    internal PathBuilder ToPathBuilder()
    {
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
public void Transform(Matrix3x2 matrix)
    {
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

    internal PathElement[] SnapshotElements() => _elements.ToArray();

    private static (double X, double Y) TransformPoint(double x, double y, Matrix3x2 matrix)
    {
        var transformed = Vector2.Transform(new Vector2((float)x, (float)y), matrix);
        return (transformed.X, transformed.Y);
    }
}

