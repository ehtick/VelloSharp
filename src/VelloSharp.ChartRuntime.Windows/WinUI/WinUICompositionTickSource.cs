#if HAS_WINUI
using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using VelloSharp.ChartRuntime;
using Windows.Foundation;

namespace VelloSharp.ChartRuntime.Windows.WinUI;

/// <summary>
/// Uses WinUI's CompositionTarget.Rendering event to align scheduler ticks with the framework animation loop.
/// </summary>
public sealed class WinUICompositionTickSource : IFrameTickSource
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueuePriority _priority;
    private EventHandler<object>? _renderHandler;
    private bool _pending;
    private bool _disposed;

    public WinUICompositionTickSource(FrameworkElement element, DispatcherQueuePriority priority = DispatcherQueuePriority.Low)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        _dispatcherQueue = element.DispatcherQueue ?? throw new InvalidOperationException("DispatcherQueue is unavailable for the provided element.");
        _priority = priority;
    }

    public WinUICompositionTickSource(DispatcherQueue dispatcherQueue, DispatcherQueuePriority priority = DispatcherQueuePriority.Low)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        _priority = priority;
    }

    public event Action? Tick;

    public void RequestTick()
    {
        if (_disposed || _pending)
        {
            return;
        }

        _pending = true;
        _dispatcherQueue.TryEnqueue(_priority, EnsureSubscribed);
    }

    private void EnsureSubscribed()
    {
        if (_disposed)
        {
            _pending = false;
            return;
        }

        _renderHandler ??= (_, _) =>
        {
            CompositionTarget.Rendering -= _renderHandler;
            _pending = false;
            Tick?.Invoke();
        };

        CompositionTarget.Rendering -= _renderHandler;
        CompositionTarget.Rendering += _renderHandler;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pending = false;

        if (_renderHandler is null)
        {
            return;
        }

        var handler = _renderHandler;
        _renderHandler = null;

        if (_dispatcherQueue.HasThreadAccess)
        {
            CompositionTarget.Rendering -= handler;
        }
        else
        {
            _dispatcherQueue.TryEnqueue(() => CompositionTarget.Rendering -= handler);
        }
    }
}
#endif
