using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace VelloSharp.ChartDiagnostics;

/// <summary>
/// Aggregates frame statistics and exposes them through .NET diagnostics primitives.
/// </summary>
public sealed class FrameDiagnosticsCollector : IDisposable
{
    private readonly Meter _meter = new("VelloSharp.ChartDiagnostics");
    private readonly Histogram<double> _cpuHistogram;
    private readonly Histogram<double> _gpuHistogram;
    private readonly ConcurrentQueue<FrameStats> _recent = new();
    private readonly ActivitySource _activitySource = new("VelloSharp.ChartDiagnostics");
    private IChartTelemetrySink? _telemetrySink;
    private bool _disposed;

    public FrameDiagnosticsCollector(IChartTelemetrySink? telemetrySink = null)
    {
        _cpuHistogram = _meter.CreateHistogram<double>("chart.cpu.frame.ms");
        _gpuHistogram = _meter.CreateHistogram<double>("chart.gpu.frame.ms");
        _telemetrySink = telemetrySink;
    }

    public void Record(FrameStats stats)
    {
        if (_disposed)
        {
            return;
        }

        _cpuHistogram.Record(stats.CpuTime.TotalMilliseconds);
        _gpuHistogram.Record(stats.GpuTime.TotalMilliseconds);
        _recent.Enqueue(stats);
        _telemetrySink?.Record(stats);

        while (_recent.Count > 120 && _recent.TryDequeue(out _))
        {
        }

        using var activity = _activitySource.StartActivity("chart.frame");
        activity?.SetTag("chart.cpu.frame.ms", stats.CpuTime.TotalMilliseconds);
        activity?.SetTag("chart.gpu.frame.ms", stats.GpuTime.TotalMilliseconds);
        activity?.SetTag("chart.queue.latency.ms", stats.QueueLatency.TotalMilliseconds);
        activity?.SetTag("chart.paths", stats.EncodedPaths);
    }

    public bool TryGetRecent(out FrameStats stats) => _recent.TryPeek(out stats);

    public void SetTelemetrySink(IChartTelemetrySink? telemetrySink)
    {
        _telemetrySink = telemetrySink;
    }

    public void RecordMetric(ChartMetric metric)
    {
        if (_disposed)
        {
            return;
        }

        _telemetrySink?.Record(metric);
    }

    public void Dispose()
    {
        _disposed = true;
        _meter.Dispose();
        _activitySource.Dispose();
    }
}
