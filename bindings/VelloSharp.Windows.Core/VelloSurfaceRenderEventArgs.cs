using System;
using SkiaSharp;
using VelloSharp;

namespace VelloSharp.Windows;

public sealed class VelloSurfaceRenderEventArgs : EventArgs
{
    private static readonly GRContext s_gpuContext = GRContext.CreateGl();
    private static readonly SKSurfaceProperties s_defaultSurfaceProperties = new(SKPixelGeometry.Unknown);

    private readonly WindowsGpuContextLease _lease;
    private readonly WgpuTextureView _textureView;
    private readonly SurfaceHandle? _surfaceHandle;
    private RenderParams _renderParams;

    internal VelloSurfaceRenderEventArgs(
        WindowsGpuContextLease lease,
        Scene scene,
        WgpuTextureView textureView,
        WgpuTextureFormat textureFormat,
        RenderParams renderParams,
        WindowsSurfaceSize pixelSize,
        TimeSpan timestamp,
        TimeSpan delta,
        long frameId,
        bool isAnimationFrame,
        SurfaceHandle? surfaceHandle = null)
    {
        _lease = lease ?? throw new ArgumentNullException(nameof(lease));
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _textureView = textureView ?? throw new ArgumentNullException(nameof(textureView));
        TextureFormat = textureFormat;
        _renderParams = renderParams;
        PixelSize = pixelSize;
        Timestamp = timestamp;
        Delta = delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        FrameId = frameId;
        IsAnimationFrame = isAnimationFrame;
        _surfaceHandle = surfaceHandle;
    }

    public WindowsGpuContextLease Lease => _lease;

    public WindowsGpuContext Context => _lease.Context;

    public WgpuRenderer Renderer => Context.Renderer;

    public Scene Scene { get; }

    public WindowsSurfaceSize PixelSize { get; }

    public RenderParams RenderParams
    {
        get => _renderParams;
        set => _renderParams = value;
    }

    public TimeSpan Timestamp { get; }

    public TimeSpan Delta { get; }

    public long FrameId { get; }

    public bool IsAnimationFrame { get; }

    public bool Handled { get; private set; }

    public WgpuTextureView TextureView => _textureView;

    public WgpuTextureFormat TextureFormat { get; }

    public bool SupportsSurfaceHandle => _surfaceHandle.HasValue;

    public bool SupportsSkiaSurface => SupportsSurfaceHandle;

    public SurfaceHandle SurfaceHandle
    {
        get
        {
            if (!_surfaceHandle.HasValue)
            {
                throw new InvalidOperationException("No surface handle is available for this render callback.");
            }

            return _surfaceHandle.Value;
        }
    }

    public SKSurface? CreateSkiaSurface(SKSurfaceProperties? properties = null, AntialiasingMode? antialiasingOverride = null)
    {
        if (!SupportsSurfaceHandle)
        {
            return null;
        }

        var antialiasing = antialiasingOverride ?? _renderParams.Antialiasing;
        var sampleCount = ResolveSampleCount(antialiasing);
        var info = CreateImageInfo(_renderParams);
        var surfaceProps = properties ?? s_defaultSurfaceProperties;
        return SKSurface.Create(s_gpuContext, info, _surfaceHandle.Value, sampleCount, surfaceProps);
    }

    public void RenderScene(Scene scene)
        => RenderScene(scene, _renderParams);

    public void RenderScene(Scene scene, RenderParams parameters)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Context.Renderer.RenderSurface(scene, _textureView, parameters, TextureFormat);
        Handled = true;
    }

    private static SKImageInfo CreateImageInfo(RenderParams renderParams)
    {
        var width = (int)Math.Clamp(renderParams.Width, 1u, int.MaxValue);
        var height = (int)Math.Clamp(renderParams.Height, 1u, int.MaxValue);
        var colorType = renderParams.Format switch
        {
            RenderFormat.Bgra8 => SKColorType.Bgra8888,
            RenderFormat.Rgba8 => SKColorType.Rgba8888,
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
