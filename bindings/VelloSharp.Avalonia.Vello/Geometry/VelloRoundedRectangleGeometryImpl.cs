using Avalonia;
using VelloSharp;
using VelloSharp.Avalonia.Vello.Geometry;

namespace VelloSharp.Avalonia.Vello;

internal sealed class VelloRoundedRectangleGeometryImpl : VelloGeometryImplBase
{
    public VelloRoundedRectangleGeometryImpl(RoundedRect rect)
        : base(CreateData(rect))
    {
        RoundedRect = rect;
    }

    public RoundedRect RoundedRect { get; }

    public override Rect Bounds => RoundedRect.Rect;

    private static VelloPathData CreateData(RoundedRect rect)
    {
        var builder = new PathBuilder();
        builder.AddRoundedRectangle(rect);
        var data = new VelloPathData();
        using var nativePath = NativePathElements.Rent(builder);
        data.Append(nativePath.Span);
        return data;
    }
}
