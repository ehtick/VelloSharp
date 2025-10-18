namespace VelloSharp.Avalonia.Core.Device;

/// <summary>
/// Encapsulates the WebGPU resources required by the Vello renderer.
/// </summary>
public sealed class WgpuDeviceResources
{
    internal WgpuDeviceResources(
        WgpuInstance instance,
        WgpuAdapter adapter,
        WgpuDevice device,
        WgpuQueue queue,
        WgpuRenderer renderer,
        RendererOptions rendererOptions)
    {
        Instance = instance ?? throw new ArgumentNullException(nameof(instance));
        Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        Device = device ?? throw new ArgumentNullException(nameof(device));
        Queue = queue ?? throw new ArgumentNullException(nameof(queue));
        Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        RendererOptions = rendererOptions;
    }

    /// <summary>
    /// Gets the WebGPU instance used to acquire the adapter.
    /// </summary>
    public WgpuInstance Instance { get; }

    /// <summary>
    /// Gets the selected WebGPU adapter.
    /// </summary>
    public WgpuAdapter Adapter { get; }

    /// <summary>
    /// Gets the WebGPU device used for rendering.
    /// </summary>
    public WgpuDevice Device { get; }

    /// <summary>
    /// Gets the queue associated with <see cref="Device"/>.
    /// </summary>
    public WgpuQueue Queue { get; }

    /// <summary>
    /// Gets the Vello renderer bound to the device.
    /// </summary>
    public WgpuRenderer Renderer { get; }

    /// <summary>
    /// Gets the renderer options used to configure <see cref="Renderer"/>.
    /// </summary>
    public RendererOptions RendererOptions { get; }
}
