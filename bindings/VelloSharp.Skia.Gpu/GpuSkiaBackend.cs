using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Core = VelloSharp;

namespace SkiaSharp;

internal sealed class GpuSkiaBackendFactory : ISkiaBackendFactory
{
    public ISkiaSurfaceBackend CreateSurface(SKImageInfo info)
    {
        return new GpuSurfaceBackend(info);
    }

    public ISkiaPictureRecorderBackend CreateRecorder(SKRect bounds, List<ICanvasCommand> commandLog)
    {
        return new GpuPictureRecorderBackend(bounds, commandLog);
    }

    [ModuleInitializer]
    internal static void Initialize()
    {
        SkiaBackendService.RegisterFactory(new GpuSkiaBackendFactory());
    }
}

internal sealed class GpuSurfaceBackend : ISkiaSurfaceBackend
{
    private readonly GpuScene _scene;
    private readonly GpuCanvasBackend _canvasBackend;
    private readonly SKImageInfo _info;

    public GpuSurfaceBackend(SKImageInfo info)
    {
        _info = info;
        _scene = new GpuScene();
        _canvasBackend = new GpuCanvasBackend(_scene, info.Width, info.Height);
    }

    public SKImageInfo Info => _info;
    public ISkiaCanvasBackend CanvasBackend => _canvasBackend;

    public SKImage Snapshot()
    {
        var width = Info.Width;
        var height = Info.Height;
        if (width == 0 || height == 0)
        {
            throw new InvalidOperationException("Cannot snapshot an empty surface.");
        }

        using var renderer = new GpuRenderer((uint)width, (uint)height);
        var stride = Info.RowBytes > 0 ? Info.RowBytes : width * 4;
        var buffer = new byte[stride * height];
        var renderParams = new Core.RenderParams(
            (uint)width,
            (uint)height,
            Core.RgbaColor.FromBytes(0, 0, 0, 0),
            Core.AntialiasingMode.Area,
            Core.RenderFormat.Bgra8);
        renderer.Render(_scene, renderParams, buffer, stride);

        var imageInfo = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul);
        return SKImage.FromPixels(imageInfo, buffer, stride);
    }

    public void Clear(Core.RgbaColor color)
    {
        _scene.Reset();
        if (color.A <= 0f)
        {
            return;
        }

        var rect = SKRect.Create(0, 0, Info.Width, Info.Height);
        var builder = rect.ToPathBuilder();
        var brush = new Core.SolidColorBrush(color);
        _scene.FillPath(builder, Core.FillRule.NonZero, Matrix3x2.Identity, brush);
    }

    public void Dispose()
    {
        _canvasBackend.Dispose();
        _scene.Dispose();
    }
}

internal sealed class GpuCanvasBackend : ISkiaCanvasBackend
{
    private readonly GpuScene _scene;

    public GpuCanvasBackend(GpuScene scene, float width, float height)
    {
        _scene = scene;
        Width = width;
        Height = height;
    }

    public float Width { get; }
    public float Height { get; }

    public void Reset() => _scene.Reset();

    public void PushLayer(Core.PathBuilder clip, Core.LayerBlend blend, Matrix3x2 transform, float alpha)
    {
        _scene.PushLayer(clip, blend, transform, alpha);
    }

    public void PopLayer() => _scene.PopLayer();

    public void FillPath(Core.PathBuilder path, Core.FillRule fillRule, Matrix3x2 transform, Core.Brush brush, Matrix3x2? brushTransform)
        => _scene.FillPath(path, fillRule, transform, brush, brushTransform);

    public void StrokePath(Core.PathBuilder path, Core.StrokeStyle style, Matrix3x2 transform, Core.Brush brush, Matrix3x2? brushTransform)
        => _scene.StrokePath(path, style, transform, brush, brushTransform);

    public void DrawImage(Core.ImageBrush brush, Matrix3x2 transform) => _scene.DrawImage(brush, transform);

    public void DrawGlyphRun(Core.Font font, ReadOnlySpan<Core.Glyph> glyphs, Core.GlyphRunOptions options)
        => _scene.DrawGlyphRun(font, glyphs, options);

    public void Dispose()
    {
        // Scene is owned by surface backend; no disposal here.
    }
}

internal sealed class GpuPictureRecorderBackend : ISkiaPictureRecorderBackend
{
    private readonly GpuScene _scene;
    private readonly GpuCanvasBackend _canvasBackend;
    private readonly List<ICanvasCommand> _commands;
    private readonly SKRect _bounds;

    public GpuPictureRecorderBackend(SKRect bounds, List<ICanvasCommand> commandLog)
    {
        _bounds = bounds;
        _commands = commandLog;
        _scene = new GpuScene();
        _canvasBackend = new GpuCanvasBackend(_scene, bounds.Width, bounds.Height);
    }

    public ISkiaCanvasBackend CanvasBackend => _canvasBackend;

    public SKPicture CreatePicture(SKRect cullRect, IReadOnlyList<ICanvasCommand> commands)
    {
        return new SKPicture(cullRect, _commands);
    }

    public void Dispose()
    {
        _scene.Dispose();
    }
}
