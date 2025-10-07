using System;
using Avalonia.Threading;
using VelloSharp.ChartDiagnostics;

namespace VelloSharp.Charting.AvaloniaSample.Infrastructure;

internal sealed class SampleTelemetrySink : IChartTelemetrySink
{
    private readonly MainWindow _window;

    public SampleTelemetrySink(MainWindow window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public void Record(FrameStats stats)
    {
        Dispatcher.UIThread.Post(() => _window.UpdateFrameStats(stats));
    }

    public void Record(ChartMetric metric)
    {
        // Metrics can be forwarded to structured logging or analytics; the sample ignores them.
    }
}
