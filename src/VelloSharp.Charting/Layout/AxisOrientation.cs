namespace VelloSharp.Charting.Layout;

/// <summary>
/// Defines the anchor of an axis relative to the plot area.
/// </summary>
public enum AxisOrientation
{
    Left,
    Right,
    Top,
    Bottom,
}

internal static class AxisOrientationExtensions
{
    public static bool IsHorizontal(this AxisOrientation orientation) =>
        orientation is AxisOrientation.Top or AxisOrientation.Bottom;

    public static bool IsVertical(this AxisOrientation orientation) =>
        orientation is AxisOrientation.Left or AxisOrientation.Right;
}
