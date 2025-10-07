using System;
using System.Collections.Generic;

namespace VelloSharp.Charting.Styling;

/// <summary>
/// Represents a collection of colors used throughout the chart.
/// </summary>
public sealed class ChartPalette
{
    public ChartPalette(
        RgbaColor background,
        RgbaColor foreground,
        RgbaColor axisLine,
        RgbaColor axisTick,
        RgbaColor gridLine,
        RgbaColor legendBackground,
        RgbaColor legendBorder,
        IReadOnlyList<RgbaColor> series)
    {
        Background = background;
        Foreground = foreground;
        AxisLine = axisLine;
        AxisTick = axisTick;
        GridLine = gridLine;
        LegendBackground = legendBackground;
        LegendBorder = legendBorder;
        Series = series ?? throw new ArgumentNullException(nameof(series));
    }

    public RgbaColor Background { get; }

    public RgbaColor Foreground { get; }

    public RgbaColor AxisLine { get; }

    public RgbaColor AxisTick { get; }

    public RgbaColor GridLine { get; }

    public RgbaColor LegendBackground { get; }

    public RgbaColor LegendBorder { get; }

    public IReadOnlyList<RgbaColor> Series { get; }
}
