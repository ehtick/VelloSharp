#if HAS_UNO

using System;
using System.Reflection;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel;
using VelloSharp;
using VelloSharp.Uno.Interop;
using VelloSharp.Windows;
using VelloSharp.Windows.Shared.Contracts;
using VelloSharp.Windows.Shared.Diagnostics;
using VelloSharp.Windows.Shared.Dispatching;
using VelloSharp.Windows.Shared.Presenters;
using VelloSharp.Uno.Dispatching;
using VelloPaintSurfaceEventArgs = VelloSharp.WinForms.Integration.VelloPaintSurfaceEventArgs;
using XamlSolidColorBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;

namespace VelloSharp.Uno.Controls;

public sealed class VelloSwapChainPanel : SwapChainPanel, IDisposable, IVelloSwapChainPresenterHost, IVelloSurfaceRenderCallback, IVelloDiagnosticsProvider
{
    public static readonly DependencyProperty DeviceOptionsProperty = DependencyProperty.Register(
        nameof(DeviceOptions),
        typeof(VelloGraphicsDeviceOptions),
        typeof(VelloSwapChainPanel),
        new PropertyMetadata(VelloGraphicsDeviceOptions.Default, OnDeviceOptionsChanged));

    public static readonly DependencyProperty PreferredBackendProperty = DependencyProperty.Register(
        nameof(PreferredBackend),
        typeof(VelloRenderBackend),
        typeof(VelloSwapChainPanel),
        new PropertyMetadata(VelloRenderBackend.Gpu, OnPreferredBackendChanged));

    public static readonly DependencyProperty RenderModeProperty = DependencyProperty.Register(
        nameof(RenderMode),
        typeof(VelloRenderMode),
        typeof(VelloSwapChainPanel),
        new PropertyMetadata(VelloRenderMode.OnDemand, OnRenderModeChanged));

    public static readonly DependencyProperty RenderLoopDriverProperty = DependencyProperty.Register(
        nameof(RenderLoopDriver),
        typeof(RenderLoopDriver),
        typeof(VelloSwapChainPanel),
        new PropertyMetadata(RenderLoopDriver.CompositionTarget, OnRenderLoopDriverChanged));

    public static readonly DependencyProperty DiagnosticsProperty = DependencyProperty.Register(
        nameof(Diagnostics),
        typeof(WindowsGpuDiagnostics),
        typeof(VelloSwapChainPanel),
        new PropertyMetadata(null));

    public static readonly DependencyProperty SuppressGraphicsViewCompositorProperty = DependencyProperty.Register(
        nameof(SuppressGraphicsViewCompositor),
        typeof(bool),
        typeof(VelloSwapChainPanel),
        new PropertyMetadata(true, OnSuppressGraphicsViewCompositorChanged));

    private static readonly object SkiaOptOutLock = new();
    private static MethodInfo? s_setNativeHostVisualMethod;

    private readonly UnoSwapChainPanelSurfaceSource _surfaceSource;
    private readonly VelloSwapChainPresenter _presenter;
    private readonly bool _isDesignMode;
    private Visual? _compositionVisual;
    private bool _skiaOptOutApplied;
    private bool _isControlVisible = true;
    private bool _isHostVisible = true;
    private long _visibilityCallbackToken = -1;
    private event EventHandler<VelloDiagnosticsChangedEventArgs>? _diagnosticsUpdated;

    public event EventHandler<VelloPaintSurfaceEventArgs>? PaintSurface;

    public event EventHandler<VelloSurfaceRenderEventArgs>? RenderSurfaceSkia;
    public event EventHandler<VelloSwapChainRenderEventArgs>? RenderSurface;

    public event EventHandler? ContentInvalidated;

    public VelloSwapChainPanel()
    {
        _isDesignMode = DesignMode.DesignModeEnabled || DesignMode.DesignMode2Enabled;
        _surfaceSource = new UnoSwapChainPanelSurfaceSource(this);
        _presenter = new VelloSwapChainPresenter(this, _surfaceSource);
        SetValue(DiagnosticsProperty, _presenter.Diagnostics);

        if (_isDesignMode)
        {
            Children.Add(CreateDesignModePlaceholder());
            Background ??= new XamlSolidColorBrush(global::Windows.UI.Color.FromArgb(12, 0, 0, 0));
        }

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
        _visibilityCallbackToken = RegisterPropertyChangedCallback(VisibilityProperty, OnVisibilityPropertyChanged);
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

    public WindowsGpuDiagnostics Diagnostics
    {
        get
        {
            if (GetValue(DiagnosticsProperty) is WindowsGpuDiagnostics diagnostics)
            {
                return diagnostics;
            }

            return _presenter.Diagnostics;
        }
    }

    public bool SuppressGraphicsViewCompositor
    {
        get => (bool)(GetValue(SuppressGraphicsViewCompositorProperty) ?? true);
        set => SetValue(SuppressGraphicsViewCompositorProperty, value);
    }

    public bool IsContinuousRendering => RenderMode == VelloRenderMode.Continuous;

    public void RequestRender() => _presenter.RequestRender();

    public void Dispose()
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        SizeChanged -= OnSizeChanged;

        if (_visibilityCallbackToken != -1)
        {
            UnregisterPropertyChangedCallback(VisibilityProperty, _visibilityCallbackToken);
            _visibilityCallbackToken = -1;
        }

        DetachLifecycleHandlers();

        _presenter.Dispose();
        _surfaceSource.ReleaseNativePointer();
        _surfaceSource.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void OnDeviceOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloSwapChainPanel panel)
        {
            panel._presenter.OnDeviceOptionsChanged();
        }
    }

    private static void OnPreferredBackendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloSwapChainPanel panel)
        {
            panel._presenter.OnPreferredBackendChanged();
        }
    }

    private static void OnRenderModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloSwapChainPanel panel)
        {
            panel._presenter.OnRenderModeChanged();
            if (panel.RenderMode == VelloRenderMode.OnDemand)
            {
                panel.RequestRender();
            }
        }
    }

    private static void OnRenderLoopDriverChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloSwapChainPanel panel)
        {
            panel._presenter.OnRenderLoopDriverChanged();
        }
    }

    private static void OnSuppressGraphicsViewCompositorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not VelloSwapChainPanel panel)
        {
            return;
        }

        if ((bool)(args.NewValue ?? true))
        {
            panel.ApplySkiaOptOut();
        }
        else
        {
            panel.RemoveSkiaOptOut();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachLifecycleHandlers();
        _isControlVisible = Visibility == Visibility.Visible;
        _presenter.OnLoaded();
        UpdateRenderSuspension();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachLifecycleHandlers();
        _presenter.OnUnloaded();
        _surfaceSource.ReleaseNativePointer();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _presenter.OnSurfaceInvalidated();
    }

    private void OnVisibilityPropertyChanged(DependencyObject sender, DependencyProperty dependencyProperty)
    {
        var isVisible = Visibility == Visibility.Visible;
        if (_isControlVisible == isVisible)
        {
            return;
        }

        _isControlVisible = isVisible;
        UpdateRenderSuspension();
        if (isVisible)
        {
            _presenter.OnSurfaceInvalidated();
        }
    }

    private void AttachLifecycleHandlers()
    {
        if (_isDesignMode)
        {
            return;
        }

        AttachXamlRootChanged();
        _isHostVisible = XamlRoot?.IsHostVisible ?? true;
    }

    private void DetachLifecycleHandlers()
    {
        DetachXamlRootChanged();
    }

    private void AttachXamlRootChanged()
    {
        if (XamlRoot is { } root)
        {
            root.Changed += OnXamlRootChanged;
        }
    }

    private void DetachXamlRootChanged()
    {
        if (XamlRoot is { } root)
        {
            root.Changed -= OnXamlRootChanged;
        }
    }

    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        ApplySkiaOptOut();

        var hostVisible = sender.IsHostVisible;
        if (_isHostVisible != hostVisible)
        {
            _isHostVisible = hostVisible;
            UpdateRenderSuspension();
        }

        _presenter.OnSurfaceInvalidated();
    }

    private void UpdateRenderSuspension()
    {
        if (_isDesignMode)
        {
            return;
        }

        var shouldSuspend = !_isControlVisible || !_isHostVisible;
        _presenter.SetRenderSuspended(shouldSuspend);
    }

    private UIElement CreateDesignModePlaceholder()
    {
        return new Border
        {
            Background = new XamlSolidColorBrush(global::Windows.UI.Color.FromArgb(24, 0, 0, 0)),
            BorderBrush = new XamlSolidColorBrush(global::Windows.UI.Color.FromArgb(48, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = new TextBlock
            {
                Text = "VelloSwapChainPanel (design-time placeholder)",
                TextWrapping = TextWrapping.WrapWholeWords,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.7,
            },
        };
    }

    private void ApplySkiaOptOut()
    {
        if (_skiaOptOutApplied)
        {
            return;
        }

        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(this);
            if (visual is null)
            {
                return;
            }

            if (TrySetNativeHostVisual(visual, true))
            {
                _compositionVisual = visual;
                _skiaOptOutApplied = true;
            }
        }
        catch (Exception)
        {
            // Ignore opt-out failures; compositor fallback will remain active.
        }
    }

    private void RemoveSkiaOptOut()
    {
        if (!_skiaOptOutApplied)
        {
            return;
        }

        try
        {
            if (_compositionVisual is { } visual)
            {
                TrySetNativeHostVisual(visual, null);
            }
        }
        catch (Exception)
        {
            // Ignore revert failures.
        }
        finally
        {
            _compositionVisual = null;
            _skiaOptOutApplied = false;
        }
    }

    private static bool TrySetNativeHostVisual(Visual visual, bool? value)
    {
        MethodInfo? method;
        lock (SkiaOptOutLock)
        {
            method = s_setNativeHostVisualMethod;
            if (method is null)
            {
                method = visual
                    .GetType()
                    .GetMethod("SetAsNativeHostVisual", BindingFlags.Instance | BindingFlags.NonPublic, binder: null, types: new[] { typeof(bool?) }, modifiers: null);
                s_setNativeHostVisualMethod = method;
            }
        }

        if (method is null)
        {
            return false;
        }

        try
        {
            method.Invoke(visual, new object?[] { value });
            return true;
        }
        catch (TargetInvocationException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    IVelloWindowsDispatcher? IVelloSwapChainPresenterHost.Dispatcher
        => UnoDispatcher.Wrap(DispatcherQueue ?? Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());

    IVelloCompositionTarget? IVelloSwapChainPresenterHost.CompositionTarget
        => UnoCompositionTargetAdapter.Instance;

    bool IVelloSwapChainPresenterHost.IsContinuousRendering => IsContinuousRendering;

    bool IVelloSwapChainPresenterHost.IsDesignMode => _isDesignMode;

    VelloGraphicsDeviceOptions IVelloSwapChainPresenterHost.DeviceOptions => DeviceOptions ?? VelloGraphicsDeviceOptions.Default;

    VelloRenderBackend IVelloSwapChainPresenterHost.PreferredBackend => PreferredBackend;

    VelloRenderMode IVelloSwapChainPresenterHost.RenderMode => RenderMode;

    RenderLoopDriver IVelloSwapChainPresenterHost.RenderLoopDriver => RenderLoopDriver;

    void IVelloSwapChainPresenterHost.OnPaintSurface(VelloPaintSurfaceEventArgs args)
        => PaintSurface?.Invoke(this, args);

    void IVelloSwapChainPresenterHost.OnRenderSurface(VelloSwapChainRenderEventArgs args)
        => RenderSurface?.Invoke(this, args);

    void IVelloSurfaceRenderCallback.OnRenderSurface(VelloSurfaceRenderEventArgs args)
        => RenderSurfaceSkia?.Invoke(this, args);

    void IVelloSwapChainPresenterHost.OnContentInvalidated()
        => ContentInvalidated?.Invoke(this, EventArgs.Empty);

    void IVelloSwapChainPresenterHost.OnDiagnosticsUpdated(WindowsGpuDiagnostics diagnostics)
        => UpdateDiagnosticsBinding(diagnostics);

    void IVelloSwapChainPresenterHost.ApplySkiaOptOut()
        => ApplySkiaOptOut();

    void IVelloSwapChainPresenterHost.RemoveSkiaOptOut()
        => RemoveSkiaOptOut();

    WindowsGpuDiagnostics IVelloDiagnosticsProvider.Diagnostics => Diagnostics;

    event EventHandler<VelloDiagnosticsChangedEventArgs>? IVelloDiagnosticsProvider.DiagnosticsUpdated
    {
        add => _diagnosticsUpdated += value;
        remove => _diagnosticsUpdated -= value;
    }

    private void UpdateDiagnosticsBinding(WindowsGpuDiagnostics diagnostics)
    {
        SetValue(DiagnosticsProperty, diagnostics);
        _diagnosticsUpdated?.Invoke(this, new VelloDiagnosticsChangedEventArgs(diagnostics));
    }
}

#endif
