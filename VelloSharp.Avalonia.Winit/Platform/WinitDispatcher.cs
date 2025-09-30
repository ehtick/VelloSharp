using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Avalonia.Input.Raw;
using Avalonia.Platform;
using Avalonia.Threading;
using VelloSharp;

namespace Avalonia.Winit;

internal sealed class WinitDispatcher : IControlledDispatcherImpl, IWinitEventHandler, IDisposable
{
    private readonly WinitEventLoop _eventLoop = new();
    private readonly Thread? _loopThread;
    private readonly ManualResetEventSlim _loopStarted = new(false);
    private readonly ManualResetEventSlim _loopExited = new(false);
    private readonly ConcurrentQueue<Action<WinitEventLoopContext>> _contextOperations = new();
    private readonly ConcurrentQueue<Action> _managedOperations = new();
    private readonly ConcurrentDictionary<nint, WinitWindowImpl> _windows = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly object _stateLock = new();
    private readonly bool _runOnMainThread = OperatingSystem.IsMacOS();
    private readonly WinitRunConfiguration _configuration = new()
    {
        CreateWindow = false,
        Window = WinitWindowOptions.Default,
    };

    private volatile bool _disposed;
    private bool _signaled;
    private long? _nextTimerDue;
    private bool _exitRequested;
    private WinitStatus _exitStatus = WinitStatus.Success;
    private int _loopThreadId;
    private volatile bool _loopRunning;

    public WinitDispatcher()
    {
        if (_runOnMainThread)
        {
            _loopThreadId = Environment.CurrentManagedThreadId;
            _loopStarted.Set();
        }
        else
        {
            _loopThread = new Thread(EventLoopThreadProc)
            {
                IsBackground = true,
                Name = "Winit UI Thread"
            };
            _loopThread.Start();
        }

        _loopStarted.Wait();
    }

    public event Action? Signaled;
    public event Action? Timer;

    public bool CurrentThreadIsLoopThread => Environment.CurrentManagedThreadId == _loopThreadId;

    internal bool RunsOnMainThread => _runOnMainThread;

    internal bool IsLoopRunning => _loopRunning;

    public bool CanQueryPendingInput => false;

    public bool HasPendingInput => false;

    public long Now => _clock.ElapsedMilliseconds;

    public void Signal()
    {
        lock (_stateLock)
        {
            _signaled = true;
        }

        WakeEventLoop();
    }

    public void UpdateTimer(long? dueTimeInMs)
    {
        lock (_stateLock)
        {
            _nextTimerDue = dueTimeInMs;
        }

        WakeEventLoop();
    }

    public void RunLoop(CancellationToken cancellationToken)
    {
        if (_runOnMainThread)
        {
            using var registration = cancellationToken.Register(RequestExit);
            try
            {
                _loopRunning = true;
                var status = _eventLoop.Run(_configuration, this);
                _exitStatus = status;
            }
            finally
            {
                _loopRunning = false;
                _loopExited.Set();
                registration.Dispose();
            }

            if (_exitStatus != WinitStatus.Success)
            {
                NativeHelpers.ThrowOnError(_exitStatus, "winit_event_loop_run");
            }

            return;
        }

        using var backgroundRegistration = cancellationToken.Register(RequestExit);
        _loopExited.Wait();
        backgroundRegistration.Dispose();

        if (_exitStatus != WinitStatus.Success)
        {
            NativeHelpers.ThrowOnError(_exitStatus, "winit_event_loop_run");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        RequestExit();
        _loopExited.Wait();
        _loopStarted.Dispose();
        _loopExited.Dispose();
    }

    internal void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _managedOperations.Enqueue(action);
        WakeEventLoop();
    }

    internal void Post(Action<WinitEventLoopContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _contextOperations.Enqueue(action);
        WakeEventLoop();
    }

    internal void RegisterWindow(nint handle, WinitWindowImpl window)
    {
        _windows[handle] = window;
    }

    internal void UnregisterWindow(nint handle)
    {
        _windows.TryRemove(handle, out _);
    }

    void IWinitEventHandler.HandleEvent(WinitEventLoopContext context, in WinitEventArgs args)
    {
        if (_disposed)
        {
            context.Exit();
            return;
        }

        ProcessQueues(context);

        switch (args.Kind)
        {
            case WinitEventKind.NewEvents:
                // queues already processed
                break;
            case WinitEventKind.AboutToWait:
                ConfigureControlFlow(context);
                break;
            case WinitEventKind.MemoryWarning:
                break;
            case WinitEventKind.Exiting:
                RequestExit();
                context.Exit();
                break;
            default:
                DispatchWindowEvent(context, args);
                break;
        }
    }

    private void DispatchWindowEvent(WinitEventLoopContext context, in WinitEventArgs args)
    {
        if (args.WindowHandle == nint.Zero)
        {
            return;
        }

        if (!_windows.TryGetValue(args.WindowHandle, out var window))
        {
            // Window may not yet be registered; allow create event handlers to register.
            return;
        }

        switch (args.Kind)
        {
            case WinitEventKind.WindowCreated:
                window.OnWindowCreated(args.Width, args.Height, args.ScaleFactor);
                break;
            case WinitEventKind.WindowResized:
                window.OnWindowResized(args.Width, args.Height, args.ScaleFactor);
                break;
            case WinitEventKind.WindowScaleFactorChanged:
                window.OnScaleFactorChanged(args.ScaleFactor);
                break;
            case WinitEventKind.WindowFocused:
                window.OnActivated();
                break;
            case WinitEventKind.WindowFocusLost:
                window.OnDeactivated();
                break;
            case WinitEventKind.WindowRedrawRequested:
                window.OnRedrawRequested();
                break;
            case WinitEventKind.WindowCloseRequested:
                window.OnCloseRequested(context);
                break;
            case WinitEventKind.WindowDestroyed:
                window.OnDestroyed();
                break;
            case WinitEventKind.CursorMoved:
                window.OnCursorMoved(args.MouseX, args.MouseY, args.Modifiers);
                break;
            case WinitEventKind.CursorEntered:
                window.OnCursorEntered();
                break;
            case WinitEventKind.CursorLeft:
                window.OnCursorLeft();
                break;
            case WinitEventKind.MouseInput:
                window.OnMouseInput(args.MouseButton, args.ElementState, args.Modifiers);
                break;
            case WinitEventKind.MouseWheel:
                window.OnMouseWheel(args.DeltaX, args.DeltaY, args.ScrollDeltaKind, args.Modifiers);
                break;
            case WinitEventKind.KeyboardInput:
                window.OnKeyboardInput(args.KeyCode, args.KeyCodeName, args.ElementState, args.Modifiers, args.KeyLocation, args.Repeat, args.Text);
                break;
            case WinitEventKind.ModifiersChanged:
                window.OnModifiersChanged(args.Modifiers);
                break;
            case WinitEventKind.TextInput:
                if (!string.IsNullOrEmpty(args.Text))
                {
                    window.OnTextInput(args.Text);
                }
                break;
            case WinitEventKind.Touch:
                window.OnTouch(args);
                break;
            default:
                break;
        }
    }

    private void ConfigureControlFlow(WinitEventLoopContext context)
    {
        if (_exitRequested)
        {
            context.Exit();
            return;
        }

        if (!_contextOperations.IsEmpty || !_managedOperations.IsEmpty)
        {
            context.SetControlFlow(WinitControlFlow.Poll);
            return;
        }

        bool hasSignal;
        long? nextTimer;
        lock (_stateLock)
        {
            hasSignal = _signaled;
            nextTimer = _nextTimerDue;
        }

        if (hasSignal)
        {
            context.SetControlFlow(WinitControlFlow.Poll);
            return;
        }

        if (nextTimer is long due)
        {
            var now = Now;
            var remaining = Math.Max(0, due - now);
            context.SetControlFlow(WinitControlFlow.WaitUntil, TimeSpan.FromMilliseconds(remaining));
        }
        else
        {
            context.SetControlFlow(WinitControlFlow.Wait);
        }
    }

    private void ProcessQueues(WinitEventLoopContext context)
    {
        while (_contextOperations.TryDequeue(out var op))
        {
            try
            {
                op(context);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"WinitDispatcher context operation failed: {ex}");
            }
        }

        while (_managedOperations.TryDequeue(out var op))
        {
            try
            {
                op();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"WinitDispatcher managed operation failed: {ex}");
            }
        }

        bool fireSignal = false;
        bool fireTimer = false;

        lock (_stateLock)
        {
            if (_signaled)
            {
                fireSignal = true;
                _signaled = false;
            }

            if (_nextTimerDue is long due && due <= Now)
            {
                fireTimer = true;
                _nextTimerDue = null;
            }
        }

        if (fireSignal)
        {
            Signaled?.Invoke();
        }

        if (fireTimer)
        {
            Timer?.Invoke();
        }

        if (_exitRequested)
        {
            context.Exit();
        }
    }

    private void RequestExit()
    {
        lock (_stateLock)
        {
            if (_exitRequested)
            {
                return;
            }

            _exitRequested = true;
        }

        WakeEventLoop();
    }

    private void WakeEventLoop()
    {
        if (_runOnMainThread)
        {
            return;
        }

        var status = WinitNativeMethods.winit_event_loop_wake();
        if (status != WinitStatus.Success && status != WinitStatus.InvalidState)
        {
            NativeHelpers.ThrowOnError(status, "winit_event_loop_wake");
        }
    }

    private void EventLoopThreadProc()
    {
        _loopThreadId = Environment.CurrentManagedThreadId;
        _loopStarted.Set();

        _loopRunning = true;
        var status = _eventLoop.Run(_configuration, this);
        _exitStatus = status;
        _loopRunning = false;
        _loopExited.Set();
    }
}
