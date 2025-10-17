using System;

namespace SkiaSharp;

public static class SKGpuExtensions
{
    public static uint ToGlSizedFormat(this SKColorType colorType) => colorType switch
    {
        SKColorType.Unknown => 0,
        SKColorType.Alpha8 => GRGlSizedFormat.ALPHA8,
        SKColorType.Rgb565 => GRGlSizedFormat.RGB565,
        SKColorType.Argb4444 => GRGlSizedFormat.RGBA4,
        SKColorType.Rgba8888 => GRGlSizedFormat.RGBA8,
        SKColorType.Rgb888x => GRGlSizedFormat.RGB8,
        SKColorType.Bgra8888 => GRGlSizedFormat.BGRA8,
        SKColorType.RgbaF16 => GRGlSizedFormat.RGBA16F,
        SKColorType.RgbaF32 => GRGlSizedFormat.RGBA32F,
        _ => throw new ArgumentOutOfRangeException(nameof(colorType), $"Unsupported colour type '{colorType}'."),
    };
}

internal static class GRGlSizedFormat
{
    internal const uint ALPHA8 = 0x803C;
    internal const uint RGB565 = 0x8D62;
    internal const uint RGBA4 = 0x8056;
    internal const uint RGBA8 = 0x8058;
    internal const uint RGB8 = 0x8051;
    internal const uint BGRA8 = 0x93A1; // GL_BGRA8_EXT
    internal const uint RGBA16F = 0x881A;
    internal const uint RGBA32F = 0x8814;
}
