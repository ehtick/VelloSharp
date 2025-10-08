using System;
using VelloSharp.Composition;

namespace VelloSharp.TreeDataGrid;

/// <summary>
/// Describes timeline preferences for TreeDataGrid row interactions.
/// </summary>
public sealed record class TreeRowAnimationProfile
{
    /// <summary>
    /// Gets the default animation profile tuned for TreeDataGrid row interactions.
    /// </summary>
    public static TreeRowAnimationProfile Default => new();

    /// <summary>
    /// Spring configuration used to ease row height towards the steady-state factor.
    /// </summary>
    public TreeSpringAnimationTrack HeightSpring { get; init; } = TreeSpringAnimationTrack.Default;

    /// <summary>
    /// Timeline controlling the selection glow emphasis after expand/collapse interactions.
    /// </summary>
    public TreeAnimationTimeline SelectionGlow { get; init; } = new(TimeSpan.FromMilliseconds(240), TimelineEasing.EaseOutQuad);

    /// <summary>
    /// Timeline controlling caret rotation during expand/collapse interactions.
    /// </summary>
    public TreeAnimationTimeline CaretRotation { get; init; } = new(TimeSpan.FromMilliseconds(180), TimelineEasing.EaseOutCubic);

    /// <summary>
    /// Lower bound applied to row height factors.
    /// </summary>
    public float MinHeightFactor { get; init; } = 0.10f;

    /// <summary>
    /// Upper bound applied to row height factors.
    /// </summary>
    public float MaxHeightFactor { get; init; } = 1.0f;

    /// <summary>
    /// Starting factor when an expand animation begins.
    /// </summary>
    public float ExpandStartFactor { get; init; } = 0.68f;

    /// <summary>
    /// Starting factor when a collapse animation begins.
    /// </summary>
    public float CollapseStartFactor { get; init; } = 0.82f;

    /// <summary>
    /// Glow intensity at the peak of the interaction.
    /// </summary>
    public float GlowPeak { get; init; } = 1.0f;

    /// <summary>
    /// Glow intensity once the animation settles.
    /// </summary>
    public float GlowFalloff { get; init; } = 0.0f;

    /// <summary>
    /// Minimum glow intensity preserved when collapsing to avoid abrupt cut-offs.
    /// </summary>
    public float CollapseGlowBaseline { get; init; } = 0.35f;

    /// <summary>
    /// Caret rotation angle when the row is expanded.
    /// </summary>
    public float CaretExpandedDegrees { get; init; } = 90f;

    /// <summary>
    /// Caret rotation angle when the row is collapsed.
    /// </summary>
    public float CaretCollapsedDegrees { get; init; } = 0f;

    /// <summary>
    /// When true, animations snap to their target state to honour reduced-motion preferences.
    /// </summary>
    public bool ReducedMotionEnabled { get; init; }
}

/// <summary>
/// Defines a spring-based animation track.
/// </summary>
public readonly record struct TreeSpringAnimationTrack
{
    public static TreeSpringAnimationTrack Default => new(220f, 18f, 1.1f, 0.0005f, 0.0005f);

    public TreeSpringAnimationTrack(
        float stiffness,
        float damping,
        float mass,
        float restVelocity,
        float restOffset)
    {
        Stiffness = Math.Clamp(stiffness, 0f, 1000f);
        Damping = Math.Clamp(damping, 0f, 200f);
        Mass = Math.Clamp(mass, 0.01f, 20f);
        RestVelocity = Math.Clamp(restVelocity, 0f, 0.05f);
        RestOffset = Math.Clamp(restOffset, 0f, 0.05f);
    }

    public float Stiffness { get; init; }

    public float Damping { get; init; }

    public float Mass { get; init; }

    public float RestVelocity { get; init; }

    public float RestOffset { get; init; }

    public bool IsEnabled => Stiffness > 0f && Damping > 0f && Mass > 0f;
}

/// <summary>
/// Defines an easing-based animation timeline.
/// </summary>
public readonly record struct TreeAnimationTimeline
{
    public TreeAnimationTimeline(TimeSpan duration, TimelineEasing easing, TimelineRepeat repeat = TimelineRepeat.Once)
    {
        Duration = duration;
        Easing = easing;
        Repeat = repeat;
    }

    public TimeSpan Duration { get; init; }

    public TimelineEasing Easing { get; init; }

    public TimelineRepeat Repeat { get; init; }

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
