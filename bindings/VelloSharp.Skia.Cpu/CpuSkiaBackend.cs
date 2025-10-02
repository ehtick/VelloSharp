using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Core = VelloSharp;

namespace SkiaSharp;

public static class CpuSkiaBackendConfiguration
{
    private static Core.SparseRenderContextOptions? s_sparseRenderOptions;

    public static Core.SparseRenderContextOptions? SparseRenderOptions
    {
        get => s_sparseRenderOptions?.Clone();
        set => s_sparseRenderOptions = value?.Clone();
    }

    internal static Core.SparseRenderContextOptions? GetSparseRenderOptionsSnapshot()
        => s_sparseRenderOptions?.Clone();
}

internal sealed class CpuSkiaBackendFactory : ISkiaBackendFactory
{
    public ISkiaSurfaceBackend CreateSurface(SKImageInfo info)
        => new CpuSurfaceBackend(info, CpuSkiaBackendConfiguration.GetSparseRenderOptionsSnapshot());

    public ISkiaPictureRecorderBackend CreateRecorder(SKRect bounds, List<ICanvasCommand> commandLog)
        => new CpuPictureRecorderBackend(bounds, commandLog, CpuSkiaBackendConfiguration.GetSparseRenderOptionsSnapshot());

    [ModuleInitializer]
    internal static void Initialize()
    {
        SkiaBackendService.RegisterFactory(new CpuSkiaBackendFactory());
    }
}

internal sealed class CpuSurfaceBackend : ISkiaSurfaceBackend
{
    private readonly CpuSparseContext _context;
    private readonly CpuCanvasBackend _canvasBackend;
    private readonly SKImageInfo _info;

    public CpuSurfaceBackend(SKImageInfo info, Core.SparseRenderContextOptions? options)
    {
        _info = info;
        var width = (ushort)Math.Clamp(info.Width, 1, ushort.MaxValue);
        var height = (ushort)Math.Clamp(info.Height, 1, ushort.MaxValue);
        _context = new CpuSparseContext(width, height, options);
        _canvasBackend = new CpuCanvasBackend(_context, info.Width, info.Height);
    }

    public SKImageInfo Info => _info;
    public ISkiaCanvasBackend CanvasBackend => _canvasBackend;

    public SKImage Snapshot()
    {
        var width = Math.Max(1, Info.Width);
        var height = Math.Max(1, Info.Height);
        var stride = Info.RowBytes > 0 ? Info.RowBytes : width * 4;
        var buffer = new byte[stride * height];
        _context.RenderTo(buffer, Core.SparseRenderMode.OptimizeSpeed);
        var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul);
        return SKImage.FromPixels(info, buffer, stride);
    }

    public void Clear(Core.RgbaColor color)
    {
        _context.Clear(color);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

internal sealed class CpuCanvasBackend : ISkiaCanvasBackend
{
    private readonly CpuSparseContext _context;

    public CpuCanvasBackend(CpuSparseContext context, float width, float height)
    {
        _context = context;
        Width = width;
        Height = height;
    }

    public float Width { get; }
    public float Height { get; }

    public void Reset() => _context.Reset();

    public void PushLayer(Core.PathBuilder clip, Core.LayerBlend blend, Matrix3x2 transform, float alpha)
        => _context.PushLayer(clip, blend, transform, alpha);

    public void PopLayer() => _context.PopLayer();

    public void FillPath(Core.PathBuilder path, Core.FillRule fillRule, Matrix3x2 transform, Core.Brush brush, Matrix3x2? brushTransform)
        => _context.FillPath(path, fillRule, transform, brush, brushTransform);

    public void StrokePath(Core.PathBuilder path, Core.StrokeStyle style, Matrix3x2 transform, Core.Brush brush, Matrix3x2? brushTransform)
        => _context.StrokePath(path, style, transform, brush, brushTransform);

    public void DrawImage(Core.ImageBrush brush, Matrix3x2 transform)
        => _context.DrawImage(brush, transform);

    public void DrawGlyphRun(Core.Font font, ReadOnlySpan<Core.Glyph> glyphs, Core.GlyphRunOptions options)
        => _context.DrawGlyphRun(font, glyphs, options);

    public void Dispose()
    {
        // Ownership handled by CpuSurfaceBackend.
    }
}

internal sealed class CpuPictureRecorderBackend : ISkiaPictureRecorderBackend
{
    private readonly CpuSparseContext _context;
    private readonly CpuCanvasBackend _canvasBackend;
    private readonly List<ICanvasCommand> _commands;
    private readonly SKRect _bounds;

    public CpuPictureRecorderBackend(SKRect bounds, List<ICanvasCommand> commandLog, Core.SparseRenderContextOptions? options)
    {
        _bounds = bounds;
        _commands = commandLog;
        var width = (ushort)Math.Clamp((int)Math.Ceiling(Math.Max(bounds.Width, 1f)), 1, ushort.MaxValue);
        var height = (ushort)Math.Clamp((int)Math.Ceiling(Math.Max(bounds.Height, 1f)), 1, ushort.MaxValue);
        _context = new CpuSparseContext(width, height, options);
        _canvasBackend = new CpuCanvasBackend(_context, bounds.Width, bounds.Height);
    }

    public ISkiaCanvasBackend CanvasBackend => _canvasBackend;

    public SKPicture CreatePicture(SKRect cullRect, IReadOnlyList<ICanvasCommand> commands)
    {
        return new SKPicture(cullRect, _commands);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
