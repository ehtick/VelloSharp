using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfD3DImage = System.Windows.Interop.D3DImage;
using WpfImage = System.Windows.Controls.Image;
using VelloSharp;
using VelloSharp.Windows;
using VelloPaintSurfaceEventArgs = VelloSharp.WinForms.Integration.VelloPaintSurfaceEventArgs;

namespace VelloSharp.Wpf.Integration;

public class VelloView : Decorator
{
    public static readonly DependencyProperty DeviceOptionsProperty = DependencyProperty.Register(
        nameof(DeviceOptions),
        typeof(VelloGraphicsDeviceOptions),
        typeof(VelloView),
        new PropertyMetadata(VelloGraphicsDeviceOptions.Default, OnDeviceOptionsChanged));

    public static readonly DependencyProperty PreferredBackendProperty = DependencyProperty.Register(
        nameof(PreferredBackend),
        typeof(VelloRenderBackend),
        typeof(VelloView),
        new PropertyMetadata(VelloRenderBackend.Gpu, OnPreferredBackendChanged));

    public static readonly DependencyProperty RenderModeProperty = DependencyProperty.Register(
        nameof(RenderMode),
        typeof(VelloRenderMode),
        typeof(VelloView),
        new PropertyMetadata(VelloRenderMode.OnDemand, OnRenderModeChanged));

    public static readonly DependencyProperty RenderLoopDriverProperty = DependencyProperty.Register(
        nameof(RenderLoopDriver),
        typeof(RenderLoopDriver),
        typeof(VelloView),
        new PropertyMetadata(RenderLoopDriver.CompositionTarget, OnRenderLoopDriverChanged));

    private static readonly DependencyPropertyKey DiagnosticsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Diagnostics),
        typeof(VelloViewDiagnostics),
        typeof(VelloView),
        new PropertyMetadata(null));

    public static readonly DependencyProperty DiagnosticsProperty = DiagnosticsPropertyKey.DependencyProperty;

    private readonly bool _isDesignMode;
    private readonly WpfImage _compositionImage;
    private readonly WpfD3DImage _d3dImage;
    private readonly VelloViewDiagnostics _diagnostics = new();
    private VelloGraphicsDevice? _device;
    private UIElement? _cpuPlaceholder;
    private UIElement? _compositionErrorPlaceholder;
    private readonly Stopwatch _frameStopwatch = new();
    private TimeSpan _lastFrameTimestamp;
    private long _frameId;
    private WindowsGpuContextLease? _contextLease;
    private D3DImageBridge? _bridge;
    private SharedGpuTexture? _currentSharedTexture;
    private WgpuTextureView? _sharedTextureView;
    private bool _forceBackBufferReset = true;
    private bool _bridgeFailed;
    private string? _compositionFailureReason;
    private bool _isRendering;
    private Window? _hostWindow;
    private bool _isControlVisible = true;
    private bool _isWindowMinimized;
    private bool _isApplicationActive = true;
    private bool _isRenderingSuspended;
    private bool _pendingRender;
    private RenderLoopDriver _activeRenderLoopDriver = RenderLoopDriver.None;
    private EventHandler? _compositionRenderingHandler;
    private EventHandler? _threadIdleHandler;

    public VelloView()
    {
        SetValue(DiagnosticsPropertyKey, _diagnostics);
        _isDesignMode = DesignerProperties.GetIsInDesignMode(this);
        if (_isDesignMode)
        {
            Child = CreateDesignModePlaceholder();
            return;
        }

        _d3dImage = new WpfD3DImage();
        _compositionImage = new WpfImage
        {
            Source = _d3dImage,
            Stretch = Stretch.Fill,
            SnapsToDevicePixels = true,
            Focusable = false,
        };

        Child = _compositionImage;
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
        get => (VelloRenderBackend)GetValue(PreferredBackendProperty);
        set => SetValue(PreferredBackendProperty, value);
    }

    public VelloRenderMode RenderMode
    {
        get => (VelloRenderMode)GetValue(RenderModeProperty);
        set => SetValue(RenderModeProperty, value);
    }

    public RenderLoopDriver RenderLoopDriver
    {
        get => (RenderLoopDriver)GetValue(RenderLoopDriverProperty);
        set => SetValue(RenderLoopDriverProperty, value);
    }

    public VelloViewDiagnostics Diagnostics => (VelloViewDiagnostics)GetValue(DiagnosticsProperty);

    public event EventHandler<VelloPaintSurfaceEventArgs>? PaintSurface;

    public event EventHandler<VelloSurfaceRenderEventArgs>? RenderSurface;

    public void RequestRender()
    {
        if (_isDesignMode)
        {
            return;
        }

        if (PreferredBackend != VelloRenderBackend.Gpu)
        {
            InvalidateVisual();
            return;
        }

        if (_isRenderingSuspended)
        {
            _pendingRender = true;
            return;
        }

        if (!UsingCompositionBridge())
        {
            return;
        }

        if (RenderMode == VelloRenderMode.Continuous)
        {
            return;
        }

        RenderFrame();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isDesignMode)
        {
            return;
        }

        ResetBridgeState();
        ResetTiming();
        EnsureChildForBackend();
        AttachVisibilityHandlers();
        UpdateRenderSuspension();

        if (PreferredBackend == VelloRenderBackend.Gpu)
        {
            UpdateRenderLoop();
            RequestRender();
        }
        else
        {
            InvalidateVisual();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_isDesignMode)
        {
            return;
        }

        DetachRenderLoop();
        DetachVisibilityHandlers();
        _pendingRender = false;
        _isRenderingSuspended = false;
        ReleaseBridgeResources();
        ReleaseContextLease();
        ResetDevice();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isDesignMode || !IsLoaded || PreferredBackend != VelloRenderBackend.Gpu)
        {
            return;
        }

        if (!UsingCompositionBridge())
        {
            return;
        }

        RenderFrame();
    }

    private static void OnDeviceOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloView view && e.NewValue is VelloGraphicsDeviceOptions options)
        {
            view.ApplyDeviceOptions(options);
        }
    }

    private void ApplyDeviceOptions(VelloGraphicsDeviceOptions options)
    {
        if (_isDesignMode)
        {
            return;
        }

        var normalized = options ?? VelloGraphicsDeviceOptions.Default;

        if (_device is not null && _device.Options != normalized)
        {
            ResetDevice();
        }

        ResetBridgeState();
        ReleaseContextLease();

        if (IsLoaded && PreferredBackend == VelloRenderBackend.Gpu)
        {
            RequestRender();
        }
    }

    private static void OnPreferredBackendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloView view)
        {
            view.HandlePreferredBackendChanged();
        }
    }

    private void HandlePreferredBackendChanged()
    {
        if (_isDesignMode)
        {
            return;
        }

        EnsureChildForBackend();

        if (PreferredBackend != VelloRenderBackend.Gpu)
        {
            DetachRenderLoop();
            ReleaseBridgeResources();
            ReleaseContextLease();
            ResetDevice();
            InvalidateVisual();
            return;
        }

        ResetBridgeState();
        ResetTiming();
        UpdateRenderLoop();
        RequestRender();
    }

    private static void OnRenderModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloView view)
        {
            view.UpdateRenderLoop();
        }
    }

    private static void OnRenderLoopDriverChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloView view)
        {
            view.UpdateRenderLoop();
        }
    }

    private void UpdateRenderLoop()
    {
        if (_isDesignMode)
        {
            return;
        }

        if (!IsLoaded || PreferredBackend != VelloRenderBackend.Gpu)
        {
            DetachRenderLoop();
            return;
        }

        var shouldRenderContinuously = RenderMode == VelloRenderMode.Continuous;

        if (_isRenderingSuspended)
        {
            DetachRenderLoop();
            return;
        }

        if (!UsingCompositionBridge())
        {
            DetachRenderLoop();
            return;
        }

        if (!shouldRenderContinuously)
        {
            DetachRenderLoop();
            return;
        }

        AttachRenderLoop(RenderLoopDriver);
    }

    private void AttachRenderLoop(RenderLoopDriver driver)
    {
        if (_activeRenderLoopDriver == driver &&
            (_compositionRenderingHandler is not null || _threadIdleHandler is not null))
        {
            return;
        }

        DetachRenderLoop();

        _activeRenderLoopDriver = driver;
        switch (driver)
        {
            case RenderLoopDriver.ComponentDispatcher:
                _threadIdleHandler = OnThreadIdle;
                ComponentDispatcher.ThreadIdle += _threadIdleHandler;
                break;
            case RenderLoopDriver.CompositionTarget:
                _compositionRenderingHandler = OnCompositionRendering;
                CompositionTarget.Rendering += _compositionRenderingHandler;
                break;
            default:
                _activeRenderLoopDriver = RenderLoopDriver.None;
                break;
        }
    }

    private void DetachRenderLoop()
    {
        if (_compositionRenderingHandler is not null)
        {
            CompositionTarget.Rendering -= _compositionRenderingHandler;
            _compositionRenderingHandler = null;
        }

        if (_threadIdleHandler is not null)
        {
            ComponentDispatcher.ThreadIdle -= _threadIdleHandler;
            _threadIdleHandler = null;
        }

        _activeRenderLoopDriver = RenderLoopDriver.None;
    }

    private void AttachVisibilityHandlers()
    {
        DetachVisibilityHandlers();
        IsVisibleChanged += OnIsVisibleChanged;
        _isControlVisible = IsVisible;

        _hostWindow = Window.GetWindow(this);
        if (_hostWindow is not null)
        {
            _isWindowMinimized = _hostWindow.WindowState == WindowState.Minimized;
            _hostWindow.StateChanged += OnHostWindowStateChanged;
        }
        else
        {
            _isWindowMinimized = false;
        }

        if (Application.Current is { } app)
        {
            _isApplicationActive = app.Windows.OfType<Window>().Any(static w => w.IsActive);
            app.Activated += OnApplicationActivated;
            app.Deactivated += OnApplicationDeactivated;
        }
        else
        {
            _isApplicationActive = true;
        }
    }

    private void DetachVisibilityHandlers()
    {
        IsVisibleChanged -= OnIsVisibleChanged;

        if (_hostWindow is { } window)
        {
            window.StateChanged -= OnHostWindowStateChanged;
        }

        _hostWindow = null;

        if (Application.Current is { } app)
        {
            app.Activated -= OnApplicationActivated;
            app.Deactivated -= OnApplicationDeactivated;
        }

        _isWindowMinimized = false;
        _isControlVisible = false;
        _isApplicationActive = true;
    }

    private void UpdateRenderSuspension(bool resumeWithRender = false)
    {
        if (_isDesignMode)
        {
            return;
        }

        var shouldSuspend = !_isControlVisible || _isWindowMinimized || !_isApplicationActive;
        if (shouldSuspend)
        {
            _pendingRender |= resumeWithRender;
        }

        if (_isRenderingSuspended == shouldSuspend)
        {
            if (!shouldSuspend && (resumeWithRender || _pendingRender))
            {
                var requestRender = resumeWithRender || _pendingRender;
                _pendingRender = false;
                if (requestRender)
                {
                    RequestRender();
                }
            }

            return;
        }

        _isRenderingSuspended = shouldSuspend;

        if (shouldSuspend)
        {
            DetachRenderLoop();
            return;
        }

        UpdateRenderLoop();
        var shouldRequestRender = resumeWithRender || _pendingRender || RenderMode != VelloRenderMode.Continuous;
        _pendingRender = false;
        if (shouldRequestRender)
        {
            RequestRender();
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _isControlVisible = IsVisible;
        UpdateRenderSuspension(resumeWithRender: _isControlVisible);
    }

    private void OnHostWindowStateChanged(object? sender, EventArgs e)
    {
        if (sender is Window window && ReferenceEquals(window, _hostWindow))
        {
            _isWindowMinimized = window.WindowState == WindowState.Minimized;
        }
        else if (_hostWindow is not null)
        {
            _isWindowMinimized = _hostWindow.WindowState == WindowState.Minimized;
        }
        else
        {
            _isWindowMinimized = false;
        }

        UpdateRenderSuspension(resumeWithRender: !_isWindowMinimized);
    }

    private void OnApplicationActivated(object? sender, EventArgs e)
    {
        _isApplicationActive = true;
        UpdateRenderSuspension(resumeWithRender: true);
    }

    private void OnApplicationDeactivated(object? sender, EventArgs e)
    {
        _isApplicationActive = false;
        UpdateRenderSuspension();
    }

    private void OnCompositionRendering(object? sender, EventArgs e)
        => RenderFrame();

    private void OnThreadIdle(object? sender, EventArgs e)
        => RenderFrame();

    private void RenderFrame()
    {
        if (_isRenderingSuspended)
        {
            _pendingRender = true;
            return;
        }

        if (!UsingCompositionBridge())
        {
            return;
        }

        if (_isRendering)
        {
            return;
        }

        _isRendering = true;
        try
        {
            RenderWithBridge();
        }
        finally
        {
            _isRendering = false;
        }
    }

    private void RenderWithBridge()
    {
        if (PreferredBackend != VelloRenderBackend.Gpu)
        {
            return;
        }

        var pixelSize = GetPixelSize();
        if (pixelSize.IsEmpty)
        {
            ReleaseTextureView();
            return;
        }

        var options = DeviceOptions ?? VelloGraphicsDeviceOptions.Default;
        var lease = EnsureContextLease(options);
        if (lease is null)
        {
            return;
        }

        bool resourcesReset = false;
        try
        {
            EnsureBridgeInitialized(lease);
            if (_bridge is null)
            {
                return;
            }

            if (!TryEnsureBridgeResources(lease, pixelSize, options, out resourcesReset))
            {
                return;
            }

            if (_sharedTextureView is null)
            {
                return;
            }

            if (!_bridge.BeginDraw(16, out var frameTexture) || frameTexture is null)
            {
                return;
            }

            bool success = false;
            var dirtyRect = new Int32Rect(0, 0, (int)pixelSize.Width, (int)pixelSize.Height);
            var forceReset = _forceBackBufferReset || resourcesReset;
            try
            {
                EnsureDevice(pixelSize.Width, pixelSize.Height);
                if (_device is null)
                {
                    return;
                }

                using var session = _device.BeginSession(pixelSize.Width, pixelSize.Height);
                var (timestamp, delta) = NextFrameTiming();
                var isAnimationFrame = RenderMode == VelloRenderMode.Continuous;
                var paintArgs = new VelloPaintSurfaceEventArgs(session, timestamp, delta, _frameId, isAnimationFrame);
                OnPaintSurface(paintArgs);

                var renderParams = new RenderParams(
                    pixelSize.Width,
                    pixelSize.Height,
                    options.BackgroundColor,
                    options.GetAntialiasingMode(),
                    options.Format);

                var targetFormat = _bridge.TextureFormat;
                var renderParamsToUse = renderParams;
                if (RenderSurface is not null)
                {
                    var surfaceArgs = new VelloSurfaceRenderEventArgs(
                        lease,
                        session.Scene,
                        _sharedTextureView,
                        targetFormat,
                        renderParams,
                        pixelSize,
                        timestamp,
                        delta,
                        _frameId,
                        isAnimationFrame);
                    OnRenderSurface(surfaceArgs);
                    renderParamsToUse = surfaceArgs.RenderParams;
                    if (!surfaceArgs.Handled)
                    {
                        lease.Context.Renderer.RenderSurface(session.Scene, _sharedTextureView, renderParamsToUse, targetFormat);
                    }
                }
                else
                {
                    lease.Context.Renderer.RenderSurface(session.Scene, _sharedTextureView, renderParamsToUse, targetFormat);
                }

                session.Complete();
                lease.Context.RecordPresentation();
                _diagnostics.UpdateFrame(delta, lease.Context.Diagnostics);
                success = true;
                _frameId++;
                _forceBackBufferReset = false;
            }
            catch (DllNotFoundException ex)
            {
                Debug.WriteLine($"[VelloView] Native wgpu binaries missing during render: {ex.Message}");
                throw;
            }
            catch (Exception)
            {
                ResetDevice();
                throw;
            }
            finally
            {
                _bridge.EndDraw(success);
                if (!success)
                {
                    _forceBackBufferReset = true;
                }
            }

            if (success)
            {
                _bridge.Present(dirtyRect, forceReset);
            }
        }
        catch (DllNotFoundException ex)
        {
            DisableCompositionBridge("Native wgpu render path unavailable.", ex);
        }
        catch (EntryPointNotFoundException ex)
        {
            DisableCompositionBridge("Shared texture interop entry point not found in vello_ffi.dll.", ex);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VelloView] Composition render failed: {ex}");
            ReleaseBridgeResources();
            DisableCompositionBridge("Composition render failure", ex);
        }
    }

    private bool TryEnsureBridgeResources(
        WindowsGpuContextLease lease,
        WindowsSurfaceSize pixelSize,
        VelloGraphicsDeviceOptions options,
        out bool resetBackBuffer)
    {
        resetBackBuffer = false;
        if (_bridge is null)
        {
            return false;
        }

        var diagnostics = lease.Context.Diagnostics;
        var label = options.DiagnosticsLabel ?? "vello.wpf.composition";
        try
        {
            if (!_bridge.EnsureResources(
                    lease,
                    pixelSize.Width,
                    pixelSize.Height,
                    useKeyedMutex: true,
                    label,
                    beforeReset: ReleaseTextureView))
            {
                ReleaseTextureView();
                return false;
            }
        }
        catch (EntryPointNotFoundException ex)
        {
            diagnostics.RecordSharedTextureFailure($"Shared texture entry point missing: {ex.Message}");
            throw;
        }

        var shared = _bridge.SharedTexture;
        if (shared is null)
        {
            ReleaseTextureView();
            return false;
        }

        if (!ReferenceEquals(shared, _currentSharedTexture))
        {
            ReleaseTextureView();
            try
            {
                _sharedTextureView = shared.CreateView();
            }
            catch (Exception ex)
            {
                lease.Context.Diagnostics.RecordSharedTextureFailure($"Failed to create texture view: {ex.Message}");
                ReleaseTextureView();
                throw;
            }

            _currentSharedTexture = shared;
            resetBackBuffer = true;
        }
        else if (_sharedTextureView is null)
        {
            try
            {
                _sharedTextureView = shared.CreateView();
            }
            catch (Exception ex)
            {
                lease.Context.Diagnostics.RecordSharedTextureFailure($"Failed to create texture view: {ex.Message}");
                ReleaseTextureView();
                throw;
            }

            resetBackBuffer = true;
        }

        return _sharedTextureView is not null;
    }

    private void EnsureBridgeInitialized(WindowsGpuContextLease lease)
    {
        if (_bridge is null)
        {
            _bridge = new D3DImageBridge(lease.Context.Diagnostics);
            _bridge.AttachImage(_d3dImage);
            _forceBackBufferReset = true;
        }
    }

    private bool UsingCompositionBridge()
        => PreferredBackend == VelloRenderBackend.Gpu && !_bridgeFailed;

    private void DisableCompositionBridge(string reason, Exception? ex = null)
    {
        if (_bridgeFailed)
        {
            return;
        }

        if (ex is not null)
        {
            Debug.WriteLine($"[VelloView] Disabling composition bridge: {reason}. Exception: {ex}");
        }
        else
        {
            Debug.WriteLine($"[VelloView] Disabling composition bridge: {reason}.");
        }

        _diagnostics.ReportError(reason);
        _bridgeFailed = true;
        _compositionFailureReason = reason;
        _compositionErrorPlaceholder = null;
        ReleaseBridgeResources();
        ReleaseContextLease();
        EnsureChildForBackend();
        UpdateRenderLoop();
        ResetDevice();
        InvalidateVisual();
    }

    private void ResetBridgeState()
    {
        ReleaseBridgeResources();
        _bridgeFailed = false;
        _compositionFailureReason = null;
        _compositionErrorPlaceholder = null;
        _forceBackBufferReset = true;
        _diagnostics.ClearError();
    }

    private void ReleaseBridgeResources()
    {
        ReleaseTextureView();
        _bridge?.Dispose();
        _bridge = null;
    }

    private void ReleaseTextureView()
    {
        _sharedTextureView?.Dispose();
        _sharedTextureView = null;
        _currentSharedTexture = null;
        _forceBackBufferReset = true;
    }

    private void EnsureChildForBackend()
    {
        if (_isDesignMode)
        {
            return;
        }

        if (PreferredBackend == VelloRenderBackend.Gpu)
        {
            if (_bridgeFailed)
            {
                EnsureCompositionFailurePlaceholder();
            }
            else
            {
                EnsureCompositionPresenter();
            }
        }
        else
        {
            _cpuPlaceholder ??= CreateCpuPlaceholder();
            if (!ReferenceEquals(Child, _cpuPlaceholder))
            {
                Child = _cpuPlaceholder;
            }
        }
    }

    private void EnsureCompositionPresenter()
    {
        if (!ReferenceEquals(Child, _compositionImage))
        {
            Child = _compositionImage;
        }
    }

    private void EnsureCompositionFailurePlaceholder()
    {
        if (_compositionErrorPlaceholder is null)
        {
            var reason = _compositionFailureReason ?? "See diagnostics log for details.";
            var instructions = reason.IndexOf("entry point", StringComparison.OrdinalIgnoreCase) >= 0
                ? "\nUpdate native binaries (vello_ffi) to enable shared texture interop."
                : string.Empty;
            var message = $"VelloView composition bridge unavailable.\n{reason}{instructions}";
            _compositionErrorPlaceholder = CreatePlaceholder(message);
        }

        var placeholder = _compositionErrorPlaceholder;
        if (!ReferenceEquals(Child, placeholder))
        {
            Child = placeholder;
        }
    }

    private WindowsSurfaceSize GetPixelSize()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return WindowsSurfaceSize.Empty;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var width = (uint)Math.Max(Math.Round(ActualWidth * dpi.DpiScaleX), 0);
        var height = (uint)Math.Max(Math.Round(ActualHeight * dpi.DpiScaleY), 0);
        return new WindowsSurfaceSize(width, height);
    }

    private void EnsureDevice(uint pixelWidth, uint pixelHeight)
    {
        if (_device is null)
        {
            var options = DeviceOptions ?? VelloGraphicsDeviceOptions.Default;
            _device = new VelloGraphicsDevice(Math.Max(pixelWidth, 1u), Math.Max(pixelHeight, 1u), options);
        }
    }

    private WindowsGpuContextLease? EnsureContextLease(VelloGraphicsDeviceOptions options)
    {
        if (_contextLease is not null)
        {
            return _contextLease;
        }

        try
        {
            var lease = WindowsGpuContext.Acquire(options);
            _contextLease = lease;
            _diagnostics.UpdateFromDiagnostics(lease.Context.Diagnostics);
            return lease;
        }
        catch (DllNotFoundException ex)
        {
            Debug.WriteLine($"[VelloView] Native wgpu binaries missing: {ex.Message}");
            DisableCompositionBridge("Native wgpu binaries are unavailable.", ex);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VelloView] Failed to acquire GPU context: {ex}");
            DisableCompositionBridge("Failed to acquire Windows GPU context.", ex);
        }

        return null;
    }

    private void ReleaseContextLease()
    {
        if (_contextLease is { } lease)
        {
            try
            {
                _diagnostics.UpdateFromDiagnostics(lease.Context.Diagnostics);
            }
            catch (ObjectDisposedException)
            {
                // Context already torn down; ignore.
            }

            lease.Dispose();
            _contextLease = null;
        }
    }

    protected virtual void OnPaintSurface(VelloPaintSurfaceEventArgs args)
        => PaintSurface?.Invoke(this, args);

    protected virtual void OnRenderSurface(VelloSurfaceRenderEventArgs args)
        => RenderSurface?.Invoke(this, args);

    private (TimeSpan Timestamp, TimeSpan Delta) NextFrameTiming()
    {
        var timestamp = _frameStopwatch.Elapsed;
        var delta = _frameId == 0 ? TimeSpan.Zero : timestamp - _lastFrameTimestamp;
        if (delta < TimeSpan.Zero)
        {
            delta = TimeSpan.Zero;
        }

        _lastFrameTimestamp = timestamp;
        return (timestamp, delta);
    }

    private void ResetDevice()
    {
        _device?.Dispose();
        _device = null;
    }

    private void ResetTiming()
    {
        _frameStopwatch.Restart();
        _lastFrameTimestamp = TimeSpan.Zero;
        _frameId = 0;
        _diagnostics.ResetFrameTiming();
    }

    private static UIElement CreateDesignModePlaceholder()
        => CreatePlaceholder("VelloView (design-time placeholder)");

    private UIElement CreateCpuPlaceholder()
        => CreatePlaceholder("VelloView CPU backend pending implementation");

    private static UIElement CreatePlaceholder(string message)
    {
        return new Border
        {
            Background = new MediaSolidColorBrush(Color.FromArgb(16, 0, 0, 0)),
            BorderBrush = new MediaSolidColorBrush(Color.FromArgb(64, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = message,
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(6),
            },
        };
    }
}

