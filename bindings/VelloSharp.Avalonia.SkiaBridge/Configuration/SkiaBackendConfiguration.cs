using VelloSharp.Avalonia.Core.Options;

namespace VelloSharp.Avalonia.SkiaBridge.Configuration;

/// <summary>
/// Describes how the Skia integration should configure its rendering backend.
/// </summary>
public sealed class SkiaBackendConfiguration
{
    /// <summary>
    /// Gets or sets the desired backend mode. Defaults to Skia-only behaviour.
    /// </summary>
    public SkiaBackendMode Mode { get; init; } = SkiaBackendMode.SkiaOnly;

    /// <summary>
    /// Gets or sets the Vello graphics options used when <see cref="Mode"/> is <see cref="SkiaBackendMode.Vello"/>.
    /// </summary>
    public GraphicsBackendOptions? GraphicsOptions { get; init; }
}

/// <summary>
/// Enumerates the supported backend modes for the Skia integration.
/// </summary>
public enum SkiaBackendMode
{
    SkiaOnly,
    Vello,
}
