using System;

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

    internal ReadOnlySpan<byte> GetReadOnlyPixels() => _pixels;

    internal Span<byte> GetWritablePixels() => _pixels.AsSpan();
}
