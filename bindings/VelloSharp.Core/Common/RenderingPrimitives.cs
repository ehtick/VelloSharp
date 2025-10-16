using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VelloSharp;

[StructLayout(LayoutKind.Sequential)]
public readonly struct RgbaColor
{
    private readonly float _r;
    private readonly float _g;
    private readonly float _b;
    private readonly float _a;

    public RgbaColor(float r, float g, float b, float a)
    {
        _r = r;
        _g = g;
        _b = b;
        _a = a;
    }

    public float R => _r;
    public float G => _g;
    public float B => _b;
    public float A => _a;

    public static RgbaColor FromBytes(byte r, byte g, byte b, byte a = 255)
    {
        const float Scale = 1f / 255f;
        return new RgbaColor(r * Scale, g * Scale, b * Scale, a * Scale);
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct GradientStop
{
    private readonly float _offset;
    private readonly RgbaColor _color;

    public GradientStop(float offset, RgbaColor color)
    {
        _offset = offset;
        _color = color;
    }

    public float Offset => _offset;
    public RgbaColor Color => _color;

    public static GradientStop At(float offset, RgbaColor color) => new(offset, color);
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
