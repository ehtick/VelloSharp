using System;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using VelloSharp.Windows.Shared.Dispatching;

namespace VelloSharp.Windows.WinUI.Dispatching;

internal sealed class WinUIDispatcher : IVelloWindowsDispatcher
{
    private readonly DispatcherQueue _queue;

    private WinUIDispatcher(DispatcherQueue queue)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
    }

    public bool HasThreadAccess => _queue.HasThreadAccess;

    public bool TryEnqueue(Action callback)
    {
        if (callback is null)
        {
            throw new ArgumentNullException(nameof(callback));
        }

        return _queue.TryEnqueue(() => callback());
    }

    public IVelloWindowsDispatcherTimer CreateTimer()
        => new WinUIDispatcherTimer(_queue.CreateTimer());

    public static IVelloWindowsDispatcher? Wrap(DispatcherQueue? queue)
        => queue is null ? null : new WinUIDispatcher(queue);
}

internal sealed class WinUIDispatcherTimer : IVelloWindowsDispatcherTimer
{
    private readonly DispatcherQueueTimer _timer;
    private EventHandler? _tick;

    public WinUIDispatcherTimer(DispatcherQueueTimer timer)
    {
        _timer = timer ?? throw new ArgumentNullException(nameof(timer));
        _timer.Tick += OnTick;
    }

    public TimeSpan Interval
    {
        get => _timer.Interval;
        set => _timer.Interval = value;
    }

    public bool IsRepeating
    {
        get => _timer.IsRepeating;
        set => _timer.IsRepeating = value;
    }

    public event EventHandler? Tick
    {
        add => _tick += value;
        remove => _tick -= value;
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _tick = null;
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
        => _tick?.Invoke(this, EventArgs.Empty);
}

internal sealed class WinUICompositionTargetAdapter : IVelloCompositionTarget
{
    public static WinUICompositionTargetAdapter Instance { get; } = new();

    private WinUICompositionTargetAdapter()
    {
    }

    public void AddRenderingHandler(EventHandler<object> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        CompositionTarget.Rendering += handler;
    }

    public void RemoveRenderingHandler(EventHandler<object> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        CompositionTarget.Rendering -= handler;
    }
}

internal sealed class WinUIDispatcherProvider : IVelloWindowsDispatcherProvider
{
    public static WinUIDispatcherProvider Instance { get; } = new();

    public IVelloWindowsDispatcher? GetForCurrentThread()
        => WinUIDispatcher.Wrap(DispatcherQueue.GetForCurrentThread());
}

internal sealed class WinUICompositionTargetProvider : IVelloCompositionTargetProvider
{
    public static WinUICompositionTargetProvider Instance { get; } = new();

    public IVelloCompositionTarget? GetForCurrentThread()
        => WinUICompositionTargetAdapter.Instance;
}

internal static class WinUIThreadingBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        VelloWindowsDispatcher.TrySetProvider(WinUIDispatcherProvider.Instance);
        VelloCompositionTarget.TrySetProvider(WinUICompositionTargetProvider.Instance);
    }
}
