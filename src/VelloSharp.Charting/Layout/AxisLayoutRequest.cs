using System;

namespace VelloSharp.Charting.Layout;

/// <summary>
/// Describes desired layout characteristics for an axis.
/// </summary>
public sealed class AxisLayoutRequest
{
    public AxisLayoutRequest(
        AxisOrientation orientation,
        double thickness,
        double? minThickness = null,
        double? maxThickness = null)
    {
        if (!double.IsFinite(thickness) || thickness < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(thickness), thickness, "Thickness must be a non-negative finite value.");
        }

        if (minThickness is { } min && (!double.IsFinite(min) || min < 0d))
        {
            throw new ArgumentOutOfRangeException(nameof(minThickness), min, "Minimum thickness must be a non-negative finite value.");
        }

        if (maxThickness is { } max && (!double.IsFinite(max) || max < 0d))
        {
            throw new ArgumentOutOfRangeException(nameof(maxThickness), max, "Maximum thickness must be a non-negative finite value.");
        }

        if (minThickness is { } minValue && minValue > thickness)
        {
            thickness = minValue;
        }

        if (maxThickness is { } maxValue && thickness > maxValue)
        {
            thickness = maxValue;
        }

        Orientation = orientation;
        Thickness = thickness;
        MinThickness = minThickness;
        MaxThickness = maxThickness;
    }

    public AxisOrientation Orientation { get; }

    /// <summary>
    /// Desired axis thickness in device independent pixels.
    /// </summary>
    public double Thickness { get; }

    public double? MinThickness { get; }

    public double? MaxThickness { get; }
}
