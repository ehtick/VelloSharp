using VelloSharp;

namespace VelloSharp.Avalonia.Vello;

/// <summary>
/// Configurable options for the Vello rendering subsystem.
/// </summary>
public sealed class VelloPlatformOptions
{
    /// <summary>
    /// Gets or sets the desired frames-per-second for the render timer.
    /// </summary>
    public int FramesPerSecond { get; set; } = 60;

    /// <summary>
    /// Gets or sets the per-frame clear color used before drawing the scene.
    /// </summary>
    public RgbaColor ClearColor { get; set; } = RgbaColor.FromBytes(0, 0, 0, 0);

    /// <summary>
    /// Gets or sets the default antialiasing mode used by the renderer.
    /// </summary>
    public AntialiasingMode Antialiasing { get; set; } = AntialiasingMode.Msaa8;

    /// <summary>
    /// Gets or sets the renderer configuration passed to <see cref="Renderer"/> creation.
    /// </summary>
    public RendererOptions RendererOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the preferred presentation mode for swapchain surfaces.
    /// </summary>
    public PresentMode PresentMode { get; set; } = PresentMode.AutoVsync;
}
