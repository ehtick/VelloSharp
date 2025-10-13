#if WINDOWS_UWP

using System;
using System.Runtime.CompilerServices;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using VelloSharp.Windows.Shared.Dispatching;

namespace VelloSharp.Uwp.Dispatching;

internal sealed class UwpDispatcher : IVelloWindowsDispatcher
{
    private readonly DispatcherQueue? _queue;
    private readonly CoreDispatcher? _coreDispatcher;

    private UwpDispatcher(DispatcherQueue? queue, CoreDispatcher? coreDispatcher)
    {
        if (queue is null && coreDispatcher is null)
        {
            throw new ArgumentException("A dispatcher instance is required.", nameof(queue));
        }

        _queue = queue;
        _coreDispatcher = coreDispatcher;
    }

    public bool HasThreadAccess
        => (_queue?.HasThreadAccess).GetValueOrDefault(_coreDispatcher?.HasThreadAccess ?? false);

    public bool TryEnqueue(Action callback)
    {
        if (callback is null)
        {
            throw new ArgumentNullException(nameof(callback));
        }

        if (_queue is not null)
        {
            return _queue.TryEnqueue(() => callback());
        }

        if (_coreDispatcher is not null)
        {
            _ = _coreDispatcher.RunAsync(CoreDispatcherPriority.Normal, () => callback());
            return true;
        }

        return false;
    }

    public IVelloWindowsDispatcherTimer CreateTimer()
    {
        if (_queue is not null)
        {
            return new UwpDispatcherQueueTimerWrapper(_queue.CreateTimer());
        }

        if (_coreDispatcher is not null)
        {
            return new UwpDispatcherTimerWrapper(_coreDispatcher);
        }

        throw new InvalidOperationException("A dispatcher timer cannot be created without an active dispatcher.");
    }

    public static IVelloWindowsDispatcher? Wrap(DispatcherQueue? queue, CoreDispatcher? coreDispatcher)
        => queue is null && coreDispatcher is null ? null : new UwpDispatcher(queue, coreDispatcher);
}

internal sealed class UwpDispatcherQueueTimerWrapper : IVelloWindowsDispatcherTimer
{
    private readonly DispatcherQueueTimer _timer;
    private EventHandler? _tick;

    public UwpDispatcherQueueTimerWrapper(DispatcherQueueTimer timer)
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

internal sealed class UwpDispatcherTimerWrapper : IVelloWindowsDispatcherTimer
{
    private readonly DispatcherTimer _timer;
    private EventHandler? _tick;
    private bool _isRepeating = true;

    public UwpDispatcherTimerWrapper(CoreDispatcher dispatcher)
    {
        if (dispatcher is null)
        {
            throw new ArgumentNullException(nameof(dispatcher));
        }

        _timer = new DispatcherTimer();
        _timer.Tick += OnTick;
    }

    public TimeSpan Interval
    {
        get => _timer.Interval;
        set => _timer.Interval = value;
    }

    public bool IsRepeating
    {
        get => _isRepeating;
        set => _isRepeating = value;
    }

    public event EventHandler? Tick
    {
        add => _tick += value;
        remove => _tick -= value;
    }

    public void Start()
    {
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    public void Stop()
    {
        if (_timer.IsEnabled)
        {
            _timer.Stop();
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _tick = null;
    }

    private void OnTick(object sender, object args)
    {
        _tick?.Invoke(this, EventArgs.Empty);
        if (!_isRepeating)
        {
            _timer.Stop();
        }
    }
}

internal sealed class UwpCompositionTargetAdapter : IVelloCompositionTarget
{
    public static UwpCompositionTargetAdapter Instance { get; } = new();

    private UwpCompositionTargetAdapter()
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

internal sealed class UwpDispatcherProvider : IVelloWindowsDispatcherProvider
{
    public static UwpDispatcherProvider Instance { get; } = new();

    public IVelloWindowsDispatcher? GetForCurrentThread()
    {
        var queue = DispatcherQueue.GetForCurrentThread();
        CoreDispatcher? coreDispatcher = null;

        try
        {
            coreDispatcher = CoreWindow.GetForCurrentThread()?.Dispatcher;
        }
        catch
        {
            coreDispatcher = null;
        }

        return UwpDispatcher.Wrap(queue, coreDispatcher);
    }
}

internal sealed class UwpCompositionTargetProvider : IVelloCompositionTargetProvider
{
    public static UwpCompositionTargetProvider Instance { get; } = new();

    public IVelloCompositionTarget? GetForCurrentThread()
        => UwpCompositionTargetAdapter.Instance;
}

internal static class UwpThreadingBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        VelloWindowsDispatcher.TrySetProvider(UwpDispatcherProvider.Instance, overwrite: true);
        VelloCompositionTarget.TrySetProvider(UwpCompositionTargetProvider.Instance, overwrite: true);
    }
}

#else

using System;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using VelloSharp.Windows.Shared.Dispatching;

namespace VelloSharp.Uwp.Dispatching;

internal sealed class UwpDispatcher : IVelloWindowsDispatcher
{
    private readonly DispatcherQueue _queue;

    private UwpDispatcher(DispatcherQueue queue)
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
        => new UwpDispatcherTimerWrapper(_queue.CreateTimer());

    public static IVelloWindowsDispatcher? Wrap(DispatcherQueue? queue)
        => queue is null ? null : new UwpDispatcher(queue);
}

internal sealed class UwpDispatcherTimerWrapper : IVelloWindowsDispatcherTimer
{
    private readonly DispatcherQueueTimer _timer;
    private EventHandler? _tick;

    public UwpDispatcherTimerWrapper(DispatcherQueueTimer timer)
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

internal sealed class UwpCompositionTargetAdapter : IVelloCompositionTarget
{
    public static UwpCompositionTargetAdapter Instance { get; } = new();

    private UwpCompositionTargetAdapter()
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

internal sealed class UwpDispatcherProvider : IVelloWindowsDispatcherProvider
{
    public static UwpDispatcherProvider Instance { get; } = new();

    public IVelloWindowsDispatcher? GetForCurrentThread()
        => UwpDispatcher.Wrap(DispatcherQueue.GetForCurrentThread());
}

internal sealed class UwpCompositionTargetProvider : IVelloCompositionTargetProvider
{
    public static UwpCompositionTargetProvider Instance { get; } = new();

    public IVelloCompositionTarget? GetForCurrentThread()
        => UwpCompositionTargetAdapter.Instance;
}

internal static class UwpThreadingBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        VelloWindowsDispatcher.TrySetProvider(UwpDispatcherProvider.Instance, overwrite: true);
        VelloCompositionTarget.TrySetProvider(UwpCompositionTargetProvider.Instance, overwrite: true);
    }
}

#endif
