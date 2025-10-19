using System;

namespace SkiaSharp;

public enum SKPath1DPathEffectStyle
{
    Translate,
    Rotate,
    Morph,
}

public enum SKTrimPathEffectMode
{
    Normal,
    Inverted,
}

public sealed class SKPathEffect : IDisposable
{
    private bool _disposed;

    private SKPathEffect()
    {
    }

    public static SKPathEffect CreateCompose(SKPathEffect outer, SKPathEffect inner)
    {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(inner);
        return ThrowNotSupported($"{nameof(SKPathEffect)}.{nameof(CreateCompose)}");
    }

    public static SKPathEffect CreateSum(SKPathEffect first, SKPathEffect second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return ThrowNotSupported($"{nameof(SKPathEffect)}.{nameof(CreateSum)}");
    }

    public static SKPathEffect CreateDiscrete(float segLength, float deviation, uint seedAssist = 0) =>
        ThrowNotSupported($"{nameof(SKPathEffect)}.{nameof(CreateDiscrete)}");

    public static SKPathEffect CreateCorner(float radius) =>
        ThrowNotSupported($"{nameof(SKPathEffect)}.{nameof(CreateCorner)}");

    public static SKPathEffect Create1DPath(SKPath path, float advance, float phase, SKPath1DPathEffectStyle style)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ThrowNotSupported($"{nameof(SKPathEffect)}.{nameof(Create1DPath)}", style.ToString());
    }

    public static SKPathEffect Create2DLine(float width, SKMatrix matrix) =>
        ThrowNotSupported($"{nameof(SKPathEffect)}.{nameof(Create2DLine)}");

    public static SKPathEffect Create2DPath(SKMatrix matrix, SKPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ThrowNotSupported($"{nameof(SKPathEffect)}.{nameof(Create2DPath)}");
    }

    public static SKPathEffect CreateDash(float[] intervals, float phase)
    {
        ArgumentNullException.ThrowIfNull(intervals);
        if (intervals.Length == 0 || intervals.Length % 2 != 0)
        {
            throw new ArgumentException("Intervals must contain an even, non-zero number of entries.", nameof(intervals));
        }

        return ThrowNotSupported($"{nameof(SKPathEffect)}.{nameof(CreateDash)}");
    }

    public static SKPathEffect CreateTrim(float start, float stop) =>
        CreateTrim(start, stop, SKTrimPathEffectMode.Normal);

    public static SKPathEffect CreateTrim(float start, float stop, SKTrimPathEffectMode mode) =>
        ThrowNotSupported($"{nameof(SKPathEffect)}.{nameof(CreateTrim)}", mode.ToString());

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static SKPathEffect ThrowNotSupported(string memberName, string? details = null)
    {
        ShimNotImplemented.Throw(memberName, details);
        throw new NotSupportedException($"TODO: {memberName}{(string.IsNullOrWhiteSpace(details) ? string.Empty : $" ({details})")}");
    }
}
