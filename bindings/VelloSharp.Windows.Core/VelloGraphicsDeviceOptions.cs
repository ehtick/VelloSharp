using System;
using VelloSharp;

namespace VelloSharp.Windows;

public sealed record class VelloGraphicsDeviceOptions
{
    public static VelloGraphicsDeviceOptions Default { get; } = new();

    public RgbaColor BackgroundColor { get; init; } = RgbaColor.FromBytes(0, 0, 0, 0);

    public AntialiasingMode Antialiasing { get; init; } = AntialiasingMode.Area;

    public RenderFormat Format { get; init; } = RenderFormat.Bgra8;

    public WindowsColorSpace ColorSpace { get; init; } = WindowsColorSpace.Srgb;

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

    internal WgpuTextureFormat GetSwapChainFormat()
    {
        return Format switch
        {
            RenderFormat.Rgba8 when ColorSpace == WindowsColorSpace.Srgb => WgpuTextureFormat.Rgba8UnormSrgb,
            RenderFormat.Rgba8 => WgpuTextureFormat.Rgba8Unorm,
            RenderFormat.Bgra8 when ColorSpace == WindowsColorSpace.Srgb => WgpuTextureFormat.Bgra8UnormSrgb,
            RenderFormat.Bgra8 => WgpuTextureFormat.Bgra8Unorm,
            _ => WgpuTextureFormat.Bgra8UnormSrgb,
        };
    }

    internal (bool SupportMsaa8, bool SupportMsaa16) GetMsaaSupportFlags(RendererOptions baseOptions)
    {
        var requestedMode = GetAntialiasingMode();
        var sampleCount = MsaaSampleCount.GetValueOrDefault();

        var supportMsaa8 = (baseOptions.SupportMsaa8 || baseOptions.SupportMsaa16)
            && (requestedMode is AntialiasingMode.Msaa8 or AntialiasingMode.Msaa16 || sampleCount >= 4);

        var supportMsaa16 = baseOptions.SupportMsaa16
            && (requestedMode is AntialiasingMode.Msaa16 || sampleCount >= 16);

        return (supportMsaa8, supportMsaa16);
    }
}


