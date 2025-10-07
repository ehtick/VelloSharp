using System;

namespace VelloSharp.ChartDiagnostics;

/// <summary>
/// Captures per-frame latency information.
/// </summary>
public readonly record struct FrameStats(
    TimeSpan CpuTime,
    TimeSpan GpuTime,
    TimeSpan QueueLatency,
    int EncodedPaths,
    DateTimeOffset Timestamp);
