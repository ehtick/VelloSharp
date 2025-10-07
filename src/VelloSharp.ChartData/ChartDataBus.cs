using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace VelloSharp.ChartData;

/// <summary>
/// Lock-free buffering of streaming chart data segments.
/// </summary>
public sealed class ChartDataBus
{
    private readonly ConcurrentQueue<ChartDataSlice> _queue = new();
    private readonly int _capacity;
    private int _count;

    public ChartDataBus(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
    }

    public int Count => _count;

    public void Write<T>(ReadOnlySpan<T> source) where T : unmanaged
    {
        if (source.IsEmpty)
        {
            return;
        }

        var bytes = checked(source.Length * Unsafe.SizeOf<T>());
        var owner = MemoryPool<byte>.Shared.Rent(bytes);
        var destination = owner.Memory.Span[..bytes];
        MemoryMarshal.AsBytes(source).CopyTo(destination);

        var slice = new ChartDataSlice(owner, bytes, source.Length, Unsafe.SizeOf<T>(), typeof(T));
        _queue.Enqueue(slice);

        if (Interlocked.Increment(ref _count) > _capacity)
        {
            if (_queue.TryDequeue(out var overflow))
            {
                overflow.Dispose();
            }

            Interlocked.Decrement(ref _count);
        }
    }

    public bool TryRead(out ChartDataSlice slice)
    {
        if (_queue.TryDequeue(out slice))
        {
            Interlocked.Decrement(ref _count);
            return true;
        }

        return false;
    }
}

/// <summary>
/// Represents a pooled data slice stored in the bus.
/// </summary>
public readonly struct ChartDataSlice : IDisposable
{
    private readonly IMemoryOwner<byte> _owner;
    private readonly int _byteLength;
    private readonly int _itemCount;
    private readonly int _itemSize;
    private readonly Type _elementType;

    internal ChartDataSlice(IMemoryOwner<byte> owner, int byteLength, int itemCount, int itemSize, Type elementType)
    {
        _owner = owner;
        _byteLength = byteLength;
        _itemCount = itemCount;
        _itemSize = itemSize;
        _elementType = elementType;
    }

    public int ItemCount => _itemCount;

    public int ItemSize => _itemSize;

    public Type ElementType => _elementType;

    public ReadOnlySpan<byte> AsBytes() => _owner.Memory.Span[.._byteLength];

    public ReadOnlySpan<T> AsSpan<T>() where T : unmanaged
    {
        if (typeof(T) != _elementType)
        {
            throw new InvalidOperationException($"Slice element type {_elementType} does not match requested {typeof(T)}.");
        }

        return MemoryMarshal.Cast<byte, T>(AsBytes());
    }

    public void Dispose()
    {
        _owner.Dispose();
    }
}
