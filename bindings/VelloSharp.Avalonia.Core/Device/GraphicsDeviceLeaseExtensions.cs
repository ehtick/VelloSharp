namespace VelloSharp.Avalonia.Core.Device;

/// <summary>
/// Extension helpers for <see cref="GraphicsDeviceLease"/> instances.
/// </summary>
public static class GraphicsDeviceLeaseExtensions
{
    /// <summary>
    /// Attempts to retrieve the underlying WebGPU device resources.
    /// </summary>
    public static bool TryGetWgpuResources(this GraphicsDeviceLease lease, out WgpuDeviceResources resources)
    {
        if (lease is null)
        {
            throw new ArgumentNullException(nameof(lease));
        }

        if (lease.TryGetDevice(out WgpuDeviceResources? value))
        {
            resources = value;
            return true;
        }

        resources = null!;
        return false;
    }
}
