using System.Collections.Generic;
using VelloSharp.Charting.Layout;

namespace VelloSharp.Charting.Rendering;

public sealed class AxisRenderResult
{
    public AxisRenderResult(LayoutRect plotArea, IReadOnlyList<AxisVisual> axes)
    {
        PlotArea = plotArea;
        Axes = axes;
    }

    public LayoutRect PlotArea { get; }

    public IReadOnlyList<AxisVisual> Axes { get; }
}
