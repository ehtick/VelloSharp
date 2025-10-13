using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using VelloSharp.Windows;
using VelloSharp.Windows.Shared.Contracts;
using VelloSharp.Windows.Shared.Diagnostics;
using VelloSharp.Windows.Shared.Presenters;
using VelloPaintSurfaceEventArgs = VelloSharp.WinForms.Integration.VelloPaintSurfaceEventArgs;

namespace VelloSharp.Windows.Controls;

/// <summary>
/// Composition-friendly wrapper that hosts a <see cref="VelloSwapChainControl"/> while allowing overlay visuals and
/// layout within XAML content trees that require transparent regions.
/// </summary>
public sealed class VelloCompositionControl : Grid, IVelloDiagnosticsProvider
{
    public static readonly DependencyProperty DeviceOptionsProperty = DependencyProperty.Register(
        nameof(DeviceOptions),
        typeof(VelloGraphicsDeviceOptions),
        typeof(VelloCompositionControl),
        new PropertyMetadata(VelloGraphicsDeviceOptions.Default, OnDeviceOptionsChanged));

    public static readonly DependencyProperty PreferredBackendProperty = DependencyProperty.Register(
        nameof(PreferredBackend),
        typeof(VelloRenderBackend),
        typeof(VelloCompositionControl),
        new PropertyMetadata(VelloRenderBackend.Gpu, OnPreferredBackendChanged));

    public static readonly DependencyProperty RenderModeProperty = DependencyProperty.Register(
        nameof(RenderMode),
        typeof(VelloRenderMode),
        typeof(VelloCompositionControl),
        new PropertyMetadata(VelloRenderMode.OnDemand, OnRenderModeChanged));

    public static readonly DependencyProperty RenderLoopDriverProperty = DependencyProperty.Register(
        nameof(RenderLoopDriver),
        typeof(RenderLoopDriver),
        typeof(VelloCompositionControl),
        new PropertyMetadata(RenderLoopDriver.CompositionTarget, OnRenderLoopDriverChanged));

    public static readonly DependencyProperty OverlayContentProperty = DependencyProperty.Register(
        nameof(OverlayContent),
        typeof(UIElement),
        typeof(VelloCompositionControl),
        new PropertyMetadata(null, OnOverlayContentChanged));

    private readonly VelloSwapChainControl _swapChainControl;
    private readonly Grid _overlayLayer;
    private IVelloDiagnosticsProvider? _diagnosticsProvider;
    private event EventHandler<VelloDiagnosticsChangedEventArgs>? _diagnosticsUpdated;

    public VelloCompositionControl()
    {
        _swapChainControl = new VelloSwapChainControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        _overlayLayer = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false,
        };

        Children.Add(_swapChainControl);
        Children.Add(_overlayLayer);

        _swapChainControl.PaintSurface += (_, args) => PaintSurface?.Invoke(this, args);
        _swapChainControl.RenderSurface += (_, args) => RenderSurface?.Invoke(this, args);
        _swapChainControl.ContentInvalidated += (_, _) => ContentInvalidated?.Invoke(this, EventArgs.Empty);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

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

    public UIElement? OverlayContent
    {
        get => (UIElement?)GetValue(OverlayContentProperty);
        set => SetValue(OverlayContentProperty, value);
    }

    public event EventHandler<VelloPaintSurfaceEventArgs>? PaintSurface;

    public event EventHandler<VelloSwapChainRenderEventArgs>? RenderSurface;

    public event EventHandler? ContentInvalidated;

    public WindowsGpuDiagnostics Diagnostics => _swapChainControl.Diagnostics;

    event EventHandler<VelloDiagnosticsChangedEventArgs>? IVelloDiagnosticsProvider.DiagnosticsUpdated
    {
        add => _diagnosticsUpdated += value;
        remove => _diagnosticsUpdated -= value;
    }

    WindowsGpuDiagnostics IVelloDiagnosticsProvider.Diagnostics => Diagnostics;

    public void RequestRender() => _swapChainControl.RequestRender();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyProperties();
        EnsureCompositionFallback();
        AttachDiagnostics();
        ForwardDiagnostics(_swapChainControl.Diagnostics);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ElementCompositionPreview.SetElementChildVisual(this, null);
        DetachDiagnostics();
    }

    private void ApplyProperties()
    {
        _swapChainControl.DeviceOptions = DeviceOptions;
        _swapChainControl.PreferredBackend = PreferredBackend;
        _swapChainControl.RenderMode = RenderMode;
        _swapChainControl.RenderLoopDriver = RenderLoopDriver;
    }

    private void ForwardDiagnostics(WindowsGpuDiagnostics diagnostics)
    {
        _diagnosticsUpdated?.Invoke(this, new VelloDiagnosticsChangedEventArgs(diagnostics));
    }

    private void EnsureCompositionFallback()
    {
        try
        {
            // Ensure a visual exists so that callers can attach additional composition content via ElementCompositionPreview.
            var visual = ElementCompositionPreview.GetElementVisual(this);
            _ = visual.Compositor; // Accessor to force compositor realization.
        }
        catch
        {
            // Ignore composition setup failures; swapchain rendering will continue to function.
        }
    }

    private void ReplaceOverlay(UIElement? content)
    {
        _overlayLayer.Children.Clear();
        if (content is not null)
        {
            _overlayLayer.Children.Add(content);
        }
    }

    private void AttachDiagnostics()
    {
        if (_diagnosticsProvider is not null)
        {
            return;
        }

        _diagnosticsProvider = (IVelloDiagnosticsProvider)_swapChainControl;
        _diagnosticsProvider.DiagnosticsUpdated += OnSwapChainDiagnosticsUpdated;
    }

    private void DetachDiagnostics()
    {
        if (_diagnosticsProvider is null)
        {
            return;
        }

        _diagnosticsProvider.DiagnosticsUpdated -= OnSwapChainDiagnosticsUpdated;
        _diagnosticsProvider = null;
    }

    private void OnSwapChainDiagnosticsUpdated(object? sender, VelloDiagnosticsChangedEventArgs e)
        => ForwardDiagnostics(e.Diagnostics);

    private static void OnDeviceOptionsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is VelloCompositionControl control)
        {
            control._swapChainControl.DeviceOptions = (VelloGraphicsDeviceOptions)(e.NewValue ?? VelloGraphicsDeviceOptions.Default);
            control.ForwardDiagnostics(control._swapChainControl.Diagnostics);
        }
    }

    private static void OnPreferredBackendChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is VelloCompositionControl control && e.NewValue is VelloRenderBackend backend)
        {
            control._swapChainControl.PreferredBackend = backend;
        }
    }

    private static void OnRenderModeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is VelloCompositionControl control && e.NewValue is VelloRenderMode mode)
        {
            control._swapChainControl.RenderMode = mode;
        }
    }

    private static void OnRenderLoopDriverChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is VelloCompositionControl control && e.NewValue is RenderLoopDriver driver)
        {
            control._swapChainControl.RenderLoopDriver = driver;
        }
    }

    private static void OnOverlayContentChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is VelloCompositionControl control)
        {
            control.ReplaceOverlay(e.NewValue as UIElement);
        }
    }
}
