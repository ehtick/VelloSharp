using System;
using System.Collections.Generic;

namespace VelloSharp.ChartDiagnostics;

/// <summary>
/// Represents a single telemetry measurement emitted by the chart runtime.
/// </summary>
public readonly record struct ChartMetric(
    string Name,
    double Value,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, string>? Dimensions = null);
