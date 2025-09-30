using System;

namespace SkiaSharp;

public enum SKColorType
{
    Unknown,
    Alpha8,
    Rgb565,
    Argb4444,
    Rgba8888,
    Bgra8888,
    Rgb888x,
    RgbaF16,
    RgbaF32,
}

public enum SKAlphaType
{
    Unknown,
    Opaque,
    Premul,
    Unpremul,
}

public readonly struct SKImageInfo
{
    public static SKColorType PlatformColorType => SKColorType.Bgra8888;

    public SKImageInfo(int width, int height, SKColorType colorType = SKColorType.Bgra8888, SKAlphaType alphaType = SKAlphaType.Premul)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        Width = width;
        Height = height;
        ColorType = colorType;
        AlphaType = alphaType;
    }

    public int Width { get; }
    public int Height { get; }
    public SKColorType ColorType { get; }
    public SKAlphaType AlphaType { get; }

    public int BytesPerPixel => ColorType switch
    {
        SKColorType.Alpha8 => 1,
        SKColorType.Rgb565 => 2,
        SKColorType.Argb4444 => 2,
        SKColorType.RgbaF16 => 8,
        SKColorType.RgbaF32 => 16,
        _ => 4,
    };

    public int RowBytes => checked(Width * BytesPerPixel);

    public int BytesSize => checked(RowBytes * Height);

    internal static unsafe Span<byte> SpanFromPointer(IntPtr address, SKImageInfo info, int rowBytes)
    {
        if (address == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (rowBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowBytes));
        }

        var length = checked(rowBytes * Math.Max(info.Height, 0));
        return new Span<byte>((void*)address, length);
    }
}
