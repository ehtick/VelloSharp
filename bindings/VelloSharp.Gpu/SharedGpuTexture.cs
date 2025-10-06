using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VelloSharp.Ffi.Gpu;

namespace VelloSharp;

public readonly struct AdapterLuid : IEquatable<AdapterLuid>
{
    public int High { get; }
    public uint Low { get; }

    public AdapterLuid(int high, uint low)
    {
        High = high;
        Low = low;
    }

    public bool Equals(AdapterLuid other) => High == other.High && Low == other.Low;

    public override bool Equals(object? obj) => obj is AdapterLuid other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(High, Low);

    public static bool operator ==(AdapterLuid left, AdapterLuid right) => left.Equals(right);

    public static bool operator !=(AdapterLuid left, AdapterLuid right) => !left.Equals(right);
}

public sealed unsafe partial class SharedGpuTexture : SafeHandle
{
    public const WgpuTextureFormat SharedFormat = WgpuTextureFormat.Bgra8Unorm;

    private SharedGpuTexture(IntPtr nativeHandle)
        : base(IntPtr.Zero, ownsHandle: true)
    {
        SetHandle(nativeHandle);
    }

    public static SharedGpuTexture Create(WgpuDevice device, uint width, uint height, bool useKeyedMutex, string? label)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (width == 0 || height == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Texture dimensions must be greater than zero.");
        }

        var desc = new SharedTextureDesc
        {
            Width = width,
            Height = height,
            Label = label is null ? IntPtr.Zero : Marshal.StringToHGlobalAnsi(label),
            UseKeyedMutex = useKeyedMutex,
        };

        try
        {
            var status = SharedTextureNativeMethods.vello_wgpu_device_create_shared_texture(device.Handle, &desc, out var handle);
            if (status != VelloStatus.Success || handle == IntPtr.Zero)
            {
                throw new InvalidOperationException(GpuNativeHelpers.GetLastErrorMessage() ?? "Failed to allocate shared GPU texture.");
            }

            return new SharedGpuTexture(handle);
        }
        finally
        {
            if (desc.Label != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(desc.Label);
            }
        }
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    private ref SharedTextureHandle Native => ref Unsafe.AsRef<SharedTextureHandle>((void*)handle);

    public IntPtr TexturePointer => Native.Texture;

    public IntPtr SharedHandle => Native.SharedHandle;

    public IntPtr KeyedMutex => Native.KeyedMutex;

    public IntPtr WgpuTexturePointer => Native.WgpuTexture;

    public bool SupportsKeyedMutex => KeyedMutex != IntPtr.Zero;

    public AdapterLuid AdapterLuid => new(Native.AdapterLuidHigh, Native.AdapterLuidLow);

    public WgpuTextureFormat Format => SharedFormat;

    public uint Width => Native.Width;

    public uint Height => Native.Height;

    public bool TryAcquireKeyedMutex(ulong key, uint timeoutMilliseconds, out bool timedOut)
    {
        if (IsInvalid)
        {
            throw new ObjectDisposedException(nameof(SharedGpuTexture));
        }

        var status = NativeMethods.vello_shared_texture_acquire_mutex(handle, key, timeoutMilliseconds);
        switch (status)
        {
            case VelloStatus.Success:
                timedOut = false;
                return true;
            case VelloStatus.Timeout:
                timedOut = true;
                return false;
            default:
                timedOut = false;
                NativeHelpers.ThrowOnError(status, "vello_shared_texture_acquire_mutex");
                return false;
        }
    }

    public void ReleaseKeyedMutex(ulong key)
    {
        if (IsInvalid)
        {
            throw new ObjectDisposedException(nameof(SharedGpuTexture));
        }

        var status = NativeMethods.vello_shared_texture_release_mutex(handle, key);
        if (status != VelloStatus.Success)
        {
            NativeHelpers.ThrowOnError(status, "vello_shared_texture_release_mutex");
        }
    }

    public WgpuTextureView CreateView(WgpuTextureViewDescriptor? descriptor = null)
    {
        if (IsInvalid)
        {
            throw new ObjectDisposedException(nameof(SharedGpuTexture));
        }

        var texturePtr = Native.WgpuTexture;
        if (texturePtr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Shared GPU textures do not expose a WGPU texture handle.");
        }

        var texture = new WgpuTexture(texturePtr, ownsHandle: false);
        try
        {
            return texture.CreateView(descriptor);
        }
        finally
        {
            texture.Dispose();
        }
    }


    public void FlushWriters()
    {
        if (IsInvalid)
        {
            throw new ObjectDisposedException(nameof(SharedGpuTexture));
        }

        var status = NativeMethods.vello_shared_texture_flush(handle);
        if (status != VelloStatus.Success)
        {
            NativeHelpers.ThrowOnError(status, "vello_shared_texture_flush");
        }
    }

    protected override bool ReleaseHandle()
    {
        SharedTextureNativeMethods.vello_shared_texture_destroy(handle);
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SharedTextureDesc
    {
        public uint Width;
        public uint Height;
        public IntPtr Label;
        [MarshalAs(UnmanagedType.I1)]
        public bool UseKeyedMutex;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SharedTextureHandle
    {
        public IntPtr Texture;
        public IntPtr SharedHandle;
        public IntPtr KeyedMutex;
        public IntPtr WgpuTexture;
        public uint AdapterLuidLow;
        public int AdapterLuidHigh;
        public uint Width;
        public uint Height;
        public IntPtr Reserved;
    }

    private static partial class SharedTextureNativeMethods
    {
        private const string LibraryName = "vello_ffi";

        [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_device_create_shared_texture")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static partial VelloStatus vello_wgpu_device_create_shared_texture(
            IntPtr device,
            SharedTextureDesc* desc,
            out IntPtr handle);

        [LibraryImport(LibraryName, EntryPoint = "vello_shared_texture_destroy")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static partial void vello_shared_texture_destroy(IntPtr handle);
    }
}


















