using System;
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

    [Fact]
    public void Composition_WithMultiplePanes_ProducesPaneMetadata()
    {
        _engine.ConfigureSeries(new ChartSeriesDefinition[]
        {
            new LineSeriesDefinition(0),
            new BarSeriesDefinition(1),
        });

        var composition = ChartComposition.Create(builder =>
        {
            builder
                .Pane("price")
                .WithSeries(0)
                .WithHeightRatio(3)
                .Done();

            builder
                .Pane("volume")
                .WithSeries(1)
                .WithHeightRatio(1)
                .Done();
        });

        _engine.ConfigureComposition(composition);

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        _engine.PumpData(new[]
        {
            new ChartSamplePoint(0, now - 10, 100.0),
            new ChartSamplePoint(0, now, 102.0),
            new ChartSamplePoint(1, now - 5, 20.0),
            new ChartSamplePoint(1, now, 24.0),
        });

        using var scene = new VelloSharp.Scene();
        _engine.Render(scene, 800, 600);

        var metadata = _engine.GetFrameMetadata();
        Assert.Equal(2, metadata.Panes.Count);
        Assert.Equal("price", metadata.Panes[0].Id);
        Assert.Equal("volume", metadata.Panes[1].Id);
        Assert.True(metadata.Panes[0].PlotHeight > metadata.Panes[1].PlotHeight);
        Assert.Contains(metadata.Series, s => s.SeriesId == 0 && s.PaneIndex == 0);
        Assert.Contains(metadata.Series, s => s.SeriesId == 1 && s.PaneIndex == 1);
    }

    [Fact]
    public void BandSeries_ProducesBandMetadata()
    {
        _engine.ConfigureSeries(new ChartSeriesDefinition[]
        {
            new LineSeriesDefinition(10),
            new PolylineBandSeriesDefinition(11, 10)
            {
                FillOpacity = 0.4,
            },
        });

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        _engine.PumpData(new[]
        {
            new ChartSamplePoint(10, now - 6, 98.0),
            new ChartSamplePoint(10, now, 99.5),
            new ChartSamplePoint(11, now - 6, 101.0),
            new ChartSamplePoint(11, now, 102.3),
        });

        using var scene = new VelloSharp.Scene();
        _engine.Render(scene, 640, 360);

        var metadata = _engine.GetFrameMetadata();
        Assert.DoesNotContain(metadata.Series, s => s.SeriesId == 10);
        var band = Assert.Single(metadata.Series, s => s.SeriesId == 11);
        Assert.Equal(ChartSeriesKind.Band, band.Kind);
        Assert.Equal<uint>(10, band.BandLowerSeriesId);
        Assert.True(band.FillOpacity > 0);
    }

    [Fact]
    public void HeatmapSeries_ReportsBucketMetadata()
    {
        _engine.ConfigureSeries(new ChartSeriesDefinition[]
        {
            new HeatmapSeriesDefinition(20)
            {
                BucketIndex = 2,
                BucketCount = 4,
            },
        });

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        _engine.PumpData(new[]
        {
            new ChartSamplePoint(20, now - 6, 0.2),
            new ChartSamplePoint(20, now, 0.8),
            new ChartSamplePoint(20, now + 6, 0.5),
        });

        using var scene = new VelloSharp.Scene();
        _engine.Render(scene, 400, 240);

        var metadata = _engine.GetFrameMetadata();
        var heatmap = Assert.Single(metadata.Series);
        Assert.Equal(ChartSeriesKind.Heatmap, heatmap.Kind);
        Assert.Equal<uint>(2, heatmap.HeatmapBucketIndex);
        Assert.Equal<uint>(4, heatmap.HeatmapBucketCount);
        Assert.True(heatmap.FillOpacity > 0);
    }

    [Fact]
    public void BackfillUpdatesDirtyBounds()
    {
        using var engine = new ChartEngine.ChartEngine(new ChartEngineOptions
        {
            VisibleDuration = TimeSpan.FromSeconds(30),
            ShowAxes = false,
        });

        engine.ConfigureSeries(new ChartSeriesDefinition[]
        {
            new LineSeriesDefinition(0),
        });

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        engine.PumpData(new[]
        {
            new ChartSamplePoint(0, now - 2, 100.0),
            new ChartSamplePoint(0, now - 1, 101.0),
        });

        using var scene = new VelloSharp.Scene();
        engine.Render(scene, 640, 360);

        engine.PumpData(new[]
        {
            new ChartSamplePoint(0, now - 1, 107.5),
        });

        engine.Render(scene, 640, 360);
        var metadata = engine.GetFrameMetadata();
        var pane = Assert.Single(metadata.Panes);

        Assert.Null(pane.DirtyTimeMin);
        Assert.Null(pane.DirtyTimeMax);
        Assert.Null(pane.DirtyValueMin);
        Assert.Null(pane.DirtyValueMax);
        Assert.True(pane.ValueMax >= 107.5);
    }

    [Fact]
    public void RenderWithoutChangesReusesPreviousFrame()
    {
        using var engine = new ChartEngine.ChartEngine(new ChartEngineOptions
        {
            VisibleDuration = TimeSpan.FromSeconds(10),
            ShowAxes = false,
        });

        engine.ConfigureSeries(new ChartSeriesDefinition[]
        {
            new LineSeriesDefinition(0),
        });

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        engine.PumpData(new[]
        {
            new ChartSamplePoint(0, now - 1, 50.0),
            new ChartSamplePoint(0, now, 52.0),
        });

        using var scene = new VelloSharp.Scene();
        engine.Render(scene, 800, 400);
        var firstStats = engine.LastFrameStats;

        engine.Render(scene, 800, 400);
        var secondStats = engine.LastFrameStats;

        Assert.Equal(firstStats.Timestamp, secondStats.Timestamp);
        Assert.Equal(firstStats.EncodedPaths, secondStats.EncodedPaths);
    }    public void Dispose()
    {
        _engine.Dispose();
    }
}

