using System;

namespace VelloSharp.Rendering;

public readonly record struct RenderTargetDescriptor(
    uint Width,
    uint Height,
    RenderFormat Format,
    int StrideBytes)
{
    public int RequiredBufferSize => checked((int)Height * StrideBytes);

    public void EnsureBuffer(Span<byte> buffer)
    {
        if (buffer.Length < RequiredBufferSize)
        {
            throw new ArgumentException($"Destination buffer must be at least {RequiredBufferSize} bytes.", nameof(buffer));
        }
    }

    public RenderParams Apply(RenderParams renderParams)
    {
        var width = Width == 0 ? renderParams.Width : Width;
        var height = Height == 0 ? renderParams.Height : Height;
        return renderParams with
        {
            Width = width,
            Height = height,
            Format = Format,
        };
    }
}
