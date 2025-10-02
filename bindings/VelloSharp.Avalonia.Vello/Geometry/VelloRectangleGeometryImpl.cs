using System;
using Avalonia;

namespace VelloSharp.Avalonia.Vello;

internal sealed class VelloRectangleGeometryImpl : VelloGeometryImplBase
{
    public VelloRectangleGeometryImpl(Rect rect)
        : base(CreateData(rect))
    {
        Rect = rect;
    }

    public Rect Rect { get; }

    public override Rect Bounds => Rect;

    private static Geometry.VelloPathData CreateData(Rect rect)
    {
        var data = new Geometry.VelloPathData();
        data.MoveTo(rect.X, rect.Y);
        data.LineTo(rect.Right, rect.Y);
        data.LineTo(rect.Right, rect.Bottom);
        data.LineTo(rect.X, rect.Bottom);
        data.Close();
        return data;
    }
}
