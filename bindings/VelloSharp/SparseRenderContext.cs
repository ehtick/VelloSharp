using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VelloSharp;

public sealed class SparseRenderContext : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    public SparseRenderContext(ushort width, ushort height)
    {
        if (width == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be non-zero.");
        }

        if (height == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be non-zero.");
        }

        _handle = SparseNativeMethods.vello_sparse_render_context_create(width, height);
        if (_handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                NativeHelpers.GetSparseLastErrorMessage() ?? "Failed to create sparse render context.");
        }

        var sizeStatus = SparseNativeMethods.vello_sparse_render_context_get_size(
            _handle,
            out var nativeWidth,
            out var nativeHeight);
        NativeHelpers.ThrowOnError(sizeStatus, "Sparse context size query failed");

        Width = nativeWidth;
        Height = nativeHeight;
    }

    ~SparseRenderContext()
    {
        Dispose(disposing: false);
    }

    public ushort Width { get; }

    public ushort Height { get; }

    public void Reset()
    {
        ThrowIfDisposed();
        var status = SparseNativeMethods.vello_sparse_render_context_reset(_handle);
        NativeHelpers.ThrowOnError(status, "Sparse render context reset failed");
    }

    public void Flush()
    {
        ThrowIfDisposed();
        var status = SparseNativeMethods.vello_sparse_render_context_flush(_handle);
        NativeHelpers.ThrowOnError(status, "Sparse render context flush failed");
    }

    public void SetFillRule(FillRule fillRule)
    {
        ThrowIfDisposed();
        var status = SparseNativeMethods.vello_sparse_render_context_set_fill_rule(
            _handle,
            (VelloFillRule)fillRule);
        NativeHelpers.ThrowOnError(status, "Sparse fill rule update failed");
    }

    public void SetTransform(Matrix3x2 transform)
    {
        ThrowIfDisposed();
        var status = SparseNativeMethods.vello_sparse_render_context_set_transform(
            _handle,
            transform.ToNativeAffine());
        NativeHelpers.ThrowOnError(status, "Sparse transform update failed");
    }

    public void ResetTransform()
    {
        ThrowIfDisposed();
        var status = SparseNativeMethods.vello_sparse_render_context_reset_transform(_handle);
        NativeHelpers.ThrowOnError(status, "Sparse transform reset failed");
    }

    public void SetPaintTransform(Matrix3x2 transform)
    {
        ThrowIfDisposed();
        var status = SparseNativeMethods.vello_sparse_render_context_set_paint_transform(
            _handle,
            transform.ToNativeAffine());
        NativeHelpers.ThrowOnError(status, "Sparse paint transform update failed");
    }

    public void ResetPaintTransform()
    {
        ThrowIfDisposed();
        var status = SparseNativeMethods.vello_sparse_render_context_reset_paint_transform(_handle);
        NativeHelpers.ThrowOnError(status, "Sparse paint transform reset failed");
    }

    public void SetAliasingThreshold(byte? threshold)
    {
        ThrowIfDisposed();
        var status = SparseNativeMethods.vello_sparse_render_context_set_aliasing_threshold(
            _handle,
            threshold.HasValue ? threshold.Value : -1);
        NativeHelpers.ThrowOnError(status, "Sparse aliasing threshold update failed");
    }

    public void SetSolidPaint(RgbaColor color)
    {
        ThrowIfDisposed();
        var status = SparseNativeMethods.vello_sparse_render_context_set_solid_paint(
            _handle,
            color.ToNative());
        NativeHelpers.ThrowOnError(status, "Sparse paint update failed");
    }

    public void SetStroke(StrokeStyle style)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(style);
        var pattern = style.DashPattern;
        if (pattern is { Length: > 0 })
        {
            unsafe
            {
                fixed (double* dashPtr = pattern)
                {
                    var nativeStyle = CreateStrokeStyle(style, (IntPtr)dashPtr, (nuint)pattern.Length);
                    var status = SparseNativeMethods.vello_sparse_render_context_set_stroke(
                        _handle,
                        nativeStyle);
                    NativeHelpers.ThrowOnError(status, "Sparse stroke update failed");
                }
            }
        }
        else
        {
            var nativeStyle = CreateStrokeStyle(style, IntPtr.Zero, 0);
            var status = SparseNativeMethods.vello_sparse_render_context_set_stroke(_handle, nativeStyle);
            NativeHelpers.ThrowOnError(status, "Sparse stroke update failed");
        }
    }

    public void FillPath(PathBuilder path, FillRule fillRule, Matrix3x2 transform, RgbaColor color)
    {
        ArgumentNullException.ThrowIfNull(path);
        FillPath(path, fillRule, transform, new SolidColorBrush(color), null);
    }

    public void FillPath(PathBuilder path, FillRule fillRule, Matrix3x2 transform, Brush brush, Matrix3x2? brushTransform = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(brush);

        using var nativePath = NativePathElements.Rent(path);
        var elements = nativePath.Span;
        if (elements.IsEmpty)
        {
            throw new ArgumentException("Path must contain at least one element.", nameof(path));
        }

        using var brushData = BrushNativeFactory.Create(brush);
        var stops = brushData.Stops;

        unsafe
        {
            fixed (VelloPathElement* elementPtr = elements)
            fixed (VelloGradientStop* stopPtr = stops)
            {
                var nativeBrush = PrepareBrushForInvocation(brushData.Brush, stops, stopPtr);
                var nativeTransform = transform.ToNativeAffine();

                VelloAffine* brushTransformPtr = null;
                VelloAffine brushTransformValue = default;
                if (brushTransform.HasValue)
                {
                    brushTransformValue = brushTransform.Value.ToNativeAffine();
                    brushTransformPtr = &brushTransformValue;
                }

                var fillStatus = SparseNativeMethods.vello_sparse_render_context_fill_path_brush(
                    _handle,
                    (VelloFillRule)fillRule,
                    nativeTransform,
                    nativeBrush,
                    brushTransformPtr,
                    elementPtr,
                    (nuint)elements.Length);
                NativeHelpers.ThrowOnError(fillStatus, "Sparse fill path failed");
            }
        }
    }

    public void StrokePath(PathBuilder path, StrokeStyle style, Matrix3x2 transform, RgbaColor color)
    {
        ArgumentNullException.ThrowIfNull(path);
        StrokePath(path, style, transform, new SolidColorBrush(color), null);
    }

    public void StrokePath(PathBuilder path, StrokeStyle style, Matrix3x2 transform, Brush brush, Matrix3x2? brushTransform = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(brush);

        using var nativePath = NativePathElements.Rent(path);
        var elements = nativePath.Span;
        if (elements.IsEmpty)
        {
            throw new ArgumentException("Path must contain at least one element.", nameof(path));
        }

        using var brushData = BrushNativeFactory.Create(brush);
        var stops = brushData.Stops;

        unsafe
        {
            fixed (VelloPathElement* elementPtr = elements)
            fixed (VelloGradientStop* stopPtr = stops)
            {
                var nativeBrush = PrepareBrushForInvocation(brushData.Brush, stops, stopPtr);
                var nativeTransform = transform.ToNativeAffine();

                VelloAffine* brushTransformPtr = null;
                VelloAffine brushTransformValue = default;
                if (brushTransform.HasValue)
                {
                    brushTransformValue = brushTransform.Value.ToNativeAffine();
                    brushTransformPtr = &brushTransformValue;
                }

                var nativeStroke = CreateStrokeStyle(style, IntPtr.Zero, 0);
                var pattern = style.DashPattern;
                if (pattern is { Length: > 0 })
                {
                    fixed (double* dashPtr = pattern)
                    {
                        nativeStroke = CreateStrokeStyle(style, (IntPtr)dashPtr, (nuint)pattern.Length);
                        var strokeStatus = SparseNativeMethods.vello_sparse_render_context_stroke_path_brush(
                            _handle,
                            nativeStroke,
                            nativeTransform,
                            nativeBrush,
                            brushTransformPtr,
                            elementPtr,
                            (nuint)elements.Length);
                        NativeHelpers.ThrowOnError(strokeStatus, "Sparse stroke path failed");
                        return;
                    }
                }

                var finalStatus = SparseNativeMethods.vello_sparse_render_context_stroke_path_brush(
                    _handle,
                    nativeStroke,
                    nativeTransform,
                    nativeBrush,
                    brushTransformPtr,
                    elementPtr,
                    (nuint)elements.Length);
                NativeHelpers.ThrowOnError(finalStatus, "Sparse stroke path failed");
            }
        }
    }

    public void PushLayer(PathBuilder clip, LayerBlend blend, Matrix3x2 transform, float alpha = 1f)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(clip);

        using var nativePath = NativePathElements.Rent(clip);
        var span = nativePath.Span;
        if (span.IsEmpty)
        {
            throw new ArgumentException("Clip path must contain at least one element.", nameof(clip));
        }

        unsafe
        {
            fixed (VelloPathElement* elementPtr = span)
            {
                var layer = new VelloLayerParams
                {
                    Mix = (VelloBlendMix)blend.Mix,
                    Compose = (VelloBlendCompose)blend.Compose,
                    Alpha = alpha,
                    Transform = transform.ToNativeAffine(),
                    ClipElements = (IntPtr)elementPtr,
                    ClipElementCount = (nuint)span.Length,
                };

                var status = SparseNativeMethods.vello_sparse_render_context_push_layer(_handle, ref layer);
                NativeHelpers.ThrowOnError(status, "Sparse push layer failed");
            }
        }
    }

    public void PopLayer()
    {
        ThrowIfDisposed();
        SparseNativeMethods.vello_sparse_render_context_pop_layer(_handle);
    }

    public void DrawImage(Image image, Matrix3x2 transform) => DrawImage(new ImageBrush(image), transform);

    public void DrawImage(ImageBrush brush, Matrix3x2 transform)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(brush);
        var status = SparseNativeMethods.vello_sparse_render_context_draw_image(
            _handle,
            brush.ToNative(),
            transform.ToNativeAffine());
        NativeHelpers.ThrowOnError(status, "Sparse draw image failed");
    }

    public void DrawGlyphRun(Font font, ReadOnlySpan<Glyph> glyphs, GlyphRunOptions options)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Brush);

        if (glyphs.IsEmpty)
        {
            return;
        }

        using var brushData = BrushNativeFactory.Create(options.Brush);
        var stops = brushData.Stops;

        var glyphArray = ArrayPool<VelloGlyph>.Shared.Rent(glyphs.Length);
        var glyphSpan = glyphArray.AsSpan(0, glyphs.Length);
        for (var i = 0; i < glyphs.Length; i++)
        {
            glyphSpan[i] = glyphs[i].ToNative();
        }

        try
        {
            unsafe
            {
                fixed (VelloGlyph* glyphPtr = glyphSpan)
                fixed (VelloGradientStop* stopPtr = stops)
                {
                    var nativeBrush = PrepareBrushForInvocation(brushData.Brush, stops, stopPtr);

                    var nativeOptions = new SparseNativeMethods.GlyphRunOptionsNative
                    {
                        Style = (VelloGlyphRunStyle)options.Style,
                        FontSize = options.FontSize,
                        Transform = options.Transform.ToNativeAffine(),
                        Brush = nativeBrush,
                        StrokeStyle = default,
                        GlyphTransform = IntPtr.Zero,
                        Hint = options.Hint,
                    };

                    VelloAffine glyphTransformValue = default;
                    if (options.GlyphTransform.HasValue)
                    {
                        glyphTransformValue = options.GlyphTransform.Value.ToNativeAffine();
                        nativeOptions.GlyphTransform = (IntPtr)(&glyphTransformValue);
                    }

                    if (options.Style == GlyphRunStyle.Stroke)
                    {
                        var stroke = options.Stroke ?? throw new ArgumentException("Stroke options must be provided when GlyphRunStyle.Stroke is used.", nameof(options));
                        if (stroke.DashPattern is { Length: > 0 } pattern)
                        {
                            fixed (double* dashPtr = pattern)
                            {
                                nativeOptions.StrokeStyle = CreateStrokeStyle(stroke, (IntPtr)dashPtr, (nuint)pattern.Length);
                                InvokeGlyphRun(font, glyphPtr, (nuint)glyphSpan.Length, nativeOptions);
                            }
                        }
                        else
                        {
                            nativeOptions.StrokeStyle = CreateStrokeStyle(stroke, IntPtr.Zero, 0);
                            InvokeGlyphRun(font, glyphPtr, (nuint)glyphSpan.Length, nativeOptions);
                        }
                    }
                    else
                    {
                        InvokeGlyphRun(font, glyphPtr, (nuint)glyphSpan.Length, nativeOptions);
                    }
                }
            }
        }
        finally
        {
            ArrayPool<VelloGlyph>.Shared.Return(glyphArray, clearArray: false);
        }
    }

    public void FillRect(double x, double y, double width, double height, RgbaColor color)
    {
        ThrowIfDisposed();
        if (width < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be non-negative.");
        }

        if (height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be non-negative.");
        }

        var rect = new VelloRect
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
        };
        var status = SparseNativeMethods.vello_sparse_render_context_fill_rect(_handle, rect, color.ToNative());
        NativeHelpers.ThrowOnError(status, "Sparse fill rect failed");
    }

    public void StrokeRect(double x, double y, double width, double height)
    {
        ThrowIfDisposed();
        if (width < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be non-negative.");
        }

        if (height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be non-negative.");
        }

        var rect = new VelloRect
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
        };
        var status = SparseNativeMethods.vello_sparse_render_context_stroke_rect(_handle, rect);
        NativeHelpers.ThrowOnError(status, "Sparse stroke rect failed");
    }

    public void RenderTo(Span<byte> destination, SparseRenderMode mode = SparseRenderMode.OptimizeSpeed)
    {
        ThrowIfDisposed();
        if (destination.IsEmpty)
        {
            throw new ArgumentException("Destination buffer must not be empty.", nameof(destination));
        }

        var expectedBytes = (ulong)Width * Height * 4UL;
        if ((ulong)destination.Length < expectedBytes)
        {
            throw new ArgumentException(
                $"Destination buffer must contain at least {expectedBytes} bytes.",
                nameof(destination));
        }

        unsafe
        {
            fixed (byte* ptr = destination)
            {
                var status = SparseNativeMethods.vello_sparse_render_context_render_to_buffer(
                    _handle,
                    (IntPtr)ptr,
                    (nuint)destination.Length,
                    Width,
                    Height,
                    (VelloSparseRenderMode)mode);
                NativeHelpers.ThrowOnError(status, "Sparse render failed");
            }
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (_handle != IntPtr.Zero)
        {
            SparseNativeMethods.vello_sparse_render_context_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed || _handle == IntPtr.Zero)
        {
            throw new ObjectDisposedException(nameof(SparseRenderContext));
        }
    }

    private static unsafe VelloBrush PrepareBrushForInvocation(
        VelloBrush brush,
        ReadOnlySpan<VelloGradientStop> stops,
        VelloGradientStop* stopPtr)
    {
        brush.Linear.Stops = IntPtr.Zero;
        brush.Radial.Stops = IntPtr.Zero;
        brush.Sweep.Stops = IntPtr.Zero;

        if (!stops.IsEmpty && stopPtr is not null)
        {
            var ptr = (IntPtr)stopPtr;
            switch (brush.Kind)
            {
                case VelloBrushKind.LinearGradient:
                    brush.Linear.Stops = ptr;
                    brush.Linear.StopCount = (nuint)stops.Length;
                    break;
                case VelloBrushKind.RadialGradient:
                    brush.Radial.Stops = ptr;
                    brush.Radial.StopCount = (nuint)stops.Length;
                    break;
                case VelloBrushKind.SweepGradient:
                    brush.Sweep.Stops = ptr;
                    brush.Sweep.StopCount = (nuint)stops.Length;
                    break;
            }
        }

        return brush;
    }

    private static VelloStrokeStyle CreateStrokeStyle(StrokeStyle style, IntPtr dashPtr, nuint dashLength) => new()
    {
        Width = style.Width,
        MiterLimit = style.MiterLimit,
        StartCap = (VelloLineCap)style.StartCap,
        EndCap = (VelloLineCap)style.EndCap,
        LineJoin = (VelloLineJoin)style.LineJoin,
        DashPhase = style.DashPhase,
        DashPattern = dashPtr,
        DashLength = dashLength,
    };

    private unsafe void InvokeGlyphRun(Font font, VelloGlyph* glyphPtr, nuint glyphCount, SparseNativeMethods.GlyphRunOptionsNative options)
    {
        var status = SparseNativeMethods.vello_sparse_render_context_draw_glyph_run(
            _handle,
            font.Handle,
            glyphPtr,
            glyphCount,
            options);
        NativeHelpers.ThrowOnError(status, "Sparse draw glyph run failed");
    }
}
