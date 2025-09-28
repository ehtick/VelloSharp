using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.Utilities;
using VelloSharp;
using VelloSharp.Avalonia.Vello.Geometry;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class VelloDrawingContextImpl : IDrawingContextImpl
{
    private readonly Scene _scene;
    private readonly PixelSize _targetSize;
    private readonly Action<VelloDrawingContextImpl> _onCompleted;
    private readonly VelloPlatformOptions _options;
    private bool _disposed;
    private readonly Stack<Matrix> _transformStack = new();
    private readonly Stack<LayerEntry> _layerStack = new();
    private readonly List<IDisposable> _deferredDisposables = new();
    private int _clipDepth;
    private int _opacityDepth;
    private int _layerDepth;
    private bool _skipInitialClip;
    private static readonly LayerBlend s_defaultLayerBlend = new(LayerMix.Normal, LayerCompose.SrcOver);
    private static readonly LayerBlend s_clipLayerBlend = new(LayerMix.Clip, LayerCompose.SrcOver);
    private static readonly PropertyInfo? s_imageBrushBitmapProperty =
        typeof(IImageBrushSource).GetProperty("Bitmap", BindingFlags.Instance | BindingFlags.NonPublic);

    public VelloDrawingContextImpl(
        Scene scene,
        PixelSize targetSize,
        VelloPlatformOptions options,
        Action<VelloDrawingContextImpl> onCompleted,
        bool skipInitialClip = false)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _targetSize = targetSize;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
        _skipInitialClip = skipInitialClip;
        Transform = Matrix.Identity;
        RenderParams = new RenderParams((uint)Math.Max(1, targetSize.Width), (uint)Math.Max(1, targetSize.Height), options.ClearColor)
        {
            Antialiasing = AntialiasingMode.Area,
            Format = RenderFormat.Bgra8,
        };
    }

    public Matrix Transform { get; set; }

    public RenderParams RenderParams { get; private set; }

    public Scene Scene => _scene;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _onCompleted(this);
        }
        finally
        {
            foreach (var disposable in _deferredDisposables)
            {
                disposable.Dispose();
            }

            _deferredDisposables.Clear();
        }
    }

    public void Clear(Color color)
    {
        var brush = new VelloSharp.SolidColorBrush(ToRgbaColor(color, 1.0));
        var builder = new PathBuilder();
        builder.AddRectangle(new Rect(0, 0, _targetSize.Width, _targetSize.Height));
        _scene.FillPath(builder, VelloSharp.FillRule.NonZero, Matrix3x2.Identity, brush);
        RenderParams = RenderParams with { BaseColor = ToRgbaColor(color, 1.0) };
    }

    public void DrawBitmap(IBitmapImpl source, double opacity, Rect sourceRect, Rect destRect)
    {
        EnsureNotDisposed();

        if (source is not VelloBitmapImpl bitmap)
        {
            throw new NotSupportedException("The provided bitmap implementation is not compatible with the Vello renderer.");
        }

        if (sourceRect.Width <= 0 || sourceRect.Height <= 0 || destRect.Width <= 0 || destRect.Height <= 0)
        {
            return;
        }

        using var image = bitmap.CreateVelloImage();
        var brush = new ImageBrush(image)
        {
            Alpha = (float)opacity,
        };

        var scaleX = destRect.Width / sourceRect.Width;
        var scaleY = destRect.Height / sourceRect.Height;

        var transform = Matrix3x2.CreateTranslation((float)(-sourceRect.X), (float)(-sourceRect.Y))
                         * Matrix3x2.CreateScale((float)scaleX, (float)scaleY)
                         * Matrix3x2.CreateTranslation((float)destRect.X, (float)destRect.Y)
                         * ToMatrix3x2(Transform);

        _scene.DrawImage(brush, transform);
    }

    public void DrawBitmap(IBitmapImpl source, IBrush opacityMask, Rect opacityMaskRect, Rect destRect)
    {
        throw new NotSupportedException("Bitmap drawing with opacity masks is not supported by the Vello renderer yet.");
    }

    public void DrawLine(IPen? pen, Point p1, Point p2)
    {
        if (pen is null)
        {
            return;
        }

        var bounds = new Rect(new Point(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y)), new Point(Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y)));

        if (!TryCreateStroke(pen, bounds, out var style, out var strokeBrush, out var brushTransform))
        {
            return;
        }

        var builder = new PathBuilder();
        builder.MoveTo(p1.X, p1.Y);
        builder.LineTo(p2.X, p2.Y);
        ApplyStroke(builder, style, strokeBrush, brushTransform);
    }

    public void DrawGeometry(IBrush? brush, IPen? pen, IGeometryImpl geometry)
    {
        if (geometry is not VelloGeometryImplBase velloGeometry)
        {
            throw new NotSupportedException("Only Vello geometry implementations are supported in this limited context.");
        }

        var pathBuilder = BuildPath(velloGeometry);
        var fillRule = ToVelloFillRule(velloGeometry.EffectiveFillRule);
        var transform = ToMatrix3x2(Transform);

        var bounds = velloGeometry.Bounds;

        if (brush is not null && TryCreateBrush(brush, bounds, out var velloBrush, out var brushTransform))
        {
            _scene.FillPath(pathBuilder, fillRule, transform, velloBrush, brushTransform);
        }

        if (pen is not null && TryCreateStroke(pen, bounds, out var strokeStyle, out var strokeBrush, out var strokeBrushTransform))
        {
            _scene.StrokePath(pathBuilder, strokeStyle, transform, strokeBrush, strokeBrushTransform);
        }
    }

    public void DrawRectangle(IBrush? brush, IPen? pen, RoundedRect rect, BoxShadows boxShadows = default)
    {
        if (brush is null && pen is null)
        {
            return;
        }

        var builder = new PathBuilder();
        builder.AddRoundedRectangle(rect);

        var bounds = rect.Rect;

        if (brush is not null && TryCreateBrush(brush, bounds, out var velloBrush, out var brushTransform))
        {
            _scene.FillPath(builder, VelloSharp.FillRule.NonZero, ToMatrix3x2(Transform), velloBrush, brushTransform);
        }

        if (pen is not null && TryCreateStroke(pen, bounds, out var strokeStyle, out var strokeBrush, out var strokeBrushTransform))
        {
            _scene.StrokePath(builder, strokeStyle, ToMatrix3x2(Transform), strokeBrush, strokeBrushTransform);
        }
    }

    public void DrawRegion(IBrush? brush, IPen? pen, IPlatformRenderInterfaceRegion region)
    {
        throw new NotSupportedException("Region drawing is not implemented.");
    }

    public void DrawEllipse(IBrush? brush, IPen? pen, Rect rect)
    {
        if (brush is null && pen is null)
        {
            return;
        }

        var builder = new PathBuilder();
        builder.AddEllipse(rect);

        if (brush is not null && TryCreateBrush(brush, rect, out var velloBrush, out var brushTransform))
        {
            _scene.FillPath(builder, VelloSharp.FillRule.NonZero, ToMatrix3x2(Transform), velloBrush, brushTransform);
        }

        if (pen is not null && TryCreateStroke(pen, rect, out var strokeStyle, out var strokeBrush, out var strokeBrushTransform))
        {
            _scene.StrokePath(builder, strokeStyle, ToMatrix3x2(Transform), strokeBrush, strokeBrushTransform);
        }
    }

    public void DrawGlyphRun(IBrush? foreground, IGlyphRunImpl glyphRun)
    {
        EnsureNotDisposed();

        if (glyphRun is not VelloGlyphRunImpl velloGlyphRun)
        {
            throw new NotSupportedException("Glyph run implementation is not compatible with the Vello renderer.");
        }

        var bounds = glyphRun.Bounds;

        if (foreground is null || !TryCreateBrush(foreground, bounds, out var velloBrush, out _))
        {
            return;
        }

        var font = VelloFontManager.GetFont(velloGlyphRun.GlyphTypeface);
        var glyphs = velloGlyphRun.GlyphsSpan;
        if (glyphs.IsEmpty)
        {
            return;
        }

        var simulations = (velloGlyphRun.GlyphTypeface as VelloGlyphTypeface)?.FontSimulations ?? FontSimulations.None;

        var transform = Matrix3x2.CreateTranslation(
                (float)velloGlyphRun.BaselineOrigin.X,
                (float)velloGlyphRun.BaselineOrigin.Y)
            * ToMatrix3x2(Transform);

        var options = new GlyphRunOptions
        {
            FontSize = (float)velloGlyphRun.FontRenderingEmSize,
            Brush = velloBrush,
            Transform = transform,
            BrushAlpha = 1f,
            Hint = false,
            Style = GlyphRunStyle.Fill,
        };

        if (simulations.HasFlag(FontSimulations.Oblique))
        {
            var skew = Matrix3x2.CreateSkew(VelloGlyphTypeface.FauxItalicSkew, 0);
            options.GlyphTransform = options.GlyphTransform.HasValue
                ? options.GlyphTransform.Value * skew
                : skew;
        }

        _scene.DrawGlyphRun(font, glyphs, options);

        if (simulations.HasFlag(FontSimulations.Bold))
        {
            var strokeWidth = Math.Max(1f, options.FontSize * (float)VelloGlyphTypeface.FauxBoldStrokeScale);
            var strokeOptions = new GlyphRunOptions
            {
                FontSize = options.FontSize,
                Brush = options.Brush,
                BrushAlpha = options.BrushAlpha,
                Transform = options.Transform,
                GlyphTransform = options.GlyphTransform,
                Hint = options.Hint,
                Style = GlyphRunStyle.Stroke,
                Stroke = new StrokeStyle
                {
                    Width = strokeWidth,
                    StartCap = LineCap.Butt,
                    EndCap = LineCap.Butt,
                    LineJoin = LineJoin.Miter,
                },
            };

            _scene.DrawGlyphRun(font, glyphs, strokeOptions);
        }
    }

    public IDrawingContextLayerImpl CreateLayer(PixelSize size)
    {
        throw new NotSupportedException("Layers are not supported in the limited Vello context.");
    }

    public void PushClip(Rect clip)
    {
        _clipDepth++;

        if (TrySkipClip())
        {
            return;
        }

        if (clip.Width <= 0 || clip.Height <= 0)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        var builder = new PathBuilder();
        builder.AddRectangle(clip);
        PushSceneLayer(builder, 1f, s_clipLayerBlend, ToMatrix3x2(Transform));
    }

    public void PushClip(RoundedRect clip)
    {
        _clipDepth++;

        if (TrySkipClip())
        {
            return;
        }

        if (clip.Rect.Width <= 0 || clip.Rect.Height <= 0)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        var builder = new PathBuilder();
        builder.AddRoundedRectangle(clip);
        PushSceneLayer(builder, 1f, s_clipLayerBlend, ToMatrix3x2(Transform));
    }

    public void PushClip(IPlatformRenderInterfaceRegion region)
    {
        _clipDepth++;

        if (TrySkipClip())
        {
            return;
        }

        if (region is not VelloRegionImpl velloRegion || velloRegion.IsEmpty)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        var builder = new PathBuilder();
        foreach (var rect in velloRegion.Rects)
        {
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0)
            {
                continue;
            }

            builder.AddRectangle(new Rect(rect.Left, rect.Top, width, height));
        }

        if (builder.Count == 0)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        PushSceneLayer(builder, 1f, s_clipLayerBlend, Matrix3x2.Identity);
    }

    public void PopClip()
    {
        PopLayer(ref _clipDepth);
    }

    public void PushLayer(Rect bounds)
    {
        _layerDepth++;

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        var builder = new PathBuilder();
        builder.AddRectangle(bounds);
        PushSceneLayer(builder, 1f, s_defaultLayerBlend, ToMatrix3x2(Transform));
    }

    public void PopLayer()
    {
        PopLayer(ref _layerDepth);
    }

    public void PushOpacity(double opacity, Rect? bounds)
    {
        _opacityDepth++;

        var alpha = (float)Math.Clamp(opacity, 0.0, 1.0);
        if (alpha <= 0f)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        var pathBounds = bounds ?? new Rect(0, 0, Math.Max(1, _targetSize.Width), Math.Max(1, _targetSize.Height));
        if (pathBounds.Width <= 0 || pathBounds.Height <= 0)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        var builder = new PathBuilder();
        builder.AddRectangle(pathBounds);

        var transform = bounds.HasValue ? ToMatrix3x2(Transform) : Matrix3x2.Identity;
        PushSceneLayer(builder, alpha, s_defaultLayerBlend, transform);
    }

    public void PopOpacity()
    {
        PopLayer(ref _opacityDepth);
    }

    public void PushOpacityMask(IBrush mask, Rect bounds)
    {
        throw new NotSupportedException("Opacity masks are not supported in the limited Vello context.");
    }

    public void PopOpacityMask()
    {
    }

    public void PushGeometryClip(IGeometryImpl clip)
    {
        _clipDepth++;

        if (TrySkipClip())
        {
            return;
        }

        if (clip is not VelloGeometryImplBase geometry)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        var builder = BuildPath(geometry);
        if (builder.Count == 0)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        PushSceneLayer(builder, 1f, s_clipLayerBlend, ToMatrix3x2(Transform));
    }

    public void PopGeometryClip()
    {
        PopLayer(ref _clipDepth);
    }

    public void PushRenderOptions(RenderOptions renderOptions)
    {
    }

    public void PopRenderOptions()
    {
    }

    public object? GetFeature(Type t) => null;

    private void ApplyStroke(PathBuilder builder, StrokeStyle style, VelloSharp.Brush brush, Matrix3x2? brushTransform)
    {
        _scene.StrokePath(builder, style, ToMatrix3x2(Transform), brush, brushTransform);
    }

    private static PathBuilder BuildPath(VelloGeometryImplBase geometry)
    {
        var builder = new PathBuilder();
        foreach (var command in geometry.GetCommandsSnapshot())
        {
            switch (command.Verb)
            {
                case VelloPathVerb.MoveTo:
                    builder.MoveTo(command.X0, command.Y0);
                    break;
                case VelloPathVerb.LineTo:
                    builder.LineTo(command.X0, command.Y0);
                    break;
                case VelloPathVerb.QuadTo:
                    builder.QuadraticTo(command.X0, command.Y0, command.X1, command.Y1);
                    break;
                case VelloPathVerb.CubicTo:
                    builder.CubicTo(command.X0, command.Y0, command.X1, command.Y1, command.X2, command.Y2);
                    break;
                case VelloPathVerb.Close:
                    builder.Close();
                    break;
            }
        }
        return builder;
    }

    private bool TryCreateBrush(IBrush brush, Rect? targetBounds, out VelloSharp.Brush velloBrush, out Matrix3x2? brushTransform)
    {
        brushTransform = null;

        switch (brush)
        {
            case ISolidColorBrush solid:
                velloBrush = new VelloSharp.SolidColorBrush(ToRgbaColor(solid.Color, solid.Opacity));
                return true;
            case IGradientBrush gradient when gradient.GradientStops is { Count: > 0 }:
                return TryCreateGradientBrush(gradient, targetBounds, out velloBrush, out brushTransform);
            case IImageBrush imageBrush:
                return TryCreateImageBrush(imageBrush, targetBounds, out velloBrush, out brushTransform);
        }

        velloBrush = new VelloSharp.SolidColorBrush(ToRgbaColor(Colors.Transparent, 0));
        return false;
    }

    private bool TryCreateStroke(IPen pen, Rect? targetBounds, out StrokeStyle strokeStyle, out VelloSharp.Brush strokeBrush, out Matrix3x2? brushTransform)
    {
        brushTransform = null;
        strokeBrush = null!;

        if (pen.Brush is null || !TryCreateBrush(pen.Brush, targetBounds, out var fillBrush, out var transform))
        {
            strokeStyle = default!;
            return false;
        }

        var style = new StrokeStyle
        {
            Width = pen.Thickness,
            MiterLimit = pen.MiterLimit,
            StartCap = ConvertLineCap(pen.LineCap),
            EndCap = ConvertLineCap(pen.LineCap),
            LineJoin = ConvertLineJoin(pen.LineJoin),
            DashPhase = pen.DashStyle?.Offset ?? 0,
            DashPattern = pen.DashStyle?.Dashes is { Count: > 0 } dashes ? dashes.ToArray() : null,
        };

        strokeStyle = style;
        strokeBrush = fillBrush;
        brushTransform = transform;
        return true;
    }

    private static RgbaColor ToRgbaColor(Color color, double opacity)
    {
        var alpha = (float)(color.A / 255.0 * opacity);
        var r = (float)(color.R / 255.0);
        var g = (float)(color.G / 255.0);
        var b = (float)(color.B / 255.0);
        return new RgbaColor(r, g, b, alpha);
    }

    private void PushSceneLayer(PathBuilder path, float alpha, LayerBlend blend, Matrix3x2 transform)
    {
        var span = path.AsSpan();
        if (span.IsEmpty)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        _scene.PushLayer(path, blend, transform, alpha);
        _layerStack.Push(LayerEntry.Scene());
    }

    private void PopLayer(ref int counter)
    {
        if (counter > 0)
        {
            counter--;
        }

        var entry = _layerStack.Pop();
        if (entry.HasLayer)
        {
            _scene.PopLayer();
        }
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VelloDrawingContextImpl));
        }
    }

    private bool TrySkipClip()
    {
        if (_skipInitialClip && _clipDepth == 1)
        {
            _skipInitialClip = false;
            _layerStack.Push(LayerEntry.Noop());
            return true;
        }

        return false;
    }

    private static LineCap ConvertLineCap(PenLineCap cap)
    {
        return cap switch
        {
            PenLineCap.Round => LineCap.Round,
            PenLineCap.Square => LineCap.Square,
            _ => LineCap.Butt,
        };
    }

    private static LineJoin ConvertLineJoin(PenLineJoin join)
    {
        return join switch
        {
            PenLineJoin.Round => LineJoin.Round,
            PenLineJoin.Bevel => LineJoin.Bevel,
            _ => LineJoin.Miter,
        };
    }

    private static Matrix3x2 ToMatrix3x2(Matrix matrix)
    {
        return new Matrix3x2(
            (float)matrix.M11,
            (float)matrix.M12,
            (float)matrix.M21,
            (float)matrix.M22,
            (float)matrix.M31,
            (float)matrix.M32);
    }

    private static VelloSharp.FillRule ToVelloFillRule(global::Avalonia.Media.FillRule fillRule)
        {
            return fillRule == global::Avalonia.Media.FillRule.EvenOdd
                ? VelloSharp.FillRule.EvenOdd
                : VelloSharp.FillRule.NonZero;
        }

    private readonly struct LayerEntry
    {
        private LayerEntry(bool hasLayer) => HasLayer = hasLayer;

        public bool HasLayer { get; }

        public static LayerEntry Scene() => new(true);
        public static LayerEntry Noop() => new(false);
    }

    private bool TryCreateGradientBrush(IGradientBrush gradient, Rect? boundsHint, out VelloSharp.Brush brush, out Matrix3x2? transform)
    {
        var bounds = ResolveBounds(boundsHint);

        var stops = CreateGradientStops(gradient);
        if (stops.Length == 0)
        {
            brush = new VelloSharp.SolidColorBrush(ToRgbaColor(Colors.Transparent, 0));
            transform = null;
            return false;
        }

        var extend = ToExtendMode(gradient.SpreadMethod);

        switch (gradient)
        {
            case ILinearGradientBrush linear:
            {
                var start = linear.StartPoint.ToPixels(bounds);
                var end = linear.EndPoint.ToPixels(bounds);

                if (MathUtilities.IsZero(end.X - start.X) && MathUtilities.IsZero(end.Y - start.Y))
                {
                    brush = new VelloSharp.SolidColorBrush(stops[^1].Color);
                    transform = null;
                    return true;
                }

                brush = new VelloSharp.LinearGradientBrush(ToVector2(start), ToVector2(end), stops, extend);
                var matrix = ComposeBrushTransform(bounds, linear.Transform, linear.TransformOrigin);
                transform = ToMatrix3x2Nullable(matrix);
                return true;
            }

            case IRadialGradientBrush radial:
            {
                var centerPoint = radial.Center.ToPixels(bounds);
                var originPoint = radial.GradientOrigin.ToPixels(bounds);
                var radiusX = radial.RadiusX.ToValue(bounds.Width);
                var radiusY = radial.RadiusY.ToValue(bounds.Height);

                if (MathUtilities.IsZero(radiusX) || MathUtilities.IsZero(radiusY))
                {
                    brush = new VelloSharp.SolidColorBrush(stops[^1].Color);
                    transform = null;
                    return true;
                }

                var startCenter = ToVector2(originPoint);
                var endCenter = ToVector2(centerPoint);
                var startRadius = 0f;
                var endRadius = (float)radiusX;

                Matrix? transformMatrix = null;

                if (!MathUtilities.IsZero(radiusY - radiusX))
                {
                    var translateToCenter = Matrix.CreateTranslation(-centerPoint.X, -centerPoint.Y);
                    var scale = Matrix.CreateScale(1, radiusY / radiusX);
                    var translateBack = Matrix.CreateTranslation(centerPoint.X, centerPoint.Y);
                    transformMatrix = translateToCenter * scale * translateBack;
                }

                var extra = ComposeBrushTransform(bounds, radial.Transform, radial.TransformOrigin);
                if (extra is { })
                {
                    transformMatrix = transformMatrix.HasValue ? transformMatrix.Value * extra.Value : extra;
                }

                transform = ToMatrix3x2Nullable(transformMatrix);
                brush = new VelloSharp.RadialGradientBrush(startCenter, startRadius, endCenter, endRadius, stops, extend);
                return true;
            }
        }

        brush = new VelloSharp.SolidColorBrush(stops[^1].Color);
        transform = null;
        return true;
    }

    private bool TryCreateImageBrush(IImageBrush brush, Rect? boundsHint, out VelloSharp.Brush velloBrush, out Matrix3x2? brushTransform)
    {
        velloBrush = null!;
        brushTransform = null;

        using var bitmapRef = TryGetBitmapReference(brush.Source);
        if (bitmapRef.BitmapImpl is not VelloBitmapImpl bitmap)
        {
            return false;
        }

        if (brush.SourceRect != RelativeRect.Fill || brush.DestinationRect != RelativeRect.Fill)
        {
            return false;
        }

        var bounds = ResolveBounds(boundsHint);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        var image = bitmap.CreateVelloImage();
        _deferredDisposables.Add(image);

        var (extendX, extendY) = ToExtendModes(brush.TileMode);
        var imageBrush = new VelloSharp.ImageBrush(image)
        {
            Alpha = (float)brush.Opacity,
            Quality = ImageQuality.Medium,
            XExtend = extendX,
            YExtend = extendY,
        };

        var imageSize = new Size(bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        if (imageSize.Width <= 0 || imageSize.Height <= 0)
        {
            velloBrush = imageBrush;
            return true;
        }

        double scaleX = bounds.Width / imageSize.Width;
        double scaleY = bounds.Height / imageSize.Height;

        switch (brush.Stretch)
        {
            case Stretch.None:
                scaleX = scaleY = 1;
                break;
            case Stretch.Uniform:
                scaleX = scaleY = Math.Min(scaleX, scaleY);
                break;
            case Stretch.UniformToFill:
                scaleX = scaleY = Math.Max(scaleX, scaleY);
                break;
            case Stretch.Fill:
            default:
                break;
        }

        var scaledWidth = imageSize.Width * scaleX;
        var scaledHeight = imageSize.Height * scaleY;

        double offsetX = bounds.X;
        double offsetY = bounds.Y;

        switch (brush.AlignmentX)
        {
            case AlignmentX.Center:
                offsetX += (bounds.Width - scaledWidth) / 2;
                break;
            case AlignmentX.Right:
                offsetX += bounds.Width - scaledWidth;
                break;
        }

        switch (brush.AlignmentY)
        {
            case AlignmentY.Center:
                offsetY += (bounds.Height - scaledHeight) / 2;
                break;
            case AlignmentY.Bottom:
                offsetY += bounds.Height - scaledHeight;
                break;
        }

        var matrix = Matrix.CreateScale(scaleX, scaleY) * Matrix.CreateTranslation(offsetX, offsetY);

        var extra = ComposeBrushTransform(bounds, brush.Transform, brush.TransformOrigin);
        if (extra is { })
        {
            matrix = matrix * extra.Value;
        }

        brushTransform = ToMatrix3x2(matrix);
        velloBrush = imageBrush;
        return true;
    }

    private static BitmapReference TryGetBitmapReference(IImageBrushSource? source)
    {
        if (source is null || s_imageBrushBitmapProperty is null)
        {
            return BitmapReference.Empty;
        }

        object? referenceObject;

        try
        {
            referenceObject = s_imageBrushBitmapProperty.GetValue(source);
        }
        catch
        {
            return BitmapReference.Empty;
        }

        if (referenceObject is null)
        {
            return BitmapReference.Empty;
        }

        IDisposable? disposable = referenceObject as IDisposable;
        IBitmapImpl? bitmapImpl = null;

        var itemProperty = referenceObject.GetType().GetProperty("Item", BindingFlags.Instance | BindingFlags.Public);
        if (itemProperty is not null)
        {
            try
            {
                bitmapImpl = itemProperty.GetValue(referenceObject) as IBitmapImpl;
            }
            catch
            {
                bitmapImpl = null;
            }
        }

        return new BitmapReference(disposable, bitmapImpl);
    }

    private readonly struct BitmapReference : IDisposable
    {
        private readonly IDisposable? _reference;

        public BitmapReference(IDisposable? reference, IBitmapImpl? bitmapImpl)
        {
            _reference = reference;
            BitmapImpl = bitmapImpl;
        }

        public IBitmapImpl? BitmapImpl { get; }

        public void Dispose()
        {
            _reference?.Dispose();
        }

        public static BitmapReference Empty => new(null, null);
    }

    private Rect ResolveBounds(Rect? bounds)
    {
        if (bounds is { } value && value.Width > 0 && value.Height > 0)
        {
            return value;
        }

        return new Rect(0, 0, Math.Max(1d, _targetSize.Width), Math.Max(1d, _targetSize.Height));
    }

    private static VelloSharp.GradientStop[] CreateGradientStops(IGradientBrush gradient)
    {
        var stops = gradient.GradientStops;
        var result = new VelloSharp.GradientStop[stops.Count];
        var opacity = gradient.Opacity;

        for (var i = 0; i < stops.Count; i++)
        {
            var stop = stops[i];
            var color = ToRgbaColor(stop.Color, opacity);
            result[i] = new VelloSharp.GradientStop((float)Math.Clamp(stop.Offset, 0, 1), color);
        }

        return result;
    }

    private static ExtendMode ToExtendMode(GradientSpreadMethod spreadMethod) => spreadMethod switch
    {
        GradientSpreadMethod.Reflect => ExtendMode.Reflect,
        GradientSpreadMethod.Repeat => ExtendMode.Repeat,
        _ => ExtendMode.Pad,
    };

    private static (ExtendMode X, ExtendMode Y) ToExtendModes(TileMode mode) => mode switch
    {
        TileMode.FlipX => (ExtendMode.Reflect, ExtendMode.Pad),
        TileMode.FlipY => (ExtendMode.Pad, ExtendMode.Reflect),
        TileMode.FlipXY => (ExtendMode.Reflect, ExtendMode.Reflect),
        TileMode.Tile => (ExtendMode.Repeat, ExtendMode.Repeat),
        _ => (ExtendMode.Pad, ExtendMode.Pad),
    };

    private static Vector2 ToVector2(Point point) => new((float)point.X, (float)point.Y);

    private static Matrix? ComposeBrushTransform(Rect bounds, ITransform? transform, RelativePoint origin)
    {
        if (transform is null)
        {
            return null;
        }

        var originPoint = origin.ToPixels(bounds);
        var translateToOrigin = Matrix.CreateTranslation(-originPoint.X, -originPoint.Y);
        var translateBack = Matrix.CreateTranslation(originPoint.X, originPoint.Y);
        var matrix = transform.Value;
        return translateToOrigin * matrix * translateBack;
    }

    private static Matrix3x2? ToMatrix3x2Nullable(Matrix? matrix) => matrix.HasValue ? ToMatrix3x2(matrix.Value) : (Matrix3x2?)null;
}
