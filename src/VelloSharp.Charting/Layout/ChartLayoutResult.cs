using System.Collections.Generic;

namespace VelloSharp.Charting.Layout;

/// <summary>
/// Represents the computed layout for a chart viewport.
/// </summary>
public sealed class ChartLayoutResult
{
    internal ChartLayoutResult(LayoutRect plotArea, IReadOnlyList<AxisLayout> axes)
    {
        PlotArea = plotArea;
        Axes = axes;
    }

    /// <summary>
    /// Gets the plot area where series content should be rendered.
    /// </summary>
    public LayoutRect PlotArea { get; }

    /// <summary>
    /// Gets the computed axis layouts.
    /// </summary>
    public IReadOnlyList<AxisLayout> Axes { get; }
}
