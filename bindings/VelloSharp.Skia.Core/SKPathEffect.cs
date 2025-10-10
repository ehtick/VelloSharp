using System;

namespace SkiaSharp;

public sealed class SKPathEffect : IDisposable
{
    private SKPathEffect()
    {
    }

    public static SKPathEffect CreateDash(float[] intervals, float phase)
    {
        ArgumentNullException.ThrowIfNull(intervals);
        ShimNotImplemented.Throw($"{nameof(SKPathEffect)}.{nameof(CreateDash)}", "dash path effects");
        _ = phase;
        return new SKPathEffect();
    }

    public void Dispose()
    {
        // TODO: implement disposal when path effects become available through Vello.
    }
}
