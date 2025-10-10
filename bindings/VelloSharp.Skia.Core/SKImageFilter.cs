using System;

namespace SkiaSharp;

public sealed class SKImageFilter : IDisposable
{
    private SKImageFilter()
    {
    }

    public static SKImageFilter CreateBlur(float sigmaX, float sigmaY)
    {
        ShimNotImplemented.Throw($"{nameof(SKImageFilter)}.{nameof(CreateBlur)}", $"σx={sigmaX}, σy={sigmaY}");
        return new SKImageFilter();
    }

    public static SKImageFilter CreateDropShadow(float dx, float dy, float sigmaX, float sigmaY, SKColor color)
    {
        _ = color;
        ShimNotImplemented.Throw($"{nameof(SKImageFilter)}.{nameof(CreateDropShadow)}", $"dx={dx}, dy={dy}, σx={sigmaX}, σy={sigmaY}");
        return new SKImageFilter();
    }

    public void Dispose()
    {
        // TODO: release native image filter resources when Vello exposes the feature.
    }
}
