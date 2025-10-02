using System;
using System.Numerics;

namespace VelloSharp;

public readonly record struct GlyphMetrics(float Advance, float XBearing, float YBearing, float Width, float Height)
{
    public float Advance { get; init; } = Advance;
    public float XBearing { get; init; } = XBearing;
    public float YBearing { get; init; } = YBearing;
    public float Width { get; init; } = Width;
    public float Height { get; init; } = Height;
}

public readonly struct Glyph
{
    public Glyph(uint id, float x, float y)
    {
        Id = id;
        X = x;
        Y = y;
    }

    public uint Id { get; }
    public float X { get; }
    public float Y { get; }
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
