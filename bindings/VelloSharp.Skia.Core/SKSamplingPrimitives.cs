using System;

namespace SkiaSharp;

public enum SKFilterMode
{
    Nearest = 0,
    Linear = 1,
    Cubic = 2,
}

public enum SKMipmapMode
{
    None = 0,
    Nearest = 1,
    Linear = 2,
}

public readonly struct SKCubicResampler
{
    public static SKCubicResampler Mitchell { get; } = new(1f / 3f, 1f / 3f);

    public SKCubicResampler(float b, float c)
    {
        B = b;
        C = c;
    }

    public float B { get; }

    public float C { get; }

    public override string ToString() => $"SKCubicResampler(B={B}, C={C})";
}
