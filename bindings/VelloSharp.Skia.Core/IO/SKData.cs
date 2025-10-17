using System;
using System.IO;

namespace SkiaSharp;

public sealed class SKData : IDisposable
{
    private byte[]? _buffer;
    private Stream? _backingStream;
    private readonly bool _ownsStream;

    private SKData(byte[] buffer)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
    }

    private SKData(Stream stream, bool ownsStream)
    {
        _backingStream = stream ?? throw new ArgumentNullException(nameof(stream));
        _ownsStream = ownsStream;
    }

    public static SKData CreateCopy(ReadOnlySpan<byte> data)
    {
        return new SKData(data.ToArray());
    }

    public static SKData Create(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (stream is MemoryStream ms && ms.TryGetBuffer(out var arraySegment))
        {
            return new SKData(arraySegment.Array!.AsSpan(arraySegment.Offset, arraySegment.Count).ToArray());
        }

        var ownsStream = stream is SKManagedStream managed && managed.LeaveOpen == false;
        if (!stream.CanSeek)
        {
            var copy = new MemoryStream();
            stream.CopyTo(copy);
            copy.Position = 0;
            return new SKData(copy, ownsStream: true);
        }

        return new SKData(stream, ownsStream);
    }

    public static SKData Create(SKManagedStream stream) => Create((Stream)stream);

    public ReadOnlySpan<byte> AsSpan()
    {
        if (_buffer is not null)
        {
            return _buffer;
        }

        if (_backingStream is null)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        if (!_backingStream.CanSeek)
        {
            using var memory = new MemoryStream();
            _backingStream.CopyTo(memory);
            _buffer = memory.ToArray();
            return _buffer;
        }

        var length = checked((int)_backingStream.Length);
        var buffer = new byte[length];
        var pos = _backingStream.Position;
        _backingStream.Position = 0;
        _backingStream.Read(buffer, 0, length);
        _backingStream.Position = pos;
        _buffer = buffer;
        return _buffer;
    }

    public void SaveTo(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var span = AsSpan();
        if (!span.IsEmpty)
        {
            stream.Write(span);
        }
    }

    public bool SaveTo(Span<byte> destination) => SaveTo(destination, out _);

    public bool SaveTo(Span<byte> destination, out int bytesWritten)
    {
        var span = AsSpan();
        if (span.Length > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        span.CopyTo(destination);
        bytesWritten = span.Length;
        return true;
    }

    public byte[] ToArray()
    {
        var span = AsSpan();
        var buffer = new byte[span.Length];
        span.CopyTo(buffer);
        return buffer;
    }

    public void Dispose()
    {
        _buffer = null;
        if (_backingStream is not null && _ownsStream)
        {
            _backingStream.Dispose();
        }
        _backingStream = null;
        GC.SuppressFinalize(this);
    }
}
