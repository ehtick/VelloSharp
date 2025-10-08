using System;
using VelloSharp.ChartEngine;
using Xunit;
using ManagedChartEngine = VelloSharp.ChartEngine.ChartEngine;

namespace VelloSharp.Charting.Tests.Engine;

public sealed class ChartEngineAnimationTests
{
    [Fact]
    public void AnimateSeriesStrokeWidth_TicksWithoutError()
    {
        var options = new ChartEngineOptions
        {
            AutomaticTicksEnabled = false,
            TimeProvider = TimeProvider.System,
        };

        using var engine = new ManagedChartEngine(options);
        engine.ConfigureSeries(new ChartSeriesDefinition[]
        {
            new LineSeriesDefinition(1) { StrokeWidth = 1.5 },
        });

        engine.AnimateSeriesStrokeWidth(1, 2.5, TimeSpan.FromMilliseconds(200));
        engine.ResetSeriesStrokeWidth(1, TimeSpan.FromMilliseconds(200));

        // Pump at least one tick to exercise the timeline loop.
        engine.TryAdvanceFrame(TimeSpan.FromMilliseconds(16));
    }
}
