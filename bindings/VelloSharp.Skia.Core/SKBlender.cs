using System;

namespace SkiaSharp;

public sealed class SKBlender : IDisposable
{
    private SKBlender()
    {
    }

    // TODO: Provide GPU-backed blend mode support.
    public static SKBlender CreateBlendMode(SKBlendMode mode) =>
        ThrowNotSupported($"{nameof(SKBlender)}.{nameof(CreateBlendMode)}", mode.ToString());

    // TODO: Provide arithmetic blending support.
    public static SKBlender CreateArithmetic(float k1, float k2, float k3, float k4, bool enforcePremulColor) =>
        ThrowNotSupported($"{nameof(SKBlender)}.{nameof(CreateArithmetic)}", "arithmetic blending");

    public void Dispose()
    {
        // No native resources yet.
    }

    private static SKBlender ThrowNotSupported(string memberName, string? details = null)
    {
        ShimNotImplemented.Throw(memberName, details);
        var suffix = string.IsNullOrWhiteSpace(details) ? string.Empty : $" ({details})";
        throw new NotSupportedException($"TODO: {memberName}{suffix}");
    }
}
