using System;
using System.IO;

namespace SkiaSharp;

public sealed class SKBitmap : IDisposable
{
    private byte[]? _pixels;
    private SKImageInfo _info;
    private int _rowBytes;
    private bool _immutable;
    private bool _disposed;

    public SKBitmap()
    {
        _info = default;
        _rowBytes = 0;
    }

    public SKBitmap(SKImageInfo info)
    {
        AllocatePixels(info);
    }

    public int Width => _info.Width;
    public int Height => _info.Height;
    public SKColorType ColorType => _info.ColorType;
    public SKAlphaType AlphaType => _info.AlphaType;
    public SKImageInfo Info => _info;
    public int RowBytes => _rowBytes;
    public bool IsEmpty => _pixels is null || _pixels.Length == 0;

    public static SKBitmap? Decode(SKData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!SKImageInfoExtensions.TryDecodePixels(data.AsSpan(), out var info, out var pixels))
        {
            return null;
        }

        return new SKBitmap(info, pixels, info.RowBytes, immutable: false);
    }

    public static SKBitmap? Decode(SKManagedStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var data = SKData.Create(stream);
        return Decode(data);
    }

    public static SKBitmap Decode(SKCodec codec, SKImageInfo desired)
    {
        ArgumentNullException.ThrowIfNull(codec);

        if (!SKImageInfoExtensions.TryDecodePixels(codec.AsSpan(), desired, out var info, out var pixels))
        {
            throw new InvalidOperationException("Failed to decode bitmap with requested dimensions.");
        }

        return new SKBitmap(info, pixels, info.RowBytes, immutable: false);
    }

    private void AllocatePixels(SKImageInfo info)
    {
        if (info.Width <= 0 || info.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(info));
        }

        _info = info;
        _rowBytes = info.RowBytes;
        _pixels = new byte[info.BytesSize];
        _immutable = false;
    }

    public void InstallPixels(SKImageInfo info, IntPtr address, int rowBytes, SKBitmapReleaseDelegate? releaseProc, object? context)
    {
        ThrowIfDisposed();
        var span = SKImageInfo.SpanFromPointer(address, info, rowBytes);
        _pixels = span.ToArray();
        _info = info;
        _rowBytes = rowBytes;
        _immutable = false;
        releaseProc?.Invoke(address, context);
    }

    public void Erase(SKColor color)
    {
        if (_pixels is null)
        {
            throw new ObjectDisposedException(nameof(SKBitmap));
        }

        var blue = color.Blue;
        var green = color.Green;
        var red = color.Red;
        var alpha = color.Alpha;
        for (var i = 0; i < _pixels.Length; i += 4)
        {
            _pixels[i + 0] = blue;
            _pixels[i + 1] = green;
            _pixels[i + 2] = red;
            _pixels[i + 3] = alpha;
        }
    }

    public void SetImmutable() => _immutable = true;
    public bool IsImmutable => _immutable;

    public IntPtr GetPixels()
    {
        if (_pixels is null)
        {
            return IntPtr.Zero;
        }

        unsafe
        {
            fixed (byte* ptr = _pixels)
            {
                return (IntPtr)ptr;
            }
        }
    }

    public SKPixmap PeekPixels()
    {
        ThrowIfDisposed();
        return new SKPixmap(_info, _pixels!, _rowBytes);
    }

    public SKBitmap Resize(SKImageInfo info, SKSamplingOptions options)
    {
        ThrowIfDisposed();
        var bitmap = new SKBitmap(info);
        if (_pixels is null)
        {
            return bitmap;
        }

        var target = bitmap._pixels.AsSpan();
        var source = _pixels.AsSpan();
        var sourceWidth = _info.Width;
        var sourceHeight = _info.Height;
        var targetWidth = info.Width;
        var targetHeight = info.Height;
        var sourceRowBytes = _rowBytes;
        var targetRowBytes = bitmap._rowBytes;
        var sourceBytesPerPixel = _info.BytesPerPixel;
        var targetBytesPerPixel = info.BytesPerPixel;
        var bytesPerPixel = Math.Min(sourceBytesPerPixel, targetBytesPerPixel);

        for (var y = 0; y < targetHeight; y++)
        {
            var sourceY = (int)(y * (sourceHeight / (double)targetHeight));
            for (var x = 0; x < targetWidth; x++)
            {
                var sourceX = (int)(x * (sourceWidth / (double)targetWidth));
                var sourceIndex = sourceY * sourceRowBytes + sourceX * sourceBytesPerPixel;
                var targetIndex = y * targetRowBytes + x * targetBytesPerPixel;
                source.Slice(sourceIndex, bytesPerPixel).CopyTo(target.Slice(targetIndex, bytesPerPixel));
                if (targetBytesPerPixel > bytesPerPixel)
                {
                    target.Slice(targetIndex + bytesPerPixel, targetBytesPerPixel - bytesPerPixel).Clear();
                }
            }
        }

        return bitmap;
    }

    public SKBitmap Copy()
    {
        ThrowIfDisposed();

        var copy = new SKBitmap(_info);
        if (_pixels is not null)
        {
            _pixels.CopyTo(copy._pixels!.AsSpan());
        }

        copy._rowBytes = _rowBytes;
        copy._immutable = _immutable;
        return copy;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _pixels = null;
        _rowBytes = 0;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKBitmap));
        }
    }

    private SKBitmap(SKImageInfo info, byte[] pixels, int rowBytes, bool immutable)
    {
        _info = info;
        _pixels = pixels ?? throw new ArgumentNullException(nameof(pixels));
        if (rowBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowBytes));
        }

        var requiredLength = checked(rowBytes * Math.Max(info.Height, 0));
        if (_pixels.Length < requiredLength)
        {
            throw new ArgumentException("Pixel buffer is smaller than expected for the provided image info.", nameof(pixels));
        }

        _rowBytes = rowBytes;
        _immutable = immutable;
    }
}

public delegate void SKBitmapReleaseDelegate(IntPtr address, object? context);
