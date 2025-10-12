namespace VelloSharp.Maui.Rendering;

/// <summary>
/// Specifies the preferred rendering backend for MAUI Vello surfaces.
/// </summary>
public enum VelloRenderBackend
{
    Gpu,
    Cpu,
}

/// <summary>
/// Controls when frames are rendered.
/// </summary>
public enum VelloRenderMode
{
    OnDemand,
    Continuous,
}

/// <summary>
/// Selects the mechanism used to drive frame pumping on platforms that expose multiple options.
/// </summary>
public enum RenderLoopDriver
{
    None = 0,
    CompositionTarget,
    DispatcherQueue,
}

/// <summary>
/// Cross-platform rendering options that map onto platform-specific device descriptors.
/// </summary>
public sealed record class VelloGraphicsDeviceOptions
{
    public static VelloGraphicsDeviceOptions Default { get; } = new();

    public bool PreferDiscreteAdapter { get; init; }

    public int? MsaaSampleCount { get; init; }

    public string? DiagnosticsLabel { get; init; }
}
