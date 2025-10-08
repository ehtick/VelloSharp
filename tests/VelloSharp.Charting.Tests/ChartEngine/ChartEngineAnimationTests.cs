using System;
#nullable enable

using System.Linq;
using VelloSharp.ChartData;
using VelloSharp.ChartEngine;
using VelloSharp.Composition;
using Xunit;
using ManagedChartEngine = VelloSharp.ChartEngine.ChartEngine;

namespace VelloSharp.Charting.Tests.Engine;

public sealed class ChartEngineAnimationTests
{
    [Fact]
    public void AnimateSeriesStrokeWidth_TicksWithoutError()
    {
        using var engine = CreateEngine();

        engine.AnimateSeriesStrokeWidth(1, 2.5, TimeSpan.FromMilliseconds(200));
        engine.ResetSeriesStrokeWidth(1, TimeSpan.FromMilliseconds(200));

        // Pump at least one tick to exercise the timeline loop.
        engine.TryAdvanceFrame(TimeSpan.FromMilliseconds(16));
    }

    [Fact]
    public void AnimateCursor_ProjectsOverlayInMetadata()
    {
        var immediateProfile = ChartAnimationProfile.Default with
        {
            CursorTrail = new ChartAnimationTimeline(TimeSpan.Zero, TimelineEasing.Linear),
            CrosshairFade = new ChartAnimationTimeline(TimeSpan.Zero, TimelineEasing.Linear),
        };

        using var engine = CreateEngine(immediateProfile);

        const double timestamp = 42.5;
        engine.AnimateCursor(new ChartCursorUpdate(timestamp, 105.0, true));

        RenderFrame(engine);
        var overlay = engine.GetFrameMetadata().CursorOverlay;
        Assert.True(overlay.HasValue);
        Assert.Equal(timestamp, overlay.Value.TimestampSeconds, precision: 3);
        Assert.Equal(105.0, overlay.Value.Value, precision: 3);
        Assert.Equal(1f, overlay.Value.Opacity, precision: 2);

        engine.AnimateCursor(new ChartCursorUpdate(timestamp, 105.0, false));
        RenderFrame(engine);

        overlay = engine.GetFrameMetadata().CursorOverlay;
        Assert.True(!overlay.HasValue || overlay.Value.Opacity <= 0.05f);
    }

    [Fact]
    public void AnimateAnnotation_SynchronizesEmphasisWithTimeline()
    {
        var immediateProfile = ChartAnimationProfile.Default with
        {
            IndicatorOverlay = new ChartAnimationTimeline(TimeSpan.Zero, TimelineEasing.Linear),
        };

        using var engine = CreateEngine(immediateProfile);

        engine.AnimateAnnotation("alert", highlighted: true);
        RenderFrame(engine);

        var overlays = engine.GetFrameMetadata().AnnotationOverlays;
        Assert.Contains(overlays, overlay => overlay.AnnotationId == "alert" && overlay.Emphasis >= 0.99f);

        engine.AnimateAnnotation("alert", highlighted: false);
        RenderFrame(engine);

        overlays = engine.GetFrameMetadata().AnnotationOverlays;
        Assert.True(overlays.Count == 0 || overlays.All(overlay => overlay.Emphasis <= 0.05f));
    }

    [Fact]
    public void ReducedMotion_DisablesAnimatedCursor()
    {
        var reducedProfile = ChartAnimationProfile.Default with { ReducedMotionEnabled = true };
        using var engine = CreateEngine(reducedProfile);

        const double timestamp = 84.25;
        engine.AnimateCursor(new ChartCursorUpdate(timestamp, 95.0, true));

        RenderFrame(engine);

        var overlay = engine.GetFrameMetadata().CursorOverlay;
        Assert.True(overlay.HasValue);
        Assert.Equal(timestamp, overlay.Value.TimestampSeconds, precision: 3);
        Assert.Equal(95.0, overlay.Value.Value, precision: 3);
    }

    private static ManagedChartEngine CreateEngine(ChartAnimationProfile? profile = null)
    {
        var options = new ChartEngineOptions
        {
            AutomaticTicksEnabled = false,
            TimeProvider = TimeProvider.System,
            VisibleDuration = TimeSpan.FromSeconds(30),
            ShowAxes = false,
            Animations = profile ?? ChartAnimationProfile.Default,
        };

        var engine = new ManagedChartEngine(options);
        engine.ConfigureSeries(new ChartSeriesDefinition[]
        {
            new LineSeriesDefinition(0),
            new LineSeriesDefinition(1) { StrokeWidth = 1.5 },
        });

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        engine.PumpData(new[]
        {
            new ChartSamplePoint(0, now - 1, 100.0),
            new ChartSamplePoint(0, now, 101.0),
            new ChartSamplePoint(1, now - 1, 200.0),
            new ChartSamplePoint(1, now, 203.0),
        });

        RenderFrame(engine);
        return engine;
    }

    private static void RenderFrame(ManagedChartEngine engine)
    {
        using var scene = new VelloSharp.Scene();
        engine.Render(scene, 640, 360);
    }
}
