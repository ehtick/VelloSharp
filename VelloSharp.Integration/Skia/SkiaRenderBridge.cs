using System;
using SkiaSharp;
using VelloSharp;
using VelloSharp.Rendering;

namespace VelloSharp.Integration.Skia;

public static class SkiaRenderBridge
{
    public static void Render(SKBitmap bitmap, Renderer renderer, Scene scene, in RenderParams renderParams)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(scene);

        using var pixmap = bitmap.PeekPixels();
        if (pixmap is null)
        {
            throw new InvalidOperationException("The provided SKBitmap does not expose readable pixels.");
        }

        RenderPixmap(pixmap, renderer, scene, renderParams);
    }

    public static void Render(SKSurface surface, Renderer renderer, Scene scene, in RenderParams renderParams)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(scene);

        using var pixmap = surface.PeekPixels();
        if (pixmap is not null)
        {
            RenderPixmap(pixmap, renderer, scene, renderParams);
            surface.Flush();
            return;
        }

        var bounds = surface.Canvas.DeviceClipBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var info = new SKImageInfo(bounds.Width, bounds.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap();
        if (!bitmap.TryAllocPixels(info))
        {
            throw new InvalidOperationException("Unable to allocate an SKBitmap for the intermediate surface copy.");
        }

        Render(bitmap, renderer, scene, renderParams);
        surface.Canvas.DrawBitmap(bitmap, 0, 0);
        surface.Flush();
    }

    private static unsafe void RenderPixmap(SKPixmap pixmap, Renderer renderer, Scene scene, in RenderParams renderParams)
    {
        var format = pixmap.Info.ColorType switch
        {
            SKColorType.Bgra8888 => RenderFormat.Bgra8,
            SKColorType.Rgba8888 => RenderFormat.Rgba8,
            _ => throw new NotSupportedException($"SKColorType '{pixmap.Info.ColorType}' is not supported for Vello rendering."),
        };

        var stride = checked((int)pixmap.RowBytes);
        var descriptor = new RenderTargetDescriptor((uint)pixmap.Width, (uint)pixmap.Height, format, stride);

        var pixels = pixmap.GetPixels();
        if (pixels == IntPtr.Zero)
        {
            throw new InvalidOperationException("SKPixmap returned a null pixel pointer.");
        }

        var span = new Span<byte>((void*)pixels, descriptor.RequiredBufferSize);
        VelloRenderPath.Render(renderer, scene, span, renderParams, descriptor);
    }
}
