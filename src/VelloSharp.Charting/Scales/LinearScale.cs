using System;
using VelloSharp.Charting.Primitives;

namespace VelloSharp.Charting.Scales;

/// <summary>
/// Projects numeric values using a linear mapping.
/// </summary>
public sealed class LinearScale : IScale<double>
{
    public LinearScale(double start, double end, bool clampToDomain = true)
        : this(new Range<double>(start, end), clampToDomain)
    {
    }

    public LinearScale(Range<double> domain, bool clampToDomain = true)
    {
        Domain = domain;
        ClampToDomain = clampToDomain;
    }

    public ScaleKind Kind => ScaleKind.Linear;

    public bool ClampToDomain { get; }

    public Type DomainType => typeof(double);

    public Range<double> Domain { get; }

    public double Project(double value)
    {
        if (!double.IsFinite(value))
        {
            return double.NaN;
        }

        var start = Domain.Start;
        var end = Domain.End;
        if (ClampToDomain)
        {
            var min = Math.Min(start, end);
            var max = Math.Max(start, end);
            value = Math.Clamp(value, min, max);
        }

        var span = end - start;
        if (Math.Abs(span) < double.Epsilon)
        {
            return 0d;
        }

        return (value - start) / span;
    }

    public bool TryProject(double value, out double unit)
    {
        unit = Project(value);
        return double.IsFinite(unit);
    }

    public double Unproject(double unit)
    {
        if (!double.IsFinite(unit))
        {
            throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unit value must be finite.");
        }

        if (ClampToDomain)
        {
            unit = Math.Clamp(unit, 0d, 1d);
        }

        var span = Domain.End - Domain.Start;
        if (Math.Abs(span) < double.Epsilon)
        {
            return Domain.Start;
        }

        return Domain.Start + unit * span;
    }
}
