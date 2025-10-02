using System;
using System.IO;
using System.Runtime.InteropServices;

namespace HarfBuzzSharp;

public sealed class Blob : IDisposable
{
    private readonly IntPtr _data;
    private readonly int _length;
    private readonly ReleaseDelegate? _release;
    private bool _disposed;

    public Blob(IntPtr data, int length, MemoryMode memoryMode, ReleaseDelegate? release = null)
    {
        _data = data;
        _length = length;
        MemoryMode = memoryMode;
        _release = release;
    }

    public static Blob FromStream(Stream stream)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var data = ms.ToArray();
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        return new Blob(handle.AddrOfPinnedObject(), data.Length, MemoryMode.ReadOnly, () => handle.Free());
    }

    public MemoryMode MemoryMode { get; }

    public int Length => _length;

    public ReadOnlySpan<byte> AsSpan()
    {
        if (_disposed || _data == IntPtr.Zero)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        unsafe
        {
            return new ReadOnlySpan<byte>((void*)_data, _length);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _release?.Invoke();
    }
}

public delegate void ReleaseDelegate();
