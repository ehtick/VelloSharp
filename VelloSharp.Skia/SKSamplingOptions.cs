namespace SkiaSharp;

public readonly record struct SKSamplingOptions
{
    public SKSamplingOptions(bool useHighQuality)
    {
        UseHighQuality = useHighQuality;
    }

    public bool UseHighQuality { get; }

    public static SKSamplingOptions Default => new(false);
}
