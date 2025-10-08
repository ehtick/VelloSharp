using System;
using System.Linq;
using VelloSharp.ChartEngine;
using VelloSharp.ChartRuntime;
using Xunit;

namespace VelloSharp.Charting.Tests;

public sealed class ChartAnimationControllerTests : IDisposable
{
    private readonly RenderScheduler _scheduler;
    private readonly ChartAnimationController _controller;

    public ChartAnimationControllerTests()
    {
        _scheduler = new RenderScheduler(TimeSpan.FromMilliseconds(16), TimeProvider.System);
        _scheduler.SetAutomaticTicksEnabled(false);
        _controller = new ChartAnimationController(engine: null!, _scheduler, 1.5, ChartAnimationProfile.Default);
    }

    [Fact]
    public void AnimateCursor_UpdatesOverlaySnapshot()
    {
        _controller.AnimateCursor(new ChartCursorUpdate(10.0, 25.0, true));
        SimulateTime(milliseconds: 160);

        var overlay = _controller.GetCursorOverlaySnapshot();
        Assert.True(overlay.HasValue);
        Assert.InRange(overlay.Value.TimestampSeconds, 9.5, 10.5);
        Assert.InRange(overlay.Value.Value, 24.5, 25.5);
        Assert.InRange(overlay.Value.Opacity, 0.4f, 1f);

        _controller.AnimateCursor(new ChartCursorUpdate(10.0, 25.0, false));
        SimulateTime(milliseconds: 200);

        var faded = _controller.GetCursorOverlaySnapshot();
        Assert.True(!faded.HasValue || faded.Value.Opacity <= 0.1f);
    }

    [Fact]
    public void AnimateAnnotation_ProducesEmphasisSnapshots()
    {
        _controller.AnimateAnnotation("highlight", highlighted: true);
        SimulateTime(milliseconds: 180);

        var overlays = _controller.GetAnnotationSnapshots();
        Assert.Contains(overlays, overlay => overlay.AnnotationId == "highlight" && overlay.Emphasis > 0.1f);

        _controller.AnimateAnnotation("highlight", highlighted: false);
        SimulateTime(milliseconds: 240);

        overlays = _controller.GetAnnotationSnapshots();
        Assert.True(!overlays.Any() || overlays.All(overlay => overlay.Emphasis <= 0.1f));
    }

    [Fact]
    public void AnimateStreaming_FadeProducesOverlaySnapshots()
    {
        var updates = new[] { new ChartStreamingUpdate(7, ChartStreamingEventKind.FadeIn) };
        _controller.AnimateStreaming(updates);

        var overlays = _controller.GetStreamingOverlaySnapshots();
        Assert.Contains(overlays, overlay => overlay.SeriesId == 7);
    }

    [Fact]
    public void AnimateStreaming_RespectsReducedMotion()
    {
        _controller.UpdateProfile(ChartAnimationProfile.Default with { ReducedMotionEnabled = true });
        var updates = new[] { new ChartStreamingUpdate(9, ChartStreamingEventKind.SlideIn) };
        _controller.AnimateStreaming(updates);

        var overlays = _controller.GetStreamingOverlaySnapshots();
        Assert.True(overlays.Count == 0 || overlays.All(overlay => overlay.SlideOffset <= 0.001f));
    }

    private void SimulateTime(int milliseconds)
    {
        var elapsed = TimeSpan.FromMilliseconds(0);
        var step = TimeSpan.FromMilliseconds(32);
        while (elapsed.TotalMilliseconds < milliseconds)
        {
            elapsed += step;
            _scheduler.TryRunManualTick(elapsed);
        }
    }

    public void Dispose()
    {
        _controller.Dispose();
        _scheduler.Dispose();
    }
}
