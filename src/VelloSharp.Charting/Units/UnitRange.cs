using System;

namespace VelloSharp.Charting.Units;

/// <summary>
/// Represents the physical range corresponding to normalized unit space (0..1).
/// </summary>
public readonly record struct UnitRange(double Start, double End)
{
    public static readonly UnitRange Normalized = new(0d, 1d);

    public double Span => End - Start;

    public UnitRange Normalize() => Span >= 0 ? this : new UnitRange(End, Start);

    public double FromUnit(double unit)
    {
        if (!double.IsFinite(unit))
        {
            return double.NaN;
        }

        return Start + Span * unit;
    }

    public double ToUnit(double value)
    {
        var span = Span;
        if (Math.Abs(span) < double.Epsilon)
        {
            return 0d;
        }

        return (value - Start) / span;
    }

    public UnitRange Clamp(UnitRange bounds)
    {
        var normalized = Normalize();
        var target = bounds.Normalize();
        var start = Math.Max(normalized.Start, target.Start);
        var end = Math.Min(normalized.End, target.End);
        return new UnitRange(start, end);
    }
}
