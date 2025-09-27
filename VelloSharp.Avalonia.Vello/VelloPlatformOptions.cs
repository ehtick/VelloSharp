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
    public AntialiasingMode Antialiasing { get; set; } = AntialiasingMode.Area;

    /// <summary>
    /// Gets or sets the renderer configuration passed to <see cref="Renderer"/> creation.
    /// </summary>
    public RendererOptions RendererOptions { get; set; } = new RendererOptions(
        useCpu: false,
        supportArea: true,
        supportMsaa8: false,
        supportMsaa16: false);

    /// <summary>
    /// Gets or sets the preferred presentation mode for swapchain surfaces.
    /// </summary>
    public PresentMode PresentMode { get; set; } = PresentMode.AutoVsync;

    internal AntialiasingMode ResolveAntialiasing(AntialiasingMode requested)
    {
        return requested switch
        {
            AntialiasingMode.Msaa16 when !RendererOptions.SupportMsaa16
                => RendererOptions.SupportMsaa8 ? AntialiasingMode.Msaa8 : AntialiasingMode.Area,
            AntialiasingMode.Msaa8 when !RendererOptions.SupportMsaa8
                => AntialiasingMode.Area,
            AntialiasingMode.Area when !RendererOptions.SupportArea
                => RendererOptions.SupportMsaa8 ? AntialiasingMode.Msaa8 : (RendererOptions.SupportMsaa16 ? AntialiasingMode.Msaa16 : AntialiasingMode.Area),
            _ => requested,
        };
    }
}
