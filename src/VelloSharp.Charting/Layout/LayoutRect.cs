using System;

namespace VelloSharp.Charting.Layout;

/// <summary>
/// Represents a rectangle in device independent units.
/// </summary>
public readonly record struct LayoutRect(double X, double Y, double Width, double Height)
{
    public static LayoutRect Empty => new(0d, 0d, 0d, 0d);

    public double Right => X + Width;

    public double Bottom => Y + Height;

    public bool IsEmpty => Width <= 0d || Height <= 0d;

    public LayoutRect Inflate(double horizontal, double vertical)
    {
        var newX = X - horizontal;
        var newY = Y - vertical;
        var newWidth = Width + horizontal * 2d;
        var newHeight = Height + vertical * 2d;
        return new LayoutRect(newX, newY, Math.Max(0d, newWidth), Math.Max(0d, newHeight));
    }
}
