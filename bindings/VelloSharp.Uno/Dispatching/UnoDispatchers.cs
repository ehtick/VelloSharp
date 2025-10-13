#if HAS_UNO

using System;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using VelloSharp.Windows.Shared.Dispatching;

namespace VelloSharp.Uno.Dispatching;

internal sealed class UnoDispatcher : IVelloWindowsDispatcher
{
    private readonly DispatcherQueue _queue;

    private UnoDispatcher(DispatcherQueue queue)
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
        => new UnoDispatcherTimer(_queue.CreateTimer());

    public static IVelloWindowsDispatcher? Wrap(DispatcherQueue? queue)
        => queue is null ? null : new UnoDispatcher(queue);
}

internal sealed class UnoDispatcherTimer : IVelloWindowsDispatcherTimer
{
    private readonly DispatcherQueueTimer _timer;
    private EventHandler? _tick;

    public UnoDispatcherTimer(DispatcherQueueTimer timer)
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

internal sealed class UnoCompositionTargetAdapter : IVelloCompositionTarget
{
    public static UnoCompositionTargetAdapter Instance { get; } = new();

    private UnoCompositionTargetAdapter()
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

internal sealed class UnoDispatcherProvider : IVelloWindowsDispatcherProvider
{
    public static UnoDispatcherProvider Instance { get; } = new();

    public IVelloWindowsDispatcher? GetForCurrentThread()
        => UnoDispatcher.Wrap(DispatcherQueue.GetForCurrentThread());
}

internal sealed class UnoCompositionTargetProvider : IVelloCompositionTargetProvider
{
    public static UnoCompositionTargetProvider Instance { get; } = new();

    public IVelloCompositionTarget? GetForCurrentThread()
        => UnoCompositionTargetAdapter.Instance;
}

internal static class UnoThreadingBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        VelloWindowsDispatcher.TrySetProvider(UnoDispatcherProvider.Instance);
        VelloCompositionTarget.TrySetProvider(UnoCompositionTargetProvider.Instance);
    }
}

#endif
