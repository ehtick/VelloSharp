using System;

namespace SkiaSharp;

public sealed class SKImageFilter : IDisposable
{
    private enum SKImageFilterType
    {
        Blur,
        DropShadow,
    }

    private readonly SKImageFilterType _type;
    private readonly BlurParameters _blur;
    private readonly DropShadowParameters _dropShadow;
    private bool _disposed;

    private SKImageFilter(SKImageFilterType type, BlurParameters blur, DropShadowParameters dropShadow)
    {
        _type = type;
        _blur = blur;
        _dropShadow = dropShadow;
    }

    public static SKImageFilter CreateBlur(float sigmaX, float sigmaY)
    {
        var blur = new BlurParameters(
            MathF.Max(0f, sigmaX),
            MathF.Max(0f, sigmaY));
        return new SKImageFilter(SKImageFilterType.Blur, blur, default);
    }

    public static SKImageFilter CreateDropShadow(float dx, float dy, float sigmaX, float sigmaY, SKColor color)
    {
        var dropShadow = new DropShadowParameters(
            dx,
            dy,
            MathF.Max(0f, sigmaX),
            MathF.Max(0f, sigmaY),
            color);
        return new SKImageFilter(SKImageFilterType.DropShadow, default, dropShadow);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    internal bool TryGetBlur(out BlurParameters parameters)
    {
        ThrowIfDisposed();
        if (_type == SKImageFilterType.Blur)
        {
            parameters = _blur;
            return true;
        }

        parameters = default;
        return false;
    }

    internal bool TryGetDropShadow(out DropShadowParameters parameters)
    {
        ThrowIfDisposed();
        if (_type == SKImageFilterType.DropShadow)
        {
            parameters = _dropShadow;
            return true;
        }

        parameters = default;
        return false;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKImageFilter));
        }
    }

    internal readonly record struct BlurParameters(float SigmaX, float SigmaY);

    internal readonly record struct DropShadowParameters(
        float Dx,
        float Dy,
        float SigmaX,
        float SigmaY,
        SKColor Color);
}
