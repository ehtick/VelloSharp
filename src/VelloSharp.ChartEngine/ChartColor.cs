using System.Runtime.InteropServices;

namespace VelloSharp.ChartEngine;

/// <summary>
/// Represents an RGBA color used for chart rendering.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ChartColor(byte R, byte G, byte B, byte A = 0xFF)
{
    public static ChartColor FromRgb(byte r, byte g, byte b) => new(r, g, b, 0xFF);

    public static ChartColor FromArgb(byte a, byte r, byte g, byte b) => new(r, g, b, a);

    internal VelloChartColor ToNative() => new(R, G, B, A);
}
