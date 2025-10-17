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
        return new SKShader(ShaderKind.Sweep, new SweepData(center, colors.ToArray(), stops, SKShaderTileMode.Clamp, localMatrix));
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

    public SKShader WithColorFilter(SKColorFilter? filter)
    {
        ShimNotImplemented.Throw($"{nameof(SKShader)}.{nameof(WithColorFilter)}");
        _ = filter;
        return this;
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
        var startAngle = rotationRadians * 180f / MathF.PI;
        var endAngle = startAngle + 360f;

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
    private readonly record struct SweepData(SKPoint Center, SKColor[] Colors, float[] Stops, SKShaderTileMode TileMode, SKMatrix? LocalMatrix);
    private readonly record struct ComposeData(SKShader Outer, SKShader Inner);
    private sealed record ImageData(SKImage Image, SKShaderTileMode TileModeX, SKShaderTileMode TileModeY, SKMatrix LocalMatrix, SKRect TileRect, SKSamplingOptions Sampling, bool OwnsImage);
}
