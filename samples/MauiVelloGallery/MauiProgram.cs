using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using VelloSharp.Maui;
using VelloSharp.Maui.Controls;

namespace MauiVelloGallery;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<VelloView, VelloViewHandler>();
            });

        return builder.Build();
    }
}
