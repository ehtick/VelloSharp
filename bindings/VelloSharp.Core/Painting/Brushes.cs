using System;
using System.Collections.Generic;
using System.Numerics;

namespace VelloSharp;

public abstract class Brush
{
    private protected Brush()
    {
    }

    internal static GradientStop[] CloneStops(IReadOnlyList<GradientStop> stops, string paramName)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (stops.Count == 0)
        {
            throw new ArgumentException("At least one gradient stop is required.", paramName);
        }

        var array = new GradientStop[stops.Count];
        for (var i = 0; i < array.Length; i++)
        {
            array[i] = stops[i];
        }

        return array;
    }

    internal static GradientStop[] CloneStops(ReadOnlySpan<GradientStop> stops, string paramName)
    {
        if (stops.IsEmpty)
        {
            throw new ArgumentException("At least one gradient stop is required.", paramName);
        }

        var array = new GradientStop[stops.Length];
        stops.CopyTo(array);
        return array;
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
        : this(start, end, ValidateAndCopy(stops), extend)
    {
    }

    private LinearGradientBrush(Vector2 start, Vector2 end, GradientStop[] stops, ExtendMode extend)
    {
        Start = start;
        End = end;
        Extend = extend;

        _stops = stops;
    }

    public Vector2 Start { get; }
    public Vector2 End { get; }
    public ExtendMode Extend { get; }
    public ReadOnlySpan<GradientStop> Stops => _stops;
    internal GradientStop[] StopsArray => _stops;

    private static GradientStop[] ValidateAndCopy(IReadOnlyList<GradientStop> stops) =>
        Brush.CloneStops(stops, nameof(stops));

    public static LinearGradientBrush FromSpan(Vector2 start, Vector2 end, ReadOnlySpan<GradientStop> stops, ExtendMode extend = ExtendMode.Pad)
        => new LinearGradientBrush(start, end, Brush.CloneStops(stops, nameof(stops)), extend);
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
        : this(startCenter, startRadius, endCenter, endRadius, Brush.CloneStops(stops, nameof(stops)), extend)
    {
    }

    private RadialGradientBrush(
        Vector2 startCenter,
        float startRadius,
        Vector2 endCenter,
        float endRadius,
        GradientStop[] stops,
        ExtendMode extend)
    {
        if (startRadius < 0f || endRadius < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(startRadius), "Radii must be non-negative.");
        }

        StartCenter = startCenter;
        StartRadius = startRadius;
        EndCenter = endCenter;
        EndRadius = endRadius;
        Extend = extend;

        _stops = stops;
    }

    public Vector2 StartCenter { get; }
    public float StartRadius { get; }
    public Vector2 EndCenter { get; }
    public float EndRadius { get; }
    public ExtendMode Extend { get; }
    public ReadOnlySpan<GradientStop> Stops => _stops;
    internal GradientStop[] StopsArray => _stops;

    public static RadialGradientBrush FromSpan(
        Vector2 startCenter,
        float startRadius,
        Vector2 endCenter,
        float endRadius,
        ReadOnlySpan<GradientStop> stops,
        ExtendMode extend = ExtendMode.Pad) =>
        new RadialGradientBrush(startCenter, startRadius, endCenter, endRadius, Brush.CloneStops(stops, nameof(stops)), extend);
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
        : this(center, startAngle, endAngle, Brush.CloneStops(stops, nameof(stops)), extend)
    {
    }

    private SweepGradientBrush(
        Vector2 center,
        float startAngle,
        float endAngle,
        GradientStop[] stops,
        ExtendMode extend)
    {
        if (!float.IsFinite(startAngle) || !float.IsFinite(endAngle))
        {
            throw new ArgumentException("Sweep gradient angles must be finite values.");
        }

        Center = center;
        StartAngle = startAngle;
        EndAngle = endAngle;
        Extend = extend;

        _stops = stops;
    }

    public Vector2 Center { get; }
    public float StartAngle { get; }
    public float EndAngle { get; }
    public ExtendMode Extend { get; }
    public ReadOnlySpan<GradientStop> Stops => _stops;
    internal GradientStop[] StopsArray => _stops;

    public static SweepGradientBrush FromSpan(
        Vector2 center,
        float startAngle,
        float endAngle,
        ReadOnlySpan<GradientStop> stops,
        ExtendMode extend = ExtendMode.Pad) =>
        new SweepGradientBrush(center, startAngle, endAngle, Brush.CloneStops(stops, nameof(stops)), extend);
}
