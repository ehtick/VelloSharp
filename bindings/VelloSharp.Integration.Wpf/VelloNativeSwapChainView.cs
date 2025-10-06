using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;
using VelloSharp;
using VelloSharp.Windows;
using VelloPaintSurfaceEventArgs = VelloSharp.WinForms.Integration.VelloPaintSurfaceEventArgs;

namespace VelloSharp.Wpf.Integration;

public class VelloNativeSwapChainView : Decorator
{
    public static readonly DependencyProperty DeviceOptionsProperty = DependencyProperty.Register(
        nameof(DeviceOptions),
        typeof(VelloGraphicsDeviceOptions),
        typeof(VelloNativeSwapChainView),
        new PropertyMetadata(VelloGraphicsDeviceOptions.Default, OnDeviceOptionsChanged));

    public static readonly DependencyProperty PreferredBackendProperty = DependencyProperty.Register(
        nameof(PreferredBackend),
        typeof(VelloRenderBackend),
        typeof(VelloNativeSwapChainView),
        new PropertyMetadata(VelloRenderBackend.Gpu, OnPreferredBackendChanged));

    public static readonly DependencyProperty RenderModeProperty = DependencyProperty.Register(
        nameof(RenderMode),
        typeof(VelloRenderMode),
        typeof(VelloNativeSwapChainView),
        new PropertyMetadata(VelloRenderMode.OnDemand, OnRenderModeChanged));

    public static readonly DependencyProperty RenderLoopDriverProperty = DependencyProperty.Register(
        nameof(RenderLoopDriver),
        typeof(RenderLoopDriver),
        typeof(VelloNativeSwapChainView),
        new PropertyMetadata(RenderLoopDriver.CompositionTarget, OnRenderLoopDriverChanged));

    private readonly bool _isDesignMode;
    private readonly Stopwatch _frameStopwatch = new();
    private VelloNativeSwapChainHost? _host;
    private VelloGraphicsDevice? _device;
    private UIElement? _cpuPlaceholder;
    private TimeSpan _lastFrameTimestamp;
    private long _frameId;

    public VelloNativeSwapChainView()
    {
        _isDesignMode = DesignerProperties.GetIsInDesignMode(this);
        if (_isDesignMode)
        {
            Child = CreateDesignModePlaceholder();
            return;
        }

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

    public event EventHandler<VelloPaintSurfaceEventArgs>? PaintSurface;

    public event EventHandler<VelloSwapChainRenderEventArgs>? RenderSurface;

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

        _host?.RequestRender();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isDesignMode)
        {
            return;
        }

        ResetTiming();
        EnsureChildForBackend();

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
        _host?.ReleaseGpuResources();
        ResetDevice();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isDesignMode || !IsLoaded)
        {
            return;
        }

        if (PreferredBackend == VelloRenderBackend.Gpu)
        {
            RequestRender();
        }
        else
        {
            InvalidateVisual();
        }
    }

    private static void OnDeviceOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloNativeSwapChainView view && e.NewValue is VelloGraphicsDeviceOptions options)
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

        if (_host is not null)
        {
            _host.DeviceOptions = normalized;
            _host.ReleaseGpuResources("Device options changed");
        }

        if (IsLoaded && PreferredBackend == VelloRenderBackend.Gpu)
        {
            RequestRender();
        }
    }

    private static void OnPreferredBackendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloNativeSwapChainView view)
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

        if (!IsLoaded)
        {
            return;
        }

        if (PreferredBackend == VelloRenderBackend.Gpu)
        {
            ResetTiming();
            UpdateRenderLoop();
            RequestRender();
        }
        else
        {
            DetachRenderLoop();
            _host?.ReleaseGpuResources("Preferred backend set to CPU");
            ResetDevice();
            InvalidateVisual();
        }
    }

    private static void OnRenderModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloNativeSwapChainView view)
        {
            view.UpdateRenderLoop();
        }
    }

    private static void OnRenderLoopDriverChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloNativeSwapChainView view)
        {
            view.UpdateRenderLoop();
        }
    }

    private void UpdateRenderLoop()
    {
        if (_isDesignMode || _host is null || !IsLoaded)
        {
            return;
        }

        if (PreferredBackend != VelloRenderBackend.Gpu)
        {
            _host.ConfigureRenderLoop(RenderLoopDriver.None, enable: false);
            return;
        }

        var enable = RenderMode == VelloRenderMode.Continuous;
        var driver = enable ? RenderLoopDriver : RenderLoopDriver.None;
        _host.ConfigureRenderLoop(driver, enable);
    }

    private void DetachRenderLoop()
    {
        _host?.ConfigureRenderLoop(RenderLoopDriver.None, enable: false);
    }

    private void EnsureChildForBackend()
    {
        if (_isDesignMode)
        {
            return;
        }

        if (PreferredBackend == VelloRenderBackend.Gpu)
        {
            EnsureHostPresenter();
        }
        else
        {
            EnsureCpuPlaceholder();
        }
    }

    private void EnsureHostPresenter()
    {
        var options = DeviceOptions ?? VelloGraphicsDeviceOptions.Default;

        if (_host is null)
        {
            _host = new VelloNativeSwapChainHost
            {
                DeviceOptions = options,
            };
            _host.SwapChainReady += OnSwapChainReady;
        }
        else
        {
            _host.DeviceOptions = options;
        }

        if (!ReferenceEquals(Child, _host))
        {
            Child = _host;
        }
    }

    private void EnsureCpuPlaceholder()
    {
        _cpuPlaceholder ??= CreateCpuPlaceholder();
        if (!ReferenceEquals(Child, _cpuPlaceholder))
        {
            Child = _cpuPlaceholder;
        }
    }

    private void OnSwapChainReady(object? sender, SwapChainLeaseEventArgs e)
    {
        if (PreferredBackend != VelloRenderBackend.Gpu || _isDesignMode)
        {
            return;
        }

        if (e.PixelSize.IsEmpty)
        {
            return;
        }

        var pixelWidth = Math.Max(1u, e.PixelSize.Width);
        var pixelHeight = Math.Max(1u, e.PixelSize.Height);

        EnsureDevice(pixelWidth, pixelHeight);
        if (_device is null)
        {
            return;
        }

        using var session = _device.BeginSession(pixelWidth, pixelHeight);
        var (timestamp, delta) = NextFrameTiming();
        var paintArgs = new VelloPaintSurfaceEventArgs(session, timestamp, delta, _frameId, RenderMode == VelloRenderMode.Continuous);
        OnPaintSurface(paintArgs);

        if (TryRenderToSwapChain(e, session, pixelWidth, pixelHeight))
        {
            var swapChainArgs = new VelloSwapChainRenderEventArgs(e.Lease, e.Surface, e.PixelSize, timestamp, delta, _frameId);
            RenderSurface?.Invoke(this, swapChainArgs);
        }

        _frameId++;
    }

    protected virtual void OnPaintSurface(VelloPaintSurfaceEventArgs args)
        => PaintSurface?.Invoke(this, args);

    private void EnsureDevice(uint pixelWidth, uint pixelHeight)
    {
        if (_device is null)
        {
            var options = DeviceOptions ?? VelloGraphicsDeviceOptions.Default;
            _device = new VelloGraphicsDevice(Math.Max(pixelWidth, 1u), Math.Max(pixelHeight, 1u), options);
        }
    }

    private bool TryRenderToSwapChain(
        SwapChainLeaseEventArgs e,
        VelloGraphicsSession session,
        uint pixelWidth,
        uint pixelHeight)
    {
        try
        {
            var surface = e.Surface;
            surface.Configure(pixelWidth, pixelHeight);

            using var surfaceTexture = surface.AcquireNextTexture();
            using var textureView = surfaceTexture.CreateView();

            var options = DeviceOptions ?? VelloGraphicsDeviceOptions.Default;
            var renderParams = new RenderParams(
                pixelWidth,
                pixelHeight,
                options.BackgroundColor,
                options.GetAntialiasingMode(),
                options.Format);

            e.Lease.Context.Renderer.RenderSurface(session.Scene, textureView, renderParams, surface.Format);
            surfaceTexture.Present();
            e.Lease.Context.RecordPresentation();
            session.Complete();
            return true;
        }
        catch (DllNotFoundException ex)
        {
            Debug.WriteLine($"[VelloNativeSwapChainView] Native wgpu binaries missing: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VelloNativeSwapChainView] GPU render failed: {ex}");
        }

        _host?.ReleaseGpuResources("GPU render failure");
        ResetDevice();
        session.Complete();
        return false;
    }

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
    }

    private static UIElement CreateDesignModePlaceholder()
        => CreatePlaceholder("VelloNativeSwapChainView (design-time placeholder)");

    private UIElement CreateCpuPlaceholder()
        => CreatePlaceholder("VelloNativeSwapChainView CPU backend pending implementation");

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
