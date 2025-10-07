using System.Runtime.InteropServices;

namespace VelloSharp.ChartData;

/// <summary>
/// Represents a single time-series data point emitted for a specific chart series.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct ChartSamplePoint
{
    public readonly int SeriesId;
    public readonly double TimestampSeconds;
    public readonly double Value;

    public ChartSamplePoint(int seriesId, double timestampSeconds, double value)
    {
        SeriesId = seriesId;
        TimestampSeconds = timestampSeconds;
        Value = value;
    }
}
