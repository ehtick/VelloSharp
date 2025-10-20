using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using VelloSharp;

namespace SkiaSharp;

public sealed class SKColorFilter : IDisposable
{
    public const int ColorMatrixSize = 20;
    public const int TableMaxLength = 256;

    private readonly IReadOnlyList<IColorFilterEffect> _effects;
    private bool _disposed;

    private SKColorFilter(IReadOnlyList<IColorFilterEffect> effects)
    {
        _effects = effects;
    }

    public static SKColorFilter CreateSrgbToLinearGamma() =>
        ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateSrgbToLinearGamma)}", "gamma conversion");

    public static SKColorFilter CreateLinearToSrgbGamma() =>
        ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateLinearToSrgbGamma)}", "gamma conversion");

    public static SKColorFilter CreateBlendMode(SKColor color, SKBlendMode mode)
    {
        if (!BlendEffect.IsSupported(mode))
        {
            return ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateBlendMode)}", mode.ToString());
        }

        return new SKColorFilter(new IColorFilterEffect[] { new BlendEffect(color, mode) });
    }

    public static SKColorFilter CreateLighting(SKColor mul, SKColor add) =>
        ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateLighting)}");

    public static SKColorFilter CreateCompose(SKColorFilter outer, SKColorFilter inner)
    {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(inner);
        outer.ThrowIfDisposed();
        inner.ThrowIfDisposed();

        var effects = new List<IColorFilterEffect>(inner._effects.Count + outer._effects.Count);
        effects.AddRange(inner._effects);
        effects.AddRange(outer._effects);
        return new SKColorFilter(effects);
    }

    public static SKColorFilter CreateLerp(float weight, SKColorFilter filter0, SKColorFilter filter1)
    {
        ArgumentNullException.ThrowIfNull(filter0);
        ArgumentNullException.ThrowIfNull(filter1);
        return ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateLerp)}");
    }

    public static SKColorFilter CreateColorMatrix(float[] matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        return CreateColorMatrix(matrix.AsSpan());
    }

    public static SKColorFilter CreateColorMatrix(ReadOnlySpan<float> matrix)
    {
        if (matrix.Length != ColorMatrixSize)
        {
            throw new ArgumentException($"Matrix must contain {ColorMatrixSize} values.", nameof(matrix));
        }

        return new SKColorFilter(new IColorFilterEffect[] { new ColorMatrixEffect(matrix) });
    }

    public static SKColorFilter CreateHslaColorMatrix(ReadOnlySpan<float> matrix) =>
        CreateColorMatrix(matrix);

    public static SKColorFilter CreateLumaColor() =>
        ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateLumaColor)}");

    public static SKColorFilter CreateTable(byte[] table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return CreateTable(table.AsSpan());
    }

    public static SKColorFilter CreateTable(ReadOnlySpan<byte> table)
    {
        if (table.Length != TableMaxLength)
        {
            throw new ArgumentException($"Table must contain {TableMaxLength} entries.", nameof(table));
        }

        return ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateTable)}", "single table");
    }

    public static SKColorFilter CreateTable(byte[] tableA, byte[] tableR, byte[] tableG, byte[] tableB)
    {
        ArgumentNullException.ThrowIfNull(tableA);
        ArgumentNullException.ThrowIfNull(tableR);
        ArgumentNullException.ThrowIfNull(tableG);
        ArgumentNullException.ThrowIfNull(tableB);
        return CreateTable(tableA.AsSpan(), tableR.AsSpan(), tableG.AsSpan(), tableB.AsSpan());
    }

    public static SKColorFilter CreateTable(ReadOnlySpan<byte> tableA, ReadOnlySpan<byte> tableR, ReadOnlySpan<byte> tableG, ReadOnlySpan<byte> tableB)
    {
        ValidateTable(tableA, nameof(tableA));
        ValidateTable(tableR, nameof(tableR));
        ValidateTable(tableG, nameof(tableG));
        ValidateTable(tableB, nameof(tableB));
        return ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateTable)}", "ARGB tables");
    }

    public static SKColorFilter CreateHighContrast(SKHighContrastConfig config)
    {
        if (!config.IsValid)
        {
            throw new ArgumentException("High contrast configuration is invalid.", nameof(config));
        }

        return ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateHighContrast)}");
    }

    public static SKColorFilter CreateHighContrast(bool grayscale, SKHighContrastConfigInvertStyle invertStyle, float contrast) =>
        CreateHighContrast(new SKHighContrastConfig(grayscale, invertStyle, contrast));

    public static void EnsureStaticInstanceAreInitialized()
    {
        // Included for API parity with SkiaSharp; nothing to do until native filters are available.
    }

    internal bool TryCopyColorMatrix(Span<float> destination)
    {
        ThrowIfDisposed();

        if (destination.Length < ColorMatrixSize)
        {
            throw new ArgumentException($"Destination span must have capacity for {ColorMatrixSize} elements.", nameof(destination));
        }

        if (_effects.Count == 1 && _effects[0] is ColorMatrixEffect matrix)
        {
            matrix.CopyTo(destination);
            return true;
        }

        destination[..ColorMatrixSize].Clear();
        return false;
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

    internal PaintBrush Apply(PaintBrush brush)
    {
        ThrowIfDisposed();
        var current = brush;
        foreach (var effect in _effects)
        {
            current = effect.Apply(current);
        }
        return current;
    }

    internal void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKColorFilter));
        }
    }

    private static void ValidateTable(ReadOnlySpan<byte> table, string argumentName)
    {
        if (table.Length != TableMaxLength)
        {
            throw new ArgumentException($"Table must contain {TableMaxLength} entries.", argumentName);
        }
    }

    private static SKColorFilter ThrowNotSupported(string memberName, string? details = null)
    {
        ShimNotImplemented.Throw(memberName, details);
        var suffix = string.IsNullOrWhiteSpace(details) ? string.Empty : $" ({details})";
        throw new NotSupportedException($"TODO: {memberName}{suffix}");
    }

    private static PaintBrush ApplyColorTransform(PaintBrush brush, Func<RgbaColor, RgbaColor> transform)
    {
        Brush transformedBrush = brush.Brush switch
        {
            SolidColorBrush solid => new SolidColorBrush(transform(solid.Color)),
            LinearGradientBrush linear => LinearGradientBrush.FromSpan(linear.Start, linear.End, TransformStops(linear.Stops, transform), linear.Extend),
            RadialGradientBrush radial => RadialGradientBrush.FromSpan(radial.StartCenter, radial.StartRadius, radial.EndCenter, radial.EndRadius, TransformStops(radial.Stops, transform), radial.Extend),
            SweepGradientBrush sweep => SweepGradientBrush.FromSpan(sweep.Center, sweep.StartAngle, sweep.EndAngle, TransformStops(sweep.Stops, transform), sweep.Extend),
            ImageBrush image => TransformImageBrush(image, transform),
            _ => throw new NotSupportedException($"Unsupported brush type: {brush.Brush.GetType().FullName}"),
        };

        return new PaintBrush(transformedBrush, brush.Transform);
    }

    private static GradientStop[] TransformStops(ReadOnlySpan<GradientStop> stops, Func<RgbaColor, RgbaColor> transform)
    {
        var result = new GradientStop[stops.Length];
        for (var i = 0; i < stops.Length; i++)
        {
            var stop = stops[i];
            result[i] = GradientStop.At(stop.Offset, transform(stop.Color));
        }

        return result;
    }

    private static float Clamp01(float value) => MathF.Min(MathF.Max(value, 0f), 1f);

    private interface IColorFilterEffect
    {
        PaintBrush Apply(PaintBrush brush);
    }

    private sealed class ColorMatrixEffect : IColorFilterEffect
    {
        private readonly float[] _matrix;

        public ColorMatrixEffect(ReadOnlySpan<float> matrix)
        {
            _matrix = new float[ColorMatrixSize];
            matrix.CopyTo(_matrix);
        }

        public PaintBrush Apply(PaintBrush brush) => ApplyColorTransform(brush, Transform);

        private RgbaColor Transform(RgbaColor color)
        {
            var r = color.R;
            var g = color.G;
            var b = color.B;
            var a = color.A;
            var m = _matrix;

            var rr = Clamp01(m[0] * r + m[1] * g + m[2] * b + m[3] * a + m[4]);
            var gg = Clamp01(m[5] * r + m[6] * g + m[7] * b + m[8] * a + m[9]);
            var bb = Clamp01(m[10] * r + m[11] * g + m[12] * b + m[13] * a + m[14]);
            var aa = Clamp01(m[15] * r + m[16] * g + m[17] * b + m[18] * a + m[19]);

            return new RgbaColor(rr, gg, bb, aa);
        }

        public void CopyTo(Span<float> destination)
        {
            _matrix.AsSpan().CopyTo(destination);
        }
    }

    private sealed class BlendEffect : IColorFilterEffect
    {
        private readonly RgbaColor _source;
        private readonly SKBlendMode _mode;

        public BlendEffect(SKColor color, SKBlendMode mode)
        {
            _source = color.ToRgbaColor();
            _mode = mode;
        }

        public PaintBrush Apply(PaintBrush brush) => ApplyColorTransform(brush, Apply);

        private RgbaColor Apply(RgbaColor destination)
        {
            var srcPremul = Premultiply(_source, out var sa);
            var dstPremul = Premultiply(destination, out var da);

            var (premul, alpha) = Blend(srcPremul, sa, dstPremul, da, _mode);
            alpha = Clamp01(alpha);

            if (alpha <= 0f)
            {
                return new RgbaColor(0f, 0f, 0f, 0f);
            }

            var invAlpha = 1f / alpha;
            var color = premul * invAlpha;
            return new RgbaColor(Clamp01(color.X), Clamp01(color.Y), Clamp01(color.Z), alpha);
        }

        private static Vector3 Premultiply(RgbaColor color, out float alpha)
        {
            alpha = Clamp01(color.A);
            var clamped = new Vector3(Clamp01(color.R), Clamp01(color.G), Clamp01(color.B));
            return clamped * alpha;
        }

        private static (Vector3 Color, float Alpha) Blend(Vector3 src, float sa, Vector3 dst, float da, SKBlendMode mode)
        {
            var invSa = 1f - sa;
            var invDa = 1f - da;

            Vector3 color;
            float alpha;

            switch (mode)
            {
                case SKBlendMode.Clear:
                    color = Vector3.Zero;
                    alpha = 0f;
                    break;
                case SKBlendMode.Src:
                    color = src;
                    alpha = sa;
                    break;
                case SKBlendMode.Dst:
                    color = dst;
                    alpha = da;
                    break;
                case SKBlendMode.SrcOver:
                    color = src + dst * invSa;
                    alpha = sa + da * invSa;
                    break;
                case SKBlendMode.DstOver:
                    color = dst + src * invDa;
                    alpha = da + sa * invDa;
                    break;
                case SKBlendMode.SrcIn:
                    color = src * da;
                    alpha = sa * da;
                    break;
                case SKBlendMode.DstIn:
                    color = dst * sa;
                    alpha = da * sa;
                    break;
                case SKBlendMode.SrcOut:
                    color = src * invDa;
                    alpha = sa * invDa;
                    break;
                case SKBlendMode.DstOut:
                    color = dst * invSa;
                    alpha = da * invSa;
                    break;
                case SKBlendMode.SrcATop:
                    color = src * da + dst * invSa;
                    alpha = da;
                    break;
                case SKBlendMode.DstATop:
                    color = dst * sa + src * invDa;
                    alpha = sa;
                    break;
                case SKBlendMode.Xor:
                    color = src * invDa + dst * invSa;
                    alpha = sa * invDa + da * invSa;
                    break;
                default:
                    color = src;
                    alpha = sa;
                    break;
            }

            return (color, alpha);
        }

        internal static bool IsSupported(SKBlendMode mode) => mode is >= SKBlendMode.Clear and <= SKBlendMode.Xor;
    }

    private static ImageBrush TransformImageBrush(ImageBrush brush, Func<RgbaColor, RgbaColor> transform)
    {
        using var imageData = PenikoImageData.FromImage(brush.Image);
        var info = imageData.GetInfo();
        var width = info.Width;
        var height = info.Height;
        var stride = info.Stride;
        var byteCount = stride * height;

        if (width <= 0 || height <= 0 || stride <= 0 || byteCount <= 0)
        {
            return brush;
        }

        var rented = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            var pixels = rented.AsSpan(0, byteCount);
            imageData.CopyPixels(pixels);

            var format = info.Format;
            var alphaType = info.Alpha;
            var isPremultiplied = alphaType == PenikoImageAlphaType.AlphaPremultiplied;

            for (var y = 0; y < height; y++)
            {
                var row = pixels.Slice(y * stride);
                for (var x = 0; x < width; x++)
                {
                    var offset = x * 4;
                    var rByte = format == PenikoImageFormat.Rgba8 ? row[offset] : row[offset + 2];
                    var gByte = row[offset + 1];
                    var bByte = format == PenikoImageFormat.Rgba8 ? row[offset + 2] : row[offset];
                    var aByte = row[offset + 3];

                    var r = rByte / 255f;
                    var g = gByte / 255f;
                    var b = bByte / 255f;
                    var a = aByte / 255f;

                    if (isPremultiplied && a > 0f)
                    {
                        var invAlpha = 1f / a;
                        r *= invAlpha;
                        g *= invAlpha;
                        b *= invAlpha;
                    }

                    var transformed = transform(new RgbaColor(r, g, b, a));
                    var outAlpha = Clamp01(transformed.A);
                    var outR = Clamp01(transformed.R);
                    var outG = Clamp01(transformed.G);
                    var outB = Clamp01(transformed.B);

                    if (isPremultiplied)
                    {
                        outR *= outAlpha;
                        outG *= outAlpha;
                        outB *= outAlpha;
                    }

                    if (format == PenikoImageFormat.Rgba8)
                    {
                        row[offset] = ToByte(outR);
                        row[offset + 1] = ToByte(outG);
                        row[offset + 2] = ToByte(outB);
                    }
                    else
                    {
                        row[offset] = ToByte(outB);
                        row[offset + 1] = ToByte(outG);
                        row[offset + 2] = ToByte(outR);
                    }

                    row[offset + 3] = ToByte(outAlpha);
                }
            }

            var renderFormat = format == PenikoImageFormat.Rgba8 ? RenderFormat.Rgba8 : RenderFormat.Bgra8;
            var alphaMode = alphaType == PenikoImageAlphaType.AlphaPremultiplied
                ? ImageAlphaMode.Premultiplied
                : ImageAlphaMode.Straight;

            var image = Image.FromPixels(pixels[..byteCount], width, height, renderFormat, alphaMode, stride);
            var filtered = new ImageBrush(image)
            {
                XExtend = brush.XExtend,
                YExtend = brush.YExtend,
                Quality = brush.Quality,
                Alpha = brush.Alpha,
            };
            return filtered;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static byte ToByte(float value)
    {
        var clamped = Clamp01(value) * 255f;
        return (byte)Math.Clamp((int)MathF.Round(clamped), 0, 255);
    }
}
