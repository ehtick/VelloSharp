using System;
using VelloSharp;

namespace VelloSharp.Windows;

public sealed class VelloSurfaceRenderEventArgs : EventArgs
{
    private readonly WindowsGpuContextLease _lease;
    private readonly WgpuTextureView _textureView;
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
        bool isAnimationFrame)
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

    public void RenderScene(Scene scene)
        => RenderScene(scene, _renderParams);

    public void RenderScene(Scene scene, RenderParams parameters)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Context.Renderer.RenderSurface(scene, _textureView, parameters, TextureFormat);
        Handled = true;
    }
}
