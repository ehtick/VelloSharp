using System;
using System.IO;
using System.Runtime.InteropServices;

namespace HarfBuzzSharp;

public sealed class Blob : NativeObject
{
    private readonly ReleaseDelegate? _release;
    private readonly GCHandle? _managedHandle;
    private bool _isImmutable;
    private int? _faceCount;

    public static Blob Empty { get; } = new Blob(IntPtr.Zero, 0, MemoryMode.ReadOnly);

    public Blob(IntPtr data, int length, MemoryMode memoryMode, ReleaseDelegate? release = null)
        : base(data)
    {
        Length = length;
        MemoryMode = memoryMode;
        _release = release;
    }

    private Blob(byte[] data, MemoryMode memoryMode)
        : base(IntPtr.Zero)
    {
        _managedHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        Handle = _managedHandle.Value.AddrOfPinnedObject();
        Length = data.Length;
        MemoryMode = memoryMode;
    }

    public MemoryMode MemoryMode { get; }

    public int Length { get; }

    public int FaceCount
    {
        get
        {
            if (_faceCount.HasValue)
            {
                return _faceCount.Value;
            }

            var count = CalculateFaceCount();
            _faceCount = count;
            return count;
        }
    }

    public bool IsImmutable => _isImmutable;

    public void MakeImmutable() => _isImmutable = true;

    public ReadOnlySpan<byte> AsSpan()
    {
        if (Handle == IntPtr.Zero || Length == 0)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        unsafe
        {
            return new ReadOnlySpan<byte>((void*)Handle, Length);
        }
    }

    public Stream AsStream()
    {
        if (Handle == IntPtr.Zero || Length == 0)
        {
            return Stream.Null;
        }

        unsafe
        {
            return new UnmanagedMemoryStream((byte*)Handle, Length);
        }
    }

    public static Blob FromStream(Stream stream)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return new Blob(ms.ToArray(), MemoryMode.ReadOnly);
    }

    protected override void DisposeHandler()
    {
        if (_managedHandle is { IsAllocated: true } handle)
        {
            handle.Free();
        }

        _release?.Invoke();
    }

    private int CalculateFaceCount()
    {
        if (Handle == IntPtr.Zero || Length == 0)
        {
            return 0;
        }

        var status = global::VelloSharp.NativeMethods.vello_font_count_faces(
            Handle,
            (nuint)Math.Max(0, Length),
            out var count);

        if (status == global::VelloSharp.VelloStatus.Success)
        {
            if (count == 0)
            {
                return Length > 0 ? 1 : 0;
            }

            return count >= int.MaxValue ? int.MaxValue : (int)count;
        }

        return Length > 0 ? 1 : 0;
    }
}

public delegate void ReleaseDelegate();
