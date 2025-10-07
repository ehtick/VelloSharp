using System;
using System.Collections.Generic;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VelloSharp;
using VelloSharp.ChartData;
using VelloSharp.ChartEngine;
using VelloSharp.ChartRuntime;
using VelloSharp.ChartRuntime.Windows.WinUI;
using VelloSharp.Charting.Annotations;
using VelloSharp.Charting.Legend;
using VelloSharp.Charting.Rendering;
using VelloSharp.Charting.Styling;
using VelloSharp.Uno.Controls;
using VelloSharp.Windows;
using Windows.ApplicationModel;
using VelloPaintSurfaceEventArgs = VelloSharp.WinForms.Integration.VelloPaintSurfaceEventArgs;

namespace VelloSharp.Uno.WinAppSdkSample;

public sealed class ChartView : UserControl, IDisposable
{
    private readonly VelloSwapChainPanel _panel;
    private ChartEngine.ChartEngine _engine;
    private bool _ownsEngine = true;
    private IFrameTickSource? _tickSource;
    private bool _renderLoopActive;
    private bool _isDisposed;
    private bool _isLoaded;
    private readonly ChartOverlayRenderer _overlayRenderer = new();
    private ChartTheme _theme = ChartTheme.Dark;
    private LegendDefinition? _legend;
    private IReadOnlyList<ChartAnnotation>? _annotations;

    public ChartView()
    {
        _panel = new VelloSwapChainPanel
        {
            RenderMode = VelloRenderMode.OnDemand,
            RenderLoopDriver = RenderLoopDriver.None,
        };

        Content = _panel;

        _panel.PaintSurface += OnPaintSurface;
        _panel.RenderSurface += OnRenderSurface;

        if (!IsDesignMode())
        {
            _engine = new ChartEngine.ChartEngine(new ChartEngineOptions());
            _engine.UpdatePalette(ToChartColors(_theme.Palette.Series));
        }
        else
        {
            _engine = null!;
        }

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public bool OwnsEngine
    {
        get => _ownsEngine;
        set => _ownsEngine = value;
    }

    public ChartEngine.ChartEngine ChartEngine
    {
        get => _engine;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_engine, value))
            {
                return;
            }

            DetachTickSource();

            if (_ownsEngine && !_engine.IsDisposed())
            {
                _engine.Dispose();
            }

            _engine = value;
            _ownsEngine = false;

            try
            {
                _engine.UpdatePalette(ToChartColors(_theme.Palette.Series));
            }
            catch (ObjectDisposedException)
            {
            }

            if (_isLoaded && !_isDisposed)
            {
                EnsureTickSource();
                BeginSchedulerLoop();
                _panel.RequestRender();
            }
        }
    }

    public VelloGraphicsDeviceOptions DeviceOptions
    {
        get => _panel.DeviceOptions;
        set => _panel.DeviceOptions = value;
    }

    public VelloRenderBackend PreferredBackend
    {
        get => _panel.PreferredBackend;
        set => _panel.PreferredBackend = value;
    }

    public VelloRenderMode RenderMode
    {
        get => _panel.RenderMode;
        set
        {
            _panel.RenderMode = value;
            if (value == VelloRenderMode.Continuous && _isLoaded)
            {
                BeginSchedulerLoop();
            }
        }
    }

    public ChartTheme Theme
    {
        get => _theme;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_theme, value))
            {
                return;
            }

            _theme = value;
            try
            {
                _engine.UpdatePalette(ToChartColors(_theme.Palette.Series));
            }
            catch (ObjectDisposedException)
            {
            }

            RequestRender();
        }
    }

    public LegendDefinition? Legend
    {
        get => _legend;
        set
        {
            if (ReferenceEquals(_legend, value))
            {
                return;
            }

            _legend = value;
            RequestRender();
        }
    }

    public IReadOnlyList<ChartAnnotation>? Annotations
    {
        get => _annotations;
        set
        {
            if (ReferenceEquals(_annotations, value))
            {
                return;
            }

            _annotations = value;
            RequestRender();
        }
    }

    public void PublishSamples(ReadOnlySpan<ChartSamplePoint> samples)
    {
        if (_isDisposed || !_isLoaded || samples.IsEmpty)
        {
            return;
        }

        try
        {
            _engine.PumpData(samples);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        RequestRender();
    }

    public void RequestRender()
    {
        if (_isDisposed || !_isLoaded)
        {
            return;
        }

        if (DispatcherQueue is { HasThreadAccess: false } queue)
        {
            queue.TryEnqueue(() => _panel.RequestRender());
        }
        else
        {
            _panel.RequestRender();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isDisposed || IsDesignMode())
        {
            return;
        }

        _isLoaded = true;
        EnsureTickSource();
        BeginSchedulerLoop();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        _renderLoopActive = false;
        DetachTickSource();
    }

    private void BeginSchedulerLoop()
    {
        if (_renderLoopActive || _isDisposed || !_isLoaded)
        {
            return;
        }

        _renderLoopActive = true;

        try
        {
            _engine.ScheduleRender(OnScheduledTick);
        }
        catch (ObjectDisposedException)
        {
            _renderLoopActive = false;
        }
    }

    private void OnScheduledTick(FrameTick tick)
    {
        if (_isDisposed || !_renderLoopActive)
        {
            return;
        }

        void Request()
        {
            if (_renderLoopActive && !_isDisposed)
            {
                _panel.RequestRender();
            }
        }

        if (DispatcherQueue is { HasThreadAccess: true })
        {
            Request();
        }
        else if (DispatcherQueue is { } queue)
        {
            queue.TryEnqueue(DispatcherQueuePriority.Low, Request);
        }
        else
        {
            Request();
        }

        try
        {
            _engine.ScheduleRender(OnScheduledTick);
        }
        catch (ObjectDisposedException)
        {
            _renderLoopActive = false;
        }
    }

    private void OnPaintSurface(object? sender, VelloPaintSurfaceEventArgs e)
    {
        if (_isDisposed || !_isLoaded)
        {
            return;
        }

        try
        {
            _engine.Render(e.Session.Scene, e.Session.Width, e.Session.Height);
            RenderOverlay(e.Session.Scene, e.Session.Width, e.Session.Height, 1.0);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void OnRenderSurface(object? sender, VelloSwapChainRenderEventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        // Hook for diagnostics if required.
    }

    private void EnsureTickSource()
    {
        if (_tickSource is not null || _isDisposed)
        {
            return;
        }

        _tickSource = new WinUICompositionTickSource(this);

        try
        {
            _engine.ConfigureTickSource(_tickSource);
        }
        catch (ObjectDisposedException)
        {
            _tickSource.Dispose();
            _tickSource = null;
        }
    }

    private void DetachTickSource()
    {
        if (_tickSource is null)
        {
            return;
        }

        try
        {
            _engine.ConfigureTickSource(null);
        }
        catch (ObjectDisposedException)
        {
        }

        _tickSource.Dispose();
        _tickSource = null;
    }

    private void RenderOverlay(Scene scene, double width, double height, double devicePixelRatio)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (!_engine.Options.ShowAxes && _legend is null && (_annotations is null || _annotations.Count == 0))
        {
            return;
        }

        ChartFrameMetadata metadata;
        try
        {
            metadata = _engine.GetFrameMetadata();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        _overlayRenderer.Render(
            scene,
            metadata,
            width,
            height,
            devicePixelRatio,
            _theme,
            _legend,
            _annotations,
            _engine.Options.ShowAxes);
    }

    private static bool IsDesignMode()
        => DesignMode.DesignModeEnabled || DesignMode.DesignMode2Enabled;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _renderLoopActive = false;
        _isLoaded = false;

        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        _panel.PaintSurface -= OnPaintSurface;
        _panel.RenderSurface -= OnRenderSurface;

        DetachTickSource();
        _overlayRenderer.Dispose();

        if (_ownsEngine && !_engine.IsDisposed())
        {
            _engine.Dispose();
        }
    }

    private static ChartColor[] ToChartColors(IReadOnlyList<RgbaColor> series)
    {
        if (series.Count == 0)
        {
            return Array.Empty<ChartColor>();
        }

        var colors = new ChartColor[series.Count];
        for (var i = 0; i < series.Count; i++)
        {
            var color = series[i];
            colors[i] = new ChartColor(color.R, color.G, color.B, color.A);
        }

        return colors;
    }
}

file static class ChartEngineExtensions
{
    public static bool IsDisposed(this ChartEngine.ChartEngine engine)
    {
        try
        {
            _ = engine.Options;
            return false;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }
}
