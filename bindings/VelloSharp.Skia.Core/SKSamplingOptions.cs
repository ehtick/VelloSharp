using System;

namespace SkiaSharp;

public readonly struct SKSamplingOptions
{
    public static SKSamplingOptions Default => new();

    public SKSamplingOptions()
    {
        Filter = SKFilterMode.Nearest;
        Mipmap = SKMipmapMode.None;
        Cubic = null;
        MaxAnisotropy = 0;
    }

    public SKSamplingOptions(SKFilterMode filter)
        : this(filter, SKMipmapMode.None)
    {
    }

    public SKSamplingOptions(SKFilterMode filter, SKMipmapMode mipmap)
    {
        Filter = filter;
        Mipmap = mipmap;
        Cubic = null;
        MaxAnisotropy = 0;
    }

    public SKSamplingOptions(SKCubicResampler cubic)
    {
        Filter = SKFilterMode.Cubic;
        Mipmap = SKMipmapMode.Linear;
        Cubic = cubic;
        MaxAnisotropy = 0;
    }

    public SKSamplingOptions(int maxAnisotropy)
    {
        Filter = SKFilterMode.Linear;
        Mipmap = SKMipmapMode.Linear;
        Cubic = null;
        MaxAnisotropy = Math.Max(0, maxAnisotropy);
    }

    public SKFilterMode Filter { get; }

    public SKMipmapMode Mipmap { get; }

    public SKCubicResampler? Cubic { get; }

    public int MaxAnisotropy { get; }

    public bool UseCubic => Cubic.HasValue;

    public bool IsAniso => MaxAnisotropy > 0;
}
