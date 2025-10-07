using System.Collections.Generic;
using VelloSharp.Charting.Scales;

namespace VelloSharp.Charting.Ticks;

/// <summary>
/// Generates ticks for a given scale.
/// </summary>
public interface IAxisTickGenerator<T>
{
    IReadOnlyList<AxisTick<T>> Generate(IScale<T> scale, TickGenerationOptions<T>? options = null);
}
