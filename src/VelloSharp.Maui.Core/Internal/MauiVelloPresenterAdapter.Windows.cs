#if WINDOWS
using System;
using Microsoft.UI.Xaml;
using VelloSharp.Maui.Controls;
using VelloSharp.Maui.Diagnostics;
using VelloSharp.Maui.Events;
using VelloSharp.Maui.Rendering;
using WinFormsIntegration = global::VelloSharp.WinForms.Integration;
using UnoControls = VelloSharp.Uno.Controls;
using MauiRendering = VelloSharp.Maui.Rendering;
using WindowsContracts = VelloSharp.Windows.Shared.Contracts;
using WindowsDiagnostics = VelloSharp.Windows.Shared.Diagnostics;
using WindowsPresenters = VelloSharp.Windows.Shared.Presenters;

namespace VelloSharp.Maui.Internal;

internal abstract partial class MauiVelloPresenterAdapter
{
    internal static partial MauiVelloPresenterAdapter? CreatePlatformAdapter(IVelloView view, IMauiContext? mauiContext)
        => new WindowsVelloPresenterAdapter(view);
}

internal sealed class WindowsVelloPresenterAdapter : MauiVelloPresenterAdapter
{
    private UnoControls.VelloSwapChainPanel? _panel;
    private TimeSpan _lastFrameTimestamp;

    public WindowsVelloPresenterAdapter(IVelloView view)
        : base(view)
    {
    }

    public override void Attach(object? platformView)
    {
        if (platformView is not UnoControls.VelloSwapChainPanel panel)
        {
            throw new InvalidOperationException("Expected VelloSwapChainPanel for Windows platform view.");
        }

        _panel = panel;
        ApplyViewState();

        _panel.PaintSurface += OnPaintSurface;
        _panel.RenderSurface += OnRenderSurface;
        _panel.ContentInvalidated += OnContentInvalidated;

        if (_panel is WindowsDiagnostics.IVelloDiagnosticsProvider diagnosticsProvider)
        {
            diagnosticsProvider.DiagnosticsUpdated += OnDiagnosticsUpdated;
        }
    }

    public override void Detach()
    {
        if (_panel is null)
        {
            return;
        }

        _panel.PaintSurface -= OnPaintSurface;
        _panel.RenderSurface -= OnRenderSurface;
        _panel.ContentInvalidated -= OnContentInvalidated;
        if (_panel is WindowsDiagnostics.IVelloDiagnosticsProvider diagnosticsProvider)
        {
            diagnosticsProvider.DiagnosticsUpdated -= OnDiagnosticsUpdated;
        }

        _panel = null;
        _lastFrameTimestamp = TimeSpan.Zero;
    }

    public override void OnDeviceOptionsChanged() => ApplyDeviceOptions();

    public override void OnPreferredBackendChanged()
    {
        if (_panel is null)
        {
            return;
        }

        _panel.PreferredBackend = ConvertBackend(View.PreferredBackend);
        if (View.PreferredBackend == MauiRendering.VelloRenderBackend.Gpu)
        {
            _panel.RequestRender();
        }
    }

    public override void OnRenderModeChanged()
    {
        if (_panel is null)
        {
            return;
        }

        _panel.RenderMode = ConvertRenderMode(View.RenderMode);
        if (View.RenderMode == VelloRenderMode.OnDemand)
        {
            _panel.RequestRender();
        }
    }

    public override void OnRenderLoopDriverChanged()
    {
        if (_panel is null)
        {
            return;
        }

        _panel.RenderLoopDriver = ConvertRenderLoopDriver(View.RenderLoopDriver);
    }

    public override void RequestRender()
    {
        _panel?.RequestRender();
    }

    public override void Suspend()
    {
        if (_panel is null)
        {
            return;
        }

        _panel.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    public override void Resume()
    {
        if (_panel is null)
        {
            return;
        }

        _panel.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        if (View.RenderMode == VelloRenderMode.OnDemand)
        {
            _panel.RequestRender();
        }
    }

    public override void OnDiagnosticsToggled()
    {
        if (!View.IsDiagnosticsEnabled)
        {
            View.Diagnostics.ResetFrameTiming();
            View.Diagnostics.ClearError();
        }
    }

    public override void OnGraphicsViewSuppressionChanged()
    {
        if (_panel is null)
        {
            return;
        }

        _panel.SuppressGraphicsViewCompositor = View.SuppressGraphicsViewCompositor;
    }

    public override void Dispose()
    {
        Detach();
        GC.SuppressFinalize(this);
    }

    private void ApplyViewState()
    {
        if (_panel is null)
        {
            return;
        }

        ApplyDeviceOptions();
        _panel.PreferredBackend = ConvertBackend(View.PreferredBackend);
        _panel.RenderMode = ConvertRenderMode(View.RenderMode);
        _panel.RenderLoopDriver = ConvertRenderLoopDriver(View.RenderLoopDriver);
        _panel.SuppressGraphicsViewCompositor = View.SuppressGraphicsViewCompositor;
        _panel.Visibility = View.IsInDesignMode
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Visible;
    }

    private void ApplyDeviceOptions()
    {
        if (_panel is null)
        {
            return;
        }

        var windowsOptions = ConvertDeviceOptions(View.DeviceOptions);
        if (!Equals(_panel.DeviceOptions, windowsOptions))
        {
            _panel.DeviceOptions = windowsOptions;
        }
    }

    private void OnPaintSurface(object? sender, WinFormsIntegration.VelloPaintSurfaceEventArgs e)
        => RaisePaintSurface(e);

    private void OnRenderSurface(object? sender, WindowsPresenters.VelloSwapChainRenderEventArgs e)
    {
        var instantaneousFps = e.Delta > TimeSpan.Zero ? 1.0 / e.Delta.TotalSeconds : 0.0;

        var snapshot = new VelloDiagnosticsSnapshot(
            FramesPerSecond: instantaneousFps,
            SwapChainResets: e.Lease.Context.Diagnostics.SurfaceConfigurations,
            KeyedMutexContention: e.Lease.Context.Diagnostics.KeyedMutexFallbacks + e.Lease.Context.Diagnostics.KeyedMutexTimeouts,
            LastError: e.Lease.Context.Diagnostics.LastError,
            ExtendedProperties: null);

        var deltaFromPrevious = _lastFrameTimestamp == TimeSpan.Zero
            ? TimeSpan.Zero
            : e.Timestamp - _lastFrameTimestamp;

        if (deltaFromPrevious > TimeSpan.Zero)
        {
            View.Diagnostics.UpdateFrame(deltaFromPrevious, snapshot);
        }
        else
        {
            View.Diagnostics.UpdateFromSnapshot(snapshot);
        }

        _lastFrameTimestamp = e.Timestamp;

        var args = new VelloSurfaceRenderEventArgs(
            e.Timestamp,
            e.Delta,
            e.FrameId,
            e.PixelSize.Width,
            e.PixelSize.Height,
            platformContext: e.Lease,
            platformSurface: e.Surface);

        if (View.IsDiagnosticsEnabled)
        {
            RaiseDiagnostics(snapshot);
        }

        RaiseRenderSurface(args);
    }

    private void OnContentInvalidated(object? sender, EventArgs e)
    {
        RequestRender();
    }

    private void OnDiagnosticsUpdated(object? sender, WindowsDiagnostics.VelloDiagnosticsChangedEventArgs e)
    {
        var diagnostics = e.Diagnostics;
        var snapshot = new VelloDiagnosticsSnapshot(
            FramesPerSecond: View.Diagnostics.FramesPerSecond,
            SwapChainResets: diagnostics.SurfaceConfigurations,
            KeyedMutexContention: diagnostics.KeyedMutexFallbacks + diagnostics.KeyedMutexTimeouts,
            LastError: diagnostics.LastError,
            ExtendedProperties: null);

        View.Diagnostics.UpdateFromSnapshot(snapshot);

        if (View.IsDiagnosticsEnabled)
        {
            RaiseDiagnostics(snapshot);
        }
    }

    private static VelloSharp.Windows.VelloGraphicsDeviceOptions ConvertDeviceOptions(MauiRendering.VelloGraphicsDeviceOptions options)
    {
        var defaults = VelloSharp.Windows.VelloGraphicsDeviceOptions.Default;
        return defaults with
        {
            PreferDiscreteAdapter = options.PreferDiscreteAdapter,
            MsaaSampleCount = options.MsaaSampleCount,
            DiagnosticsLabel = options.DiagnosticsLabel,
        };
    }

    private static WindowsContracts.VelloRenderBackend ConvertBackend(MauiRendering.VelloRenderBackend backend)
        => backend == MauiRendering.VelloRenderBackend.Cpu
            ? WindowsContracts.VelloRenderBackend.Cpu
            : WindowsContracts.VelloRenderBackend.Gpu;

    private static WindowsContracts.VelloRenderMode ConvertRenderMode(MauiRendering.VelloRenderMode mode)
        => mode == MauiRendering.VelloRenderMode.Continuous
            ? WindowsContracts.VelloRenderMode.Continuous
            : WindowsContracts.VelloRenderMode.OnDemand;

    private static VelloSharp.Windows.RenderLoopDriver ConvertRenderLoopDriver(MauiRendering.RenderLoopDriver driver)
        => driver switch
        {
            MauiRendering.RenderLoopDriver.CompositionTarget => VelloSharp.Windows.RenderLoopDriver.CompositionTarget,
            MauiRendering.RenderLoopDriver.DispatcherQueue => VelloSharp.Windows.RenderLoopDriver.ComponentDispatcher,
            _ => VelloSharp.Windows.RenderLoopDriver.None,
        };
}
#endif
