using System;
using Avalonia;
using Avalonia.Logging;
using VelloSharp.Avalonia.Vello;

namespace VelloSharp.Integration.Avalonia;

public static class AppBuilderExtensions
{
    /// <summary>
    /// Configures Avalonia text services to use Vello-backed implementations when the active
    /// <see cref="IPlatformRenderInterface"/> is compatible. Currently this is limited to the Vello
    /// rendering backend; when running on other backends (for example Avalonia's Skia renderer) the
    /// registration is skipped to avoid invalid glyph typeface casts.
    /// </summary>
    public static AppBuilder UseVelloSkiaTextServices(this AppBuilder builder)
    {
        builder.AfterSetup(b =>
        {
            var renderer = b.RenderingSubsystemName;

            if (!string.Equals(renderer, "Skia", StringComparison.OrdinalIgnoreCase))
            {
                VelloTextServices.Initialize();
                return;
            }

            Logger.TryGet(LogEventLevel.Warning, "VelloSharp")?
                .Log(typeof(AppBuilderExtensions),
                    "Skipping Vello text services registration because the active rendering subsystem '{Renderer}' is not compatible.",
                    renderer ?? "unknown");
        });
        return builder;
    }
}
