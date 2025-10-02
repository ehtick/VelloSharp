using Avalonia;
using VelloSharp;

namespace Avalonia.Winit;

/// <summary>
/// Exposes Vello surface creation helpers for the winit window backend.
/// </summary>
public interface IVelloWinitSurfaceProvider
{
    /// <summary>
    /// Creates a Vello surface handle describing the underlying native window.
    /// </summary>
    SurfaceHandle CreateSurfaceHandle();

    /// <summary>
    /// Gets the current pixel size of the swapchain surface.
    /// </summary>
    PixelSize SurfacePixelSize { get; }

    /// <summary>
    /// Gets the render scaling factor applied to drawing commands.
    /// </summary>
    double RenderScaling { get; }

    /// <summary>
    /// Notifies the underlying window that presentation is about to occur.
    /// </summary>
    void PrePresent();

    /// <summary>
    /// Requests a redraw from the host event loop.
    /// </summary>
    void RequestRedraw();
}
