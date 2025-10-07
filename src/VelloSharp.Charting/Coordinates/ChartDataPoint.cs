namespace VelloSharp.Charting.Coordinates;

/// <summary>
/// Represents a pair of values in the data domain.
/// </summary>
public readonly record struct ChartDataPoint<TX, TY>(TX X, TY Y);
