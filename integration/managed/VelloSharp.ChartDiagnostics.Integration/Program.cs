using System;
using VelloSharp.ChartDiagnostics;

Console.WriteLine("Verifying VelloSharp.ChartDiagnostics package usage…");

using var collector = new FrameDiagnosticsCollector();
collector.Record(new FrameStats(
    CpuTime: TimeSpan.FromMilliseconds(4.2),
    GpuTime: TimeSpan.FromMilliseconds(3.6),
    QueueLatency: TimeSpan.FromMilliseconds(1.1),
    EncodedPaths: 128,
    Timestamp: DateTimeOffset.UtcNow));

if (collector.TryGetRecent(out var recent))
{
    Console.WriteLine($"Last frame CPU {recent.CpuTime.TotalMilliseconds:F2} ms.");
}

collector.RecordMetric(new ChartMetric("engine.frames", 1d, DateTimeOffset.UtcNow));

Console.WriteLine("VelloSharp.ChartDiagnostics integration test completed.");

