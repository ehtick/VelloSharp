using VelloSharp.Avalonia.Core.Options;

namespace VelloSharp.Avalonia.Core.Device;

/// <summary>
/// Provides rendering devices aligned with the shared Avalonia abstractions.
/// </summary>
public interface IGraphicsDeviceProvider : IDisposable
{
    /// <summary>
    /// Acquires a device for the supplied options. Callers must dispose the returned lease.
    /// </summary>
    GraphicsDeviceLease Acquire(GraphicsDeviceOptions options);
}
