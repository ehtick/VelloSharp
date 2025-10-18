using VelloSharp.Avalonia.Core.Device;

namespace VelloSharp.Avalonia.Core.Surface;

/// <summary>
/// Coordinates acquisition of renderable surfaces backed by native swapchains or framebuffers.
/// </summary>
public interface IRenderSurfaceManager
{
    /// <summary>
    /// Attempts to acquire a surface compatible with the provided device and request.
    /// </summary>
    /// <remarks>
    /// The operation may be asynchronous when platform constraints require a thread switch (e.g., macOS).
    /// </remarks>
    ValueTask<RenderSurfaceLease?> TryAcquireSurfaceAsync(
        GraphicsDeviceLease device,
        SurfaceRequest request,
        CancellationToken cancellationToken = default);
}
