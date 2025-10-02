using System;
using System.Buffers;

namespace VelloSharp;

internal static class PathElementNativeExtensions
{
    public static VelloPathElement ToNative(this PathElement element) => new()
    {
        Verb = (VelloPathVerb)element.Verb,
        X0 = element.X0,
        Y0 = element.Y0,
        X1 = element.X1,
        Y1 = element.Y1,
        X2 = element.X2,
        Y2 = element.Y2,
    };
}

internal struct NativePathElements : IDisposable
{
    private VelloPathElement[]? _buffer;
    private int _length;

    private NativePathElements(VelloPathElement[]? buffer, int length)
    {
        _buffer = buffer;
        _length = length;
    }

    public ReadOnlySpan<VelloPathElement> Span =>
        _buffer is null ? ReadOnlySpan<VelloPathElement>.Empty : _buffer.AsSpan(0, _length);

    public static NativePathElements Rent(PathBuilder path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var source = path.AsSpan();
        if (source.IsEmpty)
        {
            return new NativePathElements(null, 0);
        }

        var buffer = ArrayPool<VelloPathElement>.Shared.Rent(source.Length);
        var span = buffer.AsSpan(0, source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            span[i] = source[i].ToNative();
        }

        return new NativePathElements(buffer, source.Length);
    }

    public void Dispose()
    {
        if (_buffer is { })
        {
            ArrayPool<VelloPathElement>.Shared.Return(_buffer);
            _buffer = null;
            _length = 0;
        }
    }
}
