using System;

namespace SkiaSharp;

internal static class SKImageInfoExtensions
{
    public static SKImageInfo DecodeBitmapInfo(ReadOnlySpan<byte> data)
    {
        return SkiaSharp.IO.SkiaImageDecoder.TryDecode(data, desiredInfo: null, out var info, out _)
            ? info
            : default;
    }

    public static bool TryDecodePixels(ReadOnlySpan<byte> data, out SKImageInfo info, out byte[] pixels)
        => SkiaSharp.IO.SkiaImageDecoder.TryDecode(data, desiredInfo: null, out info, out pixels);

    public static bool TryDecodePixels(ReadOnlySpan<byte> data, SKImageInfo desiredInfo, out SKImageInfo info, out byte[] pixels)
        => SkiaSharp.IO.SkiaImageDecoder.TryDecode(data, desiredInfo, out info, out pixels);
}
