using System;
using Avalonia;

namespace VelloSharp.Avalonia.Vello;

/// <summary>
/// Provides registration helpers for the Vello rendering subsystem.
/// </summary>
public static class VelloApplicationExtensions
{
    /// <summary>
    /// Configures Avalonia to use the Vello renderer backed by VelloSharp and WGPU.
    /// </summary>
    /// <param name="builder">The <see cref="AppBuilder"/> instance.</param>
    /// <param name="options">Optional renderer configuration.</param>
    /// <returns>The original builder for fluent configuration.</returns>
    public static AppBuilder UseVello(this AppBuilder builder, VelloPlatformOptions? options = null)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        return builder.UseRenderingSubsystem(() => VelloPlatform.Initialize(options ?? new VelloPlatformOptions()), "Vello");
    }
}
