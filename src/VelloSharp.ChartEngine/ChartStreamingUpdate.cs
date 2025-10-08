namespace VelloSharp.ChartEngine;

/// <summary>
/// Describes a streaming motion event to animate via the shared timeline runtime.
/// </summary>
public readonly record struct ChartStreamingUpdate(
    uint SeriesId,
    ChartStreamingEventKind Kind,
    double ShiftSeconds = 0.0);

/// <summary>
/// Enumerates streaming animation event kinds.
/// </summary>
public enum ChartStreamingEventKind
{
    FadeIn,
    SlideIn,
    RollingWindowShift,
}
