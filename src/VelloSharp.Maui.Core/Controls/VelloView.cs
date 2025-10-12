using System;
using Microsoft.Maui.Controls;
using VelloSharp.Composition.Input;
using VelloSharp.Maui.Diagnostics;
using VelloSharp.Maui.Events;
using VelloSharp.Maui.Rendering;
using VelloSharp.WinForms.Integration;

namespace VelloSharp.Maui.Controls;

/// <summary>
/// MAUI surface that exposes Vello rendering hooks to platform handlers.
/// </summary>
public partial class VelloView : View, IVelloView
{
    public static readonly BindableProperty DeviceOptionsProperty = BindableProperty.Create(
        nameof(DeviceOptions),
        typeof(VelloGraphicsDeviceOptions),
        typeof(VelloView),
        VelloGraphicsDeviceOptions.Default);

    public static readonly BindableProperty PreferredBackendProperty = BindableProperty.Create(
        nameof(PreferredBackend),
        typeof(VelloRenderBackend),
        typeof(VelloView),
        VelloRenderBackend.Gpu);

    public static readonly BindableProperty RenderModeProperty = BindableProperty.Create(
        nameof(RenderMode),
        typeof(VelloRenderMode),
        typeof(VelloView),
        VelloRenderMode.OnDemand);

    public static readonly BindableProperty RenderLoopDriverProperty = BindableProperty.Create(
        nameof(RenderLoopDriver),
        typeof(RenderLoopDriver),
        typeof(VelloView),
        RenderLoopDriver.CompositionTarget);

    public static readonly BindableProperty IsDiagnosticsEnabledProperty = BindableProperty.Create(
        nameof(IsDiagnosticsEnabled),
        typeof(bool),
        typeof(VelloView),
        false);

    public static readonly BindableProperty UseTextureViewProperty = BindableProperty.Create(
        nameof(UseTextureView),
        typeof(bool),
        typeof(VelloView),
        false);

    public static readonly BindableProperty SuppressGraphicsViewCompositorProperty = BindableProperty.Create(
        nameof(SuppressGraphicsViewCompositor),
        typeof(bool),
        typeof(VelloView),
        false);

    private readonly VelloViewDiagnostics _diagnostics = new();
    private readonly bool _isDesignMode;

    public VelloView()
    {
        _isDesignMode = DesignMode.IsDesignModeEnabled;
    }

    public event EventHandler<VelloPaintSurfaceEventArgs>? PaintSurface;

    public event EventHandler<VelloSurfaceRenderEventArgs>? RenderSurface;

    public event EventHandler<VelloDiagnosticsChangedEventArgs>? DiagnosticsChanged;

    public event EventHandler<string?>? GpuUnavailable;

    public VelloGraphicsDeviceOptions DeviceOptions
    {
        get => (VelloGraphicsDeviceOptions)(GetValue(DeviceOptionsProperty) ?? VelloGraphicsDeviceOptions.Default);
        set => SetValue(DeviceOptionsProperty, value ?? VelloGraphicsDeviceOptions.Default);
    }

    public VelloRenderBackend PreferredBackend
    {
        get => (VelloRenderBackend)(GetValue(PreferredBackendProperty) ?? VelloRenderBackend.Gpu);
        set => SetValue(PreferredBackendProperty, value);
    }

    public VelloRenderMode RenderMode
    {
        get => (VelloRenderMode)(GetValue(RenderModeProperty) ?? VelloRenderMode.OnDemand);
        set => SetValue(RenderModeProperty, value);
    }

    public RenderLoopDriver RenderLoopDriver
    {
        get => (RenderLoopDriver)(GetValue(RenderLoopDriverProperty) ?? RenderLoopDriver.CompositionTarget);
        set => SetValue(RenderLoopDriverProperty, value);
    }

    public bool IsDiagnosticsEnabled
    {
        get => (bool)(GetValue(IsDiagnosticsEnabledProperty) ?? false);
        set => SetValue(IsDiagnosticsEnabledProperty, value);
    }

    public bool UseTextureView
    {
        get => (bool)(GetValue(UseTextureViewProperty) ?? false);
        set => SetValue(UseTextureViewProperty, value);
    }

    public bool SuppressGraphicsViewCompositor
    {
        get => (bool)(GetValue(SuppressGraphicsViewCompositorProperty) ?? false);
        set => SetValue(SuppressGraphicsViewCompositorProperty, value);
    }

    public VelloViewDiagnostics Diagnostics => _diagnostics;

    public bool IsDesignMode => _isDesignMode;

    public void RequestRender() => InvalidateSurface();

    public void InvalidateSurface()
    {
        Handler?.Invoke(nameof(IVelloView.InvalidateSurface));
    }

    protected virtual void OnPaintSurface(VelloPaintSurfaceEventArgs args)
        => PaintSurface?.Invoke(this, args);

    protected virtual void OnRenderSurface(VelloSurfaceRenderEventArgs args)
        => RenderSurface?.Invoke(this, args);

    protected virtual void OnDiagnosticsUpdated(VelloDiagnosticsChangedEventArgs args)
        => DiagnosticsChanged?.Invoke(this, args);

    protected virtual void OnGpuUnavailable(string? reason)
        => GpuUnavailable?.Invoke(this, reason);

    VelloViewDiagnostics IVelloView.Diagnostics => Diagnostics;

    bool IVelloView.IsInDesignMode => IsDesignMode;

    bool IVelloView.SuppressGraphicsViewCompositor => SuppressGraphicsViewCompositor;

    public ICompositionInputSource? CompositionInputSource
        => (Handler as IVelloViewHandler)?.CompositionInputSource;

    void IVelloView.OnPaintSurface(VelloPaintSurfaceEventArgs args)
        => OnPaintSurface(args);

    void IVelloView.OnRenderSurface(VelloSurfaceRenderEventArgs args)
        => OnRenderSurface(args);

    void IVelloView.OnDiagnosticsUpdated(VelloDiagnosticsChangedEventArgs args)
    {
        Diagnostics.UpdateFromSnapshot(args.Snapshot);
        OnDiagnosticsUpdated(args);
    }

    void IVelloView.OnGpuUnavailable(string? message)
        => OnGpuUnavailable(message);
}
