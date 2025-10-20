using System;
using System.Runtime.InteropServices;
using Core = VelloSharp;

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

public sealed class VelloFilterHandle : SafeHandle
{
    private const int ColorMatrixElementCount = Core.VelloFilterColorMatrix.ElementCount;

    private VelloFilterHandle() : base(IntPtr.Zero, ownsHandle: true)
    {
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public static VelloFilterHandle CreateBlur(float sigmaX, float sigmaY)
    {
        var ptr = NativeMethods.vello_filter_blur_create(sigmaX, sigmaY);
        if (ptr == IntPtr.Zero)
        {
            throw new InvalidOperationException(GpuNativeHelpers.GetLastErrorMessage() ?? "Failed to create blur filter.");
        }

        return new VelloFilterHandle
        {
            handle = ptr,
        };
    }

    public static VelloFilterHandle CreateDropShadow(Core.VelloPoint offset, float sigmaX, float sigmaY, Core.VelloColor color)
    {
        var ptr = NativeMethods.vello_filter_drop_shadow_create(offset, sigmaX, sigmaY, color);
        if (ptr == IntPtr.Zero)
        {
            throw new InvalidOperationException(GpuNativeHelpers.GetLastErrorMessage() ?? "Failed to create drop shadow filter.");
        }

        return new VelloFilterHandle
        {
            handle = ptr,
        };
    }

    public static VelloFilterHandle CreateBlend(Core.LayerBlend blend)
    {
        var mix = (Core.VelloBlendMix)(int)blend.Mix;
        var compose = (Core.VelloBlendCompose)(int)blend.Compose;
        var ptr = NativeMethods.vello_filter_blend_create(mix, compose);
        if (ptr == IntPtr.Zero)
        {
            throw new InvalidOperationException(GpuNativeHelpers.GetLastErrorMessage() ?? "Failed to create blend filter.");
        }

        return new VelloFilterHandle
        {
            handle = ptr,
        };
    }

    public static unsafe VelloFilterHandle CreateColorMatrix(ReadOnlySpan<float> matrix)
    {
        if (matrix.Length != ColorMatrixElementCount)
        {
            throw new ArgumentException($"Color matrix must contain {ColorMatrixElementCount} elements.", nameof(matrix));
        }

        fixed (float* ptrMatrix = matrix)
        {
            var ptr = NativeMethods.vello_filter_color_matrix_create(ptrMatrix, (nuint)matrix.Length);
            if (ptr == IntPtr.Zero)
            {
                throw new InvalidOperationException(GpuNativeHelpers.GetLastErrorMessage() ?? "Failed to create color matrix filter.");
            }

            return new VelloFilterHandle
            {
                handle = ptr,
            };
        }
    }

    public VelloFilterHandle Clone()
    {
        var ptr = NativeMethods.vello_filter_retain(handle);
        if (ptr == IntPtr.Zero)
        {
            throw new InvalidOperationException(GpuNativeHelpers.GetLastErrorMessage() ?? "Failed to retain filter handle.");
        }

        return new VelloFilterHandle
        {
            handle = ptr,
        };
    }

    public Core.VelloFilterKind GetKind()
    {
        GpuNativeHelpers.ThrowOnError(NativeMethods.vello_filter_get_kind(handle, out var kind), "vello_filter_get_kind failed");
        return kind;
    }

    public Core.VelloFilterBlur GetBlur()
    {
        GpuNativeHelpers.ThrowOnError(NativeMethods.vello_filter_get_blur(handle, out var blur), "vello_filter_get_blur failed");
        return blur;
    }

    public Core.VelloFilterDropShadow GetDropShadow()
    {
        GpuNativeHelpers.ThrowOnError(NativeMethods.vello_filter_get_drop_shadow(handle, out var dropShadow), "vello_filter_get_drop_shadow failed");
        return dropShadow;
    }

    public Core.VelloFilterBlend GetBlend()
    {
        GpuNativeHelpers.ThrowOnError(NativeMethods.vello_filter_get_blend(handle, out var blend), "vello_filter_get_blend failed");
        return blend;
    }

    public float[] GetColorMatrix()
    {
        GpuNativeHelpers.ThrowOnError(NativeMethods.vello_filter_get_color_matrix(handle, out var matrix), "vello_filter_get_color_matrix failed");
        var result = new float[ColorMatrixElementCount];
        unsafe
        {
            for (var i = 0; i < ColorMatrixElementCount; i++)
            {
                result[i] = matrix.Values[i];
            }
        }
        return result;
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.vello_filter_release(handle);
        return true;
    }
}
