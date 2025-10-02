using System;
using System.Runtime.InteropServices;
using VelloSharp;

namespace SkiaSharp.IO;

internal static class SkiaImageDecoder
{
    // TODO (Step 3e3): Extend decode support beyond PNG once the Vello FFI exposes JPEG/WebP helpers.
    public static bool TryDecode(ReadOnlySpan<byte> data, out SKImageInfo info, out byte[] pixels)
        => TryDecode(data, desiredInfo: null, out info, out pixels);

    public static bool TryDecode(ReadOnlySpan<byte> data, SKImageInfo? desiredInfo, out SKImageInfo info, out byte[] pixels)
    {
        try
        {
            return TryDecodeInternal(data, desiredInfo, out info, out pixels);
        }
        catch (NotSupportedException)
        {
            info = default;
            pixels = Array.Empty<byte>();
            return false;
        }
        catch
        {
            info = default;
            pixels = Array.Empty<byte>();
            return false;
        }
    }

    private static bool TryDecodeInternal(ReadOnlySpan<byte> data, SKImageInfo? desiredInfo, out SKImageInfo info, out byte[] pixels)
    {
        info = default;
        pixels = Array.Empty<byte>();

        if (data.IsEmpty)
        {
            return false;
        }

        var buffer = data.ToArray();
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                var status = NativeMethods.vello_image_decode_png(ptr, (nuint)buffer.LongLength, out var decodedHandle);
                if (status != VelloStatus.Success || decodedHandle == IntPtr.Zero)
                {
                    return false;
                }

                var initialHandle = decodedHandle;
                var workingHandle = decodedHandle;

                try
                {
                    if (!TryGetInfo(workingHandle, out var nativeInfo))
                    {
                        return false;
                    }

                    if (desiredInfo is { } desired && RequiresResize(nativeInfo, desired))
                    {
                        if (!TryResize(workingHandle, desired, out var resizedHandle))
                        {
                            return false;
                        }

                        workingHandle = resizedHandle;

                        if (!TryGetInfo(workingHandle, out nativeInfo))
                        {
                            return false;
                        }
                    }

                    if (!TryCopyPixels(workingHandle, nativeInfo, out pixels))
                    {
                        return false;
                    }

                    var colorType = ConvertColorType(nativeInfo.Format);
                    if (colorType == SKColorType.Unknown)
                    {
                        throw new NotSupportedException($"Not implemented: unsupported image format '{nativeInfo.Format}'.");
                    }

                    var alphaType = ConvertAlphaType(nativeInfo.Alpha);
                    var width = checked((int)nativeInfo.Width);
                    var height = checked((int)nativeInfo.Height);

                    if (desiredInfo is { } target)
                    {
                        ConvertColor(pixels.AsSpan(), width, height, ref colorType, target.ColorType);
                        ConvertAlpha(pixels.AsSpan(), width, height, ref alphaType, target.AlphaType);
                    }

                    info = new SKImageInfo(width, height, colorType, alphaType);
                    EnsureBufferMatchesInfo(pixels.Length, info);
                    return true;
                }
                finally
                {
                    if (workingHandle != IntPtr.Zero)
                    {
                        NativeMethods.vello_image_destroy(workingHandle);
                    }

                    if (workingHandle != initialHandle && initialHandle != IntPtr.Zero)
                    {
                        NativeMethods.vello_image_destroy(initialHandle);
                    }
                }
            }
        }
    }

    private static bool TryGetInfo(IntPtr handle, out VelloImageInfoNative info)
    {
        var status = NativeMethods.vello_image_get_info(handle, out info);
        return status == VelloStatus.Success;
    }

    private static bool RequiresResize(VelloImageInfoNative info, SKImageInfo desired)
    {
        if (desired.Width <= 0 || desired.Height <= 0)
        {
            return false;
        }

        return info.Width != (uint)desired.Width || info.Height != (uint)desired.Height;
    }

    private static bool TryResize(IntPtr handle, SKImageInfo desired, out IntPtr resized)
    {
        resized = IntPtr.Zero;
        if (desired.Width <= 0 || desired.Height <= 0)
        {
            return false;
        }

        var status = NativeMethods.vello_image_resize(handle, (uint)desired.Width, (uint)desired.Height, VelloImageQualityMode.High, out resized);
        return status == VelloStatus.Success && resized != IntPtr.Zero;
    }

    internal static bool TryCopyPixels(IntPtr handle, VelloImageInfoNative nativeInfo, out byte[] pixels)
    {
        pixels = Array.Empty<byte>();

        var status = NativeMethods.vello_image_map_pixels(handle, out var pixelsPtr, out var length);
        if (status != VelloStatus.Success || pixelsPtr == IntPtr.Zero || length == 0)
        {
            return false;
        }

        try
        {
            var colorType = ConvertColorType(nativeInfo.Format);
            if (colorType == SKColorType.Unknown)
            {
                throw new NotSupportedException($"Not implemented: unsupported image format '{nativeInfo.Format}'.");
            }

            var tempInfo = new SKImageInfo((int)nativeInfo.Width, (int)nativeInfo.Height, colorType, SKAlphaType.Unpremul);
            var expectedStride = tempInfo.RowBytes;
            var stride = checked((int)nativeInfo.Stride);
            var height = checked((int)nativeInfo.Height);
            var totalBytes = checked(expectedStride * height);
            var buffer = totalBytes == 0 ? Array.Empty<byte>() : new byte[totalBytes];

            if (totalBytes == 0)
            {
                pixels = buffer;
                return true;
            }

            if (stride == expectedStride)
            {
                Marshal.Copy(pixelsPtr, buffer, 0, totalBytes);
            }
            else
            {
                for (var y = 0; y < height; y++)
                {
                    var source = IntPtr.Add(pixelsPtr, y * stride);
                    var destinationIndex = y * expectedStride;
                    var bytesToCopy = Math.Min(stride, expectedStride);
                    Marshal.Copy(source, buffer, destinationIndex, bytesToCopy);
                    if (bytesToCopy < expectedStride)
                    {
                        Array.Clear(buffer, destinationIndex + bytesToCopy, expectedStride - bytesToCopy);
                    }
                }
            }

            pixels = buffer;
            return true;
        }
        finally
        {
            NativeMethods.vello_image_unmap_pixels(handle);
        }
    }

    internal static SKColorType ConvertColorType(VelloRenderFormat format) => format switch
    {
        VelloRenderFormat.Bgra8 => SKColorType.Bgra8888,
        VelloRenderFormat.Rgba8 => SKColorType.Rgba8888,
        _ => SKColorType.Unknown,
    };

    internal static SKAlphaType ConvertAlphaType(VelloImageAlphaMode alpha) => alpha switch
    {
        VelloImageAlphaMode.Premultiplied => SKAlphaType.Premul,
        _ => SKAlphaType.Unpremul,
    };

    internal static void ConvertColor(Span<byte> pixels, int width, int height, ref SKColorType current, SKColorType desired)
    {
        if (desired == SKColorType.Unknown || current == desired)
        {
            return;
        }

        if ((current == SKColorType.Rgba8888 && desired == SKColorType.Bgra8888) ||
            (current == SKColorType.Bgra8888 && desired == SKColorType.Rgba8888))
        {
            SwapRedBlue(pixels);
            current = desired;
            return;
        }

        if (desired == SKColorType.Rgb888x)
        {
            ForceOpaque(pixels, width, height);
            current = desired;
            return;
        }

        throw new NotSupportedException($"Not implemented: colour conversion from {current} to {desired}.");
    }

    internal static void ConvertAlpha(Span<byte> pixels, int width, int height, ref SKAlphaType current, SKAlphaType desired)
    {
        if (desired == SKAlphaType.Unknown || current == desired)
        {
            return;
        }

        switch (desired)
        {
            case SKAlphaType.Premul:
                if (current == SKAlphaType.Unpremul || current == SKAlphaType.Unknown)
                {
                    Premultiply(pixels, width, height);
                }
                else if (current == SKAlphaType.Opaque)
                {
                    // already opaque, nothing to do
                }
                else
                {
                    return;
                }

                current = SKAlphaType.Premul;
                return;

            case SKAlphaType.Unpremul:
                if (current == SKAlphaType.Premul)
                {
                    Unpremultiply(pixels, width, height);
                }
                else if (current == SKAlphaType.Opaque)
                {
                    // treat opaque as unpremultiplied
                }
                else
                {
                    return;
                }

                current = SKAlphaType.Unpremul;
                return;

            case SKAlphaType.Opaque:
                ForceOpaque(pixels, width, height);
                current = SKAlphaType.Opaque;
                return;
        }

        throw new NotSupportedException($"Not implemented: alpha conversion from {current} to {desired}.");
    }

    internal static void SwapRedBlue(Span<byte> pixels)
    {
        for (var i = 0; i < pixels.Length; i += 4)
        {
            (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        }
    }

    internal static void Premultiply(Span<byte> pixels, int width, int height)
    {
        var stride = width * 4;
        for (var y = 0; y < height; y++)
        {
            var row = pixels.Slice(y * stride, stride);
            for (var x = 0; x < width; x++)
            {
                var index = x * 4;
                var alpha = row[index + 3];
                if (alpha == 255)
                {
                    continue;
                }

                if (alpha == 0)
                {
                    row[index + 0] = 0;
                    row[index + 1] = 0;
                    row[index + 2] = 0;
                    continue;
                }

                row[index + 0] = (byte)((row[index + 0] * alpha + 127) / 255);
                row[index + 1] = (byte)((row[index + 1] * alpha + 127) / 255);
                row[index + 2] = (byte)((row[index + 2] * alpha + 127) / 255);
            }
        }
    }

    internal static void Unpremultiply(Span<byte> pixels, int width, int height)
    {
        var stride = width * 4;
        for (var y = 0; y < height; y++)
        {
            var row = pixels.Slice(y * stride, stride);
            for (var x = 0; x < width; x++)
            {
                var index = x * 4;
                var alpha = row[index + 3];
                if (alpha == 0)
                {
                    row[index + 0] = 0;
                    row[index + 1] = 0;
                    row[index + 2] = 0;
                    continue;
                }

                if (alpha == 255)
                {
                    continue;
                }

                var scale = 255f / alpha;
                row[index + 0] = (byte)Math.Clamp(row[index + 0] * scale, 0f, 255f);
                row[index + 1] = (byte)Math.Clamp(row[index + 1] * scale, 0f, 255f);
                row[index + 2] = (byte)Math.Clamp(row[index + 2] * scale, 0f, 255f);
            }
        }
    }

    internal static void ForceOpaque(Span<byte> pixels, int width, int height)
    {
        var stride = width * 4;
        for (var y = 0; y < height; y++)
        {
            var row = pixels.Slice(y * stride, stride);
            for (var x = 0; x < width; x++)
            {
                row[x * 4 + 3] = 255;
            }
        }
    }

    internal static void EnsureBufferMatchesInfo(int length, SKImageInfo info)
    {
        var expected = checked(info.RowBytes * info.Height);
        if (length != expected)
        {
            throw new InvalidOperationException("Decoded pixel buffer does not match the provided image information.");
        }
    }
}
