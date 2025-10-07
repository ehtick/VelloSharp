using System;

namespace VelloSharp.ChartEngine;

[Flags]
internal enum SeriesOverrideFlags : uint
{
    None = 0,
    LabelSet = 1 << 0,
    LabelClear = 1 << 1,
    StrokeWidthSet = 1 << 2,
    StrokeWidthClear = 1 << 3,
    ColorSet = 1 << 4,
    ColorClear = 1 << 5,
}

/// <summary>
/// Describes a batchable style override for a chart series.
/// </summary>
public readonly record struct ChartSeriesOverride
{
    internal ChartSeriesOverride(int seriesId, SeriesOverrideFlags flags, string? label, double strokeWidth, ChartColor color)
    {
        SeriesId = seriesId;
        Flags = flags;
        Label = label;
        StrokeWidth = strokeWidth;
        Color = color;
    }

    public ChartSeriesOverride(int seriesId)
        : this(seriesId, SeriesOverrideFlags.None, null, 0d, default)
    {
    }

    public int SeriesId { get; init; }

    internal SeriesOverrideFlags Flags { get; init; }

    internal string? Label { get; init; }

    internal double StrokeWidth { get; init; }

    internal ChartColor Color { get; init; }

    internal bool IsEmpty => Flags == SeriesOverrideFlags.None;

    public ChartSeriesOverride WithLabel(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        return this with
        {
            Flags = (Flags | SeriesOverrideFlags.LabelSet) & ~SeriesOverrideFlags.LabelClear,
            Label = label,
        };
    }

    public ChartSeriesOverride ClearLabel()
    {
        return this with
        {
            Flags = (Flags | SeriesOverrideFlags.LabelClear) & ~SeriesOverrideFlags.LabelSet,
            Label = null,
        };
    }

    public ChartSeriesOverride WithStrokeWidth(double strokeWidth)
    {
        if (!double.IsFinite(strokeWidth) || strokeWidth <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(strokeWidth), "Stroke width must be positive and finite.");
        }

        return this with
        {
            Flags = (Flags | SeriesOverrideFlags.StrokeWidthSet) & ~SeriesOverrideFlags.StrokeWidthClear,
            StrokeWidth = strokeWidth,
        };
    }

    public ChartSeriesOverride ClearStrokeWidth()
    {
        return this with
        {
            Flags = (Flags | SeriesOverrideFlags.StrokeWidthClear) & ~SeriesOverrideFlags.StrokeWidthSet,
            StrokeWidth = 0.0,
        };
    }

    public ChartSeriesOverride WithColor(ChartColor color)
    {
        return this with
        {
            Flags = (Flags | SeriesOverrideFlags.ColorSet) & ~SeriesOverrideFlags.ColorClear,
            Color = color,
        };
    }

    public ChartSeriesOverride ClearColor()
    {
        return this with
        {
            Flags = (Flags | SeriesOverrideFlags.ColorClear) & ~SeriesOverrideFlags.ColorSet,
            Color = default,
        };
    }

    internal void Validate()
    {
        if ((Flags & SeriesOverrideFlags.LabelSet) != 0 && (Flags & SeriesOverrideFlags.LabelClear) != 0)
        {
            throw new InvalidOperationException("Cannot set and clear the label in the same override.");
        }

        if ((Flags & SeriesOverrideFlags.StrokeWidthSet) != 0 && (Flags & SeriesOverrideFlags.StrokeWidthClear) != 0)
        {
            throw new InvalidOperationException("Cannot set and clear the stroke width in the same override.");
        }

        if ((Flags & SeriesOverrideFlags.StrokeWidthSet) != 0
            && (!double.IsFinite(StrokeWidth) || StrokeWidth <= 0.0))
        {
            throw new InvalidOperationException("Stroke width overrides must provide a positive, finite value.");
        }

        if ((Flags & SeriesOverrideFlags.ColorSet) != 0 && (Flags & SeriesOverrideFlags.ColorClear) != 0)
        {
            throw new InvalidOperationException("Cannot set and clear the color in the same override.");
        }
    }
}
