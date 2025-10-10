namespace SkiaSharp;

public readonly record struct SKSamplingOptions
{
    public SKSamplingOptions(bool useHighQuality)
    {
        UseHighQuality = useHighQuality;
        FilterMode = useHighQuality ? SKFilterMode.Linear : SKFilterMode.Nearest;
        MipmapMode = SKMipmapMode.None;
        Cubic = null;
    }

    public SKSamplingOptions(SKFilterMode filter, SKMipmapMode mipmap)
    {
        FilterMode = filter;
        MipmapMode = mipmap;
        UseHighQuality = filter != SKFilterMode.Nearest;
        Cubic = null;
    }

    public SKSamplingOptions(SKCubicResampler cubic)
    {
        FilterMode = SKFilterMode.Cubic;
        MipmapMode = SKMipmapMode.Linear;
        Cubic = cubic;
        UseHighQuality = true;
    }

    public bool UseHighQuality { get; }

    public SKFilterMode FilterMode { get; }

    public SKMipmapMode MipmapMode { get; }

    public SKCubicResampler? Cubic { get; }

    public static SKSamplingOptions Default => new(false);
}
