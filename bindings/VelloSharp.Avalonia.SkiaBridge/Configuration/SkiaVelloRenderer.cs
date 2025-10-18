using VelloSharp;
using VelloSharp.Avalonia.Core.Options;
using VelloSharp.Avalonia.Vello;

namespace VelloSharp.Avalonia.SkiaBridge.Configuration;

/// <summary>
/// Configures Avalonia to use the Vello rendering backend in environments that currently bootstrap Skia.
/// </summary>
public static class SkiaVelloRenderer
{
    /// <summary>
    /// Initializes the shared Vello renderer using the supplied backend options.
    /// </summary>
    public static void Initialize(GraphicsBackendOptions backendOptions)
    {
        ArgumentNullException.ThrowIfNull(backendOptions);

        var options = CreateVelloOptions(backendOptions);
        VelloRenderer.Initialize(options);
    }

    private static VelloPlatformOptions CreateVelloOptions(GraphicsBackendOptions backendOptions)
    {
        var features = backendOptions.Features;
        var rendererOptions = new RendererOptions(
            features.EnableCpuFallback,
            features.EnableAreaAa,
            features.EnableMsaa8,
            features.EnableMsaa16);

        return new VelloPlatformOptions
        {
            FramesPerSecond = backendOptions.Presentation.SwapChainFps,
            ClearColor = backendOptions.Presentation.ClearColor,
            PresentMode = backendOptions.Presentation.PresentMode,
            RendererOptions = rendererOptions,
        };
    }
}
