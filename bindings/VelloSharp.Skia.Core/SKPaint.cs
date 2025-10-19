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
    }

    public bool GetFillPath(SKPath source, SKPath destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ThrowIfDisposed();

        destination.Reset();

        SKPath? fillPath = null;
        SKPath? strokePath = null;
        SKPath? combinedPath = null;

        try
        {
            if (Style != SKPaintStyle.Stroke)
            {
                fillPath = new SKPath(source)
                {
                    FillType = source.FillType,
                };
            }

            if (Style != SKPaintStyle.Fill && StrokeWidth > 0f)
            {
                strokePath = ComputeStrokePath(source) ?? new SKPath(source)
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
        if (Shader is { } shader)
        {
            return shader.CreateBrush(this);
        }
        return CreateSolidColorBrush();
    }

    private PaintBrush CreateSolidColorBrush()
    {
        var alpha = Math.Clamp(Opacity, 0f, 1f);
        var color = Color.ToRgbaColor();
        var brush = new SolidColorBrush(new RgbaColor(color.R, color.G, color.B, color.A * alpha));
        return new PaintBrush(brush, null);
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
        using var nativeElements = VelloSharp.NativePathElements.Rent(builder);
        var span = nativeElements.Span;
        if (span.IsEmpty)
        {
            return null;
        }

        var stroke = CreateStrokeStyle();

        IntPtr handle = IntPtr.Zero;
        try
        {
            unsafe
            {
                fixed (VelloSharp.VelloPathElement* elementPtr = span)
                {
                    VelloStatus status;

                    if (stroke.DashPattern is { Length: > 0 } pattern)
                    {
                        fixed (double* dashPtr = pattern)
                        {
                            var nativeStyle = StrokeInterop.Create(stroke, (IntPtr)dashPtr, (nuint)pattern.Length);
                            status = NativeMethods.vello_path_stroke_to_fill(
                                elementPtr,
                                (nuint)span.Length,
                                nativeStyle,
                                0.25,
                                out handle);
                        }
                    }
                    else
                    {
                        var nativeStyle = StrokeInterop.Create(stroke, IntPtr.Zero, 0);
                        status = NativeMethods.vello_path_stroke_to_fill(
                            elementPtr,
                            (nuint)span.Length,
                            nativeStyle,
                            0.25,
                            out handle);
                    }

                    if (status != VelloStatus.Success || handle == IntPtr.Zero)
                    {
                        return null;
                    }
                }
            }

            var path = PathOps.CreatePathFromCommandList(handle, SKPathFillType.Winding);
            return path;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
        catch (DllNotFoundException)
        {
            return null;
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                NativeMethods.vello_path_command_list_destroy(handle);
            }
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
