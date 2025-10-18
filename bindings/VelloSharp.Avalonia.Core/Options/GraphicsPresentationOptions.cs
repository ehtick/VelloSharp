namespace VelloSharp.Avalonia.Core.Options;

/// <summary>
/// Presentation-related preferences shared by all render backends.
/// </summary>
/// <param name="PresentMode">Desired swapchain presentation mode.</param>
/// <param name="ClearColor">Color used to clear the framebuffer before drawing.</param>
/// <param name="SwapChainFps">Target frames-per-second for throttled render loops.</param>
public sealed record GraphicsPresentationOptions(
    PresentMode PresentMode,
    RgbaColor ClearColor,
    int SwapChainFps = 60)
{
    /// <summary>
    /// Default presentation options: vsync and a transparent clear color.
    /// </summary>
    public static GraphicsPresentationOptions Default { get; } = new(
        PresentMode.AutoVsync,
        RgbaColor.FromBytes(0, 0, 0, 0),
        60);
}
