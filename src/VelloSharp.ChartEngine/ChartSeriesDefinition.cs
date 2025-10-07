using System;
using System.Runtime.CompilerServices;

namespace VelloSharp.ChartEngine;

/// <summary>
/// Identifies the rendering strategy for a chart series.
/// </summary>
public enum ChartSeriesKind : uint
{
    Line = 0,
    Area = 1,
    Scatter = 2,
    Bar = 3,
    Band = 4,
    Heatmap = 5,
}

[Flags]
internal enum SeriesDefinitionFlags : uint
{
    None = 0,
    BaselineSet = 1 << 0,
    FillOpacitySet = 1 << 1,
    StrokeWidthSet = 1 << 2,
    MarkerSizeSet = 1 << 3,
    BarWidthSet = 1 << 4,
    BandLowerSet = 1 << 5,
    HeatmapBucketsSet = 1 << 6,
}

/// <summary>
/// Base descriptor for configuring how a series should be rendered by the engine.
/// </summary>
public abstract record ChartSeriesDefinition(uint SeriesId)
{
    private const double MinimumStrokeWidth = 0.01;

    /// <summary>
    /// Gets or sets an optional baseline value used by area and bar series.
    /// When omitted the engine chooses a baseline derived from the visible range.
    /// </summary>
    public double? Baseline { get; init; }

    /// <summary>
    /// Gets or sets an optional stroke width override in device-independent pixels.
    /// </summary>
    public double? StrokeWidth { get; init; }

    /// <summary>
    /// Gets or sets an optional fill opacity (0..1) used for filled series variants.
    /// </summary>
    public double? FillOpacity { get; init; }

    /// <summary>
    /// Gets or sets an optional marker size (diameter, in device-independent pixels) for scatter points.
    /// </summary>
    public double? MarkerSize { get; init; }

    /// <summary>
    /// Gets or sets the optional bar width expressed in seconds along the horizontal axis.
    /// </summary>
    public double? BarWidthSeconds { get; init; }

    internal abstract ChartSeriesKind Kind { get; }

    protected virtual void Validate()
    {
        if (Baseline is { } baseline && !double.IsFinite(baseline))
        {
            throw new ArgumentOutOfRangeException(nameof(Baseline), Baseline, "Baseline must be finite when provided.");
        }

        if (StrokeWidth is { } strokeWidth)
        {
            if (!double.IsFinite(strokeWidth) || strokeWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(StrokeWidth), StrokeWidth, "Stroke width must be positive and finite.");
            }
        }

        if (FillOpacity is { } fillOpacity)
        {
            if (!double.IsFinite(fillOpacity) || fillOpacity is < 0 or > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(FillOpacity), FillOpacity, "Fill opacity must be between 0 and 1.");
            }
        }

        if (MarkerSize is { } markerSize)
        {
            if (!double.IsFinite(markerSize) || markerSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MarkerSize), MarkerSize, "Marker size must be positive and finite.");
            }
        }

        if (BarWidthSeconds is { } barWidth)
        {
            if (!double.IsFinite(barWidth) || barWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(BarWidthSeconds), BarWidthSeconds, "Bar width must be positive and finite.");
            }
        }
    }

    internal VelloChartSeriesDefinition ToNative()
    {
        Validate();

        var definition = new VelloChartSeriesDefinition
        {
            SeriesId = unchecked((uint)SeriesId),
            Kind = (uint)Kind,
            Reserved = 0,
            Baseline = 0d,
            FillOpacity = 0d,
            StrokeWidth = 0d,
            MarkerSize = 0d,
            BarWidthSeconds = 0d,
            Flags = SeriesDefinitionFlags.None,
        };

        if (Baseline is { } baseline)
        {
            definition.Baseline = baseline;
            definition.Flags |= SeriesDefinitionFlags.BaselineSet;
        }

        if (FillOpacity is { } fillOpacity)
        {
            definition.FillOpacity = fillOpacity;
            definition.Flags |= SeriesDefinitionFlags.FillOpacitySet;
        }

        if (StrokeWidth is { } strokeWidth)
        {
            definition.StrokeWidth = Math.Max(strokeWidth, MinimumStrokeWidth);
            definition.Flags |= SeriesDefinitionFlags.StrokeWidthSet;
        }

        if (MarkerSize is { } markerSize)
        {
            definition.MarkerSize = markerSize;
            definition.Flags |= SeriesDefinitionFlags.MarkerSizeSet;
        }

        if (BarWidthSeconds is { } barWidthSeconds)
        {
            definition.BarWidthSeconds = barWidthSeconds;
            definition.Flags |= SeriesDefinitionFlags.BarWidthSet;
        }

        PopulateNative(ref definition);

        return definition;
    }

    internal virtual void PopulateNative(ref VelloChartSeriesDefinition definition)
    {
    }
}

/// <summary>
/// Configures a line series rendered with optional fill.
/// </summary>
public sealed record LineSeriesDefinition(uint SeriesId) : ChartSeriesDefinition(SeriesId)
{
    internal override ChartSeriesKind Kind => ChartSeriesKind.Line;
}

/// <summary>
/// Configures an area series rendered as a filled polygon.
/// </summary>
public sealed record AreaSeriesDefinition(uint SeriesId) : ChartSeriesDefinition(SeriesId)
{
    internal override ChartSeriesKind Kind => ChartSeriesKind.Area;
}

/// <summary>
/// Configures a scatter series rendered as markers.
/// </summary>
public sealed record ScatterSeriesDefinition(uint SeriesId) : ChartSeriesDefinition(SeriesId)
{
    internal override ChartSeriesKind Kind => ChartSeriesKind.Scatter;
}

/// <summary>
/// Configures a bar series rendered as vertical columns.
/// </summary>
public sealed record BarSeriesDefinition(uint SeriesId) : ChartSeriesDefinition(SeriesId)
{
    internal override ChartSeriesKind Kind => ChartSeriesKind.Bar;
}

public sealed record PolylineBandSeriesDefinition(uint SeriesId, uint LowerSeriesId) : ChartSeriesDefinition(SeriesId)
{
    public uint LowerSeriesId { get; init; } = LowerSeriesId;

    internal override ChartSeriesKind Kind => ChartSeriesKind.Band;

    internal override void PopulateNative(ref VelloChartSeriesDefinition definition)
    {
        if (LowerSeriesId == SeriesId)
        {
            throw new ArgumentOutOfRangeException(nameof(LowerSeriesId), LowerSeriesId, "Lower series id must differ from the band series.");
        }

        definition.BandLowerSeriesId = LowerSeriesId;
        definition.Flags |= SeriesDefinitionFlags.BandLowerSet;
    }
}

public sealed record HeatmapSeriesDefinition(uint SeriesId) : ChartSeriesDefinition(SeriesId)
{
    public uint BucketIndex { get; init; }

    public uint BucketCount { get; init; }

    internal override ChartSeriesKind Kind => ChartSeriesKind.Heatmap;

    internal override void PopulateNative(ref VelloChartSeriesDefinition definition)
    {
        if (BucketCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BucketCount), BucketCount, "Bucket count must be greater than zero.");
        }

        if (BucketIndex >= BucketCount)
        {
            throw new ArgumentOutOfRangeException(nameof(BucketIndex), BucketIndex, "Bucket index must be less than the bucket count.");
        }

        definition.HeatmapBucketIndex = BucketIndex;
        definition.HeatmapBucketCount = BucketCount;
        definition.Flags |= SeriesDefinitionFlags.HeatmapBucketsSet;
    }
}
