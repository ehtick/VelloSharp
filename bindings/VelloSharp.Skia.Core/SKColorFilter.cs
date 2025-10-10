using System;

namespace SkiaSharp;

public sealed class SKColorFilter : IDisposable
{
    private SKColorFilter()
    {
    }

    public static SKColorFilter CreateTable(byte[] tableA, byte[] tableR, byte[] tableG, byte[] tableB)
    {
        ArgumentNullException.ThrowIfNull(tableA);
        ArgumentNullException.ThrowIfNull(tableR);
        ArgumentNullException.ThrowIfNull(tableG);
        ArgumentNullException.ThrowIfNull(tableB);
        ShimNotImplemented.Throw($"{nameof(SKColorFilter)}.{nameof(CreateTable)}", "color table creation");
        return new SKColorFilter();
    }

    public static SKColorFilter CreateBlendMode(SKColor color, SKBlendMode mode)
    {
        ShimNotImplemented.Throw($"{nameof(SKColorFilter)}.{nameof(CreateBlendMode)}", "blend mode filters");
        _ = color;
        _ = mode;
        return new SKColorFilter();
    }

    public void Dispose()
    {
        // TODO: integrate with Vello color filter lifetime management once available.
    }
}
