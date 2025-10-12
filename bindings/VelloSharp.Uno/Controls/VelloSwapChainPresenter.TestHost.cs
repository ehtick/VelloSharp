#if HAS_UNO && WINDOWS
using Microsoft.UI.Dispatching;
using VelloSharp.WinForms.Integration;
using VelloSharp.Windows;

namespace VelloSharp.Uno.Controls;

/// <summary>
/// Test host implementation used by unit tests to exercise <see cref="VelloSwapChainPresenter"/> without WinUI dependencies.
/// </summary>
internal sealed class TestSwapChainPresenterHost : IVelloSwapChainPresenterHost
{
    public DispatcherQueue? DispatcherQueue { get; set; }

    public bool IsDesignMode { get; set; }

    public VelloGraphicsDeviceOptions DeviceOptions { get; set; } = VelloGraphicsDeviceOptions.Default;

    public VelloRenderBackend PreferredBackend { get; set; } = VelloRenderBackend.Gpu;

    public VelloRenderMode RenderMode { get; set; } = VelloRenderMode.OnDemand;

    public RenderLoopDriver RenderLoopDriver { get; set; } = RenderLoopDriver.CompositionTarget;

    public bool IsContinuousRendering => RenderMode == VelloRenderMode.Continuous;

    public int ApplySkiaOptOutCount { get; private set; }

    public int RemoveSkiaOptOutCount { get; private set; }

    public int ContentInvalidatedCount { get; private set; }

    public int RenderSurfaceCount { get; private set; }

    public int DiagnosticsUpdatedCount { get; private set; }

    public void ApplySkiaOptOut()
        => ApplySkiaOptOutCount++;

    public void RemoveSkiaOptOut()
        => RemoveSkiaOptOutCount++;

    public void OnPaintSurface(VelloPaintSurfaceEventArgs args)
    {
    }

    public void OnRenderSurface(VelloSwapChainRenderEventArgs args)
        => RenderSurfaceCount++;

    public void OnContentInvalidated()
        => ContentInvalidatedCount++;

    public void OnDiagnosticsUpdated(WindowsGpuDiagnostics diagnostics)
        => DiagnosticsUpdatedCount++;
}
#endif
