using System;
using SkiaSharp;
using VelloSharp;
using VelloSharp.Windows;

namespace VelloSharp.Windows.Shared.Presenters;

public sealed class VelloSwapChainRenderEventArgs : EventArgs
{
    private static readonly GRContext s_gpuContext = GRContext.CreateGl();
    private static readonly SKSurfaceProperties s_defaultSurfaceProperties = new(SKPixelGeometry.Unknown);

    internal VelloSwapChainRenderEventArgs(
        WindowsGpuContextLease lease,
        WindowsSwapChainSurface surface,
        WindowsSurfaceSize pixelSize,
        RenderParams renderParams,
        TimeSpan timestamp,
        TimeSpan delta,
        long frameId)
    {
        Lease = lease ?? throw new ArgumentNullException(nameof(lease));
        Surface = surface ?? throw new ArgumentNullException(nameof(surface));
        PixelSize = pixelSize;
        RenderParams = renderParams;
        Timestamp = timestamp;
        Delta = delta;
        FrameId = frameId;
    }

    public WindowsGpuContextLease Lease { get; }

    public WindowsSwapChainSurface Surface { get; }

    public WindowsSurfaceSize PixelSize { get; }

    public RenderParams RenderParams { get; }

    public TimeSpan Timestamp { get; }

    public TimeSpan Delta { get; }

    public long FrameId { get; }

    public SurfaceHandle SurfaceHandle => Surface.SurfaceHandle;

    public bool SupportsSurfaceHandle => SurfaceHandle.Kind is not VelloWindowHandleKind.Headless and not VelloWindowHandleKind.None;

    public bool SupportsSkiaSurface => SupportsSurfaceHandle;

    public SKSurface? CreateSkiaSurface(SKSurfaceProperties? properties = null, AntialiasingMode? antialiasingOverride = null)
    {
        if (SurfaceHandle.Kind is VelloWindowHandleKind.Headless or VelloWindowHandleKind.None)
        {
            return null;
        }

        var antialiasing = antialiasingOverride ?? RenderParams.Antialiasing;
        var sampleCount = ResolveSampleCount(antialiasing);
        var info = CreateImageInfo(RenderParams);
        var props = properties ?? s_defaultSurfaceProperties;
        return SKSurface.Create(s_gpuContext, info, SurfaceHandle, sampleCount, props);
    }

    private static SKImageInfo CreateImageInfo(RenderParams renderParams)
    {
        var width = (int)Math.Clamp(renderParams.Width, 1u, int.MaxValue);
        var height = (int)Math.Clamp(renderParams.Height, 1u, int.MaxValue);
        var colorType = renderParams.Format switch
        {
            RenderFormat.Rgba8 => SKColorType.Rgba8888,
            RenderFormat.Bgra8 => SKColorType.Bgra8888,
            _ => SKColorType.Bgra8888,
        };

        return new SKImageInfo(width, height, colorType, SKAlphaType.Premul);
    }

    private static int ResolveSampleCount(AntialiasingMode antialiasing) => antialiasing switch
    {
        AntialiasingMode.Msaa16 => 16,
        AntialiasingMode.Msaa8 => 8,
        _ => 1,
    };
}
