using System;
using SkiaSharp;
using VelloSharp;

namespace VelloSharp.Skia.Core.Tests;

internal static class SurfaceRenderHelper
{
    public static byte[] RenderToBgra(SKSurface surface, RgbaColor? baseColor = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var info = surface.Info;
        using var renderer = new Renderer((uint)info.Width, (uint)info.Height);
        var stride = info.RowBytes;
        var buffer = new byte[stride * info.Height];
        var renderParams = new RenderParams(
            (uint)info.Width,
            (uint)info.Height,
            baseColor ?? RgbaColor.FromBytes(0, 0, 0, 0),
            AntialiasingMode.Area,
            RenderFormat.Bgra8);

        renderer.Render(surface.Scene, renderParams, buffer, stride);
        return buffer;
    }

    public static SKColor GetPixel(byte[] buffer, SKImageInfo info, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (x < 0 || x >= info.Width || y < 0 || y >= info.Height)
        {
            throw new ArgumentOutOfRangeException($"Pixel ({x},{y}) outside surface {info.Width}x{info.Height}.");
        }

        var stride = info.RowBytes;
        var offset = y * stride + x * 4;
        var blue = buffer[offset + 0];
        var green = buffer[offset + 1];
        var red = buffer[offset + 2];
        var alpha = buffer[offset + 3];
        return new SKColor(red, green, blue, alpha);
    }

    public static ulong ComputeChecksum(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ulong hash = 1469598103934665603UL; // FNV offset basis
        const ulong prime = 1099511628211UL;
        foreach (var value in buffer)
        {
            hash ^= value;
            hash *= prime;
        }

        return hash;
    }

    public static int CountNonTransparentPixels(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var count = 0;
        for (var i = 0; i < buffer.Length; i += 4)
        {
            if (buffer[i + 3] != 0)
            {
                count++;
            }
        }

        return count;
    }
}
