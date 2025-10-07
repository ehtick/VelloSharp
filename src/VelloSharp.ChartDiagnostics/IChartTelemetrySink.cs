using System;

namespace VelloSharp.ChartDiagnostics;

/// <summary>
/// Contract used by host applications to receive telemetry emitted by the chart runtime.
/// </summary>
public interface IChartTelemetrySink
{
    void Record(FrameStats stats);
    void Record(ChartMetric metric);
}
