using System;
using System.Collections.Generic;
using VelloSharp.Charting.Primitives;
using VelloSharp.Charting.Scales;

namespace VelloSharp.Charting.Ticks;

/// <summary>
/// Generates ticks for linear numeric scales.
/// </summary>
public sealed class LinearTickGenerator : IAxisTickGenerator<double>
{
    private static readonly double[] NiceMultipliers = { 1d, 2d, 2.5d, 5d, 10d };

    public IReadOnlyList<AxisTick<double>> Generate(IScale<double> scale, TickGenerationOptions<double>? options = null)
    {
        ArgumentNullException.ThrowIfNull(scale);

        options ??= new TickGenerationOptions<double>();
        var domain = scale.Domain.Normalize();
        var (min, max) = (domain.Start, domain.End);

        if (Math.Abs(max - min) < double.Epsilon)
        {
            var unit = scale.Project(min);
            return new[]
            {
                new AxisTick<double>(min, unit, Format(options, min))
            };
        }

        var rawRange = Math.Abs(max - min);
        var niceRange = NiceNumber(rawRange, round: false);
        var targetSpacing = niceRange / Math.Max(1, options.TargetTickCount - 1);
        var spacing = NiceNumber(targetSpacing, round: true);

        var niceMin = Math.Floor(min / spacing) * spacing;
        var niceMax = Math.Ceiling(max / spacing) * spacing;

        var ticks = new List<AxisTick<double>>();
        for (var value = niceMin; value <= niceMax + spacing * 0.5d; value += spacing)
        {
            var unit = scale.Project(value);
            ticks.Add(new AxisTick<double>(value, unit, Format(options, value)));
        }

        return ticks;
    }

    private static string Format(TickGenerationOptions<double> options, double value) =>
        options.LabelFormatter?.Invoke(value) ?? value.ToString("G");

    private static double NiceNumber(double value, bool round)
    {
        if (value <= 0d || !double.IsFinite(value))
        {
            return 0d;
        }

        var exponent = Math.Floor(Math.Log10(value));
        var fraction = value / Math.Pow(10d, exponent);
        double niceFraction;

        if (round)
        {
            if (fraction < 1.5d) niceFraction = 1d;
            else if (fraction < 3d) niceFraction = 2d;
            else if (fraction < 4.5d) niceFraction = 2.5d;
            else if (fraction < 7d) niceFraction = 5d;
            else niceFraction = 10d;
        }
        else
        {
            if (fraction <= 1d) niceFraction = 1d;
            else if (fraction <= 2d) niceFraction = 2d;
            else if (fraction <= 2.5d) niceFraction = 2.5d;
            else if (fraction <= 5d) niceFraction = 5d;
            else niceFraction = 10d;
        }

        return niceFraction * Math.Pow(10d, exponent);
    }
}
