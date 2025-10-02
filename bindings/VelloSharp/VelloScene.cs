using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VelloSharp;

public sealed class Scene : IDisposable
{
    private IntPtr _handle;

    public Scene()
    {
        _handle = NativeMethods.vello_scene_create();
        if (_handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create Vello scene.");
        }
    }

    public void Reset()
    {
        ThrowIfDisposed();
        NativeMethods.vello_scene_reset(_handle);
    }

    public void FillPath(PathBuilder path, FillRule fillRule, Matrix3x2 transform, RgbaColor color)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(path);

        var span = path.AsSpan();
        if (span.IsEmpty)
        {
            throw new ArgumentException("Path must contain at least one element.", nameof(path));
        }

        unsafe
        {
            fixed (VelloPathElement* elementPtr = span)
            {
                var status = NativeMethods.vello_scene_fill_path(
                    _handle,
                    (VelloFillRule)fillRule,
                    transform.ToNativeAffine(),
                    color.ToNative(),
                    elementPtr,
                    (nuint)span.Length);

                NativeHelpers.ThrowOnError(status, "FillPath failed");
            }
        }
    }

    public void FillPath(
        PathBuilder path,
        FillRule fillRule,
        Matrix3x2 transform,
        Brush brush,
        Matrix3x2? brushTransform = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(brush);

        var span = path.AsSpan();
        FillPathInternal(span, fillRule, transform, brush, brushTransform);
    }

    public void FillPath(
        PathBuilder path,
        FillRule fillRule,
        Matrix3x2 transform,
        PenikoBrush brush,
        Matrix3x2? brushTransform = null)
    {
        ArgumentNullException.ThrowIfNull(brush);
        FillPath(path, fillRule, transform, Brush.FromPenikoBrush(brush), brushTransform);
    }

    public void FillPath(
        KurboPath path,
        FillRule fillRule,
        Matrix3x2 transform,
        Brush brush,
        Matrix3x2? brushTransform = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(brush);

        var elements = path.GetElements();
        var converted = ConvertKurboElements(elements);
        FillPathInternal(converted, fillRule, transform, brush, brushTransform);
    }

    public void FillPath(
        KurboPath path,
        FillRule fillRule,
        Matrix3x2 transform,
        PenikoBrush brush,
        Matrix3x2? brushTransform = null)
    {
        ArgumentNullException.ThrowIfNull(brush);
        FillPath(path, fillRule, transform, Brush.FromPenikoBrush(brush), brushTransform);
    }

    public void StrokePath(PathBuilder path, StrokeStyle style, Matrix3x2 transform, RgbaColor color)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(style);

        var span = path.AsSpan();
        if (span.IsEmpty)
        {
            throw new ArgumentException("Path must contain at least one element.", nameof(path));
        }

        unsafe
        {
            fixed (VelloPathElement* elementPtr = span)
            {
                if (style.DashPattern is { Length: > 0 } pattern)
                {
                    fixed (double* dashPtr = pattern)
                    {
                        var nativeStyle = CreateStrokeStyle(style, (IntPtr)dashPtr, (nuint)pattern.Length);
                        var status = NativeMethods.vello_scene_stroke_path(
                            _handle,
                            nativeStyle,
                            transform.ToNativeAffine(),
                            color.ToNative(),
                            elementPtr,
                            (nuint)span.Length);

                        NativeHelpers.ThrowOnError(status, "StrokePath failed");
                    }
                }
                else
                {
                    var nativeStyle = CreateStrokeStyle(style, IntPtr.Zero, 0);
                    var status = NativeMethods.vello_scene_stroke_path(
                        _handle,
                        nativeStyle,
                        transform.ToNativeAffine(),
                        color.ToNative(),
                        elementPtr,
                        (nuint)span.Length);

                    NativeHelpers.ThrowOnError(status, "StrokePath failed");
                }
            }
        }
    }

    public void StrokePath(
        PathBuilder path,
        StrokeStyle style,
        Matrix3x2 transform,
        Brush brush,
        Matrix3x2? brushTransform = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(brush);

        var span = path.AsSpan();
        StrokePathInternal(span, style, transform, brush, brushTransform);
    }

    public void StrokePath(
        PathBuilder path,
        StrokeStyle style,
        Matrix3x2 transform,
        PenikoBrush brush,
        Matrix3x2? brushTransform = null)
    {
        ArgumentNullException.ThrowIfNull(brush);
        StrokePath(path, style, transform, Brush.FromPenikoBrush(brush), brushTransform);
    }

    public void StrokePath(
        KurboPath path,
        StrokeStyle style,
        Matrix3x2 transform,
        Brush brush,
        Matrix3x2? brushTransform = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(brush);

        var elements = path.GetElements();
        var converted = ConvertKurboElements(elements);
        StrokePathInternal(converted, style, transform, brush, brushTransform);
    }

    public void StrokePath(
        KurboPath path,
        StrokeStyle style,
        Matrix3x2 transform,
        PenikoBrush brush,
        Matrix3x2? brushTransform = null)
    {
        ArgumentNullException.ThrowIfNull(brush);
        StrokePath(path, style, transform, Brush.FromPenikoBrush(brush), brushTransform);
    }

    internal IntPtr Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_scene_destroy(_handle);
            _handle = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }
    }

    public void PushLayer(PathBuilder clip, LayerBlend blend, Matrix3x2 transform, float alpha = 1f)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(clip);

        var span = clip.AsSpan();
        if (span.IsEmpty)
        {
            throw new ArgumentException("Clip path must contain at least one element.", nameof(clip));
        }

        unsafe
        {
            fixed (VelloPathElement* elementPtr = span)
            {
                var native = new VelloLayerParams
                {
                    Mix = (VelloBlendMix)blend.Mix,
                    Compose = (VelloBlendCompose)blend.Compose,
                    Alpha = alpha,
                    Transform = transform.ToNativeAffine(),
                    ClipElements = (IntPtr)elementPtr,
                    ClipElementCount = (nuint)span.Length,
                };

                var status = NativeMethods.vello_scene_push_layer(_handle, native);
                NativeHelpers.ThrowOnError(status, "PushLayer failed");
            }
        }
    }

    public void PushLuminanceMaskLayer(PathBuilder clip, Matrix3x2 transform, float alpha = 1f)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(clip);

        var span = clip.AsSpan();
        if (span.IsEmpty)
        {
            throw new ArgumentException("Clip path must contain at least one element.", nameof(clip));
        }

        unsafe
        {
            fixed (VelloPathElement* elementPtr = span)
            {
                var status = NativeMethods.vello_scene_push_luminance_mask_layer(
                    _handle,
                    alpha,
                    transform.ToNativeAffine(),
                    elementPtr,
                    (nuint)span.Length);
                NativeHelpers.ThrowOnError(status, "PushLuminanceMaskLayer failed");
            }
        }
    }

    public void PopLayer()
    {
        ThrowIfDisposed();
        NativeMethods.vello_scene_pop_layer(_handle);
    }

    public void DrawBlurredRoundedRect(Vector2 origin, Vector2 size, Matrix3x2 transform, RgbaColor color, double radius, double stdDev)
    {
        ThrowIfDisposed();
        if (size.X < 0 || size.Y < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Size components must be non-negative.");
        }

        var rect = new VelloRect
        {
            X = origin.X,
            Y = origin.Y,
            Width = size.X,
            Height = size.Y,
        };

        var status = NativeMethods.vello_scene_draw_blurred_rounded_rect(
            _handle,
            transform.ToNativeAffine(),
            rect,
            color.ToNative(),
            radius,
            stdDev);

        NativeHelpers.ThrowOnError(status, "DrawBlurredRoundedRect failed");
    }

    public void DrawImage(Image image, Matrix3x2 transform) => DrawImage(new ImageBrush(image), transform);

    public void DrawImage(ImageBrush brush, Matrix3x2 transform)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(brush);

        var status = NativeMethods.vello_scene_draw_image(
            _handle,
            brush.ToNative(),
            transform.ToNativeAffine());
        NativeHelpers.ThrowOnError(status, "DrawImage failed");
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

        using var brushData = options.Brush.CreateNative();
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

                    var nativeOptions = new VelloGlyphRunOptions
                    {
                        Transform = options.Transform.ToNativeAffine(),
                        FontSize = options.FontSize,
                        Hint = options.Hint,
                        Style = (VelloGlyphRunStyle)options.Style,
                        Brush = nativeBrush,
                        BrushAlpha = options.BrushAlpha,
                        StrokeStyle = default,
                        GlyphTransform = IntPtr.Zero,
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
                                InvokeGlyphRun(
                                    _handle,
                                    font.Handle,
                                    glyphPtr,
                                    (nuint)glyphSpan.Length,
                                    ref nativeOptions);
                            }
                        }
                        else
                        {
                            nativeOptions.StrokeStyle = CreateStrokeStyle(stroke, IntPtr.Zero, 0);
                            InvokeGlyphRun(
                                _handle,
                                font.Handle,
                                glyphPtr,
                                (nuint)glyphSpan.Length,
                                ref nativeOptions);
                        }
                    }
                    else
                    {
                        InvokeGlyphRun(
                            _handle,
                            font.Handle,
                            glyphPtr,
                            (nuint)glyphSpan.Length,
                            ref nativeOptions);
                    }
                }
            }
        }
        finally
        {
            ArrayPool<VelloGlyph>.Shared.Return(glyphArray, clearArray: false);
        }
    }

    ~Scene()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_scene_destroy(_handle);
        }
    }

    private void FillPathInternal(
        ReadOnlySpan<VelloPathElement> elements,
        FillRule fillRule,
        Matrix3x2 transform,
        Brush brush,
        Matrix3x2? brushTransform)
    {
        if (elements.IsEmpty)
        {
            throw new ArgumentException("Path must contain at least one element.", nameof(elements));
        }

        using var brushData = brush.CreateNative();
        var stops = brushData.Stops;

        unsafe
        {
            fixed (VelloPathElement* elementPtr = elements)
            fixed (VelloGradientStop* stopPtr = stops)
            {
                var nativeBrush = PrepareBrushForInvocation(brushData.Brush, stops, stopPtr);

                VelloAffine* brushTransformPtr = null;
                VelloAffine brushAffine = default;
                if (brushTransform.HasValue)
                {
                    brushAffine = brushTransform.Value.ToNativeAffine();
                    brushTransformPtr = &brushAffine;
                }

                var status = NativeMethods.vello_scene_fill_path_brush(
                    _handle,
                    (VelloFillRule)fillRule,
                    transform.ToNativeAffine(),
                    nativeBrush,
                    brushTransformPtr,
                    elementPtr,
                    (nuint)elements.Length);

                NativeHelpers.ThrowOnError(status, "FillPath failed");
            }
        }
    }

    private void StrokePathInternal(
        ReadOnlySpan<VelloPathElement> elements,
        StrokeStyle style,
        Matrix3x2 transform,
        Brush brush,
        Matrix3x2? brushTransform)
    {
        if (elements.IsEmpty)
        {
            throw new ArgumentException("Path must contain at least one element.", nameof(elements));
        }

        using var brushData = brush.CreateNative();
        var stops = brushData.Stops;

        unsafe
        {
            fixed (VelloPathElement* elementPtr = elements)
            fixed (VelloGradientStop* stopPtr = stops)
            {
                var nativeBrush = PrepareBrushForInvocation(brushData.Brush, stops, stopPtr);

                VelloAffine* brushTransformPtr = null;
                VelloAffine brushAffine = default;
                if (brushTransform.HasValue)
                {
                    brushAffine = brushTransform.Value.ToNativeAffine();
                    brushTransformPtr = &brushAffine;
                }

                if (style.DashPattern is { Length: > 0 } pattern)
                {
                    fixed (double* dashPtr = pattern)
                    {
                        var nativeStyle = CreateStrokeStyle(style, (IntPtr)dashPtr, (nuint)pattern.Length);
                        var status = NativeMethods.vello_scene_stroke_path_brush(
                            _handle,
                            nativeStyle,
                            transform.ToNativeAffine(),
                            nativeBrush,
                            brushTransformPtr,
                            elementPtr,
                            (nuint)elements.Length);

                        NativeHelpers.ThrowOnError(status, "StrokePath failed");
                    }
                }
                else
                {
                    var nativeStyle = CreateStrokeStyle(style, IntPtr.Zero, 0);
                    var status = NativeMethods.vello_scene_stroke_path_brush(
                        _handle,
                        nativeStyle,
                        transform.ToNativeAffine(),
                        nativeBrush,
                        brushTransformPtr,
                        elementPtr,
                        (nuint)elements.Length);

                    NativeHelpers.ThrowOnError(status, "StrokePath failed");
                }
            }
        }
    }

    private static VelloPathElement[] ConvertKurboElements(ReadOnlySpan<KurboPathElement> elements)
    {
        if (elements.Length == 0)
        {
            return Array.Empty<VelloPathElement>();
        }

        var converted = new VelloPathElement[elements.Length];
        for (var i = 0; i < elements.Length; i++)
        {
            var source = elements[i];
            converted[i] = new VelloPathElement
            {
                Verb = (VelloPathVerb)source.Verb,
                X0 = source.X0,
                Y0 = source.Y0,
                X1 = source.X1,
                Y1 = source.Y1,
                X2 = source.X2,
                Y2 = source.Y2,
            };
        }

        return converted;
    }

    private static unsafe void InvokeGlyphRun(
        IntPtr sceneHandle,
        IntPtr fontHandle,
        VelloGlyph* glyphPtr,
        nuint glyphCount,
        ref VelloGlyphRunOptions options)
    {
        var status = NativeMethods.vello_scene_draw_glyph_run(
            sceneHandle,
            fontHandle,
            glyphPtr,
            glyphCount,
            options);

        NativeHelpers.ThrowOnError(status, "DrawGlyphRun failed");
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
                    break;
                case VelloBrushKind.RadialGradient:
                    brush.Radial.Stops = ptr;
                    break;
                case VelloBrushKind.SweepGradient:
                    brush.Sweep.Stops = ptr;
                    break;
            }
        }

        return brush;
    }

    private void ThrowIfDisposed()
    {
        if (_handle == IntPtr.Zero)
        {
            throw new ObjectDisposedException(nameof(Scene));
        }
    }
}
