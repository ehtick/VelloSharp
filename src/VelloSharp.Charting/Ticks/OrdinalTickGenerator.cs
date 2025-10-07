using System;
using System.Collections.Generic;
using VelloSharp.Charting.Scales;

namespace VelloSharp.Charting.Ticks;

/// <summary>
/// Generates ticks for ordinal scales.
/// </summary>
public sealed class OrdinalTickGenerator<T> : IAxisTickGenerator<T> where T : notnull
{
    public IReadOnlyList<AxisTick<T>> Generate(IScale<T> scale, TickGenerationOptions<T>? options = null)
    {
        ArgumentNullException.ThrowIfNull(scale);

        if (scale is not OrdinalScale<T> ordinal)
        {
            throw new ArgumentException("Ordinal tick generator requires an ordinal scale instance.", nameof(scale));
        }

        options ??= new TickGenerationOptions<T>();
        var ticks = new List<AxisTick<T>>(ordinal.Categories.Count);

        foreach (var category in ordinal.Categories)
        {
            if (!ordinal.TryProject(category, out var unit))
            {
                continue;
            }

            ticks.Add(new AxisTick<T>(category, unit, Format(options, category)));
        }

        return ticks;
    }

    private static string Format(TickGenerationOptions<T> options, T value) =>
        options.LabelFormatter?.Invoke(value) ?? value?.ToString() ?? string.Empty;
}
