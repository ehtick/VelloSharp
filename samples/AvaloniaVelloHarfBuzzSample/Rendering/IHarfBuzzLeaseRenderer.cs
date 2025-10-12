using System;
using System.Numerics;
using Avalonia;
using AvaloniaVelloHarfBuzzSample.Diagnostics;
using AvaloniaVelloHarfBuzzSample.Services;
using VelloSharp;
using VelloSharp.Avalonia.Vello.Rendering;

namespace AvaloniaVelloHarfBuzzSample.Rendering;

public interface IHarfBuzzLeaseRenderer
{
    void Render(HarfBuzzLeaseRenderContext context);
}

public sealed class HarfBuzzLeaseRenderContext : IDisposable
{
    private readonly IVelloPlatformGraphicsLease? _platformLease;
    private bool _disposed;

    public HarfBuzzLeaseRenderContext(
        Scene scene,
        Rect bounds,
        Matrix3x2 globalTransform,
        double scaling,
        TimeSpan elapsed,
        ulong frameIndex,
        HarfBuzzSampleServices services,
        IVelloPlatformGraphicsLease? platformLease)
    {
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));
        Bounds = bounds;
        GlobalTransform = globalTransform;
        Scaling = scaling;
        Elapsed = elapsed;
        FrameIndex = frameIndex;
        Services = services ?? throw new ArgumentNullException(nameof(services));
        _platformLease = platformLease;
    }

    public Scene Scene { get; }

    public Rect Bounds { get; }

    public Matrix3x2 GlobalTransform { get; }

    public double Scaling { get; }

    public TimeSpan Elapsed { get; }

    public ulong FrameIndex { get; }

    public HarfBuzzSampleServices Services { get; }

    public IVelloPlatformGraphicsLease? PlatformLease => _platformLease;

    public FontAssetService FontAssets => Services.FontAssets;

    public HarfBuzzShapeService ShapeService => Services.ShapeService;

    public ShapeCaptureRecorder CaptureRecorder => Services.CaptureRecorder;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _platformLease?.Dispose();
    }
}
