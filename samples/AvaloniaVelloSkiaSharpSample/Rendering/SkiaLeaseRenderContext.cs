using System;
using Avalonia;
using SkiaSharp;

namespace AvaloniaVelloSkiaSharpSample.Rendering;

public readonly record struct SkiaLeaseRenderContext(
    SKSurface Surface,
    SKCanvas Canvas,
    SKImageInfo ImageInfo,
    Rect ViewBounds,
    double Scaling,
    TimeSpan Elapsed,
    ulong Frame);

public interface ISkiaLeaseRenderer
{
    void Render(in SkiaLeaseRenderContext context);
}
