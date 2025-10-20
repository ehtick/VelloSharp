using System;
using VelloSharp;
using VelloSharp.Ffi.Gpu;

namespace SkiaSharp;

public sealed class SKImageFilter : IDisposable
{
    private enum SKImageFilterType
    {
        Blur,
        DropShadow,
        Blend,
        ColorMatrix,
    }

    private readonly SKImageFilterType _type;
    private readonly BlurParameters _blur;
    private readonly DropShadowParameters _dropShadow;
    private readonly LayerBlend _blend;
    private readonly float[]? _colorMatrix;
    private readonly VelloFilterHandle _handle;
    private bool _disposed;

    private SKImageFilter(
        SKImageFilterType type,
        BlurParameters blur,
        DropShadowParameters dropShadow,
        LayerBlend blend,
        float[]? colorMatrix,
        VelloFilterHandle handle)
    {
        _type = type;
        _blur = blur;
        _dropShadow = dropShadow;
        _blend = blend;
        _colorMatrix = colorMatrix;
        _handle = handle;
    }

    private static T ThrowNotSupported<T>(string memberName, string? details = null)
    {
        ShimNotImplemented.Throw(memberName, details);
        var suffix = string.IsNullOrWhiteSpace(details) ? string.Empty : $" ({details})";
        throw new NotSupportedException($"TODO: {memberName}{suffix}");
    }

    public static SKImageFilter CreateMatrix(in SKMatrix matrix) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateMatrix)}", "matrix transform");

    public static SKImageFilter CreateMatrix(in SKMatrix matrix, SKSamplingOptions sampling) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateMatrix)}", "matrix transform");

    public static SKImageFilter CreateMatrix(in SKMatrix matrix, SKFilterQuality quality) =>
        CreateMatrix(matrix, quality.ToSamplingOptions());

    public static SKImageFilter CreateMatrix(in SKMatrix matrix, SKSamplingOptions sampling, SKImageFilter? input) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateMatrix)}", "matrix transform");

    public static SKImageFilter CreateMatrix(in SKMatrix matrix, SKFilterQuality quality, SKImageFilter? input) =>
        CreateMatrix(matrix, quality.ToSamplingOptions(), input);

    public static SKImageFilter CreateBlur(float sigmaX, float sigmaY)
    {
        var blur = new BlurParameters(
            MathF.Max(0f, sigmaX),
            MathF.Max(0f, sigmaY));
        var handle = VelloFilterHandle.CreateBlur(blur.SigmaX, blur.SigmaY);
        return new SKImageFilter(SKImageFilterType.Blur, blur, default, default, null, handle);
    }

    public static SKImageFilter CreateDropShadow(float dx, float dy, float sigmaX, float sigmaY, SKColor color)
    {
        var dropShadow = new DropShadowParameters(
            dx,
            dy,
            MathF.Max(0f, sigmaX),
            MathF.Max(0f, sigmaY),
            color);
        var offset = new VelloPoint
        {
            X = dropShadow.Dx,
            Y = dropShadow.Dy,
        };
        var nativeColor = new VelloColor
        {
            R = color.Red / 255f,
            G = color.Green / 255f,
            B = color.Blue / 255f,
            A = color.Alpha / 255f,
        };
        var handle = VelloFilterHandle.CreateDropShadow(offset, dropShadow.SigmaX, dropShadow.SigmaY, nativeColor);
        return new SKImageFilter(SKImageFilterType.DropShadow, default, dropShadow, default, null, handle);
    }

    public static SKImageFilter CreateBlur(float sigmaX, float sigmaY, SKImageFilter? input) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateBlur)}", "chained blur");

    public static SKImageFilter CreateBlur(float sigmaX, float sigmaY, SKImageFilter? input, SKRect cropRect) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateBlur)}", "crop rect");

    public static SKImageFilter CreateBlur(float sigmaX, float sigmaY, SKShaderTileMode tileMode) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateBlur)}", $"tile mode {tileMode}");

    public static SKImageFilter CreateBlur(float sigmaX, float sigmaY, SKShaderTileMode tileMode, SKImageFilter? input) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateBlur)}", $"tile mode {tileMode}");

    public static SKImageFilter CreateBlur(float sigmaX, float sigmaY, SKShaderTileMode tileMode, SKImageFilter? input, SKRect cropRect) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateBlur)}", $"tile mode {tileMode} with crop");

    public static SKImageFilter CreateColorFilter(SKColorFilter cf)
    {
        ArgumentNullException.ThrowIfNull(cf);
        return CreateColorFilter(cf, input: null);
    }

    public static SKImageFilter CreateColorFilter(SKColorFilter cf, SKImageFilter? input)
    {
        ArgumentNullException.ThrowIfNull(cf);
        if (input is not null)
        {
            return ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateColorFilter)}", "chained color filter");
        }

        Span<float> matrixSpan = stackalloc float[SKColorFilter.ColorMatrixSize];
        if (!cf.TryCopyColorMatrix(matrixSpan))
        {
            return ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateColorFilter)}", "unsupported filter type");
        }

        var matrixCopy = matrixSpan.ToArray();
        var handle = VelloFilterHandle.CreateColorMatrix(matrixSpan);
        return new SKImageFilter(SKImageFilterType.ColorMatrix, default, default, default, matrixCopy, handle);
    }

    public static SKImageFilter CreateColorFilter(SKColorFilter cf, SKImageFilter? input, SKRect cropRect)
    {
        ArgumentNullException.ThrowIfNull(cf);
        return ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateColorFilter)}", "crop rect");
    }

    public static SKImageFilter CreateCompose(SKImageFilter outer, SKImageFilter inner)
    {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(inner);
        return ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateCompose)}");
    }

    public static SKImageFilter CreateDisplacementMapEffect(SKColorChannel xChannelSelector, SKColorChannel yChannelSelector, float scale, SKImageFilter displacement) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateDisplacementMapEffect)}", "displacement map");

    public static SKImageFilter CreateDisplacementMapEffect(SKColorChannel xChannelSelector, SKColorChannel yChannelSelector, float scale, SKImageFilter displacement, SKImageFilter? input) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateDisplacementMapEffect)}", "displacement map");

    public static SKImageFilter CreateDisplacementMapEffect(SKColorChannel xChannelSelector, SKColorChannel yChannelSelector, float scale, SKImageFilter displacement, SKImageFilter? input, SKRect cropRect) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateDisplacementMapEffect)}", "displacement map");

    public static SKImageFilter CreateDropShadow(float dx, float dy, float sigmaX, float sigmaY, SKColor color, SKImageFilter? input) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateDropShadow)}", "chained drop shadow");

    public static SKImageFilter CreateDropShadow(float dx, float dy, float sigmaX, float sigmaY, SKColor color, SKImageFilter? input, SKRect cropRect) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateDropShadow)}", "crop rect");

    public static SKImageFilter CreateDropShadow(float dx, float dy, float sigmaX, float sigmaY, SKColor color, SKDropShadowImageFilterShadowMode shadowMode, SKImageFilter? input, SKRect? cropRect = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateDropShadow)}", $"shadow mode {shadowMode}");

    public static SKImageFilter CreateDropShadowOnly(float dx, float dy, float sigmaX, float sigmaY, SKColor color, SKImageFilter? input, SKRect? cropRect = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateDropShadowOnly)}");

    public static SKImageFilter CreateDilate(int radiusX, int radiusY, SKImageFilter? input = null, SKRect? cropRect = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateDilate)}");

    public static SKImageFilter CreateErode(int radiusX, int radiusY, SKImageFilter? input = null, SKRect? cropRect = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateErode)}");

    public static SKImageFilter CreateMorphology(SKImageFilterMorphologyType morphologyType, int radiusX, int radiusY, SKImageFilter? input = null, SKRect? cropRect = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateMorphology)}", morphologyType.ToString());

    public static SKImageFilter CreateOffset(float dx, float dy, SKImageFilter? input = null, SKRect? cropRect = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateOffset)}");

    public static SKImageFilter CreatePicture(SKPicture picture) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreatePicture)}");

    public static SKImageFilter CreatePicture(SKPicture picture, SKRect cropRect) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreatePicture)}", "crop rect");

    public static SKImageFilter CreatePicture(SKPicture picture, SKMatrix localMatrix, SKRect cropRect) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreatePicture)}", "local matrix");

    public static SKImageFilter CreateShader(SKShader shader, SKImageFilter? input = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateShader)}");

    public static SKImageFilter CreateShader(SKShader shader, SKImageFilter? input, SKRect cropRect) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateShader)}", "crop rect");

    public static SKImageFilter CreateImage(SKImage image, SKRect srcRect, SKRect dstRect, SKSamplingOptions sampling, SKImageFilter? input = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateImage)}");

    public static SKImageFilter CreateImage(SKImage image, SKRect srcRect, SKRect dstRect, SKFilterQuality filterQuality) =>
        CreateImage(image, srcRect, dstRect, filterQuality.ToSamplingOptions());

    public static SKImageFilter CreateImage(SKImage image, SKRect srcRect, SKRect dstRect, SKFilterQuality filterQuality, SKImageFilter? input) =>
        CreateImage(image, srcRect, dstRect, filterQuality.ToSamplingOptions(), input);

    public static SKImageFilter CreateImage(SKImage image, SKRect srcRect, SKRect dstRect, SKSamplingOptions sampling, SKImageFilter? input, SKRect cropRect) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateImage)}", "crop rect");

    public static SKImageFilter CreateImage(SKImage image, SKRect srcRect, SKRect dstRect, SKFilterQuality filterQuality, SKImageFilter? input, SKRect cropRect) =>
        CreateImage(image, srcRect, dstRect, filterQuality.ToSamplingOptions(), input, cropRect);

    public static SKImageFilter CreateArithmetic(float k1, float k2, float k3, float k4, bool enforcePremul, SKImageFilter? background, SKImageFilter? foreground, SKRect? cropRect = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateArithmetic)}");

    public static SKImageFilter CreateBlendMode(SKBlendMode mode, SKImageFilter? background, SKImageFilter? foreground = null, SKRect? cropRect = null)
    {
        if (background is not null || foreground is not null || cropRect is not null)
        {
            return ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateBlendMode)}", "multiple inputs");
        }

        if (!TryConvertBlendMode(mode, out var blend))
        {
            return ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateBlendMode)}", mode.ToString());
        }

        var handle = VelloFilterHandle.CreateBlend(blend);
        return new SKImageFilter(SKImageFilterType.Blend, default, default, blend, null, handle);
    }

    public static SKImageFilter CreateBlendMode(SKBlender blender, SKImageFilter? background, SKImageFilter? foreground = null, SKRect? cropRect = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateBlendMode)}", "custom blender");

    public static SKImageFilter CreateMagnifier(SKRect lensBounds, float zoomAmount, float inset, SKSamplingOptions sampling, SKImageFilter? input = null, SKRect? cropRect = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateMagnifier)}");

    public static SKImageFilter CreateMatrixConvolution(SKSizeI kernelSize, float[] kernel, float gain, float bias, SKPointI kernelOffset, SKShaderTileMode tileMode, bool convolveAlpha, SKImageFilter? input = null, SKRect? cropRect = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateMatrixConvolution)}");

    public static SKImageFilter CreateTable(SKColorTable table, SKImageFilter? input = null, SKRect? cropRect = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateTable)}");

    public static SKImageFilter CreateTable(byte[]? tableA, byte[]? tableR, byte[]? tableG, byte[]? tableB, SKImageFilter? input = null, SKRect? cropRect = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateTable)}", "explicit tables");

    public static SKImageFilter CreateTile(SKRect src, SKRect dst, SKImageFilter? input = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateTile)}");

    public static SKImageFilter CreatePaint(SKPaint paint, SKImageFilter? input = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreatePaint)}");

    public static SKImageFilter CreateDistantLitDiffuse(SKPoint3 direction, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input = null, SKRect? cropRect = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateDistantLitDiffuse)}");

    public static SKImageFilter CreatePointLitDiffuse(SKPoint3 location, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input = null, SKRect? cropRect = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreatePointLitDiffuse)}");

    public static SKImageFilter CreateSpotLitDiffuse(SKPoint3 location, SKPoint3 target, float specularExponent, float cutoffAngle, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input = null, SKRect? cropRect = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateSpotLitDiffuse)}");

    public static SKImageFilter CreateDistantLitSpecular(SKPoint3 direction, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input = null, SKRect? cropRect = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateDistantLitSpecular)}");

    public static SKImageFilter CreatePointLitSpecular(SKPoint3 location, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input = null, SKRect? cropRect = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreatePointLitSpecular)}");

    public static SKImageFilter CreateSpotLitSpecular(SKPoint3 location, SKPoint3 target, float specularExponent, float cutoffAngle, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input = null, SKRect? cropRect = null) =>
        ThrowNotSupported<SKImageFilter>($"{nameof(SKImageFilter)}.{nameof(CreateSpotLitSpecular)}");

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _handle.Dispose();
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

    internal bool TryGetBlend(out LayerBlend blend)
    {
        ThrowIfDisposed();
        if (_type == SKImageFilterType.Blend)
        {
            blend = _blend;
            return true;
        }

        blend = default;
        return false;
    }

    internal bool TryCopyColorMatrix(Span<float> destination)
    {
        ThrowIfDisposed();
        if (_type == SKImageFilterType.ColorMatrix && _colorMatrix is { } matrix)
        {
            if (destination.Length < matrix.Length)
            {
                throw new ArgumentException($"Destination span must contain at least {matrix.Length} elements.", nameof(destination));
            }

            matrix.CopyTo(destination);
            return true;
        }

        if (destination.Length >= SKColorFilter.ColorMatrixSize)
        {
            destination[..SKColorFilter.ColorMatrixSize].Clear();
        }

        return false;
    }

    internal VelloFilterHandle CloneNativeHandle()
    {
        ThrowIfDisposed();
        return _handle.Clone();
    }

    private static bool TryConvertBlendMode(SKBlendMode mode, out LayerBlend blend)
    {
        switch (mode)
        {
            case SKBlendMode.Clear:
                blend = new LayerBlend(LayerMix.Normal, LayerCompose.Clear);
                return true;
            case SKBlendMode.Src:
                blend = new LayerBlend(LayerMix.Normal, LayerCompose.Copy);
                return true;
            case SKBlendMode.Dst:
                blend = new LayerBlend(LayerMix.Normal, LayerCompose.Dest);
                return true;
            case SKBlendMode.SrcOver:
                blend = new LayerBlend(LayerMix.Normal, LayerCompose.SrcOver);
                return true;
            case SKBlendMode.DstOver:
                blend = new LayerBlend(LayerMix.Normal, LayerCompose.DestOver);
                return true;
            case SKBlendMode.SrcIn:
                blend = new LayerBlend(LayerMix.Normal, LayerCompose.SrcIn);
                return true;
            case SKBlendMode.DstIn:
                blend = new LayerBlend(LayerMix.Normal, LayerCompose.DestIn);
                return true;
            case SKBlendMode.SrcOut:
                blend = new LayerBlend(LayerMix.Normal, LayerCompose.SrcOut);
                return true;
            case SKBlendMode.DstOut:
                blend = new LayerBlend(LayerMix.Normal, LayerCompose.DestOut);
                return true;
            case SKBlendMode.SrcATop:
                blend = new LayerBlend(LayerMix.Normal, LayerCompose.SrcAtop);
                return true;
            case SKBlendMode.DstATop:
                blend = new LayerBlend(LayerMix.Normal, LayerCompose.DestAtop);
                return true;
            case SKBlendMode.Xor:
                blend = new LayerBlend(LayerMix.Normal, LayerCompose.Xor);
                return true;
            case SKBlendMode.Plus:
                blend = new LayerBlend(LayerMix.Normal, LayerCompose.Plus);
                return true;
            case SKBlendMode.Modulate:
            case SKBlendMode.Multiply:
                blend = new LayerBlend(LayerMix.Multiply, LayerCompose.SrcOver);
                return true;
            case SKBlendMode.Screen:
                blend = new LayerBlend(LayerMix.Screen, LayerCompose.SrcOver);
                return true;
            case SKBlendMode.Overlay:
                blend = new LayerBlend(LayerMix.Overlay, LayerCompose.SrcOver);
                return true;
            case SKBlendMode.Darken:
                blend = new LayerBlend(LayerMix.Darken, LayerCompose.SrcOver);
                return true;
            case SKBlendMode.Lighten:
                blend = new LayerBlend(LayerMix.Lighten, LayerCompose.SrcOver);
                return true;
            case SKBlendMode.ColorDodge:
                blend = new LayerBlend(LayerMix.ColorDodge, LayerCompose.SrcOver);
                return true;
            case SKBlendMode.ColorBurn:
                blend = new LayerBlend(LayerMix.ColorBurn, LayerCompose.SrcOver);
                return true;
            case SKBlendMode.HardLight:
                blend = new LayerBlend(LayerMix.HardLight, LayerCompose.SrcOver);
                return true;
            case SKBlendMode.SoftLight:
                blend = new LayerBlend(LayerMix.SoftLight, LayerCompose.SrcOver);
                return true;
            case SKBlendMode.Difference:
                blend = new LayerBlend(LayerMix.Difference, LayerCompose.SrcOver);
                return true;
            case SKBlendMode.Exclusion:
                blend = new LayerBlend(LayerMix.Exclusion, LayerCompose.SrcOver);
                return true;
            case SKBlendMode.Hue:
                blend = new LayerBlend(LayerMix.Hue, LayerCompose.SrcOver);
                return true;
            case SKBlendMode.Saturation:
                blend = new LayerBlend(LayerMix.Saturation, LayerCompose.SrcOver);
                return true;
            case SKBlendMode.Color:
                blend = new LayerBlend(LayerMix.Color, LayerCompose.SrcOver);
                return true;
            case SKBlendMode.Luminosity:
                blend = new LayerBlend(LayerMix.Luminosity, LayerCompose.SrcOver);
                return true;
            default:
                blend = default;
                return false;
        }
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
