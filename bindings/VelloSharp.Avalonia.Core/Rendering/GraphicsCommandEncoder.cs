using VelloSharp.Avalonia.Core.Options;

namespace VelloSharp.Avalonia.Core.Rendering;

/// <summary>
/// Allows callers to access backend-specific contexts during scene submission.
/// </summary>
public interface IGraphicsCommandEncoder
{
    /// <summary>
    /// Gets the backend used to produce the encoder.
    /// </summary>
    GraphicsBackendKind Backend { get; }

    /// <summary>
    /// Attempts to retrieve the underlying backend context as the requested type.
    /// </summary>
    /// <typeparam name="TContext">Type of context requested.</typeparam>
    /// <param name="context">Receives the context when available.</param>
    /// <returns><c>true</c> when the context is available; otherwise, <c>false</c>.</returns>
    bool TryGetContext<TContext>(out TContext context);
}

/// <summary>
/// Backend-specific context for encoding WebGPU commands.
/// </summary>
public readonly struct WgpuCommandEncoderContext
{
    /// <summary>
    /// Initializes a new <see cref="WgpuCommandEncoderContext"/>.
    /// </summary>
    /// <param name="device">WebGPU device associated with the command encoder.</param>
    /// <param name="queue">Queue used to submit encoded work.</param>
    /// <param name="targetView">Swapchain texture view targeted by the encoder.</param>
    /// <param name="surfaceFormat">Texture format of the swapchain surface.</param>
    public WgpuCommandEncoderContext(
        WgpuDevice device,
        WgpuQueue queue,
        WgpuTextureView targetView,
        WgpuTextureFormat surfaceFormat)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));
        Queue = queue ?? throw new ArgumentNullException(nameof(queue));
        TargetView = targetView ?? throw new ArgumentNullException(nameof(targetView));
        SurfaceFormat = surfaceFormat;
    }

    /// <summary>
    /// Gets the WebGPU device.
    /// </summary>
    public WgpuDevice Device { get; }

    /// <summary>
    /// Gets the queue that should receive encoded work.
    /// </summary>
    public WgpuQueue Queue { get; }

    /// <summary>
    /// Gets the swapchain texture view for the current frame.
    /// </summary>
    public WgpuTextureView TargetView { get; }

    /// <summary>
    /// Gets the format of the swapchain surface.
    /// </summary>
    public WgpuTextureFormat SurfaceFormat { get; }
}
