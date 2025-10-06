using System;
using System.Runtime.InteropServices;

namespace VelloSharp;

[StructLayout(LayoutKind.Sequential)]
internal struct VelloSharedTextureDesc
{
    public uint Width;
    public uint Height;
    public IntPtr Label;
    [MarshalAs(UnmanagedType.I1)]
    public bool UseKeyedMutex;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloSharedTextureHandle
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

