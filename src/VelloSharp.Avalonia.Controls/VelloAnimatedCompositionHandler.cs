using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using VelloSharp.Avalonia.Vello.Rendering;

namespace VelloSharp.Avalonia.Controls;

internal enum VelloAnimationVisualCommand
{
    Start,
    Pause,
    Resume,
    Seek,
    Stop,
    Update,
    Redraw,
    Dispose,
}

internal readonly record struct VelloAnimationVisualPayload(
    VelloAnimationVisualCommand Command,
    double? PlaybackRate = null,
    TimeSpan? Position = null);

internal sealed class VelloAnimatedCompositionHandler : CompositionCustomVisualHandler
{
    private readonly VelloAnimatedCanvasControl _owner;
    private bool _running;
    private TimeSpan _total;
    private TimeSpan? _lastServerTime;
    private double _playbackRate = 1d;

    public VelloAnimatedCompositionHandler(VelloAnimatedCanvasControl owner)
    {
        _owner = owner;
    }

    public override void OnMessage(object message)
    {
        if (message is not VelloAnimationVisualPayload payload)
        {
            return;
        }

        switch (payload.Command)
        {
            case VelloAnimationVisualCommand.Start:
                _running = true;
                _lastServerTime = null;
                _playbackRate = NormalizeRate(payload.PlaybackRate);
                if (payload.Position is { } startPos)
                {
                    _total = startPos;
                }
                RegisterForNextAnimationFrameUpdate();
                break;

            case VelloAnimationVisualCommand.Resume:
                _running = true;
                _lastServerTime = null;
                _playbackRate = NormalizeRate(payload.PlaybackRate);
                RegisterForNextAnimationFrameUpdate();
                break;

            case VelloAnimationVisualCommand.Pause:
                _running = false;
                Invalidate();
                break;

            case VelloAnimationVisualCommand.Stop:
                _running = false;
                _lastServerTime = null;
                _total = payload.Position ?? TimeSpan.Zero;
                Invalidate();
                break;

            case VelloAnimationVisualCommand.Seek:
                if (payload.Position is { } seekPos)
                {
                    _total = seekPos;
                }
                _lastServerTime = null;
                Invalidate();
                RegisterForNextAnimationFrameUpdate();
                break;

            case VelloAnimationVisualCommand.Update:
                _playbackRate = NormalizeRate(payload.PlaybackRate);
                Invalidate();
                if (_running)
                {
                    RegisterForNextAnimationFrameUpdate();
                }
                break;

            case VelloAnimationVisualCommand.Redraw:
                Invalidate();
                break;

            case VelloAnimationVisualCommand.Dispose:
                _running = false;
                break;
        }
    }

    public override void OnAnimationFrameUpdate()
    {
        if (_running)
        {
            Invalidate();
            RegisterForNextAnimationFrameUpdate();
        }
    }

    public override void OnRender(ImmediateDrawingContext context)
    {
        var now = CompositionNow;
        TimeSpan delta = TimeSpan.Zero;

        if (_running)
        {
            if (_lastServerTime is TimeSpan last)
            {
                var elapsed = now - last;
                delta = ScaleElapsed(elapsed);
                _total += delta;
            }

            _lastServerTime = now;
        }
        else
        {
            _lastServerTime = null;
        }

        _owner.OnCompositionFrame(_total, delta, _running);

        if (!VelloCanvasControl.TryGetLeaseFeature(context, out var feature) || feature is null)
        {
            return;
        }

        try
        {
            using var lease = feature.Lease();
            if (lease is null)
            {
                return;
            }

            var bounds = new Rect(GetRenderBounds().Size);
            _owner.HandleDraw(lease, bounds, _total, delta);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private double NormalizeRate(double? rate)
    {
        var value = rate ?? _playbackRate;
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            value = 1d;
        }

        return value;
    }

    private TimeSpan ScaleElapsed(TimeSpan elapsed)
    {
        if (Math.Abs(_playbackRate - 1d) < double.Epsilon)
        {
            return elapsed;
        }

        return TimeSpan.FromTicks((long)(elapsed.Ticks * _playbackRate));
    }
}
