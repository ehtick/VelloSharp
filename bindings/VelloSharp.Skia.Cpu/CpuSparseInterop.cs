using System;
using System.Buffers;
using System.Numerics;
using Core = VelloSharp;

namespace SkiaSharp;

internal sealed class CpuSparseContext : IDisposable
{
    private Core.SparseRenderContextHandle? _handle;

    public CpuSparseContext(ushort width, ushort height, Core.SparseRenderContextOptions? options = null)
    {
        var threadCount = ResolveThreadCount(options);
        var simdLevel = options?.SimdLevel ?? Core.SparseSimdLevel.Auto;
        var enableMultithreading = options?.EnableMultithreading ?? true;

        _handle = Core.SparseRenderContextHandle.Create(width, height, threadCount, enableMultithreading, simdLevel);
        Width = width;
        Height = height;
        var status = Core.SparseNativeMethods.vello_sparse_render_context_get_size(
            Handle,
            out var nativeWidth,
            out var nativeHeight);
        Core.SparseNativeHelpers.ThrowOnError(status, "Failed to query sparse context size");
        Width = nativeWidth;
        Height = nativeHeight;
    }

    public ushort Width { get; private set; }

    public ushort Height { get; private set; }

    public void Reset()
    {
        Core.SparseNativeHelpers.ThrowOnError(
            Core.SparseNativeMethods.vello_sparse_render_context_reset(Handle),
            "Sparse context reset failed");
    }

    public void RenderTo(Span<byte> destination, Core.SparseRenderMode mode)
    {
        if (destination.IsEmpty)
        {
            throw new ArgumentException("Destination buffer must not be empty.", nameof(destination));
        }

        var expected = (ulong)Width * Height * 4UL;
        if ((ulong)destination.Length < expected)
        {
            throw new ArgumentException($"Destination buffer must contain at least {expected} bytes.", nameof(destination));
        }

        unsafe
        {
            fixed (byte* ptr = destination)
            {
                Core.SparseNativeHelpers.ThrowOnError(
                    Core.SparseNativeMethods.vello_sparse_render_context_render_to_buffer(
                        Handle,
                        (IntPtr)ptr,
                        (nuint)destination.Length,
                        Width,
                        Height,
                        (Core.VelloSparseRenderMode)mode),
                    "Sparse render failed");
            }
        }
    }

    public void Clear(Core.RgbaColor color)
    {
        Reset();
        if (color.A <= 0f)
        {
            return;
        }

        FillRect(0, 0, Width, Height, color);
    }

    public void FillRect(double x, double y, double width, double height, Core.RgbaColor color)
    {
        if (width < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be non-negative.");
        }

        if (height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be non-negative.");
        }

        var rect = new VelloSharp.VelloRect
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
        };

        Core.SparseNativeHelpers.ThrowOnError(
            Core.SparseNativeMethods.vello_sparse_render_context_fill_rect(
                Handle,
                rect,
                color.ToNative()),
            "Sparse fill rect failed");
    }

    public void PushLayer(Core.PathBuilder clip, Core.LayerBlend blend, Matrix3x2 transform, float alpha)
    {
        ArgumentNullException.ThrowIfNull(clip);

        using var nativePath = NativePathElements.Rent(clip);
        var span = nativePath.Span;
        if (span.IsEmpty)
        {
            throw new ArgumentException("Clip path must contain at least one element.", nameof(clip));
        }

        unsafe
        {
            fixed (VelloSharp.VelloPathElement* elementPtr = span)
            {
                var layer = new VelloSharp.VelloLayerParams
                {
                    Mix = (VelloSharp.VelloBlendMix)blend.Mix,
                    Compose = (VelloSharp.VelloBlendCompose)blend.Compose,
                    Alpha = alpha,
                    Transform = transform.ToNativeAffine(),
                    ClipElements = (IntPtr)elementPtr,
                    ClipElementCount = (nuint)span.Length,
                };

                Core.SparseNativeHelpers.ThrowOnError(
                    Core.SparseNativeMethods.vello_sparse_render_context_push_layer(Handle, ref layer),
                    "Sparse push layer failed");
            }
        }
    }

    public void PopLayer()
    {
        Core.SparseNativeMethods.vello_sparse_render_context_pop_layer(Handle);
    }

    public void FillPath(Core.PathBuilder path, Core.FillRule fillRule, Matrix3x2 transform, Core.Brush brush, Matrix3x2? brushTransform)
    {
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
            fixed (VelloSharp.VelloPathElement* elementPtr = elements)
            fixed (VelloSharp.VelloGradientStop* stopPtr = stops)
            {
                var nativeBrush = BrushInvoker.Prepare(brushData.Brush, stops, stopPtr);
                var nativeTransform = transform.ToNativeAffine();

                VelloSharp.VelloAffine* brushTransformPtr = null;
                VelloSharp.VelloAffine brushTransformValue = default;
                if (brushTransform.HasValue)
                {
                    brushTransformValue = brushTransform.Value.ToNativeAffine();
                    brushTransformPtr = &brushTransformValue;
                }

                Core.SparseNativeHelpers.ThrowOnError(
                    Core.SparseNativeMethods.vello_sparse_render_context_fill_path_brush(
                        Handle,
                        (VelloSharp.VelloFillRule)fillRule,
                        nativeTransform,
                        nativeBrush,
                        brushTransformPtr,
                        elementPtr,
                        (nuint)elements.Length),
                    "Sparse fill path failed");
            }
        }
    }

    public void StrokePath(Core.PathBuilder path, Core.StrokeStyle style, Matrix3x2 transform, Core.Brush brush, Matrix3x2? brushTransform)
    {
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
            fixed (VelloSharp.VelloPathElement* elementPtr = elements)
            fixed (VelloSharp.VelloGradientStop* stopPtr = stops)
            {
                var nativeBrush = BrushInvoker.Prepare(brushData.Brush, stops, stopPtr);
                var nativeTransform = transform.ToNativeAffine();

                VelloSharp.VelloAffine* brushTransformPtr = null;
                VelloSharp.VelloAffine brushTransformValue = default;
                if (brushTransform.HasValue)
                {
                    brushTransformValue = brushTransform.Value.ToNativeAffine();
                    brushTransformPtr = &brushTransformValue;
                }

                var stroke = StrokeInterop.Create(style, IntPtr.Zero, 0);
                if (style.DashPattern is { Length: > 0 } pattern)
                {
                    fixed (double* dashPtr = pattern)
                    {
                        stroke = StrokeInterop.Create(style, (IntPtr)dashPtr, (nuint)pattern.Length);
                        Core.SparseNativeHelpers.ThrowOnError(
                            Core.SparseNativeMethods.vello_sparse_render_context_stroke_path_brush(
                                Handle,
                                stroke,
                                nativeTransform,
                                nativeBrush,
                                brushTransformPtr,
                                elementPtr,
                                (nuint)elements.Length),
                            "Sparse stroke path failed");
                        return;
                    }
                }

                Core.SparseNativeHelpers.ThrowOnError(
                    Core.SparseNativeMethods.vello_sparse_render_context_stroke_path_brush(
                        Handle,
                        stroke,
                        nativeTransform,
                        nativeBrush,
                        brushTransformPtr,
                        elementPtr,
                        (nuint)elements.Length),
                    "Sparse stroke path failed");
            }
        }
    }

    public void DrawImage(Core.ImageBrush brush, Matrix3x2 transform)
    {
        ArgumentNullException.ThrowIfNull(brush);
        Core.SparseNativeHelpers.ThrowOnError(
            Core.SparseNativeMethods.vello_sparse_render_context_draw_image(
                Handle,
                brush.ToNative(),
                transform.ToNativeAffine()),
            "Sparse draw image failed");
    }

    public void DrawGlyphRun(Core.Font font, ReadOnlySpan<Core.Glyph> glyphs, Core.GlyphRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Brush);

        if (glyphs.IsEmpty)
        {
            return;
        }

        using var brushData = BrushNativeFactory.Create(options.Brush);
        var stops = brushData.Stops;

        var glyphBuffer = ArrayPool<VelloSharp.VelloGlyph>.Shared.Rent(glyphs.Length);
        var glyphSpan = glyphBuffer.AsSpan(0, glyphs.Length);
        for (var i = 0; i < glyphs.Length; i++)
        {
            glyphSpan[i] = glyphs[i].ToNative();
        }

        try
        {
            unsafe
            {
                fixed (VelloSharp.VelloGlyph* glyphPtr = glyphSpan)
                fixed (VelloSharp.VelloGradientStop* stopPtr = stops)
                {
                    var nativeBrush = BrushInvoker.Prepare(brushData.Brush, stops, stopPtr);
                    var nativeOptions = new Core.SparseNativeMethods.GlyphRunOptionsNative
                    {
                        Transform = options.Transform.ToNativeAffine(),
                        FontSize = options.FontSize,
                        Hint = options.Hint,
                        Style = (VelloSharp.VelloGlyphRunStyle)options.Style,
                        Brush = nativeBrush,
                        StrokeStyle = default,
                        GlyphTransform = IntPtr.Zero,
                    };

                    VelloSharp.VelloAffine glyphTransformValue = default;
                    if (options.GlyphTransform.HasValue)
                    {
                        glyphTransformValue = options.GlyphTransform.Value.ToNativeAffine();
                        nativeOptions.GlyphTransform = (IntPtr)(&glyphTransformValue);
                    }

                    if (options.Style == Core.GlyphRunStyle.Stroke)
                    {
                        var stroke = options.Stroke ?? throw new ArgumentException("Stroke options must be provided when GlyphRunStyle.Stroke is used.", nameof(options));
                        if (stroke.DashPattern is { Length: > 0 } pattern)
                        {
                            fixed (double* dashPtr = pattern)
                            {
                                nativeOptions.StrokeStyle = StrokeInterop.Create(stroke, (IntPtr)dashPtr, (nuint)pattern.Length);
                                InvokeGlyphRun(font, glyphPtr, (nuint)glyphSpan.Length, nativeOptions);
                            }
                        }
                        else
                        {
                            nativeOptions.StrokeStyle = StrokeInterop.Create(stroke, IntPtr.Zero, 0);
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
            ArrayPool<VelloSharp.VelloGlyph>.Shared.Return(glyphBuffer, clearArray: false);
        }
    }

    private static ushort ResolveThreadCount(Core.SparseRenderContextOptions? options)
    {
        if (options is null)
        {
            return 0;
        }

        if (!options.EnableMultithreading)
        {
            return 0;
        }

        if (options.ThreadCount is not int value || value <= 0)
        {
            return 0;
        }

        return (ushort)Math.Clamp(value, 1, ushort.MaxValue);
    }

    public void Dispose()
    {
        if (_handle is { } handle)
        {
            handle.Dispose();
            _handle = null;
            GC.SuppressFinalize(this);
        }
    }

    ~CpuSparseContext()
    {
        _handle?.Dispose();
    }

    private IntPtr Handle
    {
        get
        {
            if (_handle is null || _handle.IsInvalid || _handle.IsClosed)
            {
                throw new ObjectDisposedException(nameof(CpuSparseContext));
            }

            return _handle.DangerousGetHandle();
        }
    }

    private unsafe void InvokeGlyphRun(Core.Font font, VelloSharp.VelloGlyph* glyphPtr, nuint glyphCount, Core.SparseNativeMethods.GlyphRunOptionsNative options)
    {
        Core.SparseNativeHelpers.ThrowOnError(
            Core.SparseNativeMethods.vello_sparse_render_context_draw_glyph_run(
                Handle,
                font.Handle,
                glyphPtr,
                glyphCount,
                options),
            "Sparse draw glyph run failed");
    }
}
