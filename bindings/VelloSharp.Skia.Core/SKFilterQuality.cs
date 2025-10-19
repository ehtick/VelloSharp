using System;

namespace SkiaSharp;

public enum SKFilterQuality
{
    None,
    Low,
    Medium,
    High,
}

internal static class SKFilterQualityExtensions
{
    public static SKSamplingOptions ToSamplingOptions(this SKFilterQuality quality) => quality switch
    {
        SKFilterQuality.None => new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None),
        SKFilterQuality.Low => new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None),
        SKFilterQuality.Medium => new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear),
        SKFilterQuality.High => new SKSamplingOptions(SKCubicResampler.Mitchell),
        _ => new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None),
    };
}
