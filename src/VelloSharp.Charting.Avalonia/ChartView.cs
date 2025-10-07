using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using VelloSharp;
using VelloSharp.ChartData;
using VelloSharp.ChartEngine;
using VelloSharp.ChartRuntime;
using VelloSharp.Integration.Avalonia;
using VelloSharp.ChartEngine.Annotations;
using VelloSharp.Charting.Legend;
using VelloSharp.Charting.Rendering;
using VelloSharp.Charting.Styling;

namespace VelloSharp.Charting.Avalonia;

/// <summary>
/// Avalonia control that renders real-time charts using the Vello engine.
/// </summary>
public sealed class ChartView : ContentControl
{
    private ChartEngine.ChartEngine _engine;
    private bool _ownsEngine;
    private bool _renderLoopActive;
    private bool _isDetached = true;
    private readonly VelloSurfaceView _surfaceView;
    private IFrameTickSource? _tickSource;
    private readonly ChartOverlayRenderer _overlayRenderer = new();
    private ChartTheme _theme = ChartTheme.Default;
    private LegendDefinition? _legend;
    private IReadOnlyList<ChartAnnotation>? _annotations;
    private ChartComposition? _composition;

    public ChartView()
    {
        _surfaceView = new VelloSurfaceView
        {
            IsLoopEnabled = true,
        };
        Content = _surfaceView;

        _engine = new ChartEngine.ChartEngine(new ChartEngineOptions());
        _ownsEngine = true;
        EnsureTickSource();
    }

    /// <summary>
    /// Gets or sets the theme used for axis, grid, legend, and annotations.
    /// </summary>
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
            if (!_isDetached)
            {
                _surfaceView.RequestRender();
            }
        }
    }

    /// <summary>
    /// Gets or sets an optional legend definition rendered on top of the chart.
    /// When null, a default legend is built from the frame metadata.
    /// </summary>
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
            if (!_isDetached)
            {
                _surfaceView.RequestRender();
            }
        }
    }

    /// <summary>
    /// Gets or sets the composition blueprint describing pane layout.
    /// </summary>
    public ChartComposition? Composition
    {
        get => _composition;
        set
        {
            if (ReferenceEquals(_composition, value))
            {
                return;
            }

            _composition = value;
            try
            {
                _engine.ConfigureComposition(_composition);
            }
            catch (ObjectDisposedException)
            {
            }

            if (!_isDetached)
            {
                _surfaceView.RequestRender();
            }
        }
    }

    /// <summary>
    /// Gets or sets annotations rendered over the plot area.
    /// </summary>
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
            if (!_isDetached)
            {
                _surfaceView.RequestRender();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the view disposes the attached <see cref="ChartEngine.ChartEngine"/>.
    /// </summary>
    public bool OwnsEngine
    {
        get => _ownsEngine;
        set => _ownsEngine = value;
    }

    /// <summary>
    /// Gets or sets the chart engine that produces frames for this view.
    /// </summary>
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
            try
            {
                _engine.ConfigureComposition(_composition);
            }
            catch (ObjectDisposedException)
            {
            }
            BeginSchedulerLoop();
            _surfaceView.RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the renderer options applied to the GPU surface view.
    /// </summary>
    public RendererOptions RendererOptions
    {
        get => _surfaceView.RendererOptions;
        set => _surfaceView.RendererOptions = value;
    }

    /// <summary>
    /// Gets or sets render parameters (dimension, background) forwarded to the underlying surface view.
    /// </summary>
    public RenderParams RenderParameters
    {
        get => _surfaceView.RenderParameters;
        set => _surfaceView.RenderParameters = value;
    }

    /// <summary>
    /// Enables or disables the internal animation loop on the surface view. When disabled
    /// the chart relies on the engine scheduler to request frames.
    /// </summary>
    public bool IsLoopEnabled
    {
        get => _surfaceView.IsLoopEnabled;
        set
        {
            _surfaceView.IsLoopEnabled = value;
            BeginSchedulerLoop();
            if (value)
            {
                _surfaceView.RequestRender();
            }
        }
    }

    /// <summary>
    /// Enqueues sample data for rendering.
    /// </summary>
    public void PublishSamples(ReadOnlySpan<ChartSamplePoint> samples)
    {
        if (_isDetached)
        {
            return;
        }

        _engine.PumpData(samples);
        _surfaceView.RequestRender();
    }

    /// <summary>
    /// Triggers a new render pass.
    /// </summary>
    public void RequestRender()
    {
        _surfaceView.RequestRender();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isDetached = false;
        _surfaceView.RenderFrame -= OnRenderFrame;
        _surfaceView.RenderFrame += OnRenderFrame;
        EnsureTickSource();
        BeginSchedulerLoop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _renderLoopActive = false;
        _isDetached = true;
        _surfaceView.RenderFrame -= OnRenderFrame;
        DetachTickSource();

        if (_ownsEngine)
        {
            _engine.Dispose();
        }
    }

    private void BeginSchedulerLoop()
    {
        if (_renderLoopActive || _isDetached)
        {
            return;
        }

        EnsureTickSource();
        _renderLoopActive = true;
        _engine.ScheduleRender(OnScheduledTick);
    }

    private void OnScheduledTick(FrameTick tick)
    {
        if (!_renderLoopActive || _isDetached)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!_renderLoopActive || _isDetached)
            {
                return;
            }

            _surfaceView.RequestRender();
            _engine.ScheduleRender(OnScheduledTick);
        }, DispatcherPriority.Render);
    }

    private void OnRenderFrame(VelloRenderFrameContext context)
    {
        if (_isDetached)
        {
            return;
        }

        try
        {
            _engine.Render(context.Scene, context.Width, context.Height);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        RenderOverlay(context.Scene, context.Width, context.Height, context.RenderScaling);
    }

    private void EnsureTickSource()
    {
        if (_engine is null)
        {
            return;
        }

        if (_tickSource is null)
        {
            _tickSource = new AvaloniaAnimationTickSource(_surfaceView);
        }

        try
        {
            _engine.ConfigureTickSource(_tickSource);
        }
        catch (ObjectDisposedException)
        {
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

        var hasCompositionAnnotations = _composition?.AnnotationLayers.Count > 0;
        var hasInlineAnnotations = _annotations is { Count: > 0 };
        if (!_engine.Options.ShowAxes && _legend is null && !hasInlineAnnotations && !hasCompositionAnnotations)
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
            _composition,
            _annotations,
            _engine.Options.ShowAxes);
    }
}
