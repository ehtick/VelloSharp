using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using VelloSharp;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class VelloBitmapImpl : IBitmapImpl, IWriteableBitmapImpl
{
    private readonly object _lock = new();
    private byte[] _pixels;
    private readonly PixelFormat _pixelFormat;
    private readonly AlphaFormat _alphaFormat;
    private GCHandle? _pinnedPixels;
    private bool _disposed;
    private int _version;

    public VelloBitmapImpl(byte[] pixels, PixelSize size, Vector dpi, PixelFormat pixelFormat, AlphaFormat alphaFormat)
    {
        _pixels = pixels ?? throw new ArgumentNullException(nameof(pixels));
        PixelSize = size;
        Dpi = dpi;
        _pixelFormat = pixelFormat;
        _alphaFormat = alphaFormat;
    }

    public Vector Dpi { get; }

    public PixelSize PixelSize { get; }

    public int Version => _version;

    public PixelFormat? Format => _pixelFormat;

    public AlphaFormat? AlphaFormat => _alphaFormat;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            if (_pinnedPixels is { IsAllocated: true } handle)
            {
                handle.Free();
                _pinnedPixels = null;
            }

            _pixels = Array.Empty<byte>();
            _disposed = true;
        }
    }

    public void Save(string fileName, int? quality = null)
    {
        EnsureNotDisposed();
        if (fileName is null)
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        var data = EncodePng(quality);
        File.WriteAllBytes(fileName, data);
    }

    public void Save(Stream stream, int? quality = null)
    {
        EnsureNotDisposed();
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        var data = EncodePng(quality);
        stream.Write(data, 0, data.Length);
    }

    public ILockedFramebuffer Lock()
    {
        EnsureNotDisposed();

        lock (_lock)
        {
            EnsureNotDisposed();

            if (_pinnedPixels is not { IsAllocated: true } handle)
            {
                handle = GCHandle.Alloc(_pixels, GCHandleType.Pinned);
                _pinnedPixels = handle;
            }

            _version++;

            return new VelloLockedFramebuffer(
                handle,
                PixelSize,
                GetStride(PixelSize.Width, _pixelFormat),
                Dpi,
                _pixelFormat);
        }
    }

    internal Image CreateVelloImage()
    {
        EnsureNotDisposed();

        return Image.FromPixels(
            _pixels,
            PixelSize.Width,
            PixelSize.Height,
            RenderFormat.Rgba8,
            _alphaFormat == global::Avalonia.Platform.AlphaFormat.Premul ? ImageAlphaMode.Premultiplied : ImageAlphaMode.Straight,
            GetStride(PixelSize.Width, _pixelFormat));
    }

    internal byte[] GetPixelsUnsafe() => _pixels;

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VelloBitmapImpl));
        }
    }

    private static int GetStride(int width, PixelFormat format)
    {
        return width * (format.Equals(PixelFormat.Rgba8888) ? 4 : 4);
    }

    private sealed class VelloLockedFramebuffer : ILockedFramebuffer
    {
        private GCHandle _handle;
        private bool _disposed;

        public VelloLockedFramebuffer(GCHandle handle, PixelSize size, int rowBytes, Vector dpi, PixelFormat format)
        {
            _handle = handle;
            Size = size;
            RowBytes = rowBytes;
            Dpi = dpi;
            Format = format;
        }

        public IntPtr Address => _handle.AddrOfPinnedObject();

        public PixelSize Size { get; }

        public int RowBytes { get; }

        public Vector Dpi { get; }

        public PixelFormat Format { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_handle.IsAllocated)
            {
                _handle.Free();
            }

            _disposed = true;
        }
    }

    public static VelloBitmapImpl Create(PixelSize size, Vector dpi)
    {
        return Create(size, dpi, PixelFormat.Rgba8888, global::Avalonia.Platform.AlphaFormat.Unpremul);
    }

    public static VelloBitmapImpl Create(PixelSize size, Vector dpi, PixelFormat pixelFormat, AlphaFormat alphaFormat)
    {
        var stride = GetStride(size.Width, pixelFormat);
        var data = new byte[size.Height * stride];
        return new VelloBitmapImpl(data, size, dpi, pixelFormat, alphaFormat);
    }

    public static VelloBitmapImpl Load(Stream stream)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return DecodeFromBytes(ms.ToArray());
    }

    public static VelloBitmapImpl Load(string fileName)
    {
        if (fileName is null)
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        var data = File.ReadAllBytes(fileName);
        return DecodeFromBytes(data);
    }

    public static VelloBitmapImpl FromPixels(byte[] pixels, PixelSize size, Vector dpi, PixelFormat format, AlphaFormat alpha)
    {
        return new VelloBitmapImpl(pixels, size, dpi, format, alpha);
    }

    public VelloBitmapImpl Resize(PixelSize destination, BitmapInterpolationMode interpolationMode)
    {
        EnsureNotDisposed();

        if (destination.Width <= 0 || destination.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(destination), "Destination size must be positive.");
        }

        var sourceHandle = CreateNativeImageHandle();
        IntPtr resizedHandle = IntPtr.Zero;

        try
        {
            var status = NativeMethods.vello_image_resize(
                sourceHandle,
                (uint)destination.Width,
                (uint)destination.Height,
                ToQuality(interpolationMode),
                out resizedHandle);

            if (status != VelloStatus.Success || resizedHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to resize bitmap.");
            }

            var bitmap = CreateFromNativeImageHandle(resizedHandle, Dpi);
            resizedHandle = IntPtr.Zero;
            return bitmap;
        }
        finally
        {
            if (resizedHandle != IntPtr.Zero)
            {
                NativeMethods.vello_image_destroy(resizedHandle);
            }

            NativeMethods.vello_image_destroy(sourceHandle);
        }
    }

    private byte[] EncodePng(int? quality)
    {
        var compression = NormalizeCompression(quality);
        var imageHandle = CreateNativeImageHandle();
        IntPtr blobHandle = IntPtr.Zero;

        try
        {
            var status = NativeMethods.vello_image_encode_png(imageHandle, compression, out blobHandle);
            if (status != VelloStatus.Success || blobHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to encode bitmap to PNG.");
            }

            status = NativeMethods.vello_blob_get_data(blobHandle, out var blobData);
            if (status != VelloStatus.Success)
            {
                throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Unable to access encoded PNG data.");
            }

            var length = checked((int)blobData.Length);
            var buffer = length == 0 ? Array.Empty<byte>() : new byte[length];
            if (length > 0)
            {
                Marshal.Copy(blobData.Data, buffer, 0, length);
            }

            return buffer;
        }
        finally
        {
            if (blobHandle != IntPtr.Zero)
            {
                NativeMethods.vello_blob_destroy(blobHandle);
            }

            NativeMethods.vello_image_destroy(imageHandle);
        }
    }

    private IntPtr CreateNativeImageHandle()
    {
        lock (_lock)
        {
            EnsureNotDisposed();

            var stride = GetStride(PixelSize.Width, _pixelFormat);
            var format = ToRenderFormat(_pixelFormat);
            var alpha = ToVelloAlphaMode(_alphaFormat);

            GCHandle handle = default;
            try
            {
                handle = GCHandle.Alloc(_pixels, GCHandleType.Pinned);
                var native = NativeMethods.vello_image_create(
                    format,
                    alpha,
                    (uint)PixelSize.Width,
                    (uint)PixelSize.Height,
                    handle.AddrOfPinnedObject(),
                    (nuint)stride);

                if (native == IntPtr.Zero)
                {
                    throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create native image.");
                }

                return native;
            }
            finally
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }
        }
    }

    private static VelloBitmapImpl CreateFromNativeImageHandle(IntPtr imageHandle, Vector dpi)
    {
        try
        {
            var status = NativeMethods.vello_image_get_info(imageHandle, out var info);
            if (status != VelloStatus.Success)
            {
                throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Unable to query image information.");
            }

            status = NativeMethods.vello_image_map_pixels(imageHandle, out var pixelsPtr, out var length);
            if (status != VelloStatus.Success)
            {
                throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Unable to map image pixels.");
            }

            try
            {
                var pixelCount = checked((int)length);
                var pixels = pixelCount == 0 ? Array.Empty<byte>() : new byte[pixelCount];

                if (pixelCount > 0)
                {
                    Marshal.Copy(pixelsPtr, pixels, 0, pixelCount);
                }

                var pixelFormat = info.Format switch
                {
                    VelloRenderFormat.Bgra8 => PixelFormat.Bgra8888,
                    VelloRenderFormat.Rgba8 => PixelFormat.Rgba8888,
                    _ => throw new NotSupportedException($"Unsupported pixel format: {info.Format}"),
                };

                var alphaFormat = info.Alpha == VelloImageAlphaMode.Premultiplied
                    ? global::Avalonia.Platform.AlphaFormat.Premul
                    : global::Avalonia.Platform.AlphaFormat.Unpremul;

                return new VelloBitmapImpl(
                    pixels,
                    new PixelSize((int)info.Width, (int)info.Height),
                    dpi,
                    pixelFormat,
                    alphaFormat);
            }
            finally
            {
                NativeMethods.vello_image_unmap_pixels(imageHandle);
            }
        }
        finally
        {
            NativeMethods.vello_image_destroy(imageHandle);
        }
    }

    private static byte NormalizeCompression(int? quality)
    {
        if (quality is null)
        {
            return 6;
        }

        return (byte)Math.Clamp(quality.Value, 0, 9);
    }

    private static VelloImageQualityMode ToQuality(BitmapInterpolationMode mode) => mode switch
    {
        BitmapInterpolationMode.None => VelloImageQualityMode.Low,
        BitmapInterpolationMode.LowQuality => VelloImageQualityMode.Low,
        BitmapInterpolationMode.MediumQuality => VelloImageQualityMode.Medium,
        BitmapInterpolationMode.HighQuality => VelloImageQualityMode.High,
        _ => VelloImageQualityMode.Medium,
    };

    private static VelloRenderFormat ToRenderFormat(PixelFormat format)
    {
        if (format.Equals(PixelFormat.Bgra8888))
        {
            return VelloRenderFormat.Bgra8;
        }

        if (format.Equals(PixelFormat.Rgba8888))
        {
            return VelloRenderFormat.Rgba8;
        }

        throw new NotSupportedException($"Unsupported pixel format: {format}");
    }

    private static VelloImageAlphaMode ToVelloAlphaMode(global::Avalonia.Platform.AlphaFormat alphaFormat) => alphaFormat switch
    {
        global::Avalonia.Platform.AlphaFormat.Premul => VelloImageAlphaMode.Premultiplied,
        _ => VelloImageAlphaMode.Straight,
    };

    private static unsafe VelloBitmapImpl DecodeFromBytes(byte[] data)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (data.Length == 0)
        {
            throw new InvalidOperationException("Image stream is empty.");
        }

        fixed (byte* ptr = data)
        {
            var status = NativeMethods.vello_image_decode_png(ptr, (nuint)data.LongLength, out var imageHandle);
            if (status != VelloStatus.Success || imageHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to decode PNG image.");
            }

            return CreateFromNativeImageHandle(imageHandle, new Vector(96, 96));
        }
    }
}
