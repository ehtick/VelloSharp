using System;

namespace VelloSharp;

public sealed class Image : IDisposable
{
    private IntPtr _handle;

    private Image(IntPtr handle)
    {
        _handle = handle;
    }

    public IntPtr Handle
    {
        get
        {
            if (_handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(Image));
            }
            return _handle;
        }
    }

    public static Image FromPixels(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        RenderFormat format = RenderFormat.Rgba8,
        ImageAlphaMode alphaMode = ImageAlphaMode.Straight,
        int stride = 0)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }
        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }
        var bytesPerRow = checked(width * 4);
        if (stride == 0)
        {
            stride = bytesPerRow;
        }
        if (stride < bytesPerRow)
        {
            throw new ArgumentOutOfRangeException(nameof(stride));
        }
        var required = checked(stride * height);
        if (pixels.Length < required)
        {
            throw new ArgumentException("Pixel data is smaller than expected for the provided stride and dimensions.", nameof(pixels));
        }

        var slice = pixels[..required];
        unsafe
        {
            fixed (byte* ptr = slice)
            {
                var native = NativeMethods.vello_image_create(
                    (VelloRenderFormat)format,
                    (VelloImageAlphaMode)alphaMode,
                    (uint)width,
                    (uint)height,
                    (IntPtr)ptr,
                    (nuint)stride);
                if (native == IntPtr.Zero)
                {
                    throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create image.");
                }
                return new Image(native);
            }
        }
    }

    public ImageInfo GetInfo()
    {
        var status = NativeMethods.vello_image_get_info(Handle, out var nativeInfo);
        NativeHelpers.ThrowOnError(status, "Failed to query image info");

        if (nativeInfo.Width == 0 || nativeInfo.Height == 0)
        {
            throw new InvalidOperationException("Image dimensions must be greater than zero.");
        }

        var width = checked((int)nativeInfo.Width);
        var height = checked((int)nativeInfo.Height);
        var stride = checked((int)nativeInfo.Stride);
        var format = (RenderFormat)nativeInfo.Format;
        var alpha = (ImageAlphaMode)nativeInfo.Alpha;
        return new ImageInfo(width, height, format, alpha, stride);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_handle != IntPtr.Zero)
        {
            var phase = disposing ? "Dispose" : "Finalizer";
            NativeMethods.vello_image_destroy(_handle);
            _handle = IntPtr.Zero;
        }
    }

    ~Image()
    {
        Dispose(disposing: false);
    }
}

public readonly record struct ImageInfo(int Width, int Height, RenderFormat Format, ImageAlphaMode AlphaMode, int Stride);
