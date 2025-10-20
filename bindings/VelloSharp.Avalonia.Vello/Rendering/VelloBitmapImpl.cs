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

            var handle = GCHandle.Alloc(_pixels, GCHandleType.Pinned);
            var address = handle.AddrOfPinnedObject();
            var size = PixelSize;
            var rowBytes = GetStride(size.Width, _pixelFormat);
            var dpi = Dpi;
            var format = _pixelFormat;

            _version++;

            return new LockedFramebuffer(
                address,
                size,
                rowBytes,
                dpi,
                format,
                () =>
                {
                    if (handle.IsAllocated)
                    {
                        handle.Free();
                    }
                });
        }
    }

    internal Image CreateVelloImage()
    {
        EnsureNotDisposed();

        lock (_lock)
        {
            EnsureNotDisposed();

            var pixelData = PreparePixelDataForVello();
            return Image.FromPixels(
                pixelData.Buffer,
                PixelSize.Width,
                PixelSize.Height,
                pixelData.Format,
                pixelData.AlphaMode,
                pixelData.Stride);
        }
    }

    internal byte[] GetPixelsUnsafe() => _pixels;

    private PixelData PreparePixelDataForVello()
    {
        var stride = GetStride(PixelSize.Width, _pixelFormat);
        var pixelBuffer = _pixels;
        var pixelFormat = _pixelFormat;

        if (_alphaFormat == global::Avalonia.Platform.AlphaFormat.Premul)
        {
            pixelBuffer = ConvertPremultipliedToStraight(pixelBuffer, PixelSize, stride, pixelFormat);
        }

        var renderFormat = (RenderFormat)ToRenderFormat(pixelFormat);

        if (renderFormat == RenderFormat.Bgra8)
        {
            pixelBuffer = ConvertBgraToRgba(pixelBuffer, PixelSize, stride);
            renderFormat = RenderFormat.Rgba8;
            pixelFormat = PixelFormat.Rgba8888;
        }

        return new PixelData(pixelBuffer, stride, renderFormat, ImageAlphaMode.Straight);
    }

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

            var pixelData = PreparePixelDataForVello();

            GCHandle handle = default;
            try
            {
                handle = GCHandle.Alloc(pixelData.Buffer, GCHandleType.Pinned);
                var native = NativeMethods.vello_image_create(
                    (VelloRenderFormat)pixelData.Format,
                    (VelloImageAlphaMode)pixelData.AlphaMode,
                    (uint)PixelSize.Width,
                    (uint)PixelSize.Height,
                    handle.AddrOfPinnedObject(),
                    (nuint)pixelData.Stride);

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

    private static byte[] ConvertPremultipliedToStraight(byte[] source, PixelSize size, int stride, PixelFormat pixelFormat)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (stride <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stride));
        }

        var height = Math.Max(0, size.Height);
        var expectedLength = stride * height;
        if (source.Length < expectedLength)
        {
            throw new ArgumentException("Pixel buffer is smaller than expected for the provided stride and size.", nameof(source));
        }

        var destination = new byte[expectedLength];
        Buffer.BlockCopy(source, 0, destination, 0, expectedLength);

        var (r, g, b, a) = GetChannelIndices(pixelFormat);
        var width = size.Width;

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * stride;
            for (var x = 0; x < width; x++)
            {
                var offset = rowOffset + (x * 4);
                var alpha = source[offset + a];

                destination[offset + a] = alpha;

                if (alpha == 0)
                {
                    destination[offset + r] = 0;
                    destination[offset + g] = 0;
                    destination[offset + b] = 0;
                    continue;
                }

                destination[offset + r] = UnpremultiplyChannel(source[offset + r], alpha);
                destination[offset + g] = UnpremultiplyChannel(source[offset + g], alpha);
                destination[offset + b] = UnpremultiplyChannel(source[offset + b], alpha);
            }
        }

        return destination;
    }

    private static byte[] ConvertBgraToRgba(byte[] source, PixelSize size, int stride)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (stride <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stride));
        }

        var height = Math.Max(0, size.Height);
        var expectedLength = stride * height;
        if (source.Length < expectedLength)
        {
            throw new ArgumentException("Pixel buffer is smaller than expected for the provided stride and size.", nameof(source));
        }

        if (expectedLength == 0)
        {
            return Array.Empty<byte>();
        }

        var destination = new byte[expectedLength];

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * stride;
            for (var x = 0; x < size.Width; x++)
            {
                var offset = rowOffset + (x * 4);
                destination[offset + 0] = source[offset + 2]; // R
                destination[offset + 1] = source[offset + 1]; // G
                destination[offset + 2] = source[offset + 0]; // B
                destination[offset + 3] = source[offset + 3]; // A
            }
        }

        return destination;
    }

    private static (int R, int G, int B, int A) GetChannelIndices(PixelFormat pixelFormat)
    {
        if (pixelFormat.Equals(PixelFormat.Bgra8888))
        {
            return (2, 1, 0, 3);
        }

        if (pixelFormat.Equals(PixelFormat.Rgba8888))
        {
            return (0, 1, 2, 3);
        }

        throw new NotSupportedException($"Unsupported pixel format: {pixelFormat}");
    }

    private static byte UnpremultiplyChannel(byte value, byte alpha)
    {
        if (alpha == 0)
        {
            return 0;
        }

        var result = (value * 255 + (alpha / 2)) / alpha;
        return (byte)Math.Clamp(result, 0, 255);
    }

    private readonly struct PixelData
    {
        public PixelData(byte[] buffer, int stride, RenderFormat format, ImageAlphaMode alphaMode)
        {
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Stride = stride;
            Format = format;
            AlphaMode = alphaMode;
        }

        public byte[] Buffer { get; }

        public int Stride { get; }

        public RenderFormat Format { get; }

        public ImageAlphaMode AlphaMode { get; }
    }

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

        if (HasPngSignature(data))
        {
            return DecodePngBytes(data);
        }

        if (HasIcoSignature(data))
        {
            return DecodeIcoBytes(data);
        }

        throw new InvalidOperationException("Unsupported image format. Only PNG and ICO are supported.");
    }

    private static bool HasPngSignature(byte[] data)
    {
        return data.Length >= 8
            && data[0] == 0x89
            && data[1] == 0x50
            && data[2] == 0x4E
            && data[3] == 0x47
            && data[4] == 0x0D
            && data[5] == 0x0A
            && data[6] == 0x1A
            && data[7] == 0x0A;
    }

    private static bool HasIcoSignature(byte[] data)
    {
        return data.Length >= 4
            && data[0] == 0x00
            && data[1] == 0x00
            && data[2] == 0x01
            && data[3] == 0x00;
    }

    private static unsafe VelloBitmapImpl DecodePngBytes(byte[] data)
    {
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

    private static unsafe VelloBitmapImpl DecodeIcoBytes(byte[] data)
    {
        fixed (byte* ptr = data)
        {
            var status = NativeMethods.vello_image_decode_ico(ptr, (nuint)data.LongLength, out var imageHandle);
            if (status != VelloStatus.Success || imageHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to decode ICO image.");
            }

            return CreateFromNativeImageHandle(imageHandle, new Vector(96, 96));
        }
    }
}
