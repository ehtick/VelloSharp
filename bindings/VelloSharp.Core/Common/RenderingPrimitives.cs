using System;
using System.Numerics;

namespace VelloSharp;

public readonly record struct RgbaColor(float R, float G, float B, float A)
{
    public static RgbaColor FromBytes(byte r, byte g, byte b, byte a = 255)
    {
        const float Scale = 1f / 255f;
        return new RgbaColor(r * Scale, g * Scale, b * Scale, a * Scale);
    }
}

public readonly record struct GradientStop(float Offset, RgbaColor Color)
{
    public float Offset { get; init; } = Offset;
    public RgbaColor Color { get; init; } = Color;
}

public readonly struct LayerBlend
{
    public LayerBlend(LayerMix mix, LayerCompose compose)
    {
        Mix = mix;
        Compose = compose;
    }

    public LayerMix Mix { get; }
    public LayerCompose Compose { get; }
}

public sealed class StrokeStyle
{
    public double Width { get; set; } = 1.0;
    public double MiterLimit { get; set; } = 4.0;
    public LineCap StartCap { get; set; } = LineCap.Butt;
    public LineCap EndCap { get; set; } = LineCap.Butt;
    public LineJoin LineJoin { get; set; } = LineJoin.Miter;
    public double DashPhase { get; set; }
    public double[]? DashPattern { get; set; }
}

public readonly record struct RenderParams(
    uint Width,
    uint Height,
    RgbaColor BaseColor,
    AntialiasingMode Antialiasing = AntialiasingMode.Area,
    RenderFormat Format = RenderFormat.Rgba8)
{
    public uint Width { get; init; } = Width;
    public uint Height { get; init; } = Height;
    public RgbaColor BaseColor { get; init; } = BaseColor;
    public AntialiasingMode Antialiasing { get; init; } = Antialiasing;
    public RenderFormat Format { get; init; } = Format;
}
