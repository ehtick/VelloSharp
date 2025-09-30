using System;
using VelloSharp;

namespace VelloSharp.Avalonia.Vello.Rendering;

/// <summary>
/// Provides the context required to encode additional wgpu work targeting the current swapchain surface.
/// </summary>
public readonly struct WgpuSurfaceRenderContext
{
    public WgpuSurfaceRenderContext(
        WgpuDevice device,
        WgpuQueue queue,
        WgpuTextureView targetView,
        RenderParams renderParams,
        WgpuTextureFormat surfaceFormat)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));
        Queue = queue ?? throw new ArgumentNullException(nameof(queue));
        TargetView = targetView ?? throw new ArgumentNullException(nameof(targetView));
        RenderParams = renderParams;
        SurfaceFormat = surfaceFormat;
    }

    /// <summary>
    /// Gets the device associated with the current Vello renderer.
    /// </summary>
    public WgpuDevice Device { get; }

    /// <summary>
    /// Gets the queue that should be used to submit encoded work.
    /// </summary>
    public WgpuQueue Queue { get; }

    /// <summary>
    /// Gets the swapchain texture view for the current frame.
    /// </summary>
    public WgpuTextureView TargetView { get; }

    /// <summary>
    /// Gets the render parameters describing the framebuffer dimensions and base color.
    /// </summary>
    public RenderParams RenderParams { get; }

    /// <summary>
    /// Gets the texture format of the swapchain surface.
    /// </summary>
    public WgpuTextureFormat SurfaceFormat { get; }
}
