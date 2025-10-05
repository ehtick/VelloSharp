using System;
using System.Drawing;
using VelloSharp;

namespace VelloSharp.WinForms;

internal static class VelloColorHelpers
{
    public static RgbaColor ToRgba(Color color) => RgbaColor.FromBytes(color.R, color.G, color.B, color.A);

    public static Color ToColor(RgbaColor color)
    {
        static int Channel(float value) => (int)Math.Clamp(MathF.Round(value * 255f), 0, 255);
        return Color.FromArgb(Channel(color.A), Channel(color.R), Channel(color.G), Channel(color.B));
    }
}
