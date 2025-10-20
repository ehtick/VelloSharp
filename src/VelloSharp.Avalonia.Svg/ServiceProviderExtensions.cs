using System;
using Avalonia.Markup.Xaml;

namespace VelloSharp.Avalonia.Svg;

internal static class ServiceProviderExtensions
{
    public static T GetService<T>(this IServiceProvider sp)
        => (T)sp.GetService(typeof(T))!;

    public static Uri? GetContextBaseUri(this IServiceProvider provider)
    {
        if (provider.GetService(typeof(IUriContext)) is IUriContext context)
        {
            return context.BaseUri;
        }

        return null;
    }
}
