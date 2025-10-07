using System;
using System.Windows.Media;
using System.Windows.Threading;
using VelloSharp.ChartRuntime;

namespace VelloSharp.ChartRuntime.Windows.Wpf;

/// <summary>
/// Drives frame ticks using WPF's CompositionTarget.Rendering callbacks.
/// </summary>
public sealed class WpfCompositionTargetTickSource : IFrameTickSource
{
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherPriority _priority;
    private EventHandler? _renderHandler;
    private bool _pending;
    private bool _disposed;

    public WpfCompositionTargetTickSource(Dispatcher dispatcher, DispatcherPriority priority = DispatcherPriority.Render)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
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
        _dispatcher.BeginInvoke(_priority, new Action(EnsureSubscribed));
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

        if (_renderHandler is null)
        {
            return;
        }

        void Detach()
        {
            CompositionTarget.Rendering -= _renderHandler;
            _renderHandler = null;
        }

        if (_dispatcher.CheckAccess())
        {
            Detach();
        }
        else
        {
            _ = _dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(Detach));
        }
    }
}
