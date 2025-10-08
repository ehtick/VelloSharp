using System.Diagnostics;

namespace VelloSharp.ChartDiagnostics;

/// <summary>
/// Dispatches chart telemetry to <see cref="DiagnosticListener"/> consumers for dashboards and tooling.
/// </summary>
public sealed class DashboardTelemetrySink : IChartTelemetrySink
{
    private const string ListenerName = "VelloSharp.ChartDiagnostics.Dashboard";
    private const string FrameEvent = "chart.frame";
    private const string MetricEvent = "chart.metric";

    private readonly DiagnosticListener _listener = new(ListenerName);

    private DashboardTelemetrySink()
    {
    }

    /// <summary>
    /// Provides a shared singleton instance suitable for application-level wiring.
    /// </summary>
    public static DashboardTelemetrySink Instance { get; } = new();

    public void Record(FrameStats stats)
    {
        if (!_listener.IsEnabled(FrameEvent))
        {
            return;
        }

        _listener.Write(FrameEvent, new { stats });
    }

    public void Record(ChartMetric metric)
    {
        if (!_listener.IsEnabled(MetricEvent))
        {
            return;
        }

        _listener.Write(MetricEvent, new { metric });
    }
}

