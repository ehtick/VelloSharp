namespace HarfBuzzSharp;

public struct GlyphInfo
{
    public GlyphInfo(uint codepoint, uint cluster, GlyphFlags flags = 0)
    {
        Codepoint = codepoint;
        Cluster = cluster;
        Flags = flags;
    }

    public uint Codepoint { get; set; }
    public uint Cluster { get; set; }
    public GlyphFlags Flags { get; set; }
}

public struct GlyphPosition
{
    public GlyphPosition(float xAdvance, float yAdvance, float xOffset, float yOffset)
    {
        XAdvance = xAdvance;
        YAdvance = yAdvance;
        XOffset = xOffset;
        YOffset = yOffset;
    }

    public float XAdvance { get; set; }
    public float YAdvance { get; set; }
    public float XOffset { get; set; }
    public float YOffset { get; set; }
}

public struct GlyphExtents
{
    public float XBearing { get; set; }
    public float YBearing { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
}

public struct FontExtents
{
    public float Ascender { get; set; }
    public float Descender { get; set; }
    public float LineGap { get; set; }
}
