namespace VelloSharp.Charting.Ticks;

/// <summary>
/// Represents an axis tick with associated domain value and normalized position.
/// </summary>
public readonly record struct AxisTick<T>(T Value, double UnitPosition, string Label);
