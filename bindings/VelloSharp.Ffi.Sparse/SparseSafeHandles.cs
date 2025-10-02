using System;
using System.Runtime.InteropServices;

namespace VelloSharp;

public sealed class SparseRenderContextHandle : SafeHandle
{
    private SparseRenderContextHandle() : base(IntPtr.Zero, ownsHandle: true)
    {
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public static SparseRenderContextHandle Create(ushort width, ushort height)
    {
        var ptr = SparseNativeMethods.vello_sparse_render_context_create(width, height);
        if (ptr == IntPtr.Zero)
        {
            throw new InvalidOperationException(SparseNativeHelpers.GetLastErrorMessage() ?? "Failed to create sparse render context.");
        }

        return new SparseRenderContextHandle { handle = ptr };
    }

    protected override bool ReleaseHandle()
    {
        SparseNativeMethods.vello_sparse_render_context_destroy(handle);
        return true;
    }
}
