using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VelloSharp;
using VelloSharp.ChartData;
using VelloSharp.ChartEngine;
using VelloSharp.ChartRuntime;
using VelloSharp.ChartRuntime.Windows.Wpf;
using VelloSharp.Wpf.Integration;
using VelloSharp.Windows;
using VelloSharp.Charting.Annotations;
using VelloSharp.Charting.Legend;
using VelloSharp.Charting.Rendering;
using VelloSharp.Charting.Styling;

namespace VelloSharp.Charting.Wpf;

/// <summary>
/// WPF control hosting the Vello chart engine.
/// </summary>
public sealed class ChartView : ContentControl, IDisposable
{
    private ChartEngine.ChartEngine _engine;
    private bool _ownsEngine;
    private readonly VelloView _surfaceView;
    private IFrameTickSource? _tickSource;
    private bool _isLoaded;
    private bool _renderLoopActive;
    private bool _isDisposed;
    private readonly ChartOverlayRenderer _overlayRenderer = new();
    private ChartTheme _theme = ChartTheme.Default;
    private LegendDefinition? _legend;
    private IReadOnlyList<ChartAnnotation>? _annotations;

    public ChartView()
    {
        _surfaceView = new VelloView
        {
            RenderMode = VelloRenderMode.OnDemand,
            RenderLoopDriver = RenderLoopDriver.None,
        };

        Content = _surfaceView;

        _engine = new ChartEngine.ChartEngine(new ChartEngineOptions());
        _ownsEngine = true;

        _surfaceView.RenderSurface += OnRenderSurface;
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

            if (_ownsEngine)
            {
                _engine.Dispose();
            }

            _engine = value;
            _ownsEngine = false;
            EnsureTickSource();
            BeginSchedulerLoop();
            _surfaceView.RequestRender();
        }
    }

    public VelloGraphicsDeviceOptions DeviceOptions
    {
        get => _surfaceView.DeviceOptions;
        set => _surfaceView.DeviceOptions = value;
    }

    public VelloRenderMode RenderMode
    {
        get => _surfaceView.RenderMode;
        set => _surfaceView.RenderMode = value;
    }

    public VelloRenderBackend PreferredBackend
    {
        get => _surfaceView.PreferredBackend;
        set => _surfaceView.PreferredBackend = value;
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
            if (_isLoaded)
            {
                RequestRender();
            }
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
            if (_isLoaded)
            {
                RequestRender();
            }
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
            if (_isLoaded)
            {
                RequestRender();
            }
        }
    }

    public void PublishSamples(ReadOnlySpan<ChartSamplePoint> samples)
    {
        if (!_isLoaded || samples.IsEmpty)
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
        => _surfaceView.RequestRender();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        EnsureTickSource();
        BeginSchedulerLoop();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _renderLoopActive = false;
        _isLoaded = false;
        DetachTickSource();

        if (_ownsEngine)
        {
            _engine.Dispose();
        }
    }

    private void BeginSchedulerLoop()
    {
        if (_renderLoopActive || !_isLoaded)
        {
            return;
        }

        EnsureTickSource();
        _renderLoopActive = true;
        _engine.ScheduleRender(OnScheduledTick);
    }

    private void OnScheduledTick(FrameTick tick)
    {
        if (!_renderLoopActive || !_isLoaded)
        {
            return;
        }

        void Request()
        {
            if (_renderLoopActive && _isLoaded)
            {
                _surfaceView.RequestRender();
            }
        }

        if (Dispatcher.CheckAccess())
        {
            Request();
        }
        else
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(Request));
        }

        try
        {
            _engine.ScheduleRender(OnScheduledTick);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void OnRenderSurface(object? sender, VelloSurfaceRenderEventArgs e)
    {
        if (!_isLoaded || e.PixelSize.Width == 0 || e.PixelSize.Height == 0)
        {
            return;
        }

        try
        {
            _engine.Render(e.Scene, e.PixelSize.Width, e.PixelSize.Height);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        RenderOverlay(e.Scene, e.PixelSize.Width, e.PixelSize.Height, 1.0);

        if (!e.Handled)
        {
            e.RenderScene(e.Scene);
        }
    }

    private void EnsureTickSource()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_tickSource is null)
        {
            _tickSource = new WpfCompositionTargetTickSource(Dispatcher);
        }

        _engine.ConfigureTickSource(_tickSource);
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

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _renderLoopActive = false;

        DetachTickSource();
        _surfaceView.RenderSurface -= OnRenderSurface;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;

        if (_ownsEngine)
        {
            _engine.Dispose();
        }

        _isLoaded = false;
        _overlayRenderer.Dispose();
    }

    private void RenderOverlay(Scene scene, double width, double height, double devicePixelRatio)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (!_engine.Options.ShowAxes && (_legend is null) && (_annotations is null || _annotations.Count == 0))
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
}
