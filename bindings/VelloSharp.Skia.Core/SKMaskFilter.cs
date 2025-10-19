using System;

namespace SkiaSharp;

[Flags]
public enum SKBlurMaskFilterFlags
{
    None = 0,
    IgnoreTransform = 1,
    HighQuality = 2,
}

public enum SKBlurStyle
{
    Normal,
    Solid,
    Outer,
    Inner,
}

public sealed class SKMaskFilter : IDisposable
{
    private const float BlurSigmaScale = 0.57735f;
    public const int TableMaxLength = 256;

    private bool _disposed;

    private SKMaskFilter()
    {
    }

    public static float ConvertRadiusToSigma(float radius) =>
        radius > 0f ? (BlurSigmaScale * radius) + 0.5f : 0f;

    public static float ConvertSigmaToRadius(float sigma) =>
        sigma > 0.5f ? (sigma - 0.5f) / BlurSigmaScale : 0f;

    public static SKMaskFilter CreateBlur(SKBlurStyle blurStyle, float sigma) =>
        CreateBlur(blurStyle, sigma, respectCTM: true);

    public static SKMaskFilter CreateBlur(SKBlurStyle blurStyle, float sigma, bool respectCTM) =>
        ThrowNotSupported($"{nameof(SKMaskFilter)}.{nameof(CreateBlur)}", $"{blurStyle} (respect CTM: {respectCTM})");

    public static SKMaskFilter CreateBlur(SKBlurStyle blurStyle, float sigma, SKBlurMaskFilterFlags flags) =>
        ThrowNotSupported($"{nameof(SKMaskFilter)}.{nameof(CreateBlur)}", $"{blurStyle} ({flags})");

    public static SKMaskFilter CreateBlur(SKBlurStyle blurStyle, float sigma, SKRect occluder) =>
        ThrowNotSupported($"{nameof(SKMaskFilter)}.{nameof(CreateBlur)}", $"{blurStyle} (occluder)");

    public static SKMaskFilter CreateBlur(SKBlurStyle blurStyle, float sigma, SKRect occluder, SKBlurMaskFilterFlags flags) =>
        ThrowNotSupported($"{nameof(SKMaskFilter)}.{nameof(CreateBlur)}", $"{blurStyle} (occluder, {flags})");

    public static SKMaskFilter CreateBlur(SKBlurStyle blurStyle, float sigma, SKRect occluder, bool respectCTM) =>
        ThrowNotSupported($"{nameof(SKMaskFilter)}.{nameof(CreateBlur)}", $"{blurStyle} (occluder, respect CTM: {respectCTM})");

    public static SKMaskFilter CreateTable(byte[] table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return CreateTable(table.AsSpan());
    }

    public static SKMaskFilter CreateTable(ReadOnlySpan<byte> table)
    {
        if (table.Length != TableMaxLength)
        {
            throw new ArgumentException($"Table must contain {TableMaxLength} entries.", nameof(table));
        }

        return ThrowNotSupported($"{nameof(SKMaskFilter)}.{nameof(CreateTable)}");
    }

    public static SKMaskFilter CreateGamma(float gamma) =>
        ThrowNotSupported($"{nameof(SKMaskFilter)}.{nameof(CreateGamma)}");

    public static SKMaskFilter CreateClip(byte min, byte max) =>
        ThrowNotSupported($"{nameof(SKMaskFilter)}.{nameof(CreateClip)}");

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static SKMaskFilter ThrowNotSupported(string memberName, string? details = null)
    {
        ShimNotImplemented.Throw(memberName, details);
        throw new NotSupportedException($"TODO: {memberName}{(string.IsNullOrWhiteSpace(details) ? string.Empty : $" ({details})")}");
    }
}
