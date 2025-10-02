using System;
using Avalonia;
using VelloSharp;

namespace VelloSharp.Avalonia.Vello.Rendering;

/// <summary>
/// Provides access to the underlying Vello rendering API for a drawing context.
/// </summary>
public interface IVelloApiLeaseFeature
{
    /// <summary>
    /// Begins an exclusive lease over the Vello rendering API for the current drawing context.
    /// </summary>
    /// <returns>An <see cref="IVelloApiLease"/> that must be disposed to release the lease.</returns>
    IVelloApiLease Lease();
}

/// <summary>
/// Represents an active lease to the Vello rendering API.
/// </summary>
public interface IVelloApiLease : IDisposable
{
    /// <summary>
    /// Gets the scene that the drawing context is currently recording into.
    /// </summary>
    Scene Scene { get; }

    /// <summary>
    /// Gets the render parameters associated with the current scene.
    /// </summary>
    RenderParams RenderParams { get; }

    /// <summary>
    /// Gets the transform applied to the drawing context when the lease was created.
    /// </summary>
    Matrix Transform { get; }

    /// <summary>
    /// Attempts to lease the underlying wgpu platform resources for direct access.
    /// </summary>
    /// <returns>An <see cref="IVelloPlatformGraphicsLease"/> when wgpu access is available; otherwise <c>null</c>.</returns>
    IVelloPlatformGraphicsLease? TryLeasePlatformGraphics();

    /// <summary>
    /// Schedules a wgpu rendering callback that will be executed on the swapchain surface prior to the Vello scene being composited.
    /// </summary>
    /// <param name="renderAction">The callback to execute.</param>
    void ScheduleWgpuSurfaceRender(Action<WgpuSurfaceRenderContext> renderAction);
}

/// <summary>
/// Provides read-only access to the wgpu objects that back the current Vello renderer.
/// </summary>
public interface IVelloPlatformGraphicsLease : IDisposable
{
    /// <summary>
    /// Gets the active wgpu instance.
    /// </summary>
    WgpuInstance Instance { get; }

    /// <summary>
    /// Gets the adapter that was negotiated for rendering.
    /// </summary>
    WgpuAdapter Adapter { get; }

    /// <summary>
    /// Gets the device used by the renderer.
    /// </summary>
    WgpuDevice Device { get; }

    /// <summary>
    /// Gets the queue associated with the device.
    /// </summary>
    WgpuQueue Queue { get; }

    /// <summary>
    /// Gets the renderer instance that executes Vello render commands.
    /// </summary>
    WgpuRenderer Renderer { get; }
}
