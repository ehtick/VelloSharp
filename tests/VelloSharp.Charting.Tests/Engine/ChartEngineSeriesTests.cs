using System;
using System.Linq;
using VelloSharp.ChartData;
using VelloSharp.ChartEngine;
using Xunit;

namespace VelloSharp.Charting.Tests.Engine;

public sealed class ChartEngineSeriesTests : IDisposable
{
    private readonly ChartEngine.ChartEngine _engine;

    public ChartEngineSeriesTests()
    {
        _engine = new ChartEngine.ChartEngine(new ChartEngineOptions
        {
            VisibleDuration = TimeSpan.FromSeconds(30),
            ShowAxes = false,
        });
    }

    [Fact]
    public void ConfigureSeries_UpdatesMetadataKind()
    {
        _engine.ConfigureSeries(new ChartSeriesDefinition[]
        {
            new ScatterSeriesDefinition(2)
            {
                MarkerSize = 5.0,
            },
        });

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        var samples = new[]
        {
            new ChartSamplePoint(2, timestamp, 42.0),
        };

        _engine.PumpData(samples);
        using var scene = new VelloSharp.Scene();
        _engine.Render(scene, 640, 360);

        var metadata = _engine.GetFrameMetadata();
        Assert.Contains(metadata.Series, s => s.SeriesId == 2 && s.Kind == ChartSeriesKind.Scatter);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}
