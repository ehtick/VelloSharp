using System;
using System.Collections.Concurrent;
using System.Threading;

namespace VelloSharp.ChartRuntime;

public delegate void FrameTickCallback(FrameTick tick);

/// <summary>
/// Coordinates render callbacks against an abstract frame tick source.
/// </summary>
public sealed class RenderScheduler : IDisposable
{
    private readonly ConcurrentQueue<FrameTickCallback> _queue = new();
    private readonly TimeSpan _frameBudget;
    private readonly TimeProvider _timeProvider;
    private readonly long _startTimestamp;
    private readonly object _gate = new();
    private readonly IFrameTickSource _backgroundTickSource;
    private IFrameTickSource? _externalTickSource;
    private bool _ownsExternalTickSource;
    private bool _automaticTicksEnabled = true;
    private int _tickRequested;
    private int _processing;
    private bool _disposed;

    public RenderScheduler(TimeSpan frameBudget, TimeProvider timeProvider)
    {
        _frameBudget = frameBudget;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _startTimestamp = timeProvider.GetTimestamp();
        _backgroundTickSource = new BackgroundFrameTickSource();
        _backgroundTickSource.Tick += OnTick;
    }

    public void Schedule(FrameTickCallback callback)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(callback);

        _queue.Enqueue(callback);
        RequestTickIfNeeded();
    }

    /// <summary>
    /// Attaches an external tick source (e.g., framework animation loop). Passing <c>null</c> reverts to the internal background driver.
    /// </summary>
    public void SetTickSource(IFrameTickSource? tickSource, bool ownsSource = false)
    {
        lock (_gate)
        {
            ThrowIfDisposed();

            if (ReferenceEquals(_externalTickSource, tickSource))
            {
                _ownsExternalTickSource = ownsSource;
                return;
            }

            DetachExternalTickSource();

            if (tickSource is null)
            {
                RequestTickIfNeeded();
                return;
            }

            _externalTickSource = tickSource;
            _ownsExternalTickSource = ownsSource;
            tickSource.Tick += OnTick;
        }

        RequestTickIfNeeded();
    }

    public void SetAutomaticTicksEnabled(bool enabled)
    {
        ThrowIfDisposed();
        _automaticTicksEnabled = enabled;
        if (enabled)
        {
            RequestTickIfNeeded();
        }
    }

    /// <summary>
    /// Executes a single scheduled callback synchronously. Returns <c>false</c> when no work is pending.
    /// </summary>
    public bool TryRunManualTick(TimeSpan? timestampOverride = null)
    {
        ThrowIfDisposed();
        Interlocked.Exchange(ref _tickRequested, 0);

        var executed = ProcessQueue(single: true, timestampOverride);
        if (!executed && !_queue.IsEmpty)
        {
            RequestTickIfNeeded();
        }

        return executed;
    }

    private void RequestTickIfNeeded()
    {
        if (_disposed || _queue.IsEmpty)
        {
            return;
        }

        var source = GetActiveTickSource();
        if (source is null)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _tickRequested, 1, 0) != 0)
        {
            return;
        }

        source.RequestTick();
    }

    private IFrameTickSource? GetActiveTickSource()
    {
        if (_externalTickSource is not null)
        {
            return _externalTickSource;
        }

        if (!_automaticTicksEnabled)
        {
            return null;
        }

        return _backgroundTickSource;
    }

    private void OnTick()
    {
        if (_disposed)
        {
            return;
        }

        Interlocked.Exchange(ref _tickRequested, 0);
        ProcessQueue(single: false, timestampOverride: null);
    }

    private bool ProcessQueue(bool single, TimeSpan? timestampOverride)
    {
        if (Interlocked.Exchange(ref _processing, 1) == 1)
        {
            return false;
        }

        try
        {
            var executed = false;

            do
            {
                if (!_queue.TryDequeue(out var callback))
                {
                    break;
                }

                executed = true;
                var tick = CreateTick(timestampOverride);
                timestampOverride = null;
                callback(tick);
            }
            while (!single);

            if (!_queue.IsEmpty)
            {
                RequestTickIfNeeded();
            }

            return executed;
        }
        finally
        {
            Interlocked.Exchange(ref _processing, 0);
        }
    }

    private FrameTick CreateTick(TimeSpan? timestampOverride)
    {
        var elapsed = timestampOverride ?? _timeProvider.GetElapsedTime(_startTimestamp, _timeProvider.GetTimestamp());
        return new FrameTick(elapsed, _frameBudget);
    }

    private void DetachExternalTickSource()
    {
        if (_externalTickSource is null)
        {
            return;
        }

        _externalTickSource.Tick -= OnTick;
        if (_ownsExternalTickSource)
        {
            _externalTickSource.Dispose();
        }

        _externalTickSource = null;
        _ownsExternalTickSource = false;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RenderScheduler));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        lock (_gate)
        {
            DetachExternalTickSource();
        }

        _backgroundTickSource.Tick -= OnTick;
        _backgroundTickSource.Dispose();
    }
}

public readonly record struct FrameTick(TimeSpan Elapsed, TimeSpan Budget);

internal sealed class BackgroundFrameTickSource : IFrameTickSource
{
    private readonly ManualResetEventSlim _signal = new(false);
    private readonly Thread _thread;
    private volatile bool _running = true;

    public BackgroundFrameTickSource()
    {
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "VelloSharp.RenderScheduler",
        };
        _thread.Start();
    }

    public event Action? Tick;

    public void RequestTick()
    {
        _signal.Set();
    }

    private void Loop()
    {
        while (_running)
        {
            _signal.Wait();
            _signal.Reset();

            if (!_running)
            {
                break;
            }

            Tick?.Invoke();
        }
    }

    public void Dispose()
    {
        _running = false;
        _signal.Set();

        if (Thread.CurrentThread != _thread)
        {
            _thread.Join();
        }

        _signal.Dispose();
    }
}
