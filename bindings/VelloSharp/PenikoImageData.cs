using System;

namespace VelloSharp;

public sealed class PenikoImageData : IDisposable
{
    private nint _handle;

    private PenikoImageData(nint handle)
    {
        if (handle == nint.Zero)
        {
            throw new InvalidOperationException("Failed to create Peniko image data.");
        }

        _handle = handle;
    }

    public static PenikoImageData FromImage(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var handle = PenikoNativeMethods.peniko_image_data_create_from_vello(image.Handle);
        if (handle == nint.Zero)
        {
            throw new InvalidOperationException("Failed to clone image into Peniko image data.");
        }

        return new PenikoImageData(handle);
    }

    public static PenikoImageData FromPixels(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        PenikoImageFormat format = PenikoImageFormat.Rgba8,
        PenikoImageAlphaType alpha = PenikoImageAlphaType.Alpha,
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
                var handle = PenikoNativeMethods.peniko_image_data_create(
                    format,
                    alpha,
                    (uint)width,
                    (uint)height,
                    (IntPtr)ptr,
                    (nuint)stride);
                if (handle == nint.Zero)
                {
                    throw new InvalidOperationException("Failed to create Peniko image data from pixels.");
                }

                return new PenikoImageData(handle);
            }
        }
    }

    public PenikoImageData Clone()
    {
        var clone = PenikoNativeMethods.peniko_image_data_clone(Handle);
        if (clone == nint.Zero)
        {
            throw new InvalidOperationException("Failed to clone Peniko image data.");
        }

        return new PenikoImageData(clone);
    }

    public PenikoImageDataInfo GetInfo()
    {
        NativeHelpers.ThrowOnError(PenikoNativeMethods.peniko_image_data_get_info(Handle, out var native), "peniko_image_data_get_info");
        var width = checked((int)native.Width);
        var height = checked((int)native.Height);
        var stride = checked((int)native.Stride);
        return new PenikoImageDataInfo(width, height, native.Format, native.Alpha, stride);
    }

    public void CopyPixels(Span<byte> destination)
    {
        var info = GetInfo();
        var required = checked(info.Stride * info.Height);
        if (destination.Length < required)
        {
            throw new ArgumentException("Destination buffer is too small for the image data.", nameof(destination));
        }

        unsafe
        {
            fixed (byte* ptr = destination)
            {
                NativeHelpers.ThrowOnError(
                    PenikoNativeMethods.peniko_image_data_copy_pixels(Handle, (IntPtr)ptr, (nuint)required),
                    "peniko_image_data_copy_pixels");
            }
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_handle != nint.Zero)
        {
            PenikoNativeMethods.peniko_image_data_destroy(_handle);
            _handle = nint.Zero;
        }
    }

    ~PenikoImageData()
    {
        Dispose(disposing: false);
    }

    internal nint DangerousGetHandle()
    {
        if (_handle == nint.Zero)
        {
            throw new ObjectDisposedException(nameof(PenikoImageData));
        }

        return _handle;
    }

    internal static PenikoImageData FromNativeHandle(nint handle) => new(handle);

    private nint Handle
    {
        get
        {
            if (_handle == nint.Zero)
            {
                throw new ObjectDisposedException(nameof(PenikoImageData));
            }

            return _handle;
        }
    }
}

public readonly record struct PenikoImageDataInfo(
    int Width,
    int Height,
    PenikoImageFormat Format,
    PenikoImageAlphaType Alpha,
    int Stride);
