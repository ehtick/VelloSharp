using System;
using VelloSharp.Charting.Primitives;

namespace VelloSharp.Charting.Scales;

/// <summary>
/// Projects numeric values using logarithmic mapping.
/// </summary>
public sealed class LogarithmicScale : IScale<double>
{
    private readonly double _logBase;
    private readonly double _logStart;
    private readonly double _logSpan;

    public LogarithmicScale(double start, double end, double logBase = 10d, bool clampToDomain = true)
        : this(new Range<double>(start, end), logBase, clampToDomain)
    {
    }

    public LogarithmicScale(Range<double> domain, double logBase = 10d, bool clampToDomain = true)
    {
        if (logBase <= 0d || Math.Abs(logBase - 1d) < double.Epsilon)
        {
            throw new ArgumentOutOfRangeException(nameof(logBase), logBase, "Logarithm base must be positive and not equal to 1.");
        }

        var (min, max) = domain.Normalize();
        if (min <= 0d || max <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(domain), "Logarithmic scale requires a strictly positive domain.");
        }

        Domain = domain;
        ClampToDomain = clampToDomain;
        _logBase = logBase;
        _logStart = Math.Log(Domain.Start, logBase);
        _logSpan = Math.Log(Domain.End, logBase) - _logStart;
    }

    public ScaleKind Kind => ScaleKind.Logarithmic;

    public bool ClampToDomain { get; }

    public Type DomainType => typeof(double);

    public Range<double> Domain { get; }

    public double Project(double value)
    {
        if (value <= 0d || !double.IsFinite(value))
        {
            if (ClampToDomain)
            {
                value = ClampPositive(value);
            }
            else
            {
                return double.NaN;
            }
        }

        var logValue = Math.Log(value, _logBase);
        if (ClampToDomain)
        {
            var min = Math.Min(_logStart, _logStart + _logSpan);
            var max = Math.Max(_logStart, _logStart + _logSpan);
            logValue = Math.Clamp(logValue, min, max);
        }

        if (Math.Abs(_logSpan) < double.Epsilon)
        {
            return 0d;
        }

        return (logValue - _logStart) / _logSpan;
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

        if (Math.Abs(_logSpan) < double.Epsilon)
        {
            return Domain.Start;
        }

        var logValue = _logStart + unit * _logSpan;
        return Math.Pow(_logBase, logValue);
    }

    private double ClampPositive(double value)
    {
        var (min, max) = Domain.Normalize();
        var positiveMin = Math.Max(min, double.Epsilon);
        if (value < positiveMin)
        {
            return positiveMin;
        }

        return Math.Min(value, max);
    }
}
