using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using VelloSharp;

namespace SkiaSharp;

public enum SKShaderTileMode
{
    Clamp,
    Repeat,
    Mirror,
    Decal,
}

public sealed class SKShader : IDisposable
{
    private enum ShaderKind
    {
        Solid,
        Linear,
        Radial,
        TwoPointConical,
        Sweep,
        Compose,
        ColorFilter,
        Image,
    }

    private readonly ShaderKind _kind;
    private readonly object _data;
    private bool _disposed;

    private SKShader(ShaderKind kind, object data)
    {
        _kind = kind;
        _data = data;
    }

    public static SKShader CreateColor(SKColor color)
        => new(ShaderKind.Solid, color);

    public static SKShader CreateLinearGradient(
        SKPoint start,
        SKPoint end,
        SKColor[] colors,
        float[]? colorPos,
        SKShaderTileMode tileMode,
        SKMatrix? localMatrix = null)
    {
        ValidateStops(colors, colorPos, out var stops);
        return new SKShader(ShaderKind.Linear, new LinearData(start, end, colors.ToArray(), stops, tileMode, localMatrix));
    }

    public static SKShader CreateLinearGradient(
        SKPoint start,
        SKPoint end,
        SKColor[] colors,
        float[]? colorPos,
        SKShaderTileMode tileMode)
        => CreateLinearGradient(start, end, colors, colorPos, tileMode, null);

    public static SKShader CreateRadialGradient(
        SKPoint center,
        float radius,
        SKColor[] colors,
        float[]? colorPos,
        SKShaderTileMode tileMode,
        SKMatrix? localMatrix = null)
    {
        ValidateStops(colors, colorPos, out var stops);
        return new SKShader(ShaderKind.Radial, new RadialData(center, radius, colors.ToArray(), stops, tileMode, localMatrix));
    }

    public static SKShader CreateRadialGradient(
        SKPoint center,
        float radius,
        SKColor[] colors,
        float[]? colorPos,
        SKShaderTileMode tileMode)
        => CreateRadialGradient(center, radius, colors, colorPos, tileMode, null);

    public static SKShader CreateTwoPointConicalGradient(
        SKPoint start,
        float startRadius,
        SKPoint end,
        float endRadius,
        SKColor[] colors,
        float[]? colorPos,
        SKShaderTileMode tileMode,
        SKMatrix? localMatrix = null)
    {
        ValidateStops(colors, colorPos, out var stops);
        return new SKShader(ShaderKind.TwoPointConical, new TwoPointData(start, startRadius, end, endRadius, colors.ToArray(), stops, tileMode, localMatrix));
    }

    public static SKShader CreateTwoPointConicalGradient(
        SKPoint start,
        float startRadius,
        SKPoint end,
        float endRadius,
        SKColor[] colors,
        float[]? colorPos,
        SKShaderTileMode tileMode)
        => CreateTwoPointConicalGradient(start, startRadius, end, endRadius, colors, colorPos, tileMode, null);

    public static SKShader CreateSweepGradient(
        SKPoint center,
        SKColor[] colors,
        float[]? colorPos,
        SKMatrix? localMatrix = null)
    {
        ValidateStops(colors, colorPos, out var stops);
        return new SKShader(ShaderKind.Sweep, new SweepData(center, colors.ToArray(), stops, SKShaderTileMode.Clamp, 0f, 360f, localMatrix));
    }

    public static SKShader CreateCompose(SKShader outer, SKShader inner)
    {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(inner);
        return new SKShader(ShaderKind.Compose, new ComposeData(outer, inner));
    }

    public static SKShader CreateBitmap(SKBitmap bitmap, SKShaderTileMode tileModeX, SKShaderTileMode tileModeY) =>
        CreateBitmap(bitmap, tileModeX, tileModeY, SKSamplingOptions.Default, SKMatrix.CreateIdentity());

    public static SKShader CreateBitmap(SKBitmap bitmap, SKShaderTileMode tileModeX, SKShaderTileMode tileModeY, SKSamplingOptions sampling) =>
        CreateBitmap(bitmap, tileModeX, tileModeY, sampling, SKMatrix.CreateIdentity());

    public static SKShader CreateBitmap(SKBitmap bitmap, SKShaderTileMode tileModeX, SKShaderTileMode tileModeY, SKSamplingOptions sampling, SKMatrix localMatrix)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var image = SKImage.FromBitmap(bitmap) ?? throw new InvalidOperationException("Unable to create shader – bitmap could not be converted to an image.");
        var tileRect = SKRect.Create(0, 0, image.Width, image.Height);
        return CreateImageShader(image, tileModeX, tileModeY, localMatrix, tileRect, sampling, takeOwnership: true);
    }

    private static T ThrowNotSupported<T>(string memberName, string? details = null)
    {
        ShimNotImplemented.Throw(memberName, details);
        var suffix = string.IsNullOrWhiteSpace(details) ? string.Empty : $" ({details})";
        throw new NotSupportedException($"TODO: {memberName}{suffix}");
    }

    public static SKShader CreateEmpty() => CreateColor(SKColors.Transparent);

    public static SKShader CreateColor(SKColorF color, SKColorSpace colorspace)
    {
        ArgumentNullException.ThrowIfNull(colorspace);
        return CreateColor(color.ToColor());
    }

    public static SKShader CreateBitmap(SKBitmap src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKMatrix localMatrix)
    {
        ArgumentNullException.ThrowIfNull(src);
        return CreateBitmap(src, tmx, tmy, SKSamplingOptions.Default, localMatrix);
    }

    public static SKShader CreateImage(SKImage src)
    {
        ArgumentNullException.ThrowIfNull(src);
        return CreateImage(src, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
    }

    public static SKShader CreateImage(SKImage src, SKShaderTileMode tmx, SKShaderTileMode tmy)
    {
        ArgumentNullException.ThrowIfNull(src);
        return CreateImage(src, tmx, tmy, SKSamplingOptions.Default, SKMatrix.CreateIdentity());
    }

    public static SKShader CreateImage(SKImage src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKSamplingOptions sampling)
    {
        ArgumentNullException.ThrowIfNull(src);
        return CreateImage(src, tmx, tmy, sampling, SKMatrix.CreateIdentity());
    }

    public static SKShader CreateImage(SKImage src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKFilterQuality quality) =>
        CreateImage(src, tmx, tmy, quality.ToSamplingOptions());

    public static SKShader CreateImage(SKImage src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKMatrix localMatrix)
    {
        ArgumentNullException.ThrowIfNull(src);
        return CreateImage(src, tmx, tmy, SKSamplingOptions.Default, localMatrix);
    }

    public static SKShader CreateImage(SKImage src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKSamplingOptions sampling, SKMatrix localMatrix)
    {
        ArgumentNullException.ThrowIfNull(src);
        return CreateImageShader(src, tmx, tmy, localMatrix, SKRect.Create(0, 0, src.Width, src.Height), sampling, takeOwnership: false);
    }

    public static SKShader CreateImage(SKImage src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKFilterQuality quality, SKMatrix localMatrix) =>
        CreateImage(src, tmx, tmy, quality.ToSamplingOptions(), localMatrix);

    public static SKShader CreatePicture(SKPicture src) =>
        CreatePicture(src, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);

    public static SKShader CreatePicture(SKPicture src, SKShaderTileMode tmx, SKShaderTileMode tmy)
    {
        ArgumentNullException.ThrowIfNull(src);
        var tile = src.CullRect;
        return src.ToShader(tmx, tmy, SKMatrix.CreateIdentity(), tile);
    }

    public static SKShader CreatePicture(SKPicture src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKRect tile)
    {
        ArgumentNullException.ThrowIfNull(src);
        return src.ToShader(tmx, tmy, SKMatrix.CreateIdentity(), tile);
    }

    public static SKShader CreatePicture(SKPicture src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKFilterMode filterMode) =>
        CreatePicture(src, tmx, tmy);

    public static SKShader CreatePicture(SKPicture src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKFilterMode filterMode, SKRect tile) =>
        CreatePicture(src, tmx, tmy, tile);

    public static SKShader CreatePicture(SKPicture src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKMatrix localMatrix, SKRect tile)
    {
        ArgumentNullException.ThrowIfNull(src);
        return src.ToShader(tmx, tmy, localMatrix, tile);
    }

    public static SKShader CreatePicture(SKPicture src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKFilterMode filterMode, SKMatrix localMatrix, SKRect tile) =>
        CreatePicture(src, tmx, tmy, localMatrix, tile);

    public static SKShader CreateLinearGradient(SKPoint start, SKPoint end, SKColor[] colors, SKShaderTileMode mode) =>
        CreateLinearGradient(start, end, colors, null, mode);

    public static SKShader CreateLinearGradient(SKPoint start, SKPoint end, SKColor[] colors, SKShaderTileMode mode, SKMatrix localMatrix) =>
        CreateLinearGradient(start, end, colors, null, mode, localMatrix);

    public static SKShader CreateLinearGradient(SKPoint start, SKPoint end, SKColorF[] colors, SKColorSpace? colorspace, SKShaderTileMode mode) =>
        CreateLinearGradient(start, end, colors, colorspace, null, mode);

    public static SKShader CreateLinearGradient(SKPoint start, SKPoint end, SKColorF[] colors, SKColorSpace? colorspace, float[]? colorPos, SKShaderTileMode mode)
    {
        ArgumentNullException.ThrowIfNull(colors);
        var solidColors = colors.Select(c => c.ToColor()).ToArray();
        return CreateLinearGradient(start, end, solidColors, colorPos, mode);
    }

    public static SKShader CreateLinearGradient(SKPoint start, SKPoint end, SKColorF[] colors, SKColorSpace? colorspace, float[]? colorPos, SKShaderTileMode mode, SKMatrix localMatrix)
    {
        ArgumentNullException.ThrowIfNull(colors);
        var solidColors = colors.Select(c => c.ToColor()).ToArray();
        return CreateLinearGradient(start, end, solidColors, colorPos, mode, localMatrix);
    }

    public static SKShader CreateRadialGradient(SKPoint center, float radius, SKColor[] colors, SKShaderTileMode mode) =>
        CreateRadialGradient(center, radius, colors, null, mode);

    public static SKShader CreateRadialGradient(SKPoint center, float radius, SKColor[] colors, SKShaderTileMode mode, SKMatrix localMatrix) =>
        CreateRadialGradient(center, radius, colors, null, mode, localMatrix);

    public static SKShader CreateRadialGradient(SKPoint center, float radius, SKColorF[] colors, SKColorSpace? colorspace, SKShaderTileMode mode) =>
        CreateRadialGradient(center, radius, colors, colorspace, null, mode);

    public static SKShader CreateRadialGradient(SKPoint center, float radius, SKColorF[] colors, SKColorSpace? colorspace, float[]? colorPos, SKShaderTileMode mode)
    {
        ArgumentNullException.ThrowIfNull(colors);
        var solidColors = colors.Select(c => c.ToColor()).ToArray();
        return CreateRadialGradient(center, radius, solidColors, colorPos, mode);
    }

    public static SKShader CreateRadialGradient(SKPoint center, float radius, SKColorF[] colors, SKColorSpace? colorspace, float[]? colorPos, SKShaderTileMode mode, SKMatrix localMatrix)
    {
        ArgumentNullException.ThrowIfNull(colors);
        var solidColors = colors.Select(c => c.ToColor()).ToArray();
        return CreateRadialGradient(center, radius, solidColors, colorPos, mode, localMatrix);
    }

    public static SKShader CreateSweepGradient(SKPoint center, SKColor[] colors) =>
        CreateSweepGradient(center, colors, null, (SKMatrix?)null);

    public static SKShader CreateSweepGradient(SKPoint center, SKColor[] colors, float[]? colorPos) =>
        CreateSweepGradient(center, colors, colorPos, (SKMatrix?)null);

    public static SKShader CreateSweepGradient(SKPoint center, SKColor[] colors, float[]? colorPos, SKMatrix localMatrix) =>
        CreateSweepGradient(center, colors, colorPos, (SKMatrix?)localMatrix);

    public static SKShader CreateSweepGradient(SKPoint center, SKColor[] colors, float[]? colorPos, SKShaderTileMode tileMode, float startAngle, float endAngle) =>
        CreateSweepGradient(center, colors, colorPos, tileMode, startAngle, endAngle, SKMatrix.CreateIdentity());

    public static SKShader CreateSweepGradient(SKPoint center, SKColor[] colors, float[]? colorPos, SKShaderTileMode tileMode, float startAngle, float endAngle, SKMatrix localMatrix)
    {
        ValidateStops(colors, colorPos, out var stops);
        return new SKShader(ShaderKind.Sweep, new SweepData(center, colors.ToArray(), stops, tileMode, startAngle, endAngle, localMatrix));
    }

    public static SKShader CreateSweepGradient(SKPoint center, SKColorF[] colors, SKColorSpace? colorspace) =>
        CreateSweepGradient(center, colors, colorspace, null);

    public static SKShader CreateSweepGradient(SKPoint center, SKColorF[] colors, SKColorSpace? colorspace, float[]? colorPos) =>
        CreateSweepGradient(center, colors, colorspace, colorPos, SKShaderTileMode.Clamp, 0f, 360f);

    public static SKShader CreateSweepGradient(SKPoint center, SKColorF[] colors, SKColorSpace? colorspace, float[]? colorPos, SKMatrix localMatrix) =>
        CreateSweepGradient(center, colors, colorspace, colorPos, SKShaderTileMode.Clamp, 0f, 360f, localMatrix);

    public static SKShader CreateSweepGradient(SKPoint center, SKColorF[] colors, SKColorSpace? colorspace, SKShaderTileMode tileMode, float startAngle, float endAngle) =>
        CreateSweepGradient(center, colors, colorspace, null, tileMode, startAngle, endAngle);

    public static SKShader CreateSweepGradient(SKPoint center, SKColorF[] colors, SKColorSpace? colorspace, float[]? colorPos, SKShaderTileMode tileMode, float startAngle, float endAngle)
    {
        ArgumentNullException.ThrowIfNull(colors);
        var solidColors = colors.Select(c => c.ToColor()).ToArray();
        return CreateSweepGradient(center, solidColors, colorPos, tileMode, startAngle, endAngle);
    }

    public static SKShader CreateSweepGradient(SKPoint center, SKColorF[] colors, SKColorSpace? colorspace, float[]? colorPos, SKShaderTileMode tileMode, float startAngle, float endAngle, SKMatrix localMatrix)
    {
        ArgumentNullException.ThrowIfNull(colors);
        var solidColors = colors.Select(c => c.ToColor()).ToArray();
        return CreateSweepGradient(center, solidColors, colorPos, tileMode, startAngle, endAngle, localMatrix);
    }

    public static SKShader CreateTwoPointConicalGradient(SKPoint start, float startRadius, SKPoint end, float endRadius, SKColor[] colors, SKShaderTileMode mode) =>
        CreateTwoPointConicalGradient(start, startRadius, end, endRadius, colors, null, mode);

    public static SKShader CreateTwoPointConicalGradient(SKPoint start, float startRadius, SKPoint end, float endRadius, SKColor[] colors, SKShaderTileMode mode, SKMatrix localMatrix) =>
        CreateTwoPointConicalGradient(start, startRadius, end, endRadius, colors, null, mode, localMatrix);

    public static SKShader CreateTwoPointConicalGradient(SKPoint start, float startRadius, SKPoint end, float endRadius, SKColorF[] colors, SKColorSpace? colorspace, SKShaderTileMode mode) =>
        CreateTwoPointConicalGradient(start, startRadius, end, endRadius, colors, colorspace, null, mode);

    public static SKShader CreateTwoPointConicalGradient(SKPoint start, float startRadius, SKPoint end, float endRadius, SKColorF[] colors, SKColorSpace? colorspace, float[]? colorPos, SKShaderTileMode mode)
    {
        ArgumentNullException.ThrowIfNull(colors);
        var solidColors = colors.Select(c => c.ToColor()).ToArray();
        return CreateTwoPointConicalGradient(start, startRadius, end, endRadius, solidColors, colorPos, mode);
    }

    public static SKShader CreateTwoPointConicalGradient(SKPoint start, float startRadius, SKPoint end, float endRadius, SKColorF[] colors, SKColorSpace? colorspace, float[]? colorPos, SKShaderTileMode mode, SKMatrix localMatrix)
    {
        ArgumentNullException.ThrowIfNull(colors);
        var solidColors = colors.Select(c => c.ToColor()).ToArray();
        return CreateTwoPointConicalGradient(start, startRadius, end, endRadius, solidColors, colorPos, mode, localMatrix);
    }

    public static SKShader CreatePerlinNoiseFractalNoise(float baseFrequencyX, float baseFrequencyY, int numOctaves, float seed) =>
        ThrowNotSupported<SKShader>($"{nameof(SKShader)}.{nameof(CreatePerlinNoiseFractalNoise)}");

    public static SKShader CreatePerlinNoiseFractalNoise(float baseFrequencyX, float baseFrequencyY, int numOctaves, float seed, SKPointI tileSize) =>
        CreatePerlinNoiseFractalNoise(baseFrequencyX, baseFrequencyY, numOctaves, seed, new SKSizeI(tileSize.X, tileSize.Y));

    public static SKShader CreatePerlinNoiseFractalNoise(float baseFrequencyX, float baseFrequencyY, int numOctaves, float seed, SKSizeI tileSize) =>
        ThrowNotSupported<SKShader>($"{nameof(SKShader)}.{nameof(CreatePerlinNoiseFractalNoise)}", "perlin noise");

    public static SKShader CreatePerlinNoiseTurbulence(float baseFrequencyX, float baseFrequencyY, int numOctaves, float seed) =>
        ThrowNotSupported<SKShader>($"{nameof(SKShader)}.{nameof(CreatePerlinNoiseTurbulence)}");

    public static SKShader CreatePerlinNoiseTurbulence(float baseFrequencyX, float baseFrequencyY, int numOctaves, float seed, SKPointI tileSize) =>
        CreatePerlinNoiseTurbulence(baseFrequencyX, baseFrequencyY, numOctaves, seed, new SKSizeI(tileSize.X, tileSize.Y));

    public static SKShader CreatePerlinNoiseTurbulence(float baseFrequencyX, float baseFrequencyY, int numOctaves, float seed, SKSizeI tileSize) =>
        ThrowNotSupported<SKShader>($"{nameof(SKShader)}.{nameof(CreatePerlinNoiseTurbulence)}", "perlin noise");

    public static SKShader CreateCompose(SKShader shaderA, SKShader shaderB, SKBlendMode mode)
    {
        ArgumentNullException.ThrowIfNull(shaderA);
        ArgumentNullException.ThrowIfNull(shaderB);
        if (mode != SKBlendMode.SrcOver)
        {
            return ThrowNotSupported<SKShader>($"{nameof(SKShader)}.{nameof(CreateCompose)}", $"blend mode {mode}");
        }

        return CreateCompose(shaderA, shaderB);
    }

    public static SKShader CreateBlend(SKBlendMode mode, SKShader shaderA, SKShader shaderB)
    {
        ArgumentNullException.ThrowIfNull(shaderA);
        ArgumentNullException.ThrowIfNull(shaderB);
        if (mode == SKBlendMode.SrcOver)
        {
            return CreateCompose(shaderA, shaderB);
        }

        return ThrowNotSupported<SKShader>($"{nameof(SKShader)}.{nameof(CreateBlend)}", $"blend mode {mode}");
    }

    public static SKShader CreateBlend(SKBlender blender, SKShader shaderA, SKShader shaderB) =>
        ThrowNotSupported<SKShader>($"{nameof(SKShader)}.{nameof(CreateBlend)}", "custom blender");

    public static SKShader CreateColorFilter(SKShader shader, SKColorFilter filter)
    {
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(filter);
        shader.ThrowIfDisposed();
        filter.ThrowIfDisposed();
        return new SKShader(ShaderKind.ColorFilter, new ColorFilterData(shader, filter));
    }

    public static SKShader CreateLocalMatrix(SKShader shader, SKMatrix localMatrix)
    {
        ArgumentNullException.ThrowIfNull(shader);
        return ThrowNotSupported<SKShader>($"{nameof(SKShader)}.{nameof(CreateLocalMatrix)}");
    }

    public SKShader WithColorFilter(SKColorFilter? filter)
    {
        ThrowIfDisposed();
        if (filter is null)
        {
            return this;
        }

        filter.ThrowIfDisposed();
        return CreateColorFilter(this, filter);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_data is ComposeData compose)
        {
            compose.Outer.Dispose();
            compose.Inner.Dispose();
        }
        else if (_data is ColorFilterData colorFilter)
        {
            colorFilter.Shader.Dispose();
            colorFilter.Filter.Dispose();
        }
        else if (_data is ImageData image && image.OwnsImage)
        {
            image.Image.Dispose();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    internal PaintBrush CreateBrush(SKPaint paint)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKShader));
        }

        return _kind switch
        {
            ShaderKind.Solid => CreateSolidBrush((SKColor)_data, paint.Opacity),
            ShaderKind.Linear => CreateLinearBrush((LinearData)_data, paint.Opacity),
            ShaderKind.Radial => CreateRadialBrush((RadialData)_data, paint.Opacity),
            ShaderKind.TwoPointConical => CreateTwoPointBrush((TwoPointData)_data, paint.Opacity),
            ShaderKind.Sweep => CreateSweepBrush((SweepData)_data, paint.Opacity),
            ShaderKind.Compose => ((ComposeData)_data).Inner.CreateBrush(paint),
            ShaderKind.ColorFilter => CreateFilteredBrush((ColorFilterData)_data, paint),
            ShaderKind.Image => CreateImageBrush((ImageData)_data, paint.Opacity),
            _ => CreateSolidBrush(paint.Color, paint.Opacity),
        };
    }

    private static PaintBrush CreateSolidBrush(SKColor color, float opacity)
    {
        var brush = new SolidColorBrush(GetColorWithOpacity(color, opacity).ToRgbaColor());
        return new PaintBrush(brush, null);
    }

    private static PaintBrush CreateLinearBrush(LinearData data, float opacity)
    {
        var (start, end) = ApplyMatrix(data.Start, data.End, data.LocalMatrix);
        var extend = ToExtendMode(data.TileMode);
        var stops = BuildStops(data.Colors, data.Stops, opacity);
        return new PaintBrush(new LinearGradientBrush(start, end, stops, extend), null);
    }

    private static PaintBrush CreateRadialBrush(RadialData data, float opacity)
    {
        var (center, _) = ApplyMatrix(data.Center, data.Center, data.LocalMatrix);
        var extend = ToExtendMode(data.TileMode);
        var stops = BuildStops(data.Colors, data.Stops, opacity);
        return new PaintBrush(new RadialGradientBrush(center, 0f, center, Math.Max(data.Radius, 0.0001f), stops, extend), null);
    }

    private static PaintBrush CreateTwoPointBrush(TwoPointData data, float opacity)
    {
        var (start, end) = ApplyMatrix(data.Start, data.End, data.LocalMatrix);
        var extend = ToExtendMode(data.TileMode);
        var stops = BuildStops(data.Colors, data.Stops, opacity);
        var brush = new RadialGradientBrush(start, Math.Max(data.StartRadius, 0f), end, Math.Max(data.EndRadius, 0.0001f), stops, extend);
        return new PaintBrush(brush, null);
    }

    private static PaintBrush CreateSweepBrush(SweepData data, float opacity)
    {
        var matrix = data.LocalMatrix?.ToMatrix3x2() ?? Matrix3x2.Identity;
        var center = Vector2.Transform(data.Center.ToVector2(), matrix);

        var rotationRadians = MathF.Atan2(matrix.M21, matrix.M11);
        var rotationDegrees = rotationRadians * 180f / MathF.PI;
        var startAngle = data.StartAngle + rotationDegrees;
        var endAngle = data.EndAngle + rotationDegrees;
        if (Math.Abs(endAngle - startAngle) < float.Epsilon)
        {
            endAngle = startAngle + 360f;
        }

        var extend = ToExtendMode(data.TileMode);
        var stops = BuildStops(data.Colors, data.Stops, opacity);
        return new PaintBrush(new SweepGradientBrush(center, startAngle, endAngle, stops, extend), null);
    }

    private static PaintBrush CreateImageBrush(ImageData data, float opacity)
    {
        var brush = new ImageBrush(data.Image.Image)
        {
            XExtend = ToExtendMode(data.TileModeX),
            YExtend = ToExtendMode(data.TileModeY),
            Alpha = Math.Clamp(opacity, 0f, 1f),
            Quality = data.Sampling.ToBrushQuality(),
        };

        var transform = data.LocalMatrix.ToMatrix3x2();
        if (data.TileRect.Left != 0 || data.TileRect.Top != 0)
        {
            transform = Matrix3x2.CreateTranslation(data.TileRect.Left, data.TileRect.Top) * transform;
        }

        return new PaintBrush(brush, transform);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKShader));
        }
    }

    private static (Vector2 Start, Vector2 End) ApplyMatrix(SKPoint startPoint, SKPoint endPoint, SKMatrix? matrix)
    {
        var start = startPoint.ToVector2();
        var end = endPoint.ToVector2();
        if (matrix.HasValue)
        {
            var m = matrix.Value.ToMatrix3x2();
            start = Vector2.Transform(start, m);
            end = Vector2.Transform(end, m);
        }
        return (start, end);
    }

    private static GradientStop[] BuildStops(SKColor[] colors, float[] stops, float opacity)
    {
        var paintAlpha = Math.Clamp(opacity, 0f, 1f);
        var gradientStops = new GradientStop[colors.Length];
        for (var i = 0; i < colors.Length; i++)
        {
            var rgba = colors[i].ToRgbaColor();
            gradientStops[i] = new GradientStop(stops[i], new RgbaColor(rgba.R, rgba.G, rgba.B, rgba.A * paintAlpha));
        }
        return gradientStops;
    }

    private static PaintBrush CreateFilteredBrush(ColorFilterData data, SKPaint paint)
    {
        var baseBrush = data.Shader.CreateBrush(paint);
        return data.Filter.Apply(baseBrush);
    }

    private static SKColor GetColorWithOpacity(SKColor color, float opacity)
    {
        var alpha = (byte)Math.Clamp(color.Alpha * opacity, 0, 255);
        return new SKColor(color.Red, color.Green, color.Blue, alpha);
    }

    private static void ValidateStops(SKColor[] colors, float[]? positions, out float[] stops)
    {
        ArgumentNullException.ThrowIfNull(colors);
        if (colors.Length == 0)
        {
            throw new ArgumentException("At least one gradient colour is required.", nameof(colors));
        }

        if (positions is null || positions.Length == 0)
        {
            stops = Enumerable.Range(0, colors.Length)
                .Select(i => colors.Length == 1 ? 0f : (float)i / (colors.Length - 1))
                .ToArray();
        }
        else
        {
            if (positions.Length != colors.Length)
            {
                throw new ArgumentException("Colour position array must match colours length.", nameof(positions));
            }
            stops = positions.ToArray();
        }
    }

    private static ExtendMode ToExtendMode(SKShaderTileMode mode) => mode switch
    {
        SKShaderTileMode.Clamp => ExtendMode.Pad,
        SKShaderTileMode.Repeat => ExtendMode.Repeat,
        SKShaderTileMode.Mirror => ExtendMode.Reflect,
        SKShaderTileMode.Decal => ExtendMode.Pad,
        _ => ExtendMode.Pad,
    };

    internal static SKShader CreateImageShader(
        SKImage image,
        SKShaderTileMode tileModeX,
        SKShaderTileMode tileModeY,
        SKMatrix localMatrix,
        SKRect tileRect,
        SKSamplingOptions sampling = default,
        bool takeOwnership = true)
    {
        ArgumentNullException.ThrowIfNull(image);
        var data = new ImageData(image, tileModeX, tileModeY, localMatrix, tileRect, sampling, takeOwnership);
        return new SKShader(ShaderKind.Image, data);
    }

    private readonly record struct LinearData(SKPoint Start, SKPoint End, SKColor[] Colors, float[] Stops, SKShaderTileMode TileMode, SKMatrix? LocalMatrix);
    private readonly record struct RadialData(SKPoint Center, float Radius, SKColor[] Colors, float[] Stops, SKShaderTileMode TileMode, SKMatrix? LocalMatrix);
    private readonly record struct TwoPointData(SKPoint Start, float StartRadius, SKPoint End, float EndRadius, SKColor[] Colors, float[] Stops, SKShaderTileMode TileMode, SKMatrix? LocalMatrix);
    private readonly record struct SweepData(SKPoint Center, SKColor[] Colors, float[] Stops, SKShaderTileMode TileMode, float StartAngle, float EndAngle, SKMatrix? LocalMatrix);
    private readonly record struct ComposeData(SKShader Outer, SKShader Inner);
    private sealed record ColorFilterData(SKShader Shader, SKColorFilter Filter);
    private sealed record ImageData(SKImage Image, SKShaderTileMode TileModeX, SKShaderTileMode TileModeY, SKMatrix LocalMatrix, SKRect TileRect, SKSamplingOptions Sampling, bool OwnsImage);
}
