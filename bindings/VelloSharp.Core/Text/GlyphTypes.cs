using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VelloSharp;

public readonly record struct GlyphMetrics(float Advance, float XBearing, float YBearing, float Width, float Height)
{
    public float Advance { get; init; } = Advance;
    public float XBearing { get; init; } = XBearing;
    public float YBearing { get; init; } = YBearing;
    public float Width { get; init; } = Width;
    public float Height { get; init; } = Height;
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct Glyph
{
    private readonly uint _id;
    private readonly float _x;
    private readonly float _y;

    public Glyph(uint id, float x, float y)
    {
        _id = id;
        _x = x;
        _y = y;
    }

    public uint Id => _id;
    public float X => _x;
    public float Y => _y;
}

public sealed class GlyphRunOptions
{
    public Brush Brush { get; set; } = new SolidColorBrush(RgbaColor.FromBytes(0, 0, 0));
    public float FontSize { get; set; } = 16f;
    public bool Hint { get; set; }
    public GlyphRunStyle Style { get; set; } = GlyphRunStyle.Fill;
    public StrokeStyle? Stroke { get; set; }
    public float BrushAlpha { get; set; } = 1f;
    public Matrix3x2 Transform { get; set; } = Matrix3x2.Identity;
    public Matrix3x2? GlyphTransform { get; set; }
}
