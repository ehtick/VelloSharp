using System;
using VelloSharp.ChartEngine;

namespace VelloSharp.Charting.Styling;

/// <summary>
/// Simple linear gradient used to map numeric values to chart colors.
/// </summary>
public sealed class ValueColorGradient
{
    private readonly ChartColor _start;
    private readonly ChartColor _end;
    private readonly double _min;
    private readonly double _max;

    public ValueColorGradient(ChartColor startColor, ChartColor endColor, double minimumValue, double maximumValue)
    {
        if (maximumValue <= minimumValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumValue), "Maximum value must exceed the minimum value.");
        }

        _start = startColor;
        _end = endColor;
        _min = minimumValue;
        _max = maximumValue;
    }

    public ChartColor Evaluate(double value)
    {
        var clamped = Math.Clamp(value, _min, _max);
        var t = (clamped - _min) / (_max - _min);
        return Lerp(_start, _end, t);
    }

    private static ChartColor Lerp(ChartColor a, ChartColor b, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        byte LerpChannel(byte start, byte end) => (byte)(start + (end - start) * t);

        return new ChartColor(
            LerpChannel(a.R, b.R),
            LerpChannel(a.G, b.G),
            LerpChannel(a.B, b.B),
            LerpChannel(a.A, b.A));
    }
}
