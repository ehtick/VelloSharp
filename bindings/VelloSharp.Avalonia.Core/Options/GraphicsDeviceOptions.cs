namespace VelloSharp.Avalonia.Core.Options;

/// <summary>
/// Immutable options describing how a rendering device should be created.
/// </summary>
/// <param name="Backend">Target backend for the device.</param>
/// <param name="Features">Feature toggles to enable during initialization.</param>
/// <param name="Presentation">Presentation preferences for swapchain-backed surfaces.</param>
/// <param name="BackendOptions">Optional backend-specific configuration payload.</param>
public sealed record GraphicsDeviceOptions(
    GraphicsBackendKind Backend,
    GraphicsFeatureSet Features,
    GraphicsPresentationOptions Presentation,
    object? BackendOptions = null)
{
    /// <summary>
    /// Creates options with default features and presentation for the specified backend.
    /// </summary>
    public static GraphicsDeviceOptions CreateDefault(GraphicsBackendKind backend) =>
        new(backend, new GraphicsFeatureSet(), GraphicsPresentationOptions.Default);
}
