using System;
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VelloSharp.Avalonia.Vello.Rendering;

namespace VelloSharp.Avalonia.Controls;

/// <summary>
/// Extends <see cref="VelloCanvasControl"/> with a render loop that drives time-based animations.
/// Supports both dispatcher-timer based updates and composition-hosted visuals for smoother playback.
/// </summary>
public class VelloAnimatedCanvasControl : VelloCanvasControl
{
    /// <summary>
    /// Defines the <see cref="IsPlaying"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<VelloAnimatedCanvasControl, bool>(nameof(IsPlaying), true);

    /// <summary>
    /// Defines the <see cref="FrameRate"/> property.
    /// </summary>
    public static readonly StyledProperty<double> FrameRateProperty =
        AvaloniaProperty.Register<VelloAnimatedCanvasControl, double>(nameof(FrameRate), 60d);

    /// <summary>
    /// Defines the <see cref="PlaybackRate"/> property.
    /// </summary>
    public static readonly StyledProperty<double> PlaybackRateProperty =
        AvaloniaProperty.Register<VelloAnimatedCanvasControl, double>(nameof(PlaybackRate), 1d);

    /// <summary>
    /// Defines the <see cref="TotalTime"/> property.
    /// </summary>
    public static readonly DirectProperty<VelloAnimatedCanvasControl, TimeSpan> TotalTimeProperty =
        AvaloniaProperty.RegisterDirect<VelloAnimatedCanvasControl, TimeSpan>(
            nameof(TotalTime),
            o => o.TotalTime);

    private DispatcherTimer? _timer;
    private Stopwatch? _stopwatch;
    private TimeSpan _offset;
    private TimeSpan _lastFrameTotal;
    private TimeSpan _totalTime;
    private bool _isAttached;

    private CompositionCustomVisual? _compositionVisual;
    private VelloAnimatedCompositionHandler? _compositionHandler;
    private bool _usingComposition;
    private bool _compositionStarted;

    /// <summary>
    /// Gets or sets a value indicating whether the animation loop is running.
    /// </summary>
    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    /// <summary>
    /// Gets or sets the target frame rate in frames per second for the dispatcher-timer fallback path.
    /// </summary>
    public double FrameRate
    {
        get => GetValue(FrameRateProperty);
        set => SetValue(FrameRateProperty, value);
    }

    /// <summary>
    /// Gets or sets the playback rate applied when composition drives the animation.
    /// Values &lt;= 0 fallback to 1.0.
    /// </summary>
    public double PlaybackRate
    {
        get => GetValue(PlaybackRateProperty);
        set => SetValue(PlaybackRateProperty, value);
    }

    /// <summary>
    /// Gets the accumulated playback time.
    /// </summary>
    public TimeSpan TotalTime
    {
        get => _totalTime;
        private set => SetAndRaise(TotalTimeProperty, ref _totalTime, value);
    }

    protected override bool ShouldRenderVelloScene => !_usingComposition;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;

        if (TryAttachCompositionVisual())
        {
            _usingComposition = true;
            StopTimer();
        }
        else
        {
            _usingComposition = false;
            UpdateTimerState();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _isAttached = false;
        DetachCompositionVisual();
        StopTimer();
    }

    /// <summary>
    /// Resets the accumulated playback time to zero. When the control is playing it restarts immediately.
    /// </summary>
    public void Reset()
    {
        if (_usingComposition)
        {
            var zero = TimeSpan.Zero;
            UpdateTotalTime(zero, updateTimerBaseline: false);
            SendCompositionMessage(new VelloAnimationVisualPayload(
                VelloAnimationVisualCommand.Seek,
                Position: zero));
            UpdateCompositionPlaybackState();
            SendCompositionMessage(new VelloAnimationVisualPayload(VelloAnimationVisualCommand.Redraw));
            return;
        }

        var wasRunning = _stopwatch is not null;
        StopTimer();

        _offset = TimeSpan.Zero;
        _lastFrameTotal = TimeSpan.Zero;
        UpdateTotalTime(TimeSpan.Zero, updateTimerBaseline: true);

        if ((IsPlaying || wasRunning) && _isAttached && IsEffectivelyVisible)
        {
            StartTimer();
        }
        else
        {
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Starts playback if it is currently stopped or paused.
    /// </summary>
    public void Play() => IsPlaying = true;

    /// <summary>
    /// Pauses playback without resetting the accumulated time.
    /// </summary>
    public void Pause() => IsPlaying = false;

    /// <summary>
    /// Alias for <see cref="Play"/>.
    /// </summary>
    public void Resume() => Play();

    /// <summary>
    /// Stops playback and resets the accumulated time to zero.
    /// </summary>
    public void Stop()
    {
        IsPlaying = false;
        Reset();
    }

    /// <summary>
    /// Seeks to the specified playback position.
    /// </summary>
    /// <param name="position">The desired playback position. Negative values are clamped to zero.</param>
    public void Seek(TimeSpan position)
    {
        if (position < TimeSpan.Zero)
        {
            position = TimeSpan.Zero;
        }

        if (_usingComposition)
        {
            UpdateTotalTime(position, updateTimerBaseline: false);
            SendCompositionMessage(new VelloAnimationVisualPayload(
                VelloAnimationVisualCommand.Seek,
                Position: position));
            UpdateCompositionPlaybackState();
            SendCompositionMessage(new VelloAnimationVisualPayload(VelloAnimationVisualCommand.Redraw));
            return;
        }

        SeekWithTimer(position);
    }

    /// <summary>
    /// Requests a redraw of the current frame without advancing time.
    /// </summary>
    public void Redraw()
    {
        if (_usingComposition)
        {
            SendCompositionMessage(new VelloAnimationVisualPayload(VelloAnimationVisualCommand.Redraw));
        }
        else
        {
            InvalidateVisual();
        }
    }

    protected override (TimeSpan Total, TimeSpan Delta) GetFrameTimes()
    {
        if (_usingComposition)
        {
            return (TotalTime, TimeSpan.Zero);
        }

        var total = ComputeTotalTime();
        var delta = total - _lastFrameTotal;
        _lastFrameTotal = total;
        return (total, delta);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsPlayingProperty || change.Property == IsVisibleProperty)
        {
            if (_usingComposition)
            {
                UpdateCompositionPlaybackState();
            }
            else
            {
                UpdateTimerState();
            }
        }
        else if (change.Property == FrameRateProperty)
        {
            if (!_usingComposition && _timer is not null)
            {
                _timer.Interval = GetFrameInterval();
            }
        }
        else if (change.Property == PlaybackRateProperty)
        {
            if (_usingComposition)
            {
                SendCompositionMessage(new VelloAnimationVisualPayload(
                    VelloAnimationVisualCommand.Update,
                    PlaybackRate: GetEffectivePlaybackRate()));
            }
        }
    }

    internal void OnCompositionFrame(TimeSpan total, TimeSpan delta, bool isRunning)
    {
        Dispatcher.UIThread.Post(() => UpdateTotalTime(total, updateTimerBaseline: false));
    }

    internal void OnCompositionLeaseReady(IVelloApiLease lease, Rect bounds, TimeSpan total, TimeSpan delta)
    {
        HandleDraw(lease, bounds, total, delta);
        Dispatcher.UIThread.Post(() => UpdateAvailability(true, null));
    }

    internal void OnCompositionLeaseUnavailable(string reason)
    {
        Dispatcher.UIThread.Post(() => UpdateAvailability(false, reason));
    }

    internal void OnCompositionLeaseException(Exception ex)
    {
        Dispatcher.UIThread.Post(() => UpdateAvailability(false, ex.Message));
    }

    private bool TryAttachCompositionVisual()
    {
        if (_compositionVisual is not null)
        {
            return true;
        }

        var elementVisual = ElementComposition.GetElementVisual(this);
        var compositor = elementVisual?.Compositor;
        if (compositor is null)
        {
            return false;
        }

        var handler = new VelloAnimatedCompositionHandler(this);
        _compositionHandler = handler;
        var visual = compositor.CreateCustomVisual(handler);
        _compositionVisual = visual;
        ElementComposition.SetElementChildVisual(this, visual);

        EnsureCompositionVisualSize();
        LayoutUpdated += OnLayoutUpdated;

        SendCompositionMessage(new VelloAnimationVisualPayload(
            VelloAnimationVisualCommand.Update,
            PlaybackRate: GetEffectivePlaybackRate()));
        SendCompositionMessage(new VelloAnimationVisualPayload(
            VelloAnimationVisualCommand.Seek,
            Position: TotalTime));

        UpdateCompositionPlaybackState();

        return true;
    }

    private void DetachCompositionVisual()
    {
        if (_compositionVisual is null)
        {
            return;
        }

        LayoutUpdated -= OnLayoutUpdated;

        SendCompositionMessage(new VelloAnimationVisualPayload(VelloAnimationVisualCommand.Dispose));
        ElementComposition.SetElementChildVisual(this, null);
        _compositionVisual = null;
        _compositionHandler = null;
        _compositionStarted = false;
        _usingComposition = false;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        EnsureCompositionVisualSize();
        SendCompositionMessage(new VelloAnimationVisualPayload(
            VelloAnimationVisualCommand.Update,
            PlaybackRate: GetEffectivePlaybackRate()));
    }

    private void EnsureCompositionVisualSize()
    {
        if (_compositionVisual is null)
        {
            return;
        }

        var size = Bounds.Size;
        _compositionVisual.Size = new Vector2(
            (float)Math.Max(0, size.Width),
            (float)Math.Max(0, size.Height));
    }

    private void UpdateCompositionPlaybackState()
    {
        if (!_usingComposition || _compositionVisual is null || _compositionHandler is null)
        {
            return;
        }

        var rate = GetEffectivePlaybackRate();

        if (IsPlaying && IsEffectivelyVisible)
        {
            var command = _compositionStarted ? VelloAnimationVisualCommand.Resume : VelloAnimationVisualCommand.Start;
            SendCompositionMessage(new VelloAnimationVisualPayload(
                command,
                PlaybackRate: rate,
                Position: TotalTime));
            _compositionStarted = true;
        }
        else
        {
            SendCompositionMessage(new VelloAnimationVisualPayload(VelloAnimationVisualCommand.Pause));
        }

        SendCompositionMessage(new VelloAnimationVisualPayload(
            VelloAnimationVisualCommand.Update,
            PlaybackRate: rate));
    }

    private void SendCompositionMessage(VelloAnimationVisualPayload payload)
    {
        _compositionVisual?.SendHandlerMessage(payload);
    }

    private void UpdateTotalTime(TimeSpan total, bool updateTimerBaseline)
    {
        if (TotalTime != total)
        {
            TotalTime = total;
        }

        if (updateTimerBaseline)
        {
            _lastFrameTotal = total;
        }
    }

    private double GetEffectivePlaybackRate()
    {
        var rate = PlaybackRate;
        if (double.IsNaN(rate) || double.IsInfinity(rate) || rate <= 0)
        {
            rate = 1d;
        }

        return rate;
    }

    private void SeekWithTimer(TimeSpan position)
    {
        var wasRunning = _stopwatch is not null;
        StopTimer();

        _offset = position;
        UpdateTotalTime(position, updateTimerBaseline: true);

        if ((IsPlaying || wasRunning) && _isAttached && IsEffectivelyVisible)
        {
            StartTimer();
        }
        else
        {
            InvalidateVisual();
        }
    }

    private TimeSpan ComputeTotalTime()
    {
        var elapsed = _stopwatch?.Elapsed ?? TimeSpan.Zero;
        var total = _offset + elapsed;
        UpdateTotalTime(total, updateTimerBaseline: false);
        return total;
    }

    private void UpdateTimerState()
    {
        if (_usingComposition)
        {
            return;
        }

        if (IsPlaying && _isAttached && IsEffectivelyVisible)
        {
            StartTimer();
        }
        else
        {
            StopTimer();
        }
    }

    private void StartTimer()
    {
        var interval = GetFrameInterval();
        if (_timer is null)
        {
            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = interval,
            };
            _timer.Tick += OnTick;
        }
        else
        {
            _timer.Interval = interval;
        }

        if (_stopwatch is null)
        {
            _stopwatch = Stopwatch.StartNew();
        }
        else if (!_stopwatch.IsRunning)
        {
            _stopwatch.Start();
        }

        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private void StopTimer()
    {
        if (_timer is not null)
        {
            _timer.Stop();
        }

        if (_stopwatch is not null)
        {
            var total = _offset + _stopwatch.Elapsed;
            UpdateTotalTime(total, updateTimerBaseline: true);
            _offset = total;
            _stopwatch.Stop();
            _stopwatch = null;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        ComputeTotalTime();
        InvalidateVisual();
    }

    private TimeSpan GetFrameInterval()
    {
        var fps = FrameRate;
        if (fps <= 0 || double.IsNaN(fps) || double.IsInfinity(fps))
        {
            fps = 60d;
        }

        return TimeSpan.FromSeconds(1d / fps);
    }
}
