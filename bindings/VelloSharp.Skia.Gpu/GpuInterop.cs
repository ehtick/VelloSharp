using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using Core = VelloSharp;
using VelloSharp.Ffi.Gpu;

namespace SkiaSharp;

internal sealed class GpuRenderer : IDisposable
{
    private VelloRendererHandle? _handle;

    public GpuRenderer(uint width, uint height)
    {
        _handle = VelloRendererHandle.Create(width, height);
    }

    public void Render(GpuScene scene, Core.RenderParams renderParams, Span<byte> destination, int strideBytes)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var handle = GetHandle();
        var nativeParams = new Core.VelloRenderParams
        {
            Width = renderParams.Width,
            Height = renderParams.Height,
            BaseColor = renderParams.BaseColor.ToNative(),
            Antialiasing = (Core.VelloAaMode)renderParams.Antialiasing,
            Format = (Core.VelloRenderFormat)renderParams.Format,
        };

        if (strideBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(strideBytes));
        }

        var minimumStride = checked((int)(renderParams.Width * 4u));
        if (strideBytes < minimumStride)
        {
            throw new ArgumentOutOfRangeException(nameof(strideBytes), $"Stride must be at least {minimumStride} bytes.");
        }

        var requiredLength = checked((long)strideBytes * renderParams.Height);
        if (destination.Length < requiredLength)
        {
            throw new ArgumentException($"Destination span must have at least {requiredLength} bytes.", nameof(destination));
        }

        unsafe
        {
            fixed (byte* ptr = destination)
            {
                var status = Core.NativeMethods.vello_renderer_render(
                    handle,
                    scene.Handle,
                    nativeParams,
                    (IntPtr)ptr,
                    (nuint)strideBytes,
                    (nuint)destination.Length);

                GpuNativeHelpers.ThrowOnError(status, "Renderer render failed");
            }
        }
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

    ~GpuRenderer()
    {
        _handle?.Dispose();
    }

    private IntPtr GetHandle()
    {
        if (_handle is null || _handle.IsInvalid || _handle.IsClosed)
        {
            throw new ObjectDisposedException(nameof(GpuRenderer));
        }

        return _handle.DangerousGetHandle();
    }
}

internal sealed class GpuScene : IDisposable
{
    private VelloSceneHandle? _handle;

    public GpuScene()
    {
        _handle = VelloSceneHandle.Create();
    }

    public IntPtr Handle
    {
        get
        {
            if (_handle is null || _handle.IsInvalid || _handle.IsClosed)
            {
                throw new ObjectDisposedException(nameof(GpuScene));
            }
            return _handle.DangerousGetHandle();
        }
    }

    public void Reset()
    {
        ThrowIfDisposed();
        Core.NativeMethods.vello_scene_reset(Handle);
    }

    public void PushLayer(Core.PathBuilder clip, Core.LayerBlend blend, Matrix3x2 transform, float alpha)
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
            fixed (Core.VelloPathElement* ptr = span)
            {
                var layer = new Core.VelloLayerParams
                {
                    Mix = (Core.VelloBlendMix)blend.Mix,
                    Compose = (Core.VelloBlendCompose)blend.Compose,
                    Alpha = alpha,
                    Transform = transform.ToNativeAffine(),
                    ClipElements = (IntPtr)ptr,
                    ClipElementCount = (nuint)span.Length,
                };

                var status = Core.NativeMethods.vello_scene_push_layer(Handle, layer);
                GpuNativeHelpers.ThrowOnError(status, "PushLayer failed");
            }
        }
    }

    public void PopLayer()
    {
        ThrowIfDisposed();
        Core.NativeMethods.vello_scene_pop_layer(Handle);
    }

    public void FillPath(Core.PathBuilder path, Core.FillRule fillRule, Matrix3x2 transform, Core.Brush brush, Matrix3x2? brushTransform = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(brush);

        using var nativePath = NativePathElements.Rent(path);
        FillPathInternal(nativePath.Span, fillRule, transform, brush, brushTransform);
    }

    public void StrokePath(Core.PathBuilder path, Core.StrokeStyle style, Matrix3x2 transform, Core.Brush brush, Matrix3x2? brushTransform = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(brush);

        using var nativePath = NativePathElements.Rent(path);
        StrokePathInternal(nativePath.Span, style, transform, brush, brushTransform);
    }

    public void DrawImage(Core.ImageBrush brush, Matrix3x2 transform)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(brush);

        var status = Core.NativeMethods.vello_scene_draw_image(
            Handle,
            brush.ToNative(),
            transform.ToNativeAffine());
        GpuNativeHelpers.ThrowOnError(status, "DrawImage failed");
    }

    public void DrawGlyphRun(Core.Font font, ReadOnlySpan<Core.Glyph> glyphs, Core.GlyphRunOptions options)
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

        var glyphArray = ArrayPool<Core.VelloGlyph>.Shared.Rent(glyphs.Length);
        var glyphSpan = glyphArray.AsSpan(0, glyphs.Length);
        for (var i = 0; i < glyphs.Length; i++)
        {
            glyphSpan[i] = glyphs[i].ToNative();
        }

        try
        {
            unsafe
            {
                fixed (Core.VelloGlyph* glyphPtr = glyphSpan)
                fixed (Core.VelloGradientStop* stopPtr = stops)
                {
                    var nativeBrush = BrushInvoker.Prepare(brushData.Brush, stops, stopPtr);

                    var nativeOptions = new Core.VelloGlyphRunOptions
                    {
                        Transform = options.Transform.ToNativeAffine(),
                        FontSize = options.FontSize,
                        Hint = options.Hint,
                        Style = (Core.VelloGlyphRunStyle)options.Style,
                        Brush = nativeBrush,
                        BrushAlpha = options.BrushAlpha,
                        StrokeStyle = default,
                        GlyphTransform = IntPtr.Zero,
                    };

                    Core.VelloAffine glyphTransformValue = default;
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
                                var status = Core.NativeMethods.vello_scene_draw_glyph_run(
                                    Handle,
                                    font.Handle,
                                    glyphPtr,
                                    (nuint)glyphSpan.Length,
                                    nativeOptions);

                                GpuNativeHelpers.ThrowOnError(status, "DrawGlyphRun failed");
                            }
                        }
                        else
                        {
                            nativeOptions.StrokeStyle = StrokeInterop.Create(stroke, IntPtr.Zero, 0);
                            var status = Core.NativeMethods.vello_scene_draw_glyph_run(
                                Handle,
                                font.Handle,
                                glyphPtr,
                                (nuint)glyphSpan.Length,
                                nativeOptions);

                            GpuNativeHelpers.ThrowOnError(status, "DrawGlyphRun failed");
                        }
                    }
                    else
                    {
                        var status = Core.NativeMethods.vello_scene_draw_glyph_run(
                            Handle,
                            font.Handle,
                            glyphPtr,
                            (nuint)glyphSpan.Length,
                            nativeOptions);

                        GpuNativeHelpers.ThrowOnError(status, "DrawGlyphRun failed");
                    }
                }
            }
        }
        finally
        {
            ArrayPool<Core.VelloGlyph>.Shared.Return(glyphArray, clearArray: false);
        }
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

    ~GpuScene()
    {
        _handle?.Dispose();
    }

    private void FillPathInternal(
        ReadOnlySpan<Core.VelloPathElement> elements,
        Core.FillRule fillRule,
        Matrix3x2 transform,
        Core.Brush brush,
        Matrix3x2? brushTransform)
    {
        if (elements.IsEmpty)
        {
            throw new ArgumentException("Path must contain at least one element.", nameof(elements));
        }

        using var brushData = BrushNativeFactory.Create(brush);
        var stops = brushData.Stops;

        unsafe
        {
            fixed (Core.VelloPathElement* elementPtr = elements)
            fixed (Core.VelloGradientStop* stopPtr = stops)
            {
                var nativeBrush = BrushInvoker.Prepare(brushData.Brush, stops, stopPtr);

                Core.VelloAffine* brushTransformPtr = null;
                Core.VelloAffine brushAffine = default;
                if (brushTransform.HasValue)
                {
                    brushAffine = brushTransform.Value.ToNativeAffine();
                    brushTransformPtr = &brushAffine;
                }

                var status = Core.NativeMethods.vello_scene_fill_path_brush(
                    Handle,
                    (Core.VelloFillRule)fillRule,
                    transform.ToNativeAffine(),
                    nativeBrush,
                    brushTransformPtr,
                    elementPtr,
                    (nuint)elements.Length);

                GpuNativeHelpers.ThrowOnError(status, "FillPath failed");
            }
        }
    }

    private void StrokePathInternal(
        ReadOnlySpan<Core.VelloPathElement> elements,
        Core.StrokeStyle style,
        Matrix3x2 transform,
        Core.Brush brush,
        Matrix3x2? brushTransform)
    {
        if (elements.IsEmpty)
        {
            throw new ArgumentException("Path must contain at least one element.", nameof(elements));
        }

        using var brushData = BrushNativeFactory.Create(brush);
        var stops = brushData.Stops;

        unsafe
        {
            fixed (Core.VelloPathElement* elementPtr = elements)
            fixed (Core.VelloGradientStop* stopPtr = stops)
            {
                var nativeBrush = BrushInvoker.Prepare(brushData.Brush, stops, stopPtr);

                Core.VelloAffine* brushTransformPtr = null;
                Core.VelloAffine brushAffine = default;
                if (brushTransform.HasValue)
                {
                    brushAffine = brushTransform.Value.ToNativeAffine();
                    brushTransformPtr = &brushAffine;
                }

                if (style.DashPattern is { Length: > 0 } pattern)
                {
                    fixed (double* dashPtr = pattern)
                    {
                        var nativeStyle = StrokeInterop.Create(style, (IntPtr)dashPtr, (nuint)pattern.Length);
                        var status = Core.NativeMethods.vello_scene_stroke_path_brush(
                            Handle,
                            nativeStyle,
                            transform.ToNativeAffine(),
                            nativeBrush,
                            brushTransformPtr,
                            elementPtr,
                            (nuint)elements.Length);

                        GpuNativeHelpers.ThrowOnError(status, "StrokePath failed");
                    }
                }
                else
                {
                    var nativeStyle = StrokeInterop.Create(style, IntPtr.Zero, 0);
                    var status = Core.NativeMethods.vello_scene_stroke_path_brush(
                        Handle,
                        nativeStyle,
                        transform.ToNativeAffine(),
                        nativeBrush,
                        brushTransformPtr,
                        elementPtr,
                        (nuint)elements.Length);

                    GpuNativeHelpers.ThrowOnError(status, "StrokePath failed");
                }
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_handle is null || _handle.IsInvalid || _handle.IsClosed)
        {
            throw new ObjectDisposedException(nameof(GpuScene));
        }
    }
}
