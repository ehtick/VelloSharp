using System;
using System.Runtime.InteropServices;
using VelloSharp.ChartDiagnostics;

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
    private readonly FrameDiagnosticsCollector _diagnostics;
    private readonly bool _ownsDiagnostics;
    private TreeFrameStats _lastFrameStats;

    public TreeRenderLoop(
        float targetFps = 120f,
        FrameDiagnosticsCollector? diagnostics = null,
        IChartTelemetrySink? telemetrySink = null)
    {
        if (!float.IsFinite(targetFps) || targetFps <= 0f)
        {
            targetFps = 120f;
        }

        _handle = TreeRendererHandle.Create(targetFps);
        if (diagnostics is null)
        {
            var sink = telemetrySink ?? DashboardTelemetrySink.Instance;
            _diagnostics = new FrameDiagnosticsCollector(sink);
            _ownsDiagnostics = true;
        }
        else
        {
            _diagnostics = diagnostics;
            _ownsDiagnostics = false;
            if (telemetrySink is not null)
            {
                _diagnostics.SetTelemetrySink(telemetrySink);
            }
        }
    }

    public FrameDiagnosticsCollector Diagnostics => _diagnostics;

    public TreeFrameStats LastFrameStats => _lastFrameStats;

    public void SetTelemetrySink(IChartTelemetrySink? telemetrySink)
    {
        _diagnostics.SetTelemetrySink(telemetrySink);
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
                var managed = new TreeFrameStats(
                    stats.FrameIndex,
                    stats.CpuTimeMs,
                    stats.GpuTimeMs,
                    stats.QueueTimeMs,
                    stats.FrameIntervalMs,
                    stats.GpuSampleCount,
                    stats.TimestampMs);
                _lastFrameStats = managed;
                _diagnostics.Record(ToChartFrameStats(managed));
                return managed;
            }
        });
    }

    public void Dispose()
    {
        _handle.Dispose();
        if (_ownsDiagnostics)
        {
            _diagnostics.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    private static FrameStats ToChartFrameStats(TreeFrameStats stats)
    {
        static TimeSpan ToTime(float valueMs)
            => double.IsFinite(valueMs) && valueMs > 0f
                ? TimeSpan.FromMilliseconds(valueMs)
                : TimeSpan.Zero;

        var timestamp = stats.TimestampMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(stats.TimestampMs)
            : DateTimeOffset.UtcNow;

        var encodedPaths = stats.GpuSampleCount > int.MaxValue
            ? int.MaxValue
            : (int)stats.GpuSampleCount;

        return new FrameStats(
            ToTime(stats.CpuTimeMs),
            ToTime(stats.GpuTimeMs),
            ToTime(stats.QueueTimeMs),
            encodedPaths,
            timestamp);
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
