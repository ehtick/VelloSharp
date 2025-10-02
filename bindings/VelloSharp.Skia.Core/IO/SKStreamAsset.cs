using System;

namespace SkiaSharp;

public sealed class SKStreamAsset : IDisposable
{
    private readonly byte[] _data;
    private int _position;

    internal SKStreamAsset(byte[] data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _position = 0;
    }

    public int Length => _data.Length;

    public int Read(byte[] buffer, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (count <= 0)
        {
            return 0;
        }

        var remaining = Length - _position;
        if (remaining <= 0)
        {
            return 0;
        }

        var toCopy = Math.Min(count, remaining);
        Array.Copy(_data, _position, buffer, 0, toCopy);
        _position += toCopy;
        return toCopy;
    }

    public void Dispose()
    {
        _position = 0;
    }
}
