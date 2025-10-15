using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Browser;
using VelloSharp.Avalonia.Vello;

namespace VelloSharp.Avalonia.Browser;

/// <summary>
/// Provides browser-specific registration helpers for the Vello renderer.
/// </summary>
public static class AppBuilderExtensions
{
    /// <summary>
    /// Configures Avalonia to use the Vello WebGPU renderer for browser targets.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="options">Optional renderer options.</param>
    /// <returns>The original builder for fluent configuration.</returns>
    [SupportedOSPlatform("browser")]
    [RequiresUnreferencedCode("Browser platform discovery relies on reflection to load Avalonia browser subsystems.")]
    public static AppBuilder UseVelloBrowser(this AppBuilder builder, VelloPlatformOptions? options = null)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder = ConfigureBrowserPlatform(builder);

        return builder.UseRenderingSubsystem(
            () => VelloBrowserPlatform.Initialize(options ?? new VelloPlatformOptions()),
            "VelloBrowser");
    }

    private static AppBuilder ConfigureBrowserPlatform(AppBuilder builder)
    {
        var browserAssembly = typeof(BrowserPlatformOptions).Assembly;

        var runtimeServicesType = browserAssembly.GetType("Avalonia.Browser.BrowserRuntimePlatformServices")
            ?? throw new InvalidOperationException("Unable to locate BrowserRuntimePlatformServices.");
        var runtimeRegister = runtimeServicesType.GetMethod("Register", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Unable to locate BrowserRuntimePlatformServices.Register method.");

        builder = builder.UseRuntimePlatformSubsystem(
            () => runtimeRegister.Invoke(null, new object?[] { builder.ApplicationType?.Assembly }),
            "BrowserRuntimePlatform");

        var windowingType = browserAssembly.GetType("Avalonia.Browser.BrowserWindowingPlatform")
            ?? throw new InvalidOperationException("Unable to locate BrowserWindowingPlatform.");
        var windowingRegister = windowingType.GetMethod("Register", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Unable to locate BrowserWindowingPlatform.Register method.");

        builder = builder.UseWindowingSubsystem(
            () => windowingRegister.Invoke(null, null),
            "BrowserWindowingPlatform");

        return builder;
    }
}
