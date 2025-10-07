using System;

namespace VelloSharp.Charting.Styling;

/// <summary>
/// Encapsulates visual styling tokens for axes.
/// </summary>
public sealed class AxisStyle
{
    public AxisStyle(
        RgbaColor lineColor,
        RgbaColor tickColor,
        double tickLength,
        ChartTypography labelTypography,
        double labelMargin = 4d)
    {
        if (!double.IsFinite(tickLength) || tickLength < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(tickLength), tickLength, "Tick length must be non-negative and finite.");
        }

        if (!double.IsFinite(labelMargin) || labelMargin < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(labelMargin), labelMargin, "Label margin must be non-negative and finite.");
        }

        LineColor = lineColor;
        TickColor = tickColor;
        TickLength = tickLength;
        LabelTypography = labelTypography ?? throw new ArgumentNullException(nameof(labelTypography));
        LabelMargin = labelMargin;
    }

    public RgbaColor LineColor { get; }

    public RgbaColor TickColor { get; }

    public double TickLength { get; }

    public ChartTypography LabelTypography { get; }

    public double LabelMargin { get; }

    public static AxisStyle Default { get; } = new(
        lineColor: RgbaColor.FromHex(0x8FA2C3FF),
        tickColor: RgbaColor.FromHex(0x8FA2C3FF),
        tickLength: 6d,
        labelTypography: ChartTypography.Default);

    public static AxisStyle FromPalette(ChartPalette palette, ChartTypography typography, double tickLength = 6d, double labelMargin = 4d)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(typography);
        return new AxisStyle(palette.AxisLine, palette.AxisTick, tickLength, typography, labelMargin);
    }
}
