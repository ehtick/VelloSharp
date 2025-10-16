using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VelloSharp;

public enum PathVerb
{
    MoveTo = 0,
    LineTo = 1,
    QuadTo = 2,
    CubicTo = 3,
    Close = 4,
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct PathElement
{
    private readonly PathVerb _verb;
    private readonly int _padding;
    private readonly double _x0;
    private readonly double _y0;
    private readonly double _x1;
    private readonly double _y1;
    private readonly double _x2;
    private readonly double _y2;

    public PathElement(
        PathVerb verb,
        double x0 = 0,
        double y0 = 0,
        double x1 = 0,
        double y1 = 0,
        double x2 = 0,
        double y2 = 0)
    {
        _verb = verb;
        _padding = 0;
        _x0 = x0;
        _y0 = y0;
        _x1 = x1;
        _y1 = y1;
        _x2 = x2;
        _y2 = y2;
    }

    public PathVerb Verb => _verb;
    public double X0 => _x0;
    public double Y0 => _y0;
    public double X1 => _x1;
    public double Y1 => _y1;
    public double X2 => _x2;
    public double Y2 => _y2;
}

public sealed class PathBuilder
{
    private readonly List<PathElement> _elements = new();

    public int Count => _elements.Count;

    internal PathVerb FirstVerb => _elements.Count > 0 ? _elements[0].Verb : default;

    public PathBuilder MoveTo(double x, double y)
    {
        _elements.Add(new PathElement(PathVerb.MoveTo, x, y));
        return this;
    }

    public PathBuilder LineTo(double x, double y)
    {
        _elements.Add(new PathElement(PathVerb.LineTo, x, y));
        return this;
    }

    public PathBuilder QuadraticTo(double cx, double cy, double x, double y)
    {
        _elements.Add(new PathElement(PathVerb.QuadTo, cx, cy, x, y));
        return this;
    }

    public PathBuilder CubicTo(double c1x, double c1y, double c2x, double c2y, double x, double y)
    {
        _elements.Add(new PathElement(PathVerb.CubicTo, c1x, c1y, c2x, c2y, x, y));
        return this;
    }

    public PathBuilder Close()
    {
        _elements.Add(new PathElement(PathVerb.Close));
        return this;
    }

    public void Clear() => _elements.Clear();

    public ReadOnlySpan<PathElement> AsSpan() => CollectionsMarshal.AsSpan(_elements);
}
