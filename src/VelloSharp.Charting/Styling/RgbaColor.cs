using System;
using VelloSharp.ChartEngine;

namespace VelloSharp.Charting.Styling;

/// <summary>
/// Simple RGBA color representation using normalized components.
/// </summary>
public readonly record struct RgbaColor(byte R, byte G, byte B, byte A)
{
    public static RgbaColor FromHex(uint hex)
    {
        var r = (byte)((hex >> 24) & 0xFF);
        var g = (byte)((hex >> 16) & 0xFF);
        var b = (byte)((hex >> 8) & 0xFF);
        var a = (byte)(hex & 0xFF);
        return new RgbaColor(r, g, b, a);
    }

    public uint ToHex() =>
        ((uint)R << 24) | ((uint)G << 16) | ((uint)B << 8) | A;

    public RgbaColor WithAlpha(byte alpha) => new(R, G, B, alpha);

    public static readonly RgbaColor Transparent = new(0, 0, 0, 0);

    public static RgbaColor FromChartColor(ChartColor color) => new(color.R, color.G, color.B, color.A);

    public VelloSharp.RgbaColor ToVelloColor()
    {
        const float scale = 1f / 255f;
        return new VelloSharp.RgbaColor(R * scale, G * scale, B * scale, A * scale);
    }
}
