using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Avalonia.Rendering;

namespace VelloSharp.Avalonia.Browser;

[SupportedOSPlatform("browser")]
internal sealed partial class VelloBrowserRenderTimer : IRenderTimer, IDisposable
{
    private readonly bool _runsInBackground;
    private readonly Action<double> _frameCallback;
    private Action<TimeSpan>? _tick;
    private bool _running;
    private int _requestId = -1;
    private bool _disposed;
    private bool _isSuspendedByVisibility;
    private bool _lifecycleSubscribed;

    public VelloBrowserRenderTimer(bool runsInBackground)
    {
        _runsInBackground = runsInBackground;
        _frameCallback = OnAnimationFrame;
        VelloBrowserDispatcherLifecycle.EnsureInitialized();
        _isSuspendedByVisibility = !VelloBrowserDispatcherLifecycle.IsVisible;
        VelloBrowserDispatcherLifecycle.VisibilityChanged += OnVisibilityChanged;
        _lifecycleSubscribed = true;
    }

    public bool RunsInBackground => _runsInBackground;

    public event Action<TimeSpan>? Tick
    {
        add
        {
            ThrowIfDisposed();
            _tick += value;
            EnsureRunning();
        }
        remove
        {
            ThrowIfDisposed();
            _tick -= value;
            if (_tick is null)
            {
                Stop();
            }
        }
    }

    public void StartOnThisThread()
    {
        ThrowIfDisposed();
        EnsureRunning();
    }

    private void EnsureRunning()
    {
        if (_running || _tick is null || _isSuspendedByVisibility)
        {
            return;
        }

        _running = true;
        ScheduleNextFrame();
    }

    private void ScheduleNextFrame()
    {
        if (!_running || _tick is null)
        {
            return;
        }

        _requestId = RequestAnimationFrame(_frameCallback);
    }

    private void OnAnimationFrame(double timestamp)
    {
        _requestId = -1;

        if (!_running)
        {
            return;
        }

        var handlers = _tick;
        if (handlers is not null)
        {
            handlers(TimeSpan.FromMilliseconds(timestamp));
        }

        if (_running && _tick is not null)
        {
            ScheduleNextFrame();
        }
        else
        {
            _running = false;
        }
    }

    private void Stop()
    {
        if (!_running)
        {
            return;
        }

        _running = false;

        var requestId = _requestId;
        if (requestId >= 0)
        {
            CancelAnimationFrame(requestId);
            _requestId = -1;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VelloBrowserRenderTimer));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _tick = null;
        if (_lifecycleSubscribed)
        {
            VelloBrowserDispatcherLifecycle.VisibilityChanged -= OnVisibilityChanged;
            _lifecycleSubscribed = false;
        }
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnVisibilityChanged(bool isVisible)
    {
        if (_disposed)
        {
            return;
        }

        if (!isVisible)
        {
            _isSuspendedByVisibility = true;
            Stop();
        }
        else
        {
            _isSuspendedByVisibility = false;
            if (_tick is not null)
            {
                EnsureRunning();
            }
        }
    }

    [JSImport("requestAnimationFrame", "globalThis")]
    private static partial int RequestAnimationFrame([JSMarshalAs<JSType.Function<JSType.Number>>] Action<double> callback);

    [JSImport("cancelAnimationFrame", "globalThis")]
    private static partial void CancelAnimationFrame(int handle);
}
