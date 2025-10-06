using System;

namespace VelloSharp.Windows;

public static class WindowsSurfaceFactory
{
    public static WindowsSwapChainSurface? EnsureSwapChainSurface(
        WindowsGpuContextLease lease,
        IWindowsSurfaceSource source,
        WindowsSwapChainSurface? current)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(source);

        var size = source.GetSurfaceSize();
        return EnsureSwapChainSurface(lease, source, current, size);
    }

    public static WindowsSwapChainSurface? EnsureSwapChainSurface(
        WindowsGpuContextLease lease,
        IWindowsSurfaceSource source,
        WindowsSwapChainSurface? current,
        WindowsSurfaceSize size)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(source);

        if (size.IsEmpty)
        {
            return current;
        }

        var descriptor = source.GetSurfaceDescriptor();
        if (descriptor.IsEmpty)
        {
            return current;
        }

        if (current is null)
        {
            var created = lease.Context.CreateSwapChainSurface(descriptor, size.Width, size.Height);
            source.OnSwapChainCreated(created);
            return created;
        }

        current.Configure(size.Width, size.Height);
        source.OnSwapChainResized(current, size);
        return current;
    }

    public static void ReleaseSwapChain(IWindowsSurfaceSource source, WindowsSwapChainSurface? surface)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (surface is null)
        {
            source.OnSwapChainDestroyed();
            return;
        }

        try
        {
            surface.Dispose();
        }
        finally
        {
            source.OnSwapChainDestroyed();
        }
    }

    public static void HandleDeviceLoss(WindowsGpuContext context, IWindowsSurfaceSource source, string? reason)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);

        context.RecordDeviceReset(reason);
        source.OnDeviceLost(reason);
    }
}
