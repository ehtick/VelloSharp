using System;
using VelloSharp;

namespace VelloSharp.Integration.Rendering;

public readonly struct RenderTargetDescriptor
{
    public RenderTargetDescriptor(uint width, uint height, RenderFormat format, int strideBytes)
    {
        if (width == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (height == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        }

        if (strideBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(strideBytes), "Stride must be positive.");
        }

        var bytesPerPixel = GetBytesPerPixel(format);
        var minimumStride = checked((int)(width * (uint)bytesPerPixel));
        if (strideBytes < minimumStride)
        {
            throw new ArgumentOutOfRangeException(nameof(strideBytes), $"Stride must be at least {minimumStride} bytes for the requested size.");
        }

        Width = width;
        Height = height;
        Format = format;
        StrideBytes = strideBytes;
        BytesPerPixel = bytesPerPixel;
        RequiredBufferSize = checked((int)((long)strideBytes * height));
    }

    public uint Width { get; }
    public uint Height { get; }
    public RenderFormat Format { get; }
    public int StrideBytes { get; }
    public int BytesPerPixel { get; }
    public int RequiredBufferSize { get; }

    public RenderParams Apply(in RenderParams renderParams) => renderParams with
    {
        Width = Width,
        Height = Height,
        Format = Format,
    };

    public void EnsureBuffer(Span<byte> buffer)
    {
        if (buffer.Length < RequiredBufferSize)
        {
            throw new ArgumentException($"Destination buffer must be at least {RequiredBufferSize} bytes.", nameof(buffer));
        }
    }

    internal static int GetBytesPerPixel(RenderFormat format) => format switch
    {
        RenderFormat.Rgba8 => 4,
        RenderFormat.Bgra8 => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported render format."),
    };
}
