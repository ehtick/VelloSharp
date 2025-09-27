using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
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
    private int _clipDepth;
    private int _opacityDepth;
    private int _layerDepth;

    public VelloDrawingContextImpl(Scene scene, PixelSize targetSize, VelloPlatformOptions options, Action<VelloDrawingContextImpl> onCompleted)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _targetSize = targetSize;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
        Transform = Matrix.Identity;
        RenderParams = new RenderParams((uint)Math.Max(1, targetSize.Width), (uint)Math.Max(1, targetSize.Height), options.ClearColor)
        {
            Antialiasing = options.Antialiasing,
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
        _onCompleted(this);
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
        throw new NotSupportedException("Bitmap drawing is not implemented in the limited Vello context.");
    }

    public void DrawBitmap(IBitmapImpl source, IBrush opacityMask, Rect opacityMaskRect, Rect destRect)
    {
        throw new NotSupportedException("Bitmap drawing with opacity mask is not implemented.");
    }

    public void DrawLine(IPen? pen, Point p1, Point p2)
    {
        if (pen is null)
        {
            return;
        }

        if (!TryCreateStroke(pen, out var style, out var strokeBrush, out var brushTransform))
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

        if (brush is not null && TryCreateBrush(brush, out var velloBrush, out var brushTransform))
        {
            _scene.FillPath(pathBuilder, fillRule, transform, velloBrush, brushTransform);
        }

        if (pen is not null && TryCreateStroke(pen, out var strokeStyle, out var strokeBrush, out var strokeBrushTransform))
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

        if (brush is not null && TryCreateBrush(brush, out var velloBrush, out var brushTransform))
        {
            _scene.FillPath(builder, VelloSharp.FillRule.NonZero, ToMatrix3x2(Transform), velloBrush, brushTransform);
        }

        if (pen is not null && TryCreateStroke(pen, out var strokeStyle, out var strokeBrush, out var strokeBrushTransform))
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

        if (brush is not null && TryCreateBrush(brush, out var velloBrush, out var brushTransform))
        {
            _scene.FillPath(builder, VelloSharp.FillRule.NonZero, ToMatrix3x2(Transform), velloBrush, brushTransform);
        }

        if (pen is not null && TryCreateStroke(pen, out var strokeStyle, out var strokeBrush, out var strokeBrushTransform))
        {
            _scene.StrokePath(builder, strokeStyle, ToMatrix3x2(Transform), strokeBrush, strokeBrushTransform);
        }
    }

    public void DrawGlyphRun(IBrush? foreground, IGlyphRunImpl glyphRun)
    {
        throw new NotSupportedException("Glyph drawing is not implemented in the limited Vello context.");
    }

    public IDrawingContextLayerImpl CreateLayer(PixelSize size)
    {
        throw new NotSupportedException("Layers are not supported in the limited Vello context.");
    }

    public void PushClip(Rect clip)
    {
        _clipDepth++;
    }

    public void PushClip(RoundedRect clip)
    {
        _clipDepth++;
    }

    public void PushClip(IPlatformRenderInterfaceRegion region)
    {
        _clipDepth++;
    }

    public void PopClip()
    {
        if (_clipDepth > 0)
        {
            _clipDepth--;
        }
    }

    public void PushLayer(Rect bounds)
    {
        _layerDepth++;
        throw new NotSupportedException("Layers are not supported in the limited Vello context.");
    }

    public void PopLayer()
    {
        if (_layerDepth > 0)
        {
            _layerDepth--;
        }
    }

    public void PushOpacity(double opacity, Rect? bounds)
    {
        _opacityDepth++;
    }

    public void PopOpacity()
    {
        if (_opacityDepth > 0)
        {
            _opacityDepth--;
        }
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
    }

    public void PopGeometryClip()
    {
        if (_clipDepth > 0)
        {
            _clipDepth--;
        }
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

    private bool TryCreateBrush(IBrush brush, out VelloSharp.Brush velloBrush, out Matrix3x2? brushTransform)
    {
        brushTransform = null;
        if (brush is ISolidColorBrush solid)
        {
            velloBrush = new VelloSharp.SolidColorBrush(ToRgbaColor(solid.Color, solid.Opacity));
            return true;
        }

        velloBrush = null!;
        return false;
    }

    private bool TryCreateStroke(IPen pen, out StrokeStyle strokeStyle, out VelloSharp.Brush strokeBrush, out Matrix3x2? brushTransform)
    {
        brushTransform = null;
        strokeBrush = null!;

        if (pen.Brush is null || !TryCreateBrush(pen.Brush, out var fillBrush, out var transform))
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

    private static VelloSharp.FillRule ToVelloFillRule(FillRule fillRule)
    {
        return fillRule == FillRule.EvenOdd ? VelloSharp.FillRule.EvenOdd : VelloSharp.FillRule.NonZero;
    }
}
