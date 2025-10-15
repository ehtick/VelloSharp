using System;
using Avalonia;
using AvaloniaVelloSkiaSharpSample.Services;
using SkiaSharp;

namespace AvaloniaVelloSkiaSharpSample.Rendering;

public readonly record struct SkiaLeaseRenderContext(
    SKSurface Surface,
    SKCanvas Canvas,
    SKImageInfo ImageInfo,
    Rect ViewBounds,
    double Scaling,
    TimeSpan Elapsed,
    ulong Frame,
    SkiaBackendDescriptor Backend);

public interface ISkiaLeaseRenderer
{
    void Render(in SkiaLeaseRenderContext context);
}

public interface ISkiaLeaseRendererInvalidation
{
    event EventHandler? RenderInvalidated;
}
