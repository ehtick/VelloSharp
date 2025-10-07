using System;
using System.Collections.Generic;
using VelloSharp.ChartDiagnostics;
using VelloSharp.ChartRuntime;

namespace VelloSharp.ChartEngine;

/// <summary>
/// Configuration container for <see cref="ChartEngine"/>.
/// </summary>
public sealed class ChartEngineOptions
{
    public TimeSpan FrameBudget { get; init; } = TimeSpan.FromMilliseconds(8);

    /// <summary>
    /// Maintained for backward compatibility; native ingestion manages its own double-buffered queue.
    /// </summary>
    public int BufferCapacity { get; init; } = 1 << 20;

    public TimeProvider? TimeProvider { get; init; }

    public TimeSpan VisibleDuration { get; init; } = TimeSpan.FromMinutes(2);

    public double VerticalPaddingRatio { get; init; } = 0.08;

    public double StrokeWidth { get; init; } = 1.5;

    public bool ShowAxes { get; init; } = true;

    public IReadOnlyList<ChartColor>? Palette { get; init; }

    /// <summary>
    /// When provided, the scheduler uses this external tick source instead of the default background driver.
    /// </summary>
    public IFrameTickSource? TickSource { get; init; }

    /// <summary>
    /// Indicates whether the engine should dispose the supplied <see cref="TickSource"/> when no longer used.
    /// </summary>
    public bool OwnsTickSource { get; init; }

    /// <summary>
    /// Controls whether the scheduler emits ticks automatically when no external source is attached.
    /// </summary>
    public bool AutomaticTicksEnabled { get; init; } = true;

    /// <summary>
    /// Optional telemetry sink used to forward frame statistics and custom metrics.
    /// </summary>
    public IChartTelemetrySink? TelemetrySink { get; init; }
}
