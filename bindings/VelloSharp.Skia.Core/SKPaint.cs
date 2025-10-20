using System;
using SkiaSharpShim;
using VelloSharp;

namespace SkiaSharp;

public enum SKPaintStyle
{
    Fill,
    Stroke,
    StrokeAndFill,
}

public enum SKStrokeCap
{
    Butt,
    Round,
    Square,
}

public enum SKStrokeJoin
{
    Miter,
    Round,
    Bevel,
}

public sealed class SKPaint : IDisposable
{
    private bool _disposed;

    public SKPaintStyle Style { get; set; } = SKPaintStyle.Fill;
    public SKStrokeCap StrokeCap { get; set; } = SKStrokeCap.Butt;
    public SKStrokeJoin StrokeJoin { get; set; } = SKStrokeJoin.Miter;
    public float StrokeWidth { get; set; } = 1f;
    public float StrokeMiter { get; set; } = 4f;
    public bool IsAntialias { get; set; } = true;
    public float TextSize { get; set; } = 16f;
    public SKColor Color { get; set; } = new(0, 0, 0, 255);
    public SKTypeface? Typeface { get; set; }
    public SKTextAlign TextAlign { get; set; } = SKTextAlign.Left;
    public float TextScaleX { get; set; } = 1f;
    public float TextSkewX { get; set; } = 0f;
    public bool IsFakeBoldText { get; set; }
    public float Opacity { get; set; } = 1f;
    public SKColorF ColorF
    {
        get => Color;
        set => Color = value;
    }
    public SKShader? Shader { get; set; }
    public SKBlendMode BlendMode { get; set; } = SKBlendMode.SrcOver;
    public SKImageFilter? ImageFilter { get; set; }
    public SKPathEffect? PathEffect { get; set; }
    public SKMaskFilter? MaskFilter { get; set; }
    public SKColorFilter? ColorFilter { get; set; }
    public SKBlender? Blender { get; set; }
    public SKFilterQuality FilterQuality { get; set; } = SKFilterQuality.None;

    public bool IsStroke
    {
        get => Style == SKPaintStyle.Stroke || Style == SKPaintStyle.StrokeAndFill;
        set => Style = value ? SKPaintStyle.Stroke : SKPaintStyle.Fill;
    }

    public void Dispose() => _disposed = true;

    public void Reset()
    {
        ThrowIfDisposed();
        Style = SKPaintStyle.Fill;
        StrokeCap = SKStrokeCap.Butt;
        StrokeJoin = SKStrokeJoin.Miter;
        StrokeWidth = 1f;
        StrokeMiter = 4f;
        IsAntialias = true;
        TextSize = 16f;
        Color = new SKColor(0, 0, 0, 255);
        Typeface = null;
        Opacity = 1f;
        Shader = null;
        BlendMode = SKBlendMode.SrcOver;
        ImageFilter = null;
        PathEffect = null;
        MaskFilter = null;
        ColorFilter = null;
        Blender = null;
        FilterQuality = SKFilterQuality.None;
        TextAlign = SKTextAlign.Left;
        TextScaleX = 1f;
        TextSkewX = 0f;
        IsFakeBoldText = false;
    }

    public float MeasureText(string text)
    {
        ThrowIfDisposed();
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        using var font = CreateMeasurementFont();
        return font.MeasureText(text.AsSpan(), this);
    }

    public float MeasureText(ReadOnlySpan<char> text)
    {
        ThrowIfDisposed();
        using var font = CreateMeasurementFont();
        return font.MeasureText(text, this);
    }

    public float MeasureText(string text, out SKRect bounds)
    {
        ThrowIfDisposed();
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        using var font = CreateMeasurementFont();
        return font.MeasureText(text.AsSpan(), out bounds, this);
    }

    public float MeasureText(ReadOnlySpan<char> text, out SKRect bounds)
    {
        ThrowIfDisposed();
        using var font = CreateMeasurementFont();
        return font.MeasureText(text, out bounds, this);
    }

    public bool GetFillPath(SKPath source, SKPath destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ThrowIfDisposed();

        destination.Reset();

        SKPath? effectedPath = null;
        SKPath sourcePath = source;
        if (PathEffect is { } pathEffect && pathEffect.TryApply(source, out var transformed))
        {
            effectedPath = transformed;
            sourcePath = transformed;
        }

        SKPath? fillPath = null;
        SKPath? strokePath = null;
        SKPath? combinedPath = null;

        try
        {
            if (Style != SKPaintStyle.Stroke)
            {
                fillPath = new SKPath(sourcePath)
                {
                    FillType = sourcePath.FillType,
                };
            }

            if (Style != SKPaintStyle.Fill && StrokeWidth > 0f)
            {
                strokePath = ComputeStrokePath(sourcePath) ?? new SKPath(sourcePath)
                {
                    FillType = SKPathFillType.Winding,
                };
            }

            if (Style == SKPaintStyle.Fill)
            {
                combinedPath = fillPath;
            }
            else if (Style == SKPaintStyle.Stroke)
            {
                combinedPath = strokePath;
            }
            else
            {
                if (fillPath is not null && strokePath is not null)
                {
                    combinedPath = PathOps.Compute(fillPath, strokePath, SKPathOp.Union);
                    if (combinedPath is null)
                    {
                        combinedPath = new SKPath(fillPath)
                        {
                            FillType = SKPathFillType.Winding,
                        };
                        combinedPath.AddPath(strokePath);
                    }
                }
                else
                {
                    combinedPath = fillPath ?? strokePath;
                }
            }

            if (combinedPath is null)
            {
                return false;
            }

            destination.FillType = combinedPath.FillType;
            destination.AddPath(combinedPath);

            var success = !destination.IsEmpty;

            if (combinedPath != fillPath && combinedPath != strokePath)
            {
                combinedPath.Dispose();
            }

            return success;
        }
        finally
        {
            fillPath?.Dispose();
            strokePath?.Dispose();
            effectedPath?.Dispose();
        }
    }

    internal void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKPaint));
        }
    }

    internal PaintBrush CreateBrush()
    {
        ThrowIfDisposed();
        var brush = Shader is { } shader
            ? shader.CreateBrush(this)
            : CreateSolidColorBrush();

        if (ColorFilter is { } filter)
        {
            brush = filter.Apply(brush);
        }

        return brush;
    }

    private PaintBrush CreateSolidColorBrush()
    {
        var alpha = Math.Clamp(Opacity, 0f, 1f);
        var color = Color.ToRgbaColor();
        var brush = new SolidColorBrush(new RgbaColor(color.R, color.G, color.B, color.A * alpha));
        return new PaintBrush(brush, null);
    }

    private SKFont CreateMeasurementFont()
    {
        var typeface = Typeface ?? SKTypeface.Default;
        var textSize = TextSize > 0f ? TextSize : 16f;
        return new SKFont(typeface, textSize)
        {
            LinearMetrics = true,
            Embolden = false,
            ScaleX = 1f,
            SkewX = 0f,
        };
    }

    internal StrokeStyle CreateStrokeStyle()
    {
        ThrowIfDisposed();
        return new StrokeStyle
        {
            Width = Math.Max(0.1, StrokeWidth),
            MiterLimit = Math.Max(1.0, StrokeMiter),
            StartCap = ToLineCap(StrokeCap),
            EndCap = ToLineCap(StrokeCap),
            LineJoin = ToLineJoin(StrokeJoin),
        };
    }

    private SKPath? ComputeStrokePath(SKPath source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var builder = source.ToPathBuilder();
        var elements = builder.AsSpan();
        if (elements.IsEmpty)
        {
            return null;
        }

        var stroke = CreateStrokeStyle();

        var kurboElements = elements.AsKurboSpan();

        nint inputHandle;
        unsafe
        {
            fixed (KurboPathElement* elementPtr = kurboElements)
            {
                inputHandle = KurboNativeMethods.kurbo_bez_path_from_elements(elementPtr, (nuint)kurboElements.Length);
            }
        }

        NativeHelpers.ThrowIfNull(inputHandle, "kurbo_bez_path_from_elements failed", KurboNativeMethods.kurbo_last_error_message);

        try
        {
            nint strokedHandle;
            KurboStatus status;

            if (stroke.DashPattern is { Length: > 0 } pattern)
            {
                unsafe
                {
                    fixed (double* dashPtr = pattern)
                    {
                        var nativeStyle = KurboStrokeInterop.Create(stroke, (IntPtr)dashPtr, (nuint)pattern.Length);
                        status = KurboNativeMethods.kurbo_bez_path_stroke(inputHandle, nativeStyle, 0.25, out strokedHandle);
                    }
                }
            }
            else
            {
                var nativeStyle = KurboStrokeInterop.Create(stroke, IntPtr.Zero, 0);
                status = KurboNativeMethods.kurbo_bez_path_stroke(inputHandle, nativeStyle, 0.25, out strokedHandle);
            }

            NativeHelpers.ThrowOnError(status, "kurbo_bez_path_stroke failed");
            NativeHelpers.ThrowIfNull(strokedHandle, "kurbo_bez_path_stroke returned null", KurboNativeMethods.kurbo_last_error_message);

            try
            {
                var path = KurboPathEffects.CreatePathFromHandle(strokedHandle, SKPathFillType.Winding);
                return path;
            }
            finally
            {
                KurboNativeMethods.kurbo_bez_path_destroy(strokedHandle);
            }
        }
        finally
        {
            KurboNativeMethods.kurbo_bez_path_destroy(inputHandle);
        }
    }

    private static LineCap ToLineCap(SKStrokeCap cap) => cap switch
    {
        SKStrokeCap.Butt => LineCap.Butt,
        SKStrokeCap.Round => LineCap.Round,
        SKStrokeCap.Square => LineCap.Square,
        _ => LineCap.Butt,
    };

    private static LineJoin ToLineJoin(SKStrokeJoin join) => join switch
    {
        SKStrokeJoin.Miter => LineJoin.Miter,
        SKStrokeJoin.Round => LineJoin.Round,
        SKStrokeJoin.Bevel => LineJoin.Bevel,
        _ => LineJoin.Miter,
    };
}
