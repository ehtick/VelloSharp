using System;
using System.Runtime.InteropServices;

namespace SkiaSharp;

public sealed class SKPixmap
{
    private readonly SKImageInfo _info;
    private readonly byte[] _pixels;
    private readonly int _rowBytes;

    public SKPixmap(SKImageInfo info, byte[] pixels, int rowBytes)
    {
        _info = info;
        _pixels = pixels ?? throw new ArgumentNullException(nameof(pixels));
        if (rowBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowBytes));
        }

        _rowBytes = rowBytes;
    }

    public int Width => _info.Width;
    public int Height => _info.Height;
    public SKImageInfo Info => _info;
    public ulong RowBytes => (ulong)_rowBytes;

    public IntPtr GetPixels()
    {
        unsafe
        {
            fixed (byte* ptr = _pixels)
            {
                return (IntPtr)ptr;
            }
        }
    }

    public SKColorF GetPixelColorF(int x, int y)
    {
        if ((uint)x >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        var offset = checked(y * _rowBytes + x * _info.BytesPerPixel);
        var span = _pixels.AsSpan();

        return _info.ColorType switch
        {
            SKColorType.Bgra8888 => FromBgra(span, offset),
            SKColorType.Rgba8888 => FromRgba(span, offset),
            SKColorType.Alpha8 => FromAlpha(span, offset),
            SKColorType.RgbaF16 => FromRgbaF16(span, offset),
            SKColorType.RgbaF32 => FromRgbaF32(span, offset),
            _ => throw new NotSupportedException($"Color type {_info.ColorType} is not supported."),
        };
    }

    internal ReadOnlySpan<byte> GetReadOnlyPixels() => _pixels;

    internal Span<byte> GetWritablePixels() => _pixels.AsSpan();

    private SKColorF FromBgra(ReadOnlySpan<byte> span, int offset)
    {
        var blue = span[offset] / 255f;
        var green = span[offset + 1] / 255f;
        var red = span[offset + 2] / 255f;
        var alpha = span[offset + 3] / 255f;
        return CreateColor(red, green, blue, alpha);
    }

    private SKColorF FromRgba(ReadOnlySpan<byte> span, int offset)
    {
        var red = span[offset] / 255f;
        var green = span[offset + 1] / 255f;
        var blue = span[offset + 2] / 255f;
        var alpha = span[offset + 3] / 255f;
        return CreateColor(red, green, blue, alpha);
    }

    private SKColorF FromAlpha(ReadOnlySpan<byte> span, int offset)
    {
        var alpha = span[offset] / 255f;
        return CreateColor(0f, 0f, 0f, alpha);
    }

    private SKColorF FromRgbaF16(ReadOnlySpan<byte> span, int offset)
    {
        var halfSpan = MemoryMarshal.Cast<byte, Half>(span.Slice(offset, 8));
        var red = (float)halfSpan[0];
        var green = (float)halfSpan[1];
        var blue = (float)halfSpan[2];
        var alpha = (float)halfSpan[3];
        return CreateColor(red, green, blue, alpha);
    }

    private SKColorF FromRgbaF32(ReadOnlySpan<byte> span, int offset)
    {
        var floatSpan = MemoryMarshal.Cast<byte, float>(span.Slice(offset, 16));
        var red = floatSpan[0];
        var green = floatSpan[1];
        var blue = floatSpan[2];
        var alpha = floatSpan[3];
        return CreateColor(red, green, blue, alpha);
    }

    private SKColorF CreateColor(float red, float green, float blue, float alpha)
    {
        if (_info.AlphaType == SKAlphaType.Opaque)
        {
            alpha = 1f;
        }

        if (_info.AlphaType == SKAlphaType.Premul && alpha > 0f)
        {
            var inverse = 1f / alpha;
            red *= inverse;
            green *= inverse;
            blue *= inverse;
        }

        return new SKColorF(Clamp(red), Clamp(green), Clamp(blue), Clamp(alpha));
    }

    private static float Clamp(float value)
    {
        if (value < 0f)
        {
            return 0f;
        }

        if (value > 1f)
        {
            return 1f;
        }

        return value;
    }
}
