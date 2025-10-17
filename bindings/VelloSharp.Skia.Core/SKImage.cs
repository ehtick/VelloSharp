using System;
using System.Runtime.InteropServices;
using SkiaSharp.IO;
using VelloSharp;

namespace SkiaSharp;

public enum SKImageCachingHint
{
    Allow,
    Disallow,
}

public sealed class SKImage : IDisposable
{
    private Image? _image;

    private SKImage(Image image, int width, int height)
    {
        _image = image;
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }

    internal Image Image => _image ?? throw new ObjectDisposedException(nameof(SKImage));

    public static SKImage FromPicture(SKPicture picture, SKSizeI dimensions, SKMatrix? matrix = null)
    {
        ArgumentNullException.ThrowIfNull(picture);
        return picture.Rasterize(dimensions, matrix);
    }

    public static SKImage? FromBitmap(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var pixmap = bitmap.PeekPixels();
        var span = pixmap.GetReadOnlyPixels();
        var rowBytes = checked((int)pixmap.RowBytes);
        return FromPixels(pixmap.Info, span, rowBytes);
    }

    public static SKImage? FromEncodedData(SKData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!SKImageInfoExtensions.TryDecodePixels(data.AsSpan(), out var info, out var pixels))
        {
            return null;
        }

        return FromPixels(info, pixels, info.RowBytes);
    }

    public SKImage ToRasterImage() => ToRasterImage(true);

    public SKImage ToRasterImage(bool cachingHint)
    {
        _ = cachingHint;

        var handle = Image.Handle;
        if (NativeMethods.vello_image_get_info(handle, out var nativeInfo) != VelloStatus.Success)
        {
            throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to query image information.");
        }

        if (!SkiaImageDecoder.TryCopyPixels(handle, nativeInfo, out var pixels))
        {
            throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to copy image pixels.");
        }

        var colorType = SkiaImageDecoder.ConvertColorType(nativeInfo.Format);
        if (colorType == SKColorType.Unknown)
        {
            throw new NotSupportedException($"Unsupported image format '{nativeInfo.Format}'.");
        }

        var alphaType = SkiaImageDecoder.ConvertAlphaType(nativeInfo.Alpha);
        var width = checked((int)nativeInfo.Width);
        var height = checked((int)nativeInfo.Height);

        var desiredInfo = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        SkiaImageDecoder.ConvertColor(pixels.AsSpan(), width, height, ref colorType, desiredInfo.ColorType);
        SkiaImageDecoder.ConvertAlpha(pixels.AsSpan(), width, height, ref alphaType, desiredInfo.AlphaType);
        SkiaImageDecoder.EnsureBufferMatchesInfo(pixels.Length, desiredInfo);

        return FromPixels(desiredInfo, pixels, desiredInfo.RowBytes);
    }

    public SKShader ToShader() => ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, SKSamplingOptions.Default, SKMatrix.CreateIdentity(), SKRect.Create(0, 0, Width, Height));

    public SKShader ToShader(SKShaderTileMode tileModeX, SKShaderTileMode tileModeY) =>
        ToShader(tileModeX, tileModeY, SKSamplingOptions.Default, SKMatrix.CreateIdentity(), SKRect.Create(0, 0, Width, Height));

    public SKShader ToShader(SKShaderTileMode tileModeX, SKShaderTileMode tileModeY, SKMatrix localMatrix) =>
        ToShader(tileModeX, tileModeY, SKSamplingOptions.Default, localMatrix, SKRect.Create(0, 0, Width, Height));

    public SKShader ToShader(SKShaderTileMode tileModeX, SKShaderTileMode tileModeY, SKSamplingOptions sampling) =>
        ToShader(tileModeX, tileModeY, sampling, SKMatrix.CreateIdentity(), SKRect.Create(0, 0, Width, Height));

    public SKShader ToShader(SKShaderTileMode tileModeX, SKShaderTileMode tileModeY, SKSamplingOptions sampling, SKMatrix localMatrix) =>
        ToShader(tileModeX, tileModeY, sampling, localMatrix, SKRect.Create(0, 0, Width, Height));

    public SKShader ToShader(SKShaderTileMode tileModeX, SKShaderTileMode tileModeY, SKSamplingOptions sampling, SKMatrix localMatrix, SKRect tileRect)
    {
        return SKShader.CreateImageShader(this, tileModeX, tileModeY, localMatrix, tileRect, sampling, takeOwnership: false);
    }

    public bool ScalePixels(SKPixmap destination, SKSamplingOptions samplingOptions)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var destInfo = destination.Info;

        var handle = Image.Handle;
        IntPtr resizedHandle = IntPtr.Zero;
        IntPtr workingHandle = handle;

        try
        {
            if (destInfo.Width <= 0 || destInfo.Height <= 0)
            {
                return false;
            }

            if (destInfo.Width != Width || destInfo.Height != Height)
            {
                var quality = samplingOptions.ToImageQuality();
                var status = NativeMethods.vello_image_resize(handle, (uint)destInfo.Width, (uint)destInfo.Height, quality, out resizedHandle);
                if (status != VelloStatus.Success || resizedHandle == IntPtr.Zero)
                {
                    return false;
                }

                workingHandle = resizedHandle;
            }

            if (NativeMethods.vello_image_get_info(workingHandle, out var nativeInfo) != VelloStatus.Success)
            {
                return false;
            }

            if (!SkiaImageDecoder.TryCopyPixels(workingHandle, nativeInfo, out var pixels))
            {
                return false;
            }

            var colorType = SkiaImageDecoder.ConvertColorType(nativeInfo.Format);
            if (colorType == SKColorType.Unknown)
            {
                return false;
            }

            var alphaType = SkiaImageDecoder.ConvertAlphaType(nativeInfo.Alpha);
            var width = checked((int)nativeInfo.Width);
            var height = checked((int)nativeInfo.Height);

            SkiaImageDecoder.ConvertColor(pixels.AsSpan(), width, height, ref colorType, destInfo.ColorType);
            SkiaImageDecoder.ConvertAlpha(pixels.AsSpan(), width, height, ref alphaType, destInfo.AlphaType);

            var convertedInfo = new SKImageInfo(width, height, colorType, alphaType);
            SkiaImageDecoder.EnsureBufferMatchesInfo(pixels.Length, convertedInfo);

            var sourceRowBytes = convertedInfo.RowBytes;
            var destinationRowBytes = checked((int)destination.RowBytes);
            if (destinationRowBytes < sourceRowBytes)
            {
                return false;
            }

            var destinationSpan = destination.GetWritablePixels();
            var rowCopyLength = Math.Min(sourceRowBytes, destinationRowBytes);
            for (var y = 0; y < height; y++)
            {
                var sourceRow = pixels.AsSpan(y * sourceRowBytes, sourceRowBytes);
                var targetRow = destinationSpan.Slice(y * destinationRowBytes, destinationRowBytes);
                sourceRow.Slice(0, rowCopyLength).CopyTo(targetRow);
                if (destinationRowBytes > rowCopyLength)
                {
                    targetRow[rowCopyLength..].Clear();
                }
            }

            return true;
        }
        finally
        {
            if (resizedHandle != IntPtr.Zero)
            {
                NativeMethods.vello_image_destroy(resizedHandle);
            }
        }
    }

    public bool ReadPixels(SKImageInfo info, IntPtr pixels, int rowBytes, int srcX, int srcY, SKImageCachingHint cachingHint)
    {
        if (pixels == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(pixels));
        }

        if (rowBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowBytes));
        }

        var handle = Image.Handle;
        if (NativeMethods.vello_image_get_info(handle, out var nativeInfo) != VelloStatus.Success)
        {
            return false;
        }

        if (!SkiaImageDecoder.TryCopyPixels(handle, nativeInfo, out var buffer))
        {
            return false;
        }

        var colorType = SkiaImageDecoder.ConvertColorType(nativeInfo.Format);
        if (colorType == SKColorType.Unknown)
        {
            return false;
        }

        var alphaType = SkiaImageDecoder.ConvertAlphaType(nativeInfo.Alpha);
        var width = checked((int)nativeInfo.Width);
        var height = checked((int)nativeInfo.Height);

        if (info.Width <= 0 || info.Height <= 0)
        {
            return false;
        }

        if (srcX < 0 || srcY < 0 || srcX >= width || srcY >= height)
        {
            return false;
        }

        SkiaImageDecoder.ConvertColor(buffer.AsSpan(), width, height, ref colorType, info.ColorType);
        SkiaImageDecoder.ConvertAlpha(buffer.AsSpan(), width, height, ref alphaType, info.AlphaType);

        var convertedInfo = new SKImageInfo(width, height, colorType, alphaType);
        SkiaImageDecoder.EnsureBufferMatchesInfo(buffer.Length, convertedInfo);

        var sourceRowBytes = convertedInfo.RowBytes;
        if (rowBytes < info.RowBytes)
        {
            return false;
        }

        unsafe
        {
            var destinationBase = (byte*)pixels;
            var bytesPerPixel = info.BytesPerPixel;
            var subsetWidth = Math.Min(info.Width, width - srcX);
            var subsetHeight = Math.Min(info.Height, height - srcY);

            if (subsetWidth <= 0 || subsetHeight <= 0)
            {
                return false;
            }

            var sourceRowLength = checked(subsetWidth * bytesPerPixel);
            for (var y = 0; y < subsetHeight; y++)
            {
                var sourceOffset = (srcY + y) * sourceRowBytes + srcX * bytesPerPixel;
                var sourceRow = buffer.AsSpan(sourceOffset, sourceRowLength);
                var destinationRow = new Span<byte>(destinationBase + y * rowBytes, rowBytes);
                sourceRow.CopyTo(destinationRow);
                if (rowBytes > sourceRowLength)
                {
                    destinationRow[sourceRowLength..].Clear();
                }
            }
        }

        return true;
    }

    public static SKImage FromPixels(SKImageInfo info, ReadOnlySpan<byte> pixels, int rowBytes)
    {
        var format = info.ColorType switch
        {
            SKColorType.Bgra8888 => RenderFormat.Bgra8,
            SKColorType.Rgba8888 => RenderFormat.Rgba8,
            _ => throw new NotSupportedException($"Unsupported colour type '{info.ColorType}'."),
        };

        var alpha = info.AlphaType switch
        {
            SKAlphaType.Premul => ImageAlphaMode.Premultiplied,
            SKAlphaType.Unpremul => ImageAlphaMode.Straight,
            SKAlphaType.Opaque => ImageAlphaMode.Straight,
            _ => ImageAlphaMode.Straight,
        };

        ReadOnlySpan<byte> pixelSpan = pixels;
        byte[]? buffer = null;

        if (format == RenderFormat.Bgra8)
        {
            buffer = new byte[rowBytes * info.Height];
            var pixelStrideBytes = info.Width * 4;
            for (var y = 0; y < info.Height; y++)
            {
                var sourceRow = pixels.Slice(y * rowBytes, rowBytes);
                var targetRow = buffer.AsSpan(y * rowBytes, rowBytes);
                for (var x = 0; x < info.Width; x++)
                {
                    var sourceIndex = x * 4;
                    targetRow[sourceIndex + 0] = sourceRow[sourceIndex + 2];
                    targetRow[sourceIndex + 1] = sourceRow[sourceIndex + 1];
                    targetRow[sourceIndex + 2] = sourceRow[sourceIndex + 0];
                    targetRow[sourceIndex + 3] = sourceRow[sourceIndex + 3];
                }

                if (rowBytes > pixelStrideBytes)
                {
                    sourceRow.Slice(pixelStrideBytes).CopyTo(targetRow.Slice(pixelStrideBytes));
                }
            }

            pixelSpan = buffer;
            format = RenderFormat.Rgba8;
        }

        if (info.AlphaType == SKAlphaType.Premul)
        {
            buffer ??= pixelSpan.ToArray();
            var span = buffer.AsSpan();
            var pixelStrideBytes = info.Width * 4;
            for (var y = 0; y < info.Height; y++)
            {
                var row = span.Slice(y * rowBytes, rowBytes);
                for (var x = 0; x < info.Width; x++)
                {
                    var index = x * 4;
                    var alphaByte = row[index + 3];
                    if (alphaByte == 0)
                    {
                        row[index + 0] = 0;
                        row[index + 1] = 0;
                        row[index + 2] = 0;
                        continue;
                    }

                    if (alphaByte < 255)
                    {
                        var scale = 255f / alphaByte;
                        row[index + 0] = (byte)Math.Clamp(row[index + 0] * scale, 0f, 255f);
                        row[index + 1] = (byte)Math.Clamp(row[index + 1] * scale, 0f, 255f);
                        row[index + 2] = (byte)Math.Clamp(row[index + 2] * scale, 0f, 255f);
                    }
                }
            }

            pixelSpan = buffer;
            alpha = ImageAlphaMode.Straight;
        }

        var image = Image.FromPixels(pixelSpan, info.Width, info.Height, format, alpha, rowBytes);
        return new SKImage(image, info.Width, info.Height);
    }

    public static SKImage FromPixels(SKImageInfo info, IntPtr pixels, int rowBytes)
    {
        if (pixels == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(pixels));
        }

        if (rowBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowBytes));
        }

        var height = Math.Max(1, info.Height);
        var length = checked(rowBytes * height);

        unsafe
        {
            var span = new ReadOnlySpan<byte>(pixels.ToPointer(), length);
            return FromPixels(info, span, rowBytes);
        }
    }

    public SKData Encode(SKEncodedImageFormat format, int quality)
    {
        if (format != SKEncodedImageFormat.Png)
        {
            throw new NotSupportedException($"Encoding '{format}' images is not supported yet.");
        }

        var compression = NormalizePngCompression(quality);
        var handle = Image.Handle;
        IntPtr blobHandle = IntPtr.Zero;

        try
        {
            var status = NativeMethods.vello_image_encode_png(handle, compression, out blobHandle);
            NativeHelpers.ThrowOnError(status, "Failed to encode image as PNG");

            status = NativeMethods.vello_blob_get_data(blobHandle, out var blobData);
            NativeHelpers.ThrowOnError(status, "Unable to access encoded PNG data");

            var length = checked((int)blobData.Length);
            if (length == 0)
            {
                return SKData.CreateCopy(ReadOnlySpan<byte>.Empty);
            }

            var buffer = new byte[length];
            Marshal.Copy(blobData.Data, buffer, 0, length);
            return SKData.CreateCopy(buffer.AsSpan());
        }
        finally
        {
            if (blobHandle != IntPtr.Zero)
            {
                NativeMethods.vello_blob_destroy(blobHandle);
            }
        }
    }

    public SKData Encode() => Encode(SKEncodedImageFormat.Png, 100);

    public bool Encode(Span<byte> destination, out int bytesWritten)
        => Encode(SKEncodedImageFormat.Png, 100, destination, out bytesWritten);

    public bool Encode(SKEncodedImageFormat format, Span<byte> destination, out int bytesWritten)
        => Encode(format, 100, destination, out bytesWritten);

    public bool Encode(SKEncodedImageFormat format, int quality, Span<byte> destination, out int bytesWritten)
    {
        using var data = Encode(format, quality);
        return data.SaveTo(destination, out bytesWritten);
    }

    private static byte NormalizePngCompression(int quality)
    {
        var clamped = Math.Clamp(quality, 0, 100);
        return (byte)Math.Clamp((int)Math.Round(clamped / 10.0), 0, 9);
    }

    public void Dispose()
    {
        _image?.Dispose();
        _image = null;
        GC.SuppressFinalize(this);
    }

    ~SKImage()
    {
        Dispose();
    }
}
