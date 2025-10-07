using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using VelloSharp.ChartRuntime;

namespace VelloSharp.Charting.Avalonia;

/// <summary>
/// Bridges Avalonia's animation frame callbacks into the shared render scheduler.
/// </summary>
internal sealed class AvaloniaAnimationTickSource : IFrameTickSource
{
    private readonly Control _control;
    private readonly DispatcherPriority _priority = DispatcherPriority.Render;
    private int _state;
    private bool _disposed;

    public AvaloniaAnimationTickSource(Control control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
    }

    public event Action? Tick;

    public void RequestTick()
    {
        if (_disposed)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(ScheduleAnimationFrame, _priority);
    }

    private void ScheduleAnimationFrame()
    {
        if (_disposed)
        {
            Interlocked.Exchange(ref _state, 0);
            return;
        }

        if (Interlocked.CompareExchange(ref _state, 2, 1) != 1)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(_control);
        if (topLevel is null)
        {
            Interlocked.Exchange(ref _state, 0);
            return;
        }

        topLevel.RequestAnimationFrame(OnAnimationFrame);
    }

    private void OnAnimationFrame(TimeSpan _)
    {
        if (_disposed)
        {
            Interlocked.Exchange(ref _state, 0);
            return;
        }

        Interlocked.Exchange(ref _state, 0);
        Tick?.Invoke();
    }

    public void Dispose()
    {
        _disposed = true;
        Interlocked.Exchange(ref _state, 0);
    }
}
