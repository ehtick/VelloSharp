using System.Collections.Generic;
using VelloSharp.Charting.Layout;
using VelloSharp.Charting.Scales;
using VelloSharp.Charting.Styling;
using VelloSharp.Charting.Ticks;

namespace VelloSharp.Charting.Axis;

/// <summary>
/// Data model consumed by axis renderers.
/// </summary>
public sealed class AxisRenderModel
{
    public AxisRenderModel(string id, AxisOrientation orientation, AxisLayout layout, IScale scale, AxisStyle style, IReadOnlyList<AxisTickInfo> ticks)
    {
        Id = id;
        Orientation = orientation;
        Layout = layout;
        Scale = scale;
        Style = style;
        Ticks = ticks;
    }

    public string Id { get; }

    public AxisOrientation Orientation { get; }

    public AxisLayout Layout { get; }

    public IScale Scale { get; }

    public AxisStyle Style { get; }

    public IReadOnlyList<AxisTickInfo> Ticks { get; }
}
