using System;
using VelloSharp.Composition;

namespace VelloSharp.ChartEngine;

/// <summary>
/// Configurable animation descriptors used by the chart engine when translating timeline requests into the shared runtime.
/// </summary>
public sealed record class ChartAnimationProfile
{
    /// <summary>
    /// Returns the default animation profile tuned for interactive chart scenarios.
    /// </summary>
    public static ChartAnimationProfile Default => new();

    /// <summary>
    /// Controls the default stroke-width animation applied when series appear or change emphasis.
    /// </summary>
    public ChartAnimationTimeline SeriesStroke { get; init; } = new(TimeSpan.FromMilliseconds(300), TimelineEasing.EaseInOutQuad);

    /// <summary>
    /// Defines the trailing cursor motion when streaming data updates arrive.
    /// </summary>
    public ChartAnimationTimeline CursorTrail { get; init; } = new(TimeSpan.FromMilliseconds(180), TimelineEasing.EaseOutCubic);

    /// <summary>
    /// Controls the fade or slide behaviour for crosshair overlays.
    /// </summary>
    public ChartAnimationTimeline CrosshairFade { get; init; } = new(TimeSpan.FromMilliseconds(140), TimelineEasing.EaseOutQuad);

    /// <summary>
    /// Configures how zoom transitions interpolate between data windows.
    /// </summary>
    public ChartAnimationTimeline ZoomTransition { get; init; } = new(TimeSpan.FromMilliseconds(260), TimelineEasing.EaseInOutCubic);

    /// <summary>
    /// Controls indicator overlay highlights and informative call-outs.
    /// </summary>
    public ChartAnimationTimeline IndicatorOverlay { get; init; } = new(TimeSpan.FromMilliseconds(220), TimelineEasing.EaseInOutSine);

    /// <summary>
    /// Preset motion descriptors for streaming-fade and rolling window scenarios.
    /// </summary>
    public ChartStreamingAnimationPreset Streaming { get; init; } = ChartStreamingAnimationPreset.Default;

    /// <summary>
    /// When set, animations favour instant transitions to honour reduced-motion accessibility preferences.
    /// </summary>
    public bool ReducedMotionEnabled { get; init; }

    /// <summary>
    /// When enabled, animation advancement uses deterministic frame deltas (derived from the frame budget) to ease recording/playback scenarios.
    /// </summary>
    public bool DeterministicPlaybackEnabled { get; init; }
}

/// <summary>
/// Defines animation presets applied to streaming data flows.
/// </summary>
public sealed record class ChartStreamingAnimationPreset
{
    public static ChartStreamingAnimationPreset Default => new();

    /// <summary>
    /// Fade applied when new data points enter the window.
    /// </summary>
    public ChartAnimationTimeline FadeIn { get; init; } = new(TimeSpan.FromMilliseconds(160), TimelineEasing.EaseOutQuad);

    /// <summary>
    /// Slide offset for incoming samples towards the rightmost edge.
    /// </summary>
    public ChartAnimationTimeline SlideIn { get; init; } = new(TimeSpan.FromMilliseconds(220), TimelineEasing.EaseInOutCubic);

    /// <summary>
    /// Rolling window shift used when the visible range advances forward.
    /// </summary>
    public ChartAnimationTimeline RollingWindowShift { get; init; } = new(TimeSpan.FromMilliseconds(240), TimelineEasing.Linear, TimelineRepeat.Loop);
}

/// <summary>
/// Describes a single animation timeline configuration.
/// </summary>
public readonly record struct ChartAnimationTimeline
{
    public ChartAnimationTimeline(TimeSpan duration, TimelineEasing easing, TimelineRepeat repeat = TimelineRepeat.Once)
    {
        Duration = duration;
        Easing = easing;
        Repeat = repeat;
    }

    /// <summary>
    /// Length of the timeline. Non-positive values disable the animation.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Easing curve applied across the timeline.
    /// </summary>
    public TimelineEasing Easing { get; init; }

    /// <summary>
    /// Repeat behaviour for the timeline (looping, ping-pong, etc.).
    /// </summary>
    public TimelineRepeat Repeat { get; init; }

    /// <summary>
    /// Indicates whether the timeline should produce interpolated samples.
    /// </summary>
    public bool IsEnabled => Duration > TimeSpan.Zero;

    internal float GetDurationSeconds()
    {
        if (Duration <= TimeSpan.Zero)
        {
            return 0f;
        }

        var seconds = (float)Duration.TotalSeconds;
        return seconds > 0f ? seconds : 0f;
    }
}
