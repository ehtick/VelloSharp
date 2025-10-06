using System;
using VelloSharp.Windows;

namespace VelloSharp.Wpf.Integration;

public sealed class SwapChainLeaseEventArgs : EventArgs
{
    public SwapChainLeaseEventArgs(WindowsGpuContextLease lease, WindowsSwapChainSurface surface, WindowsSurfaceSize pixelSize)
    {
        Lease = lease ?? throw new ArgumentNullException(nameof(lease));
        Surface = surface ?? throw new ArgumentNullException(nameof(surface));
        PixelSize = pixelSize;
    }

    public WindowsGpuContextLease Lease { get; }

    public WindowsSwapChainSurface Surface { get; }

    public WindowsSurfaceSize PixelSize { get; }
}
