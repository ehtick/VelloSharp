using System;
using VelloSharp.Charting.Primitives;

namespace VelloSharp.Charting.Scales;

/// <summary>
/// Projects temporal values (DateTimeOffset) using linear mapping in Unix milliseconds.
/// </summary>
public sealed class TimeScale : IScale<DateTimeOffset>
{
    private readonly long _startMillis;
    private readonly long _endMillis;
    private readonly long _spanMillis;

    public TimeScale(DateTimeOffset start, DateTimeOffset end, bool clampToDomain = true)
        : this(new Range<DateTimeOffset>(start, end), clampToDomain)
    {
    }

    public TimeScale(Range<DateTimeOffset> domain, bool clampToDomain = true)
    {
        Domain = domain;
        ClampToDomain = clampToDomain;
        _startMillis = domain.Start.ToUnixTimeMilliseconds();
        _endMillis = domain.End.ToUnixTimeMilliseconds();
        _spanMillis = _endMillis - _startMillis;
    }

    public ScaleKind Kind => ScaleKind.Time;

    public bool ClampToDomain { get; }

    public Type DomainType => typeof(DateTimeOffset);

    public Range<DateTimeOffset> Domain { get; }

    public double Project(DateTimeOffset value)
    {
        var millis = value.ToUnixTimeMilliseconds();
        if (ClampToDomain)
        {
            var (min, max) = GetBounds();
            millis = Math.Clamp(millis, min, max);
        }

        if (_spanMillis == 0)
        {
            return 0d;
        }

        return (millis - _startMillis) / (double)_spanMillis;
    }

    public bool TryProject(DateTimeOffset value, out double unit)
    {
        unit = Project(value);
        return double.IsFinite(unit);
    }

    public DateTimeOffset Unproject(double unit)
    {
        if (!double.IsFinite(unit))
        {
            throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unit value must be finite.");
        }

        if (ClampToDomain)
        {
            unit = Math.Clamp(unit, 0d, 1d);
        }

        if (_spanMillis == 0)
        {
            return Domain.Start;
        }

        var millis = _startMillis + (long)Math.Round(unit * _spanMillis, MidpointRounding.AwayFromZero);
        return DateTimeOffset.FromUnixTimeMilliseconds(millis);
    }

    private (long Min, long Max) GetBounds()
    {
        var min = Math.Min(_startMillis, _endMillis);
        var max = Math.Max(_startMillis, _endMillis);
        return (min, max);
    }
}
