using System;

namespace SkiaSharp;

public sealed class SKColorSpace : IDisposable
{
    private SKColorSpace()
    {
    }

    public static SKColorSpace CreateSrgb()
    {
        ShimNotImplemented.Throw($"{nameof(SKColorSpace)}.{nameof(CreateSrgb)}");
        return new SKColorSpace();
    }

    public void Dispose()
    {
        // TODO: dispose Vello-backed color space resources when available.
    }
}
