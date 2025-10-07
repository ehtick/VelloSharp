namespace VelloSharp.Charting.Layout;

/// <summary>
/// Represents the computed layout for an axis.
/// </summary>
public sealed class AxisLayout
{
    internal AxisLayout(AxisOrientation orientation, LayoutRect bounds, double actualThickness)
    {
        Orientation = orientation;
        Bounds = bounds;
        ActualThickness = actualThickness;
    }

    public AxisOrientation Orientation { get; }

    public LayoutRect Bounds { get; }

    public double ActualThickness { get; }
}
