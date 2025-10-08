using System;

namespace VelloSharp.ChartEngine;

/// <summary>
/// Describes a cursor transition request for the chart animation controller.
/// </summary>
public readonly record struct ChartCursorUpdate(
    double TimestampSeconds,
    double Value,
    bool IsVisible,
    TimeSpan? PositionDuration = null,
    TimeSpan? FadeDuration = null);
