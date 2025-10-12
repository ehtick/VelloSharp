using Microsoft.Maui.Hosting;
using VelloSharp.Maui.Controls;
using VelloSharp.Maui;

namespace VelloSharp.Maui.Hosting;

public static class AppHostBuilderExtensions
{
    public static MauiAppBuilder UseVelloSharp(this MauiAppBuilder builder)
        => builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler<VelloView, VelloViewHandler>();
        });
}
