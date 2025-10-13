using System;
using VelloSharp.Windows;

namespace VelloSharp.Windows.Shared.Presenters;

public sealed class VelloSwapChainRenderEventArgs : EventArgs
{
    internal VelloSwapChainRenderEventArgs(
        WindowsGpuContextLease lease,
        WindowsSwapChainSurface surface,
        WindowsSurfaceSize pixelSize,
        TimeSpan timestamp,
        TimeSpan delta,
        long frameId)
    {
        Lease = lease ?? throw new ArgumentNullException(nameof(lease));
        Surface = surface ?? throw new ArgumentNullException(nameof(surface));
        PixelSize = pixelSize;
        Timestamp = timestamp;
        Delta = delta;
        FrameId = frameId;
    }

    public WindowsGpuContextLease Lease { get; }

    public WindowsSwapChainSurface Surface { get; }

    public WindowsSurfaceSize PixelSize { get; }

    public TimeSpan Timestamp { get; }

    public TimeSpan Delta { get; }

    public long FrameId { get; }
}
