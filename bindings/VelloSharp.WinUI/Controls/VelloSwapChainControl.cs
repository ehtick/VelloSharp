using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation.Peers;
using Windows.ApplicationModel;
using VelloSharp;
using VelloSharp.Composition.Input;
using VelloSharp.Windows;
using VelloSharp.Windows.Interop;
using VelloSharp.Windows.Accessibility;
using VelloSharp.Windows.Input;
using VelloSharp.Windows.Shared.Contracts;
using VelloSharp.Windows.Shared.Diagnostics;
using VelloSharp.Windows.Shared.Dispatching;
using VelloSharp.Windows.Shared.Presenters;
using VelloSharp.Windows.WinUI.Dispatching;
using VelloPaintSurfaceEventArgs = VelloSharp.WinForms.Integration.VelloPaintSurfaceEventArgs;
using XamlSolidColorBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;

namespace VelloSharp.Windows.Controls;

/// <summary>
/// SwapChainPanel-based WinUI control that hosts the Vello GPU renderer via <see cref="VelloSwapChainPresenter"/>.
/// </summary>
public sealed class VelloSwapChainControl : SwapChainPanel, IDisposable, IVelloSwapChainPresenterHost, IVelloDiagnosticsProvider
{
    public static readonly DependencyProperty DeviceOptionsProperty = DependencyProperty.Register(
        nameof(DeviceOptions),
        typeof(VelloGraphicsDeviceOptions),
        typeof(VelloSwapChainControl),
        new PropertyMetadata(VelloGraphicsDeviceOptions.Default, OnDeviceOptionsChanged));

    public static readonly DependencyProperty PreferredBackendProperty = DependencyProperty.Register(
        nameof(PreferredBackend),
        typeof(VelloRenderBackend),
        typeof(VelloSwapChainControl),
        new PropertyMetadata(VelloRenderBackend.Gpu, OnPreferredBackendChanged));

    public static readonly DependencyProperty RenderModeProperty = DependencyProperty.Register(
        nameof(RenderMode),
        typeof(VelloRenderMode),
        typeof(VelloSwapChainControl),
        new PropertyMetadata(VelloRenderMode.OnDemand, OnRenderModeChanged));

    public static readonly DependencyProperty RenderLoopDriverProperty = DependencyProperty.Register(
        nameof(RenderLoopDriver),
        typeof(RenderLoopDriver),
        typeof(VelloSwapChainControl),
        new PropertyMetadata(RenderLoopDriver.CompositionTarget, OnRenderLoopDriverChanged));

    public static readonly DependencyProperty DiagnosticsProperty = DependencyProperty.Register(
        nameof(Diagnostics),
        typeof(WindowsGpuDiagnostics),
        typeof(VelloSwapChainControl),
        new PropertyMetadata(null));

    public static readonly DependencyProperty SuppressGraphicsViewCompositorProperty = DependencyProperty.Register(
        nameof(SuppressGraphicsViewCompositor),
        typeof(bool),
        typeof(VelloSwapChainControl),
        new PropertyMetadata(true, OnSuppressGraphicsViewCompositorChanged));

    private static readonly object SkiaOptOutLock = new();
    private static MethodInfo? s_setNativeHostVisualMethod;

    private readonly WinUISwapChainPanelSurfaceSource _surfaceSource;
    private readonly VelloSwapChainPresenter _presenter;
    private readonly bool _isDesignMode;
    private Visual? _compositionVisual;
    private bool _skiaOptOutApplied;
    private long _visibilityCallbackToken = -1;
    private bool _disposed;
    private event EventHandler<VelloDiagnosticsChangedEventArgs>? _diagnosticsUpdated;
    private AccessKitTreeUpdate? _accessKitTree;
    private AccessKitTreeSnapshot? _accessibilitySnapshot;

    public event EventHandler<VelloPaintSurfaceEventArgs>? PaintSurface;

    public event EventHandler<VelloSwapChainRenderEventArgs>? RenderSurface;

    public event EventHandler? ContentInvalidated;

    public event EventHandler<AccessKitTreeUpdate>? AccessKitTreeUpdated;

    public event EventHandler<AccessKitActionRequest>? AccessKitActionRequested;

    public AccessKitTreeUpdate? CurrentAccessKitTree => _accessKitTree;

    internal ulong AccessibilityFocusNodeId
        => _accessibilitySnapshot?.FocusId ?? _accessibilitySnapshot?.RootId ?? 0;

    public void SubmitAccessKitTreeUpdate(AccessKitTreeUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (DispatcherQueue is { HasThreadAccess: false } dispatcher)
        {
            var queuedUpdate = update.Clone();
            dispatcher.TryEnqueue(() =>
            {
                using var scoped = queuedUpdate;
                SubmitAccessKitTreeUpdateInternal(scoped);
            });
            return;
        }

        SubmitAccessKitTreeUpdateInternal(update);
    }

    public void SubmitAccessKitActionRequest(AccessKitActionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (DispatcherQueue is { HasThreadAccess: false } dispatcher)
        {
            var json = request.ToJson();
            request.Dispose();
            dispatcher.TryEnqueue(() =>
            {
                var cloned = AccessKitActionRequest.FromJson(json);
                SubmitAccessKitActionRequestInternal(cloned);
            });
            return;
        }

        SubmitAccessKitActionRequestInternal(request);
    }

    private void SubmitAccessKitTreeUpdateInternal(AccessKitTreeUpdate update)
    {
        var clone = update.Clone();
        _accessKitTree?.Dispose();
        _accessKitTree = clone;

        try
        {
            using var document = update.ToJsonDocument();
            _accessibilitySnapshot = AccessKitTreeSnapshot.FromUpdate(document);
        }
        catch
        {
            _accessibilitySnapshot = null;
        }

        if (FrameworkElementAutomationPeer.FromElement(this) is VelloSwapChainAutomationPeer peer)
        {
            peer.UpdateSnapshot(_accessibilitySnapshot);
        }

        AccessKitTreeUpdated?.Invoke(this, clone);
    }

    private void SubmitAccessKitActionRequestInternal(AccessKitActionRequest request)
    {
        AccessKitActionRequested?.Invoke(this, request);
    }

    public ICompositionInputSource CreateCompositionInputSource()
        => new VelloSwapChainInputSource(this);

    internal void RequestAccessKitAction(string action, ulong targetId)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return;
        }

        if (DispatcherQueue is { HasThreadAccess: false } dispatcher)
        {
            dispatcher.TryEnqueue(() => RequestAccessKitAction(action, targetId));
            return;
        }

        var payload = new AccessKitActionRequestDto
        {
            Action = action,
            Target = targetId,
        };

        var request = AccessKitActionRequest.FromObject(payload);
        SubmitAccessKitActionRequestInternal(request);
    }

    public VelloSwapChainControl()
    {
        _isDesignMode = DesignMode.DesignModeEnabled || DesignMode.DesignMode2Enabled;
        _surfaceSource = new WinUISwapChainPanelSurfaceSource(this);
        _presenter = new VelloSwapChainPresenter(this, _surfaceSource);
        SetValue(DiagnosticsProperty, _presenter.Diagnostics);
        IsTabStop = true;

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

    protected override AutomationPeer OnCreateAutomationPeer()
        => new VelloSwapChainAutomationPeer(this);

    public bool SuppressGraphicsViewCompositor
    {
        get => (bool)(GetValue(SuppressGraphicsViewCompositorProperty) ?? true);
        set => SetValue(SuppressGraphicsViewCompositorProperty, value);
    }

    public bool IsContinuousRendering => RenderMode == VelloRenderMode.Continuous;

    public void RequestRender() => _presenter.RequestRender();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        SizeChanged -= OnSizeChanged;

        if (_visibilityCallbackToken != -1)
        {
            UnregisterPropertyChangedCallback(VisibilityProperty, _visibilityCallbackToken);
            _visibilityCallbackToken = -1;
        }

        _presenter.SetRenderSuspended(true);
        _presenter.Dispose();
        _surfaceSource.ReleaseNativePointer();
        _surfaceSource.Dispose();
        _accessKitTree?.Dispose();
        _accessKitTree = null;
        GC.SuppressFinalize(this);
    }

    private static void OnDeviceOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloSwapChainControl control)
        {
            control._presenter.OnDeviceOptionsChanged();
        }
    }

    private static void OnPreferredBackendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloSwapChainControl control)
        {
            control._presenter.OnPreferredBackendChanged();
        }
    }

    private static void OnRenderModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloSwapChainControl control)
        {
            control._presenter.OnRenderModeChanged();
            if (control.RenderMode == VelloRenderMode.OnDemand)
            {
                control.RequestRender();
            }
        }
    }

    private static void OnRenderLoopDriverChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloSwapChainControl control)
        {
            control._presenter.OnRenderLoopDriverChanged();
        }
    }

    private static void OnSuppressGraphicsViewCompositorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not VelloSwapChainControl control || control._isDesignMode)
        {
            return;
        }

        if ((bool)(args.NewValue ?? true))
        {
            control.ApplySkiaOptOut();
        }
        else
        {
            control.RemoveSkiaOptOut();
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_isDesignMode || _disposed)
        {
            return;
        }

        _presenter.OnLoaded();
        _presenter.SetRenderSuspended(Visibility != Visibility.Visible);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (_isDesignMode || _disposed)
        {
            return;
        }

        _presenter.SetRenderSuspended(true);
        _presenter.OnUnloaded();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isDesignMode || _disposed)
        {
            return;
        }

        _presenter.OnSurfaceInvalidated();
    }

    private void OnVisibilityPropertyChanged(DependencyObject sender, DependencyProperty dp)
    {
        if (_isDesignMode || _disposed)
        {
            return;
        }

        _presenter.SetRenderSuspended(Visibility != Visibility.Visible);
    }

    private void ApplySkiaOptOut()
    {
        if (_skiaOptOutApplied || _isDesignMode)
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
        => WinUIDispatcher.Wrap(DispatcherQueue ?? Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());

    IVelloCompositionTarget? IVelloSwapChainPresenterHost.CompositionTarget
        => WinUICompositionTargetAdapter.Instance;

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

    private static UIElement CreateDesignModePlaceholder()
    {
        return new Border
        {
            Background = new XamlSolidColorBrush(global::Windows.UI.Color.FromArgb(12, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            BorderBrush = new XamlSolidColorBrush(global::Windows.UI.Color.FromArgb(32, 0, 0, 0)),
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock
            {
                Text = "VelloSwapChainControl design-time placeholder.",
                TextWrapping = TextWrapping.WrapWholeWords,
            },
        };
    }

    private sealed class AccessKitActionRequestDto
    {
        [JsonPropertyName("action")]
        public string Action { get; init; } = string.Empty;

        [JsonPropertyName("target")]
        public ulong Target { get; init; }
    }
}
