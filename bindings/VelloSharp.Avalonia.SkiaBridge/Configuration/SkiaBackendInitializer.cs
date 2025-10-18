using VelloSharp.Avalonia.Core.Options;

namespace VelloSharp.Avalonia.SkiaBridge.Configuration;

/// <summary>
/// Helper for applying backend selection decisions to the Skia pipeline.
/// </summary>
public static class SkiaBackendInitializer
{
    /// <summary>
    /// Applies the specified configuration after the host has initialized Skia.
    /// </summary>
    public static void Apply(SkiaBackendConfiguration? configuration = null)
    {
        configuration ??= new SkiaBackendConfiguration();

        if (configuration.Mode != SkiaBackendMode.Vello)
        {
            return;
        }

        var backendOptions = configuration.GraphicsOptions ?? GraphicsBackendOptions.Default;
        SkiaVelloRenderer.Initialize(backendOptions);
    }
}
