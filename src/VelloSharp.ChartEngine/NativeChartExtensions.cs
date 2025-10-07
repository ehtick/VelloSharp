using System;
using VelloSharp.ChartDiagnostics;

namespace VelloSharp.ChartEngine;

internal static class NativeChartExtensions
{
    public static FrameStats ToManaged(this VelloChartFrameStats stats)
    {
        var cpu = TimeSpan.FromMilliseconds(stats.CpuTimeMs);
        var gpu = TimeSpan.FromMilliseconds(stats.GpuTimeMs);
        var queue = TimeSpan.FromMilliseconds(stats.QueueLatencyMs);
        var encoded = stats.EncodedPaths > int.MaxValue ? int.MaxValue : (int)stats.EncodedPaths;
        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(Math.Max(0, stats.TimestampMs));
        return new FrameStats(cpu, gpu, queue, encoded, timestamp);
    }
}
