using System;
using System.Collections.Generic;
using System.Numerics;

namespace VelloSharp;

public abstract class Brush
{
    private protected Brush()
    {
    }
}

public sealed class SolidColorBrush : Brush
{
    public SolidColorBrush(RgbaColor color)
    {
        Color = color;
    }

    public RgbaColor Color { get; }
}

public sealed class LinearGradientBrush : Brush
{
    private readonly GradientStop[] _stops;

    public LinearGradientBrush(Vector2 start, Vector2 end, IReadOnlyList<GradientStop> stops, ExtendMode extend = ExtendMode.Pad)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (stops.Count == 0)
        {
            throw new ArgumentException("At least one gradient stop is required.", nameof(stops));
        }

        Start = start;
        End = end;
        Extend = extend;

        _stops = new GradientStop[stops.Count];
        for (var i = 0; i < stops.Count; i++)
        {
            _stops[i] = stops[i];
        }
    }

    public Vector2 Start { get; }
    public Vector2 End { get; }
    public ExtendMode Extend { get; }
    public ReadOnlySpan<GradientStop> Stops => _stops;
}

public sealed class RadialGradientBrush : Brush
{
    private readonly GradientStop[] _stops;

    public RadialGradientBrush(
        Vector2 startCenter,
        float startRadius,
        Vector2 endCenter,
        float endRadius,
        IReadOnlyList<GradientStop> stops,
        ExtendMode extend = ExtendMode.Pad)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (stops.Count == 0)
        {
            throw new ArgumentException("At least one gradient stop is required.", nameof(stops));
        }

        if (startRadius < 0f || endRadius < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(startRadius), "Radii must be non-negative.");
        }

        StartCenter = startCenter;
        StartRadius = startRadius;
        EndCenter = endCenter;
        EndRadius = endRadius;
        Extend = extend;

        _stops = new GradientStop[stops.Count];
        for (var i = 0; i < stops.Count; i++)
        {
            _stops[i] = stops[i];
        }
    }

    public Vector2 StartCenter { get; }
    public float StartRadius { get; }
    public Vector2 EndCenter { get; }
    public float EndRadius { get; }
    public ExtendMode Extend { get; }
    public ReadOnlySpan<GradientStop> Stops => _stops;
}

public sealed class SweepGradientBrush : Brush
{
    private readonly GradientStop[] _stops;

    public SweepGradientBrush(
        Vector2 center,
        float startAngle,
        float endAngle,
        IReadOnlyList<GradientStop> stops,
        ExtendMode extend = ExtendMode.Pad)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (stops.Count == 0)
        {
            throw new ArgumentException("At least one gradient stop is required.", nameof(stops));
        }
        if (!float.IsFinite(startAngle) || !float.IsFinite(endAngle))
        {
            throw new ArgumentException("Sweep gradient angles must be finite values.");
        }

        Center = center;
        StartAngle = startAngle;
        EndAngle = endAngle;
        Extend = extend;

        _stops = new GradientStop[stops.Count];
        for (var i = 0; i < stops.Count; i++)
        {
            _stops[i] = stops[i];
        }
    }

    public Vector2 Center { get; }
    public float StartAngle { get; }
    public float EndAngle { get; }
    public ExtendMode Extend { get; }
    public ReadOnlySpan<GradientStop> Stops => _stops;
}
