using System;
using System.Runtime.InteropServices;

namespace VelloSharp.Ffi.Gpu;

public sealed class VelloRendererHandle : SafeHandle
{
    private VelloRendererHandle() : base(IntPtr.Zero, ownsHandle: true)
    {
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public static VelloRendererHandle Create(uint width, uint height)
    {
        var ptr = NativeMethods.vello_renderer_create(width, height);
        if (ptr == IntPtr.Zero)
        {
            throw new InvalidOperationException(GpuNativeHelpers.GetLastErrorMessage() ?? "Failed to create Vello renderer.");
        }

        return new VelloRendererHandle
        {
            handle = ptr,
        };
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.vello_renderer_destroy(handle);
        return true;
    }
}

public sealed class VelloSceneHandle : SafeHandle
{
    private VelloSceneHandle() : base(IntPtr.Zero, ownsHandle: true)
    {
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public static VelloSceneHandle Create()
    {
        var ptr = NativeMethods.vello_scene_create();
        if (ptr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create Vello scene.");
        }

        return new VelloSceneHandle
        {
            handle = ptr,
        };
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.vello_scene_destroy(handle);
        return true;
    }
}
