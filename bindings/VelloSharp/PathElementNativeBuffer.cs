using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace VelloSharp;

internal static class PathElementNativeExtensions
{
    public static ReadOnlySpan<VelloPathElement> AsNativeSpan(this PathBuilder path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.AsSpan().AsNativeSpan();
    }

    public static ReadOnlySpan<VelloPathElement> AsNativeSpan(this ReadOnlySpan<PathElement> elements) =>
        MemoryMarshal.Cast<PathElement, VelloPathElement>(elements);

    public static ReadOnlySpan<KurboPathElement> AsKurboSpan(this PathBuilder path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.AsSpan().AsKurboSpan();
    }

    public static ReadOnlySpan<KurboPathElement> AsKurboSpan(this ReadOnlySpan<PathElement> elements) =>
        MemoryMarshal.Cast<PathElement, KurboPathElement>(elements);
}

internal struct NativePathElements : IDisposable
{
    private readonly PathBuilder? _path;
    private readonly VelloPathElement[]? _buffer;
    private readonly int _length;

    private NativePathElements(PathBuilder path)
    {
        _path = path;
        _buffer = null;
        _length = path.Count;
    }

    private NativePathElements(VelloPathElement[]? buffer, int length)
    {
        _path = null;
        _buffer = buffer;
        _length = length;
    }

    public ReadOnlySpan<VelloPathElement> Span
    {
        get
        {
            if (_path is PathBuilder builder)
            {
                return builder.AsSpan().AsNativeSpan();
            }

            return _buffer is null ? ReadOnlySpan<VelloPathElement>.Empty : _buffer.AsSpan(0, _length);
        }
    }

    public static NativePathElements Rent(PathBuilder path) => new(path);

    public void Dispose()
    {
        if (_buffer is { })
        {
            ArrayPool<VelloPathElement>.Shared.Return(_buffer);
        }
    }
}
