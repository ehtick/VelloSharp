using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace VelloSharp.Windows.Hosting;

public static class HostBuilderExtensions
{
    public static IHostBuilder UseVelloSharpWinUI(this IHostBuilder builder, Action<VelloWinUIOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureServices((_, services) =>
        {
            services.AddSingleton(provider =>
            {
                var options = new VelloWinUIOptions();
                configure?.Invoke(options);
                return options;
            });
            services.AddSingleton<VelloWinUIService>();
        });

        return builder;
    }

    public static IServiceCollection AddVelloSharpWinUI(this IServiceCollection services, Action<VelloWinUIOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(provider =>
        {
            var options = new VelloWinUIOptions();
            configure?.Invoke(options);
            return options;
        });
        services.AddSingleton<VelloWinUIService>();
        return services;
    }
}
