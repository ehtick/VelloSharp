using System;

namespace VelloSharp.Charting.Styling;

/// <summary>
/// Encapsulates styling tokens for legends.
/// </summary>
public sealed class LegendStyle
{
    public LegendStyle(
        RgbaColor background,
        RgbaColor border,
        double borderThickness,
        ChartTypography labelTypography,
        double markerSize = 10d,
        double itemSpacing = 8d,
        double padding = 12d,
        double labelSpacing = 6d)
    {
        if (!double.IsFinite(borderThickness) || borderThickness < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(borderThickness), borderThickness, "Border thickness must be non-negative and finite.");
        }

        if (!double.IsFinite(markerSize) || markerSize < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(markerSize), markerSize, "Marker size must be non-negative and finite.");
        }

        if (!double.IsFinite(itemSpacing) || itemSpacing < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(itemSpacing), itemSpacing, "Item spacing must be non-negative and finite.");
        }

        if (!double.IsFinite(padding) || padding < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(padding), padding, "Padding must be non-negative and finite.");
        }

        if (!double.IsFinite(labelSpacing) || labelSpacing < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(labelSpacing), labelSpacing, "Label spacing must be non-negative and finite.");
        }

        Background = background;
        Border = border;
        BorderThickness = borderThickness;
        LabelTypography = labelTypography ?? throw new ArgumentNullException(nameof(labelTypography));
        MarkerSize = markerSize;
        ItemSpacing = itemSpacing;
        Padding = padding;
        LabelSpacing = labelSpacing;
    }

    public RgbaColor Background { get; }

    public RgbaColor Border { get; }

    public double BorderThickness { get; }

    public ChartTypography LabelTypography { get; }

    public double MarkerSize { get; }

    public double ItemSpacing { get; }

    public double Padding { get; }

    public double LabelSpacing { get; }
}
