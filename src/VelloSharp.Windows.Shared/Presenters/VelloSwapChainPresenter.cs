using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VelloSharp;
using VelloSharp.Windows;
using VelloSharp.Windows.Shared.Contracts;
using VelloSharp.Windows.Shared.Dispatching;
using VelloSharp.Windows.Shared.Diagnostics;
using VelloPaintSurfaceEventArgs = VelloSharp.WinForms.Integration.VelloPaintSurfaceEventArgs;

namespace VelloSharp.Windows.Shared.Presenters;

public interface IVelloSwapChainPresenterHost
{
    IVelloWindowsDispatcher? Dispatcher { get; }
    IVelloCompositionTarget? CompositionTarget { get; }
    bool IsContinuousRendering { get; }
    bool IsDesignMode { get; }
    VelloGraphicsDeviceOptions DeviceOptions { get; }
    VelloRenderBackend PreferredBackend { get; }
    VelloRenderMode RenderMode { get; }
    RenderLoopDriver RenderLoopDriver { get; }
    void OnPaintSurface(VelloPaintSurfaceEventArgs args);
    void OnRenderSurface(VelloSwapChainRenderEventArgs args);
    void OnContentInvalidated();
    void OnDiagnosticsUpdated(WindowsGpuDiagnostics diagnostics);
    void ApplySkiaOptOut();
    void RemoveSkiaOptOut();
}

public interface IVelloSurfaceRenderCallback
{
    void OnRenderSurface(VelloSurfaceRenderEventArgs args);
}

public sealed class VelloSwapChainPresenter : IDisposable
{
    internal static Func<VelloGraphicsDeviceOptions, WindowsGpuContextLease?> AcquireContext { get; set; } = WindowsGpuContext.Acquire;

    internal static void ResetTestingHooks()
        => AcquireContext = WindowsGpuContext.Acquire;

    private readonly IVelloSwapChainPresenterHost _host;
    private readonly IWindowsSurfaceSource _surfaceSource;
    private readonly WindowsGpuDiagnostics _fallbackDiagnostics = new();
    private readonly ManualResetEventSlim _renderIdle = new(true);
    private readonly object _timingLock = new();
    private WindowsGpuContextLease? _gpuLease;
    private WindowsSwapChainSurface? _swapChain;
    private VelloGraphicsDevice? _device;
    private IVelloWindowsDispatcherTimer? _renderTimer;
    private EventHandler<object>? _compositionRenderingHandler;
    private IVelloCompositionTarget? _compositionTarget;
    private bool _disposed;
    private bool _isLoaded;
    private int _pendingRender;
    private int _isRendering;
    private RenderLoopDriver _activeRenderLoopDriver = RenderLoopDriver.None;
    private readonly Stopwatch _frameStopwatch = new();
    private TimeSpan _lastFrameTimestamp;
    private long _frameId;
    private long _lastPresentationCount = -1;
    private long _lastSurfaceConfigCount = -1;
    private bool _isSuspended;

    public VelloSwapChainPresenter(IVelloSwapChainPresenterHost host, IWindowsSurfaceSource surfaceSource)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _surfaceSource = surfaceSource ?? throw new ArgumentNullException(nameof(surfaceSource));
    }

    public WindowsGpuDiagnostics Diagnostics
        => _gpuLease?.Context.Diagnostics ?? _fallbackDiagnostics;

    public void RequestRender()
    {
        if (_disposed || !_isLoaded || _host.IsDesignMode || _host.PreferredBackend != VelloRenderBackend.Gpu)
        {
            return;
        }

        var previousPending = Interlocked.Exchange(ref _pendingRender, 1);
        if (_isSuspended)
        {
            return;
        }

        if (previousPending == 1 && Volatile.Read(ref _isRendering) == 1)
        {
            return;
        }

        var dispatcher = GetDispatcher();
        if (dispatcher is null)
        {
            return;
        }

        if (dispatcher.HasThreadAccess)
        {
            RenderFrame();
        }
        else
        {
            dispatcher.TryEnqueue(RenderFrame);
        }
    }

    public void OnLoaded()
    {
        if (_disposed)
        {
            return;
        }

        _isLoaded = true;
        _isSuspended = false;

        if (_host.IsDesignMode)
        {
            return;
        }

        _host.ApplySkiaOptOut();
        ResetTiming();
        AcquireGpuLeaseIfNeeded();
        EnsureSwapChain();
        UpdateRenderLoop();
        RequestRender();
    }

    public void OnUnloaded()
    {
        _isLoaded = false;
        if (_host.IsDesignMode)
        {
            return;
        }

        StopRenderLoop();
        _host.RemoveSkiaOptOut();
        ReleaseGpuResources();
    }

    public void OnDeviceOptionsChanged()
    {
        if (_disposed || !_isLoaded || _host.IsDesignMode)
        {
            return;
        }

        RecreateGpuLease();
        RequestRender();
    }

    public void OnPreferredBackendChanged()
    {
        if (_disposed || !_isLoaded || _host.IsDesignMode)
        {
            return;
        }

        if (_host.PreferredBackend == VelloRenderBackend.Gpu)
        {
            _host.ApplySkiaOptOut();
            AcquireGpuLeaseIfNeeded();
            EnsureSwapChain();
            ResetTiming();
            UpdateRenderLoop();
            RequestRender();
        }
        else
        {
            StopRenderLoop();
            ReleaseGpuResources();
            _host.RemoveSkiaOptOut();
            _host.OnContentInvalidated();
        }
    }

    public void OnRenderModeChanged()
    {
        if (_disposed || _host.IsDesignMode)
        {
            return;
        }

        UpdateRenderLoop();
        if (_host.RenderMode == VelloRenderMode.OnDemand)
        {
            RequestRender();
        }
    }

    public void OnRenderLoopDriverChanged()
    {
        if (_disposed || _host.IsDesignMode)
        {
            return;
        }

        UpdateRenderLoop();
    }

    public void OnSurfaceInvalidated()
    {
        if (_disposed || !_isLoaded || _host.IsDesignMode || _host.PreferredBackend != VelloRenderBackend.Gpu)
        {
            return;
        }

        EnsureSwapChain();
        RequestRender();
    }

    public void SetRenderSuspended(bool isSuspended)
    {
        if (_disposed || _host.IsDesignMode)
        {
            return;
        }

        if (_isSuspended == isSuspended)
        {
            return;
        }

        _isSuspended = isSuspended;

        if (_isSuspended)
        {
            var hadPendingRender = Volatile.Read(ref _pendingRender) == 1;
            StopRenderLoop();
            if (hadPendingRender)
            {
                Interlocked.Exchange(ref _pendingRender, 1);
            }
            WaitForRenderIdle();
        }
        else
        {
            ResetTiming();
            UpdateRenderLoop();
            if (_host.RenderMode == VelloRenderMode.Continuous || Volatile.Read(ref _pendingRender) == 1)
            {
                RequestRender();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopRenderLoop();
        WaitForRenderIdle();
        _host.RemoveSkiaOptOut();
        ReleaseGpuResources();
        _renderIdle.Dispose();
    }

    private IVelloWindowsDispatcher? GetDispatcher()
        => _host.Dispatcher ?? VelloWindowsDispatcher.GetForCurrentThread();

    private void AcquireGpuLeaseIfNeeded()
    {
        if (_host.IsDesignMode)
        {
            return;
        }

        if (_gpuLease is not null)
        {
            return;
        }

        var options = _host.DeviceOptions ?? VelloGraphicsDeviceOptions.Default;
        _gpuLease = AcquireContext(options);
        if (_gpuLease is not null)
        {
            NotifyDiagnosticsUpdated(_gpuLease.Context.Diagnostics);
        }
    }

    private void RecreateGpuLease()
    {
        if (_host.IsDesignMode)
        {
            return;
        }

        StopRenderLoop();
        ReleaseGpuResources();
        AcquireGpuLeaseIfNeeded();
        UpdateRenderLoop();
    }

    private void ReleaseGpuResources()
    {
        WaitForRenderIdle();
        Interlocked.Exchange(ref _pendingRender, 0);

        WindowsSurfaceFactory.ReleaseSwapChain(_surfaceSource, _swapChain);
        _swapChain = null;
        ResetDevice();

        _gpuLease?.Dispose();
        _gpuLease = null;
        InvalidateDiagnosticsSnapshot();
        NotifyDiagnosticsUpdated(_fallbackDiagnostics);
    }

    private void ResetDevice()
    {
        _device?.Dispose();
        _device = null;
    }

    private void EnsureDevice(uint pixelWidth, uint pixelHeight)
    {
        if (_device is null)
        {
            var options = _host.DeviceOptions ?? VelloGraphicsDeviceOptions.Default;
            _device = new VelloGraphicsDevice(Math.Max(pixelWidth, 1u), Math.Max(pixelHeight, 1u), options);
        }
    }

    private void EnsureSwapChain()
    {
        if (_gpuLease is null || _host.IsDesignMode || _isSuspended || _host.PreferredBackend != VelloRenderBackend.Gpu)
        {
            return;
        }

        var size = _surfaceSource.GetSurfaceSize();
        if (size.IsEmpty)
        {
            return;
        }

        _swapChain = WindowsSurfaceFactory.EnsureSwapChainSurface(_gpuLease, _surfaceSource, _swapChain, size);
    }

    private void UpdateRenderLoop()
    {
        if (_disposed)
        {
            return;
        }

        var shouldRun = _isLoaded
            && !_isSuspended
            && !_host.IsDesignMode
            && _host.PreferredBackend == VelloRenderBackend.Gpu
            && _host.RenderMode == VelloRenderMode.Continuous;
        if (!shouldRun)
        {
            StopRenderLoop();
            return;
        }

        var desiredDriver = _host.RenderLoopDriver;
        if (desiredDriver == RenderLoopDriver.None)
        {
            desiredDriver = RenderLoopDriver.ComponentDispatcher;
        }

        if (_activeRenderLoopDriver == desiredDriver)
        {
            return;
        }

        StopRenderLoop();

        switch (desiredDriver)
        {
            case RenderLoopDriver.CompositionTarget:
                AttachCompositionLoop();
                break;
            case RenderLoopDriver.ComponentDispatcher:
            default:
                AttachDispatcherTimerLoop();
                break;
        }
    }

    private void AttachDispatcherTimerLoop()
    {
        if (_renderTimer is not null)
        {
            _renderTimer.Tick -= OnRenderTimerTick;
            _renderTimer.Stop();
        }

        var dispatcher = GetDispatcher();
        if (dispatcher is null)
        {
            return;
        }

        _renderTimer = dispatcher.CreateTimer();
        _renderTimer.Interval = TimeSpan.FromMilliseconds(16);
        _renderTimer.IsRepeating = true;
        _renderTimer.Tick += OnRenderTimerTick;
        _renderTimer.Start();

        _activeRenderLoopDriver = RenderLoopDriver.ComponentDispatcher;
    }

    private void AttachCompositionLoop()
    {
        var target = _host.CompositionTarget ?? VelloCompositionTarget.GetForCurrentThread();
        if (target is null)
        {
            return;
        }

        _compositionRenderingHandler ??= OnCompositionRendering;
        target.AddRenderingHandler(_compositionRenderingHandler);
        _compositionTarget = target;
        _activeRenderLoopDriver = RenderLoopDriver.CompositionTarget;
    }

    private void StopRenderLoop()
    {
        if (_renderTimer is not null)
        {
            _renderTimer.Tick -= OnRenderTimerTick;
            _renderTimer.Stop();
            _renderTimer.Dispose();
            _renderTimer = null;
        }

        if (_compositionRenderingHandler is not null && _compositionTarget is not null)
        {
            _compositionTarget.RemoveRenderingHandler(_compositionRenderingHandler);
            _compositionRenderingHandler = null;
        }

        _compositionTarget = null;
        _activeRenderLoopDriver = RenderLoopDriver.None;
        Interlocked.Exchange(ref _pendingRender, 0);
    }

    private void OnRenderTimerTick(object? sender, EventArgs args)
    {
        if (ShouldThrottleFrameTick())
        {
            return;
        }

        RequestRender();
    }

    private void OnCompositionRendering(object? sender, object args)
    {
        if (ShouldThrottleFrameTick())
        {
            return;
        }

        RequestRender();
    }

    private void RenderFrame()
    {
        if (_disposed || !_isLoaded || _host.IsDesignMode || _host.PreferredBackend != VelloRenderBackend.Gpu || _isSuspended)
        {
            Interlocked.Exchange(ref _pendingRender, 0);
            return;
        }

        if (Interlocked.CompareExchange(ref _isRendering, 1, 0) == 1)
        {
            return;
        }

        _renderIdle.Reset();
        Interlocked.Exchange(ref _pendingRender, 0);

        if (!TryPrepareRenderWork(out var work))
        {
            FinishRenderWork();
            return;
        }

        _ = Task.Run(() => RenderFrameCore(work));
    }

    private readonly struct RenderWork
    {
        public RenderWork(WindowsGpuContextLease lease, WindowsSwapChainSurface surface, WindowsSurfaceSize pixelSize, uint pixelWidth, uint pixelHeight)
        {
            Lease = lease;
            Surface = surface;
            PixelSize = pixelSize;
            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
        }

        public WindowsGpuContextLease Lease { get; }
        public WindowsSwapChainSurface Surface { get; }
        public WindowsSurfaceSize PixelSize { get; }
        public uint PixelWidth { get; }
        public uint PixelHeight { get; }
    }

    private bool TryPrepareRenderWork(out RenderWork work)
    {
        work = default;

        AcquireGpuLeaseIfNeeded();
        var lease = _gpuLease;
        if (lease is null || !_isLoaded || _host.IsDesignMode || _isSuspended || _host.PreferredBackend != VelloRenderBackend.Gpu)
        {
            return false;
        }

        var size = _surfaceSource.GetSurfaceSize();
        if (size.IsEmpty)
        {
            return false;
        }

        _swapChain = WindowsSurfaceFactory.EnsureSwapChainSurface(lease, _surfaceSource, _swapChain, size);
        if (_swapChain is null)
        {
            return false;
        }

        var pixelWidth = Math.Max(1u, size.Width);
        var pixelHeight = Math.Max(1u, size.Height);
        EnsureDevice(pixelWidth, pixelHeight);
        if (_device is null)
        {
            return false;
        }

        work = new RenderWork(lease, _swapChain, size, pixelWidth, pixelHeight);
        return true;
    }

    private void RenderFrameCore(RenderWork work)
    {
        try
        {
            if (_disposed || !_isLoaded || _host.IsDesignMode || _isSuspended || _host.PreferredBackend != VelloRenderBackend.Gpu)
            {
                return;
            }

            var device = _device;
            if (device is null)
            {
                return;
            }

            using var session = device.BeginSession(work.PixelWidth, work.PixelHeight);
            var (timestamp, delta, frameId) = CaptureFrameTiming();
            var paintArgs = new VelloPaintSurfaceEventArgs(session, timestamp, delta, frameId, _host.IsContinuousRendering);
            _host.OnPaintSurface(paintArgs);

            if (_disposed)
            {
                session.Complete();
                return;
            }

            if (TryRenderToSwapChain(work.Lease, work.Surface, session, work.PixelWidth, work.PixelHeight, work.PixelSize, timestamp, delta, frameId, out var renderArgs))
            {
                IncrementFrameId();
                UpdateDiagnosticsSnapshot(work.Lease);
                if (renderArgs is not null)
                {
                    DispatchRenderEvents(renderArgs);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VelloSwapChainPresenter] Render failure: {ex}");
        }
        finally
        {
            FinishRenderWork();
        }
    }

    private void DispatchRenderEvents(VelloSwapChainRenderEventArgs renderArgs)
    {
        var dispatcher = GetDispatcher();
        if (dispatcher is null)
        {
            _host.OnContentInvalidated();
            _host.OnRenderSurface(renderArgs);
            return;
        }

        if (dispatcher.HasThreadAccess)
        {
            _host.OnContentInvalidated();
            _host.OnRenderSurface(renderArgs);
        }
        else
        {
            dispatcher.TryEnqueue(() =>
            {
                _host.OnContentInvalidated();
                _host.OnRenderSurface(renderArgs);
            });
        }
    }

    private void FinishRenderWork()
    {
        _renderIdle.Set();
        Interlocked.Exchange(ref _isRendering, 0);

        if (Volatile.Read(ref _pendingRender) == 0)
        {
            return;
        }

        if (_disposed || !_isLoaded || _host.IsDesignMode || _isSuspended || _host.PreferredBackend != VelloRenderBackend.Gpu)
        {
            Interlocked.Exchange(ref _pendingRender, 0);
            return;
        }

        var dispatcher = GetDispatcher();
        if (dispatcher is null)
        {
            Interlocked.Exchange(ref _pendingRender, 0);
            return;
        }

        if (dispatcher.HasThreadAccess)
        {
            RenderFrame();
        }
        else
        {
            dispatcher.TryEnqueue(RenderFrame);
        }
    }

    private void UpdateDiagnosticsSnapshot(WindowsGpuContextLease lease)
    {
        try
        {
            var diagnostics = lease.Context.Diagnostics;
            _lastPresentationCount = diagnostics.SwapChainPresentations;
            _lastSurfaceConfigCount = diagnostics.SurfaceConfigurations;
            NotifyDiagnosticsUpdated(diagnostics);
        }
        catch (ObjectDisposedException)
        {
            InvalidateDiagnosticsSnapshot();
        }
    }

    private void InvalidateDiagnosticsSnapshot()
    {
        _lastPresentationCount = -1;
        _lastSurfaceConfigCount = -1;
    }

    private void NotifyDiagnosticsUpdated(WindowsGpuDiagnostics diagnostics)
    {
        if (_disposed)
        {
            return;
        }

        var dispatcher = GetDispatcher();
        if (dispatcher is null)
        {
            _host.OnDiagnosticsUpdated(diagnostics);
            return;
        }

        if (dispatcher.HasThreadAccess)
        {
            _host.OnDiagnosticsUpdated(diagnostics);
        }
        else
        {
            dispatcher.TryEnqueue(() => _host.OnDiagnosticsUpdated(diagnostics));
        }
    }

    private (TimeSpan Timestamp, TimeSpan Delta, long FrameId) CaptureFrameTiming()
    {
        lock (_timingLock)
        {
            if (!_frameStopwatch.IsRunning)
            {
                _frameStopwatch.Start();
                _lastFrameTimestamp = TimeSpan.Zero;
                return (TimeSpan.Zero, TimeSpan.Zero, _frameId);
            }

            var timestamp = _frameStopwatch.Elapsed;
            var delta = _frameId == 0 ? TimeSpan.Zero : timestamp - _lastFrameTimestamp;
            if (delta < TimeSpan.Zero)
            {
                delta = TimeSpan.Zero;
            }

            _lastFrameTimestamp = timestamp;
            return (timestamp, delta, _frameId);
        }
    }

    private void IncrementFrameId()
    {
        lock (_timingLock)
        {
            _frameId++;
        }
    }

    private void WaitForRenderIdle()
    {
        try
        {
            _renderIdle.Wait();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private bool ShouldThrottleFrameTick()
    {
        if (_disposed || !_isLoaded || _host.IsDesignMode || _isSuspended || _host.PreferredBackend != VelloRenderBackend.Gpu)
        {
            Interlocked.Exchange(ref _pendingRender, 0);
            return true;
        }

        if (_host.IsContinuousRendering)
        {
            return false;
        }

        if (Volatile.Read(ref _pendingRender) == 1)
        {
            return false;
        }

        var lease = _gpuLease;
        if (lease?.Context.Diagnostics is not { } diagnostics)
        {
            return false;
        }

        var presentations = diagnostics.SwapChainPresentations;
        var configurations = diagnostics.SurfaceConfigurations;
        return presentations == _lastPresentationCount && configurations == _lastSurfaceConfigCount;
    }

    private bool TryRenderToSwapChain(
        WindowsGpuContextLease lease,
        WindowsSwapChainSurface surface,
        VelloGraphicsSession session,
        uint pixelWidth,
        uint pixelHeight,
        WindowsSurfaceSize pixelSize,
        TimeSpan timestamp,
        TimeSpan delta,
        long frameId,
        out VelloSwapChainRenderEventArgs? renderArgs)
    {
        renderArgs = null;

        try
        {
            surface.Configure(pixelWidth, pixelHeight);

            using var texture = surface.AcquireNextTexture();
            using var view = texture.CreateView();

            var options = _host.DeviceOptions ?? VelloGraphicsDeviceOptions.Default;
            var renderParams = new RenderParams(
                pixelWidth,
                pixelHeight,
                options.BackgroundColor,
                options.GetAntialiasingMode(),
                options.Format);

            var adjustedParams = renderParams;
            if (_host is IVelloSurfaceRenderCallback callback)
            {
                var surfaceArgs = new VelloSurfaceRenderEventArgs(
                    lease,
                    session.Scene,
                    view,
                    surface.Format,
                    renderParams,
                    pixelSize,
                    timestamp,
                    delta,
                    frameId,
                    _host.IsContinuousRendering,
                    surface.SurfaceHandle);
                callback.OnRenderSurface(surfaceArgs);
                adjustedParams = surfaceArgs.RenderParams;
                if (!surfaceArgs.Handled)
                {
                    lease.Context.Renderer.RenderSurface(session.Scene, view, adjustedParams, surface.Format);
                }
            }
            else
            {
                lease.Context.Renderer.RenderSurface(session.Scene, view, adjustedParams, surface.Format);
            }
            texture.Present();
            lease.Context.RecordPresentation();
            session.Complete();

            renderArgs = new VelloSwapChainRenderEventArgs(lease, surface, pixelSize, adjustedParams, timestamp, delta, frameId);
            return true;
        }
        catch (DllNotFoundException ex)
        {
            HandleDeviceLoss(ex.Message);
        }
        catch (Exception ex)
        {
            HandleDeviceLoss(ex.Message);
        }

        session.Complete();
        InvalidateDiagnosticsSnapshot();
        return false;
    }

    private void HandleDeviceLoss(string? reason)
    {
        if (_gpuLease is { Context: { } context })
        {
            WindowsSurfaceFactory.HandleDeviceLoss(context, _surfaceSource, reason);
        }

        WindowsSurfaceFactory.ReleaseSwapChain(_surfaceSource, _swapChain);
        _swapChain = null;
        ResetDevice();
        InvalidateDiagnosticsSnapshot();
        NotifyDiagnosticsUpdated(_fallbackDiagnostics);
    }

    private void ResetTiming()
    {
        lock (_timingLock)
        {
            _frameStopwatch.Restart();
            _lastFrameTimestamp = TimeSpan.Zero;
            _frameId = 0;
        }
    }
}
