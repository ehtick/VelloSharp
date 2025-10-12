#if !WINDOWS
using VelloSharp;

namespace VelloSharp.Windows;

public sealed record class VelloGraphicsDeviceOptions
{
    public static VelloGraphicsDeviceOptions Default { get; } = new();

    public RgbaColor BackgroundColor { get; init; } = RgbaColor.FromBytes(0, 0, 0, 0);

    public AntialiasingMode Antialiasing { get; init; } = AntialiasingMode.Area;

    public RenderFormat Format { get; init; } = RenderFormat.Bgra8;

    public PresentMode PresentMode { get; init; } = PresentMode.AutoVsync;

    public RendererOptions? RendererOptions { get; init; }

    public bool PreferDiscreteAdapter { get; init; }

    public int? MsaaSampleCount { get; init; }

    public string? DiagnosticsLabel { get; init; }

    public bool EnablePipelineCaching { get; init; } = true;

    public AntialiasingMode GetAntialiasingMode() => MsaaSampleCount switch
    {
        >= 16 => AntialiasingMode.Msaa16,
        >= 8 => AntialiasingMode.Msaa8,
        _ => Antialiasing,
    };
}

public enum PresentMode
{
    AutoVsync = 0,
    AutoNoVsync = 1,
    Fifo = 2,
    Immediate = 3,
}
#endif