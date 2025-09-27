using System;
using Avalonia;
using VelloSharp.Avalonia.Vello.Geometry;

namespace VelloSharp.Avalonia.Vello;

internal sealed class VelloLineGeometryImpl : VelloGeometryImplBase
{
    public VelloLineGeometryImpl(Point p1, Point p2)
        : base(CreateData(p1, p2))
    {
        Start = p1;
        End = p2;
    }

    public Point Start { get; }
    public Point End { get; }

    public override Rect Bounds => new Rect(Start, End);

    private static VelloPathData CreateData(Point p1, Point p2)
    {
        var data = new VelloPathData();
        data.MoveTo(p1.X, p1.Y);
        data.LineTo(p2.X, p2.Y);
        return data;
    }
}
