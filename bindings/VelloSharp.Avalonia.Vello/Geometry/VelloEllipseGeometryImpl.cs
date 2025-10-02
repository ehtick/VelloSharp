using Avalonia;
using VelloSharp;
using VelloSharp.Avalonia.Vello.Geometry;

namespace VelloSharp.Avalonia.Vello;

internal sealed class VelloEllipseGeometryImpl : VelloGeometryImplBase
{
    public VelloEllipseGeometryImpl(Rect rect)
        : base(CreateData(rect))
    {
        Rect = rect;
    }

    public Rect Rect { get; }

    public override Rect Bounds => Rect;

    private static VelloPathData CreateData(Rect rect)
    {
        var builder = new PathBuilder();
        builder.AddEllipse(rect);
        var data = new VelloPathData();
        data.Append(builder.AsSpan());
        return data;
    }
}
