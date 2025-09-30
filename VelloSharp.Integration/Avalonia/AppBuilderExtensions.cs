using Avalonia;
using VelloSharp.Avalonia.Vello;

namespace VelloSharp.Integration.Avalonia;

public static class AppBuilderExtensions
{
    /// <summary>
    /// Configures Avalonia text services to use Vello-backed implementations when running with the Skia shim.
    /// </summary>
    public static AppBuilder UseVelloSkiaTextServices(this AppBuilder builder)
    {
        builder.AfterSetup(_ => VelloTextServices.Initialize());
        return builder;
    }
}
