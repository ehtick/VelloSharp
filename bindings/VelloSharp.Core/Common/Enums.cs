namespace VelloSharp;

public enum FillRule
{
    NonZero = 0,
    EvenOdd = 1,
}

public enum LineCap
{
    Butt = 0,
    Round = 1,
    Square = 2,
}

public enum LineJoin
{
    Miter = 0,
    Round = 1,
    Bevel = 2,
}

public enum AntialiasingMode
{
    Area = 0,
    Msaa8 = 1,
    Msaa16 = 2,
}

public enum SparseRenderMode
{
    OptimizeSpeed = 0,
    OptimizeQuality = 1,
}

public enum RenderFormat
{
    Rgba8 = 0,
    Bgra8 = 1,
}

public enum PresentMode
{
    AutoVsync = 0,
    AutoNoVsync = 1,
    Fifo = 2,
    Immediate = 3,
}

public enum ImageAlphaMode
{
    Straight = 0,
    Premultiplied = 1,
}

public enum ExtendMode
{
    Pad = 0,
    Repeat = 1,
    Reflect = 2,
}

public enum ImageQuality
{
    Low = 0,
    Medium = 1,
    High = 2,
}

public enum LayerMix
{
    Normal = 0,
    Multiply = 1,
    Screen = 2,
    Overlay = 3,
    Darken = 4,
    Lighten = 5,
    ColorDodge = 6,
    ColorBurn = 7,
    HardLight = 8,
    SoftLight = 9,
    Difference = 10,
    Exclusion = 11,
    Hue = 12,
    Saturation = 13,
    Color = 14,
    Luminosity = 15,
    Clip = 128,
}

public enum LayerCompose
{
    Clear = 0,
    Copy = 1,
    Dest = 2,
    SrcOver = 3,
    DestOver = 4,
    SrcIn = 5,
    DestIn = 6,
    SrcOut = 7,
    DestOut = 8,
    SrcAtop = 9,
    DestAtop = 10,
    Xor = 11,
    Plus = 12,
    PlusLighter = 13,
}

public enum GlyphRunStyle
{
    Fill = 0,
    Stroke = 1,
}
