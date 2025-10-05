using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using VelloSharp;
using VelloImage = VelloSharp.Image;

namespace VelloSharp.WinForms;

public sealed class VelloBitmap : IDisposable
{
    private bool _disposed;
    private readonly bool _ownsImage;

    private VelloBitmap(VelloImage image, int width, int height, RenderFormat format, ImageAlphaMode alphaMode, bool ownsImage)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        Image = image;
        Width = width;
        Height = height;
        Format = format;
        AlphaMode = alphaMode;
        _ownsImage = ownsImage;
    }

    public int Width { get; }

    public int Height { get; }

    public RenderFormat Format { get; }

    public ImageAlphaMode AlphaMode { get; }

    public VelloImage Image { get; }

    public static VelloBitmap FromPixels(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        RenderFormat format = RenderFormat.Bgra8,
        ImageAlphaMode alphaMode = ImageAlphaMode.Premultiplied,
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

        var image = VelloImage.FromPixels(pixels, width, height, format, alphaMode, stride);
        return new VelloBitmap(image, width, height, format, alphaMode, ownsImage: true);
    }

    public static VelloBitmap FromBitmap(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            var byteCount = data.Stride * data.Height;
            var buffer = new byte[byteCount];
            Marshal.Copy(data.Scan0, buffer, 0, byteCount);
            var image = VelloImage.FromPixels(buffer, bitmap.Width, bitmap.Height, RenderFormat.Bgra8, ImageAlphaMode.Premultiplied, data.Stride);
            return new VelloBitmap(image, bitmap.Width, bitmap.Height, RenderFormat.Bgra8, ImageAlphaMode.Premultiplied, ownsImage: true);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    public static VelloBitmap Wrap(VelloImage image, int width, int height, RenderFormat format, ImageAlphaMode alphaMode)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        return new VelloBitmap(image, width, height, format, alphaMode, ownsImage: false);
    }

    public static VelloBitmap Wrap(VelloImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var info = image.GetInfo();
        return new VelloBitmap(image, info.Width, info.Height, info.Format, info.AlphaMode, ownsImage: false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownsImage)
        {
            Image.Dispose();
        }

        _disposed = true;
    }
}
