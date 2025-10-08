using System;
using System.Runtime.InteropServices;

namespace VelloSharp.TreeDataGrid;

public readonly record struct TreeGpuTimestampSummary(
    float GpuTimeMs,
    float QueueTimeMs,
    uint SampleCount);

public readonly record struct TreeFrameStats(
    ulong FrameIndex,
    float CpuTimeMs,
    float GpuTimeMs,
    float QueueTimeMs,
    float FrameIntervalMs,
    uint GpuSampleCount,
    long TimestampMs);

internal sealed class TreeRendererHandle : SafeHandle
{
    private TreeRendererHandle()
        : base(IntPtr.Zero, ownsHandle: true)
    {
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.vello_tdg_renderer_destroy(handle);
        SetHandle(IntPtr.Zero);
        return true;
    }

    public static TreeRendererHandle Create(float targetFps)
    {
        var options = new NativeMethods.VelloTdgRendererOptions
        {
            TargetFps = targetFps,
        };

        var ptr = NativeMethods.vello_tdg_renderer_create(options);
        if (ptr == nint.Zero)
        {
            throw TreeInterop.CreateException("Failed to create tree renderer loop");
        }

        var handle = new TreeRendererHandle();
        handle.SetHandle(ptr);
        return handle;
    }
}

public sealed class TreeRenderLoop : IDisposable
{
    private readonly TreeRendererHandle _handle;

    public TreeRenderLoop(float targetFps = 120f)
    {
        if (!float.IsFinite(targetFps) || targetFps <= 0f)
        {
            targetFps = 120f;
        }

        _handle = TreeRendererHandle.Create(targetFps);
    }

    public bool BeginFrame()
    {
        return Invoke(handle => NativeMethods.vello_tdg_renderer_begin_frame(handle));
    }

    public void RecordGpuSummary(TreeGpuTimestampSummary summary)
    {
        Invoke(handle =>
        {
            var nativeSummary = new NativeMethods.VelloTdgGpuTimestampSummary
            {
                GpuTimeMs = summary.GpuTimeMs,
                QueueTimeMs = summary.QueueTimeMs,
                SampleCount = summary.SampleCount,
            };
            TreeInterop.ThrowIfFalse(
                NativeMethods.vello_tdg_renderer_record_gpu_summary(handle, nativeSummary),
                "Renderer GPU summary submission failed");
            return true;
        });
    }

    public TreeFrameStats EndFrame(float gpuTimeMs, float queueTimeMs)
    {
        return Invoke(handle =>
        {
            unsafe
            {
                NativeMethods.VelloTdgFrameStats stats;
                TreeInterop.ThrowIfFalse(
                    NativeMethods.vello_tdg_renderer_end_frame(handle, gpuTimeMs, queueTimeMs, &stats),
                    "Renderer end frame failed");
                return new TreeFrameStats(
                    stats.FrameIndex,
                    stats.CpuTimeMs,
                    stats.GpuTimeMs,
                    stats.QueueTimeMs,
                    stats.FrameIntervalMs,
                    stats.GpuSampleCount,
                    stats.TimestampMs);
            }
        });
    }

    public void Dispose()
    {
        _handle.Dispose();
        GC.SuppressFinalize(this);
    }

    private T Invoke<T>(Func<nint, T> action)
    {
        bool added = false;
        try
        {
            _handle.DangerousAddRef(ref added);
            return action(_handle.DangerousGetHandle());
        }
        finally
        {
            if (added)
            {
                _handle.DangerousRelease();
            }
        }
    }
}
