using System;
using System.Runtime.InteropServices;

namespace HarfBuzzSharp;

[StructLayout(LayoutKind.Sequential)]
public struct GlyphInfo
{
    private uint _codepoint;
    private uint _mask;
    private uint _cluster;
    private int _var1;
    private int _var2;

    public GlyphInfo(uint codepoint, uint cluster, GlyphFlags flags = 0)
    {
        _codepoint = codepoint;
        _mask = 0;
        _cluster = cluster;
        _var1 = (int)flags;
        _var2 = 0;
    }

    public uint Codepoint
    {
        readonly get => _codepoint;
        set => _codepoint = value;
    }

    public uint Mask
    {
        readonly get => _mask;
        set => _mask = value;
    }

    public uint Cluster
    {
        readonly get => _cluster;
        set => _cluster = value;
    }

    public int Var1
    {
        readonly get => _var1;
        set => _var1 = value;
    }

    public int Var2
    {
        readonly get => _var2;
        set => _var2 = value;
    }

    public GlyphFlags Flags
    {
        readonly get => (GlyphFlags)_var1;
        set => _var1 = (int)value;
    }

    public GlyphFlags GlyphFlags => Flags;
}

[StructLayout(LayoutKind.Sequential)]
public struct GlyphPosition
{
    public GlyphPosition(float xAdvance, float yAdvance, float xOffset, float yOffset)
        : this(
            RoundToInt(xAdvance),
            RoundToInt(yAdvance),
            RoundToInt(xOffset),
            RoundToInt(yOffset),
            0)
    {
    }

    public GlyphPosition(int xAdvance, int yAdvance, int xOffset, int yOffset)
        : this(xAdvance, yAdvance, xOffset, yOffset, 0)
    {
    }

    public GlyphPosition(int xAdvance, int yAdvance, int xOffset, int yOffset, int var)
    {
        _xAdvance = xAdvance;
        _yAdvance = yAdvance;
        _xOffset = xOffset;
        _yOffset = yOffset;
        _var = var;
    }

    private int _xAdvance;
    private int _yAdvance;
    private int _xOffset;
    private int _yOffset;
    private int _var;

    public int XAdvance
    {
        readonly get => _xAdvance;
        set => _xAdvance = value;
    }

    public int YAdvance
    {
        readonly get => _yAdvance;
        set => _yAdvance = value;
    }

    public int XOffset
    {
        readonly get => _xOffset;
        set => _xOffset = value;
    }

    public int YOffset
    {
        readonly get => _yOffset;
        set => _yOffset = value;
    }

    public int Var
    {
        readonly get => _var;
        set => _var = value;
    }

    private static int RoundToInt(float value) => (int)MathF.Round(value);
}

[StructLayout(LayoutKind.Sequential)]
public struct GlyphExtents
{
    private int _xBearing;
    private int _yBearing;
    private int _width;
    private int _height;

    public int XBearing
    {
        readonly get => _xBearing;
        set => _xBearing = value;
    }

    public int YBearing
    {
        readonly get => _yBearing;
        set => _yBearing = value;
    }

    public int Width
    {
        readonly get => _width;
        set => _width = value;
    }

    public int Height
    {
        readonly get => _height;
        set => _height = value;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct FontExtents
{
    private int _ascender;
    private int _descender;
    private int _lineGap;
    private int _reserved9;
    private int _reserved8;
    private int _reserved7;
    private int _reserved6;
    private int _reserved5;
    private int _reserved4;
    private int _reserved3;
    private int _reserved2;
    private int _reserved1;

    public int Ascender
    {
        readonly get => _ascender;
        set => _ascender = value;
    }

    public int Descender
    {
        readonly get => _descender;
        set => _descender = value;
    }

    public int LineGap
    {
        readonly get => _lineGap;
        set => _lineGap = value;
    }

    public int Reserved9
    {
        readonly get => _reserved9;
        set => _reserved9 = value;
    }

    public int Reserved8
    {
        readonly get => _reserved8;
        set => _reserved8 = value;
    }

    public int Reserved7
    {
        readonly get => _reserved7;
        set => _reserved7 = value;
    }

    public int Reserved6
    {
        readonly get => _reserved6;
        set => _reserved6 = value;
    }

    public int Reserved5
    {
        readonly get => _reserved5;
        set => _reserved5 = value;
    }

    public int Reserved4
    {
        readonly get => _reserved4;
        set => _reserved4 = value;
    }

    public int Reserved3
    {
        readonly get => _reserved3;
        set => _reserved3 = value;
    }

    public int Reserved2
    {
        readonly get => _reserved2;
        set => _reserved2 = value;
    }

    public int Reserved1
    {
        readonly get => _reserved1;
        set => _reserved1 = value;
    }
}
