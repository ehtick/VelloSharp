using System;
using System.Collections.Generic;
using System.Numerics;
using VelloSharp;

namespace SkiaSharp;

internal interface ISkiaCanvasBackend : IDisposable
{
    float Width { get; }
    float Height { get; }

    void Reset();
    void PushLayer(PathBuilder clip, LayerBlend blend, Matrix3x2 transform, float alpha);
    void PopLayer();
    void FillPath(PathBuilder path, FillRule fillRule, Matrix3x2 transform, Brush brush, Matrix3x2? brushTransform);
    void StrokePath(PathBuilder path, StrokeStyle style, Matrix3x2 transform, Brush brush, Matrix3x2? brushTransform);
    void DrawImage(ImageBrush brush, Matrix3x2 transform);
    void DrawGlyphRun(Font font, ReadOnlySpan<Glyph> glyphs, GlyphRunOptions options);
}

internal interface ISkiaSurfaceBackend : IDisposable
{
    SKImageInfo Info { get; }
    ISkiaCanvasBackend CanvasBackend { get; }

    SKImage Snapshot();
    void Clear(RgbaColor color);
}

internal interface ISkiaPictureRecorderBackend : IDisposable
{
    ISkiaCanvasBackend CanvasBackend { get; }
    SKPicture CreatePicture(SKRect cullRect, IReadOnlyList<ICanvasCommand> commands);
}

internal interface ISkiaBackendFactory
{
    ISkiaSurfaceBackend CreateSurface(SKImageInfo info);
    ISkiaPictureRecorderBackend CreateRecorder(SKRect bounds, List<ICanvasCommand> commandLog);
}

internal static class SkiaBackendService
{
    private static readonly object Sync = new();
    private static ISkiaBackendFactory? s_factory;

    internal static void RegisterFactory(ISkiaBackendFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        lock (Sync)
        {
            s_factory = factory;
        }
    }

    internal static ISkiaBackendFactory Factory
    {
        get
        {
            lock (Sync)
            {
                return s_factory ?? throw new InvalidOperationException("Skia backend factory not registered.");
            }
        }
    }
}
