using System;

namespace SkiaSharp;

public enum SKHighContrastConfigInvertStyle
{
    NoInvert = 0,
    InvertBrightness = 1,
    InvertLightness = 2,
}

public readonly struct SKHighContrastConfig
{
    public static SKHighContrastConfig Default { get; } = new(false, SKHighContrastConfigInvertStyle.NoInvert, 0f);

    public SKHighContrastConfig(bool grayscale, SKHighContrastConfigInvertStyle invertStyle, float contrast)
    {
        Grayscale = grayscale;
        InvertStyle = invertStyle;
        Contrast = contrast;
    }

    public bool Grayscale { get; }
    public SKHighContrastConfigInvertStyle InvertStyle { get; }
    public float Contrast { get; }

    public bool IsValid =>
        InvertStyle >= SKHighContrastConfigInvertStyle.NoInvert &&
        InvertStyle <= SKHighContrastConfigInvertStyle.InvertLightness &&
        Contrast is >= -1f and <= 1f;
}
