using System.Collections.Generic;
using VelloSharp.Charting.Layout;

namespace VelloSharp.Charting.Axis;

/// <summary>
/// Output of the axis composition process with plot area and axis models.
/// </summary>
public sealed class AxisRenderSurface
{
    public AxisRenderSurface(LayoutRect plotArea, IReadOnlyList<AxisRenderModel> axes)
    {
        PlotArea = plotArea;
        Axes = axes;
    }

    public LayoutRect PlotArea { get; }

    public IReadOnlyList<AxisRenderModel> Axes { get; }
}
