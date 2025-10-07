using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VelloSharp.ChartEngine;

/// <summary>
/// Captures metadata from the most recent chart frame for use by overlay renderers.
/// </summary>
public sealed class ChartFrameMetadata
{
    private ChartFrameMetadata(
        double rangeStartSeconds,
        double rangeEndSeconds,
        double valueMin,
        double valueMax,
        double plotLeft,
        double plotTop,
        double plotWidth,
        double plotHeight,
        AxisTickMetadata[] timeTicks,
        AxisTickMetadata[] valueTicks,
        SeriesMetadata[] series)
    {
        RangeStartSeconds = rangeStartSeconds;
        RangeEndSeconds = rangeEndSeconds;
        ValueMin = valueMin;
        ValueMax = valueMax;
        PlotLeft = plotLeft;
        PlotTop = plotTop;
        PlotWidth = plotWidth;
        PlotHeight = plotHeight;
        TimeTicks = timeTicks;
        ValueTicks = valueTicks;
        Series = series;
    }

    /// <summary>
    /// Gets the visible time range start (seconds since Unix epoch).
    /// </summary>
    public double RangeStartSeconds { get; }

    /// <summary>
    /// Gets the visible time range end (seconds since Unix epoch).
    /// </summary>
    public double RangeEndSeconds { get; }

    /// <summary>
    /// Gets the minimum data value rendered in the last frame.
    /// </summary>
    public double ValueMin { get; }

    /// <summary>
    /// Gets the maximum data value rendered in the last frame.
    /// </summary>
    public double ValueMax { get; }

    /// <summary>
    /// Gets the left offset of the plot area in device independent pixels.
    /// </summary>
    public double PlotLeft { get; }

    /// <summary>
    /// Gets the top offset of the plot area in device independent pixels.
    /// </summary>
    public double PlotTop { get; }

    /// <summary>
    /// Gets the plot area width in device independent pixels.
    /// </summary>
    public double PlotWidth { get; }

    /// <summary>
    /// Gets the plot area height in device independent pixels.
    /// </summary>
    public double PlotHeight { get; }

    /// <summary>
    /// Gets tick metadata for the horizontal axis.
    /// </summary>
    public IReadOnlyList<AxisTickMetadata> TimeTicks { get; }

    /// <summary>
    /// Gets tick metadata for the vertical axis.
    /// </summary>
    public IReadOnlyList<AxisTickMetadata> ValueTicks { get; }

    /// <summary>
    /// Gets series metadata for legend and styling.
    /// </summary>
    public IReadOnlyList<SeriesMetadata> Series { get; }

    internal static unsafe ChartFrameMetadata FromNative(in VelloChartFrameMetadata native)
    {
        var timeTicks = new AxisTickMetadata[(int)native.TimeTickCount];
        if (native.TimeTickCount > 0 && native.TimeTicks != IntPtr.Zero)
        {
            var tickPtr = (VelloChartAxisTickMetadata*)native.TimeTicks;
            for (var i = 0; i < timeTicks.Length; i++)
            {
                var tick = tickPtr[i];
                var label = Marshal.PtrToStringUTF8(tick.Label, (int)tick.LabelLength) ?? string.Empty;
                timeTicks[i] = new AxisTickMetadata(tick.Position, label);
            }
        }

        var valueTicks = new AxisTickMetadata[(int)native.ValueTickCount];
        if (native.ValueTickCount > 0 && native.ValueTicks != IntPtr.Zero)
        {
            var tickPtr = (VelloChartAxisTickMetadata*)native.ValueTicks;
            for (var i = 0; i < valueTicks.Length; i++)
            {
                var tick = tickPtr[i];
                var label = Marshal.PtrToStringUTF8(tick.Label, (int)tick.LabelLength) ?? string.Empty;
                valueTicks[i] = new AxisTickMetadata(tick.Position, label);
            }
        }

        var series = new SeriesMetadata[(int)native.SeriesCount];
        if (native.SeriesCount > 0 && native.Series != IntPtr.Zero)
        {
            var seriesPtr = (VelloChartSeriesMetadataNative*)native.Series;
            for (var i = 0; i < series.Length; i++)
            {
                var item = seriesPtr[i];
                var label = Marshal.PtrToStringUTF8(item.Label, (int)item.LabelLength) ?? string.Empty;
                var color = new ChartColor(item.Color.R, item.Color.G, item.Color.B, item.Color.A);
                var kind = Enum.IsDefined(typeof(ChartSeriesKind), (uint)item.Kind)
                    ? (ChartSeriesKind)item.Kind
                    : ChartSeriesKind.Line;
                series[i] = new SeriesMetadata(
                    item.SeriesId,
                    color,
                    item.StrokeWidth,
                    label,
                    kind,
                    item.FillOpacity,
                    item.MarkerSize,
                    item.BarWidthSeconds,
                    item.Baseline);
            }
        }

        return new ChartFrameMetadata(
            native.RangeStart,
            native.RangeEnd,
            native.ValueMin,
            native.ValueMax,
            native.PlotLeft,
            native.PlotTop,
            native.PlotWidth,
            native.PlotHeight,
            timeTicks,
            valueTicks,
            series);
    }

    public readonly record struct AxisTickMetadata(double Position, string Label);

    public readonly record struct SeriesMetadata(
        uint SeriesId,
        ChartColor Color,
        double StrokeWidth,
        string Label,
        ChartSeriesKind Kind,
        double FillOpacity,
        double MarkerSize,
        double BarWidthSeconds,
        double Baseline);
}
