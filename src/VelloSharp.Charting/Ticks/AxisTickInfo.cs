namespace VelloSharp.Charting.Ticks;

/// <summary>
/// Represents tick data prepared for rendering.
/// </summary>
public readonly record struct AxisTickInfo(object? Value, double UnitPosition, string Label);
