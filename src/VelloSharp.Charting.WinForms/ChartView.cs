using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using VelloSharp;
using VelloSharp.ChartData;
using VelloSharp.ChartEngine;
using VelloSharp.ChartRuntime;
using VelloSharp.ChartRuntime.Windows.WinForms;
using VelloSharp.Charting.Annotations;
using VelloSharp.Charting.Legend;
using VelloSharp.Charting.Rendering;
using VelloSharp.Charting.Styling;
using VelloSharp.WinForms.Integration;
using VelloSharp.Windows;

namespace VelloSharp.Charting.WinForms;

/// <summary>
/// WinForms control that hosts the chart engine on top of <see cref="VelloRenderControl"/>.
/// </summary>
public sealed class ChartView : UserControl
{
    private readonly VelloRenderControl _renderControl;
    private ChartEngine.ChartEngine _engine;
    private bool _ownsEngine = true;
    private IFrameTickSource? _tickSource;
    private bool _renderLoopActive;
    private bool _isDisposed;
    private readonly ChartOverlayRenderer _overlayRenderer = new();
    private ChartTheme _theme = ChartTheme.Default;
    private LegendDefinition? _legend;
    private IReadOnlyList<ChartAnnotation>? _annotations;

    public ChartView()
    {
        if (!IsInDesignMode())
        {
            SetStyle(ControlStyles.Opaque, true);
        }

        _renderControl = new VelloRenderControl
        {
            Dock = DockStyle.Fill,
            RenderMode = VelloRenderMode.OnDemand,
        };
        _renderControl.RenderSurface += OnRenderSurface;

        Controls.Add(_renderControl);

        _engine = new ChartEngine.ChartEngine(new ChartEngineOptions());
    }

    [DefaultValue(true)]
    [Category("Behavior")]
    public bool OwnsEngine
    {
        get => _ownsEngine;
        set => _ownsEngine = value;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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

            if (IsHandleCreated && !IsInDesignMode())
            {
                EnsureTickSource();
                BeginSchedulerLoop();
                RequestRender();
            }
        }
    }

    [Category("Behavior")]
    public VelloGraphicsDeviceOptions DeviceOptions
    {
        get => _renderControl.DeviceOptions;
        set => _renderControl.DeviceOptions = value;
    }

    [Category("Behavior")]
    public VelloRenderMode RenderMode
    {
        get => _renderControl.RenderMode;
        set
        {
            _renderControl.RenderMode = value;
            if (value == VelloRenderMode.Continuous && IsHandleCreated && !_renderLoopActive)
            {
                BeginSchedulerLoop();
            }
        }
    }

    [Category("Behavior")]
    public VelloRenderBackend PreferredBackend
    {
        get => _renderControl.PreferredBackend;
        set => _renderControl.PreferredBackend = value;
    }

    [Category("Appearance")]
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
            if (!_isDisposed && !IsInDesignMode())
            {
                RequestRender();
            }
        }
    }

    [Browsable(false)]
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
            if (!_isDisposed && !IsInDesignMode())
            {
                RequestRender();
            }
        }
    }

    [Browsable(false)]
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
            if (!_isDisposed && !IsInDesignMode())
            {
                RequestRender();
            }
        }
    }

    public void PublishSamples(ReadOnlySpan<ChartSamplePoint> samples)
    {
        if (_isDisposed || IsInDesignMode() || samples.IsEmpty)
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
        if (_isDisposed || IsInDesignMode())
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(RequestRender));
            return;
        }

        if (!_renderControl.IsDisposed)
        {
            _renderControl.Invalidate();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (_isDisposed || DesignMode)
        {
            return;
        }

        EnsureTickSource();
        BeginSchedulerLoop();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _renderLoopActive = false;
        DetachTickSource();
        base.OnHandleDestroyed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            base.Dispose(disposing);
            return;
        }

        if (disposing)
        {
            _isDisposed = true;
            _renderLoopActive = false;

            DetachTickSource();
            _renderControl.RenderSurface -= OnRenderSurface;
            _overlayRenderer.Dispose();

            if (_ownsEngine && !_engine.IsDisposed())
            {
                _engine.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private void BeginSchedulerLoop()
    {
        if (_renderLoopActive || _isDisposed || IsInDesignMode() || !IsHandleCreated)
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
            if (_renderLoopActive && !_isDisposed && IsHandleCreated)
            {
                _renderControl.Invalidate();
            }
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(Request));
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

    private void OnRenderSurface(object? sender, VelloSurfaceRenderEventArgs e)
    {
        if (_isDisposed || IsInDesignMode())
        {
            return;
        }

        if (e.PixelSize.Width == 0 || e.PixelSize.Height == 0)
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
        if (_tickSource is not null || _isDisposed || IsInDesignMode())
        {
            return;
        }

        _tickSource = new WinFormsTickSource(this);

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

    private bool IsInDesignMode()
        => LicenseManager.UsageMode == LicenseUsageMode.Designtime
           || Site?.DesignMode == true
           || DesignMode;

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
