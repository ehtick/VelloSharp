#if HAS_UNO

using System;
using System.Reflection;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.UI.Core;
using VelloSharp;
using VelloSharp.Uno.Interop;
using VelloSharp.Windows;
using VelloPaintSurfaceEventArgs = VelloSharp.WinForms.Integration.VelloPaintSurfaceEventArgs;
using CoreWindowActivatedEventArgs = Windows.UI.Core.WindowActivatedEventArgs;

namespace VelloSharp.Uno.Controls;

public sealed class VelloCoreWindowHost : FrameworkElement, IDisposable, IVelloSwapChainPresenterHost, IVelloDiagnosticsProvider
{
    public static readonly DependencyProperty DeviceOptionsProperty = DependencyProperty.Register(
        nameof(DeviceOptions),
        typeof(VelloGraphicsDeviceOptions),
        typeof(VelloCoreWindowHost),
        new PropertyMetadata(VelloGraphicsDeviceOptions.Default, OnDeviceOptionsChanged));

    public static readonly DependencyProperty PreferredBackendProperty = DependencyProperty.Register(
        nameof(PreferredBackend),
        typeof(VelloRenderBackend),
        typeof(VelloCoreWindowHost),
        new PropertyMetadata(VelloRenderBackend.Gpu, OnPreferredBackendChanged));

    public static readonly DependencyProperty RenderModeProperty = DependencyProperty.Register(
        nameof(RenderMode),
        typeof(VelloRenderMode),
        typeof(VelloCoreWindowHost),
        new PropertyMetadata(VelloRenderMode.OnDemand, OnRenderModeChanged));

    public static readonly DependencyProperty RenderLoopDriverProperty = DependencyProperty.Register(
        nameof(RenderLoopDriver),
        typeof(RenderLoopDriver),
        typeof(VelloCoreWindowHost),
        new PropertyMetadata(RenderLoopDriver.CompositionTarget, OnRenderLoopDriverChanged));

    public static readonly DependencyProperty DiagnosticsProperty = DependencyProperty.Register(
        nameof(Diagnostics),
        typeof(WindowsGpuDiagnostics),
        typeof(VelloCoreWindowHost),
        new PropertyMetadata(null));

    private static readonly object SkiaOptOutLock = new();
    private static MethodInfo? s_setNativeHostVisualMethod;

    private readonly CoreWindow _coreWindow;
    private readonly UnoCoreWindowSurfaceSource _surfaceSource;
    private readonly VelloSwapChainPresenter _presenter;
    private readonly bool _isDesignMode;
    private Visual? _compositionVisual;
    private bool _skiaOptOutApplied;
    private bool _isWindowVisible = true;
    private bool _isWindowActive = true;
    private bool _isHostVisible = true;
    private bool _coreWindowHandlersAttached;
    private event EventHandler<VelloDiagnosticsChangedEventArgs>? _diagnosticsUpdated;

    public event EventHandler<VelloPaintSurfaceEventArgs>? PaintSurface;

    public event EventHandler<VelloSwapChainRenderEventArgs>? RenderSurface;

    public event EventHandler? ContentInvalidated;

    public VelloCoreWindowHost()
    {
        _isDesignMode = DesignMode.DesignModeEnabled || DesignMode.DesignMode2Enabled;
        _coreWindow = CoreWindow.GetForCurrentThread()
            ?? throw new InvalidOperationException("CoreWindow is not available for the current thread.");

        _surfaceSource = new UnoCoreWindowSurfaceSource(_coreWindow);
        _presenter = new VelloSwapChainPresenter(this, _surfaceSource);
        SetValue(DiagnosticsProperty, _presenter.Diagnostics);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
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

    public bool IsContinuousRendering => RenderMode == VelloRenderMode.Continuous;

    public void RequestRender() => _presenter.RequestRender();

    public void Dispose()
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        SizeChanged -= OnSizeChanged;
        DetachLifecycleHandlers();

        _presenter.Dispose();
        _surfaceSource.ReleaseNativePointer();
        _surfaceSource.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override Size MeasureOverride(Size availableSize)
        => availableSize;

    protected override Size ArrangeOverride(Size finalSize)
        => finalSize;

    private static void OnDeviceOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloCoreWindowHost host)
        {
            host._presenter.OnDeviceOptionsChanged();
        }
    }

    private static void OnPreferredBackendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloCoreWindowHost host)
        {
            host._presenter.OnPreferredBackendChanged();
        }
    }

    private static void OnRenderModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloCoreWindowHost host)
        {
            host._presenter.OnRenderModeChanged();
            if (host.RenderMode == VelloRenderMode.OnDemand)
            {
                host.RequestRender();
            }
        }
    }

    private static void OnRenderLoopDriverChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloCoreWindowHost host)
        {
            host._presenter.OnRenderLoopDriverChanged();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachLifecycleHandlers();
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
        => _presenter.OnSurfaceInvalidated();

    private void AttachLifecycleHandlers()
    {
        if (_isDesignMode)
        {
            return;
        }

        AttachCoreWindowHandlers();
        AttachXamlRootChanged();
        _isHostVisible = XamlRoot?.IsHostVisible ?? true;
    }

    private void DetachLifecycleHandlers()
    {
        DetachCoreWindowHandlers();
        DetachXamlRootChanged();
    }

    private void AttachCoreWindowHandlers()
    {
        if (_coreWindowHandlersAttached)
        {
            return;
        }

        _coreWindow.VisibilityChanged += OnCoreWindowVisibilityChanged;
        _coreWindow.Activated += OnCoreWindowActivated;
        _isWindowVisible = _coreWindow.Visible;
        _coreWindowHandlersAttached = true;
    }

    private void DetachCoreWindowHandlers()
    {
        if (!_coreWindowHandlersAttached)
        {
            return;
        }

        _coreWindow.VisibilityChanged -= OnCoreWindowVisibilityChanged;
        _coreWindow.Activated -= OnCoreWindowActivated;
        _coreWindowHandlersAttached = false;
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

    private void OnCoreWindowVisibilityChanged(CoreWindow sender, VisibilityChangedEventArgs args)
    {
        _isWindowVisible = args.Visible;
        UpdateRenderSuspension();
        if (args.Visible)
        {
            _presenter.OnSurfaceInvalidated();
        }
    }

    private void OnCoreWindowActivated(CoreWindow sender, CoreWindowActivatedEventArgs args)
    {
        var isActive = args.WindowActivationState != CoreWindowActivationState.Deactivated;
        if (_isWindowActive == isActive)
        {
            return;
        }

        _isWindowActive = isActive;
        UpdateRenderSuspension();
    }

    private void UpdateRenderSuspension()
    {
        if (_isDesignMode)
        {
            return;
        }

        var shouldSuspend = !_isWindowVisible || !_isWindowActive || !_isHostVisible;
        _presenter.SetRenderSuspended(shouldSuspend);
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
            // Ignore failures.
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
            // Ignore failures.
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

    DispatcherQueue? IVelloSwapChainPresenterHost.DispatcherQueue
        => DispatcherQueue ?? Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

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
