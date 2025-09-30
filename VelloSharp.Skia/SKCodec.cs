using System;
using System.IO;

namespace SkiaSharp;

public sealed class SKCodec : IDisposable
{
    private readonly SKData _data;
    private readonly SKImageInfo _info;

    private SKCodec(SKData data, SKImageInfo info)
    {
        _data = data;
        _info = info;
    }

    public static SKCodec Create(SKData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var info = SKImageInfoExtensions.DecodeBitmapInfo(data.AsSpan());
        return new SKCodec(data, info);
    }

    public static SKCodec Create(SKManagedStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Create(SKData.Create(stream));
    }

    public SKImageInfo Info => _info;

    public SKImageInfo GetScaledDimensions(float scale)
    {
        var width = Math.Max(1, (int)Math.Round(_info.Width * scale));
        var height = Math.Max(1, (int)Math.Round(_info.Height * scale));
        return new SKImageInfo(width, height, _info.ColorType, _info.AlphaType);
    }

    public SKImageInfo GetScaledDimensions(float scaleX, float scaleY)
    {
        var width = Math.Max(1, (int)Math.Round(_info.Width * scaleX));
        var height = Math.Max(1, (int)Math.Round(_info.Height * scaleY));
        return new SKImageInfo(width, height, _info.ColorType, _info.AlphaType);
    }

    public byte[] ToEncodedBytes() => _data.AsSpan().ToArray();

    internal ReadOnlySpan<byte> AsSpan() => _data.AsSpan();

    public void Dispose() => _data.Dispose();
}
