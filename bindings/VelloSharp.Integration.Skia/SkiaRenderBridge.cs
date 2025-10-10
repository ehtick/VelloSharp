using System;
using SkiaSharp;
using VelloSharp;
using VelloSharp.Rendering;

namespace VelloSharp.Integration.Skia;

public static class SkiaRenderBridge
{
    // Reuse a thread-local scratch bitmap so GPU surfaces do not incur a new allocation per frame.
    [ThreadStatic]
    private static SKBitmap? s_sharedBitmap;
    [ThreadStatic]
    private static SKImageInfo s_sharedBitmapInfo;

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
        var width = bounds.Width;
        var height = bounds.Height;

        if (width <= 0 || height <= 0)
        {
            width = (int)renderParams.Width;
            height = (int)renderParams.Height;
        }

        if (width <= 0 || height <= 0)
        {
            return;
        }

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var bitmap = RentBitmap(info);

        Render(bitmap, renderer, scene, renderParams);
        surface.Canvas.DrawBitmap(bitmap, 0, 0);
        surface.Flush();
    }

    private static SKBitmap RentBitmap(SKImageInfo info)
    {
        var bitmap = s_sharedBitmap;
        if (bitmap is null || !IsCompatible(info))
        {
            bitmap?.Dispose();

            bitmap = new SKBitmap();
            if (!bitmap.TryAllocPixels(info))
            {
                bitmap.Dispose();
                throw new InvalidOperationException("Unable to allocate an SKBitmap for the intermediate surface copy.");
            }

            s_sharedBitmapInfo = info;
            s_sharedBitmap = bitmap;
        }

        return bitmap;

        bool IsCompatible(in SKImageInfo requested)
        {
            return s_sharedBitmapInfo.Width == requested.Width &&
                   s_sharedBitmapInfo.Height == requested.Height &&
                   s_sharedBitmapInfo.ColorType == requested.ColorType &&
                   s_sharedBitmapInfo.AlphaType == requested.AlphaType &&
                   Equals(s_sharedBitmapInfo.ColorSpace, requested.ColorSpace);
        }
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
