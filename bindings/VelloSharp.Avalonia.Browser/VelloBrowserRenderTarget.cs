using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Platform;
using VelloSharp;
using VelloSharp.Avalonia.Vello;
using VelloSharp.Avalonia.Vello.Rendering;
using WebGpuSurfaceDescriptor = VelloSharp.WebGpuRuntime.WebGpuSurfaceDescriptor;
using WebGpuTextureFormat = VelloSharp.WebGpuRuntime.WebGpuTextureFormat;
using WebGpuPresentMode = VelloSharp.WebGpuRuntime.WebGpuPresentMode;
using WebGpuSurfaceConfiguration = VelloSharp.WebGpuRuntime.WebGpuSurfaceConfiguration;

namespace VelloSharp.Avalonia.Browser;

[SupportedOSPlatform("browser")]
internal sealed class VelloBrowserRenderTarget : IRenderTarget2
{
    private static readonly RenderTargetProperties s_properties = new()
    {
        RetainsPreviousFrameContents = true,
        IsSuitableForDirectRendering = true,
    };

    private readonly ITopLevelImpl _topLevel;
    private readonly VelloPlatformOptions _options;
    private readonly VelloBrowserSwapChainSurface _swapChainSurface;
    private readonly object _syncRoot = new();
    private readonly WebGpuPresentMode _presentMode;
    private bool _disposed;
    private string? _lastAcquireError;
    private Task<uint?>? _adapterRequestTask;
    private Task<WebGpuRuntime.WebGpuDeviceHandles?>? _deviceRequestTask;
    private uint _adapterHandle;
    private uint _deviceHandle;
    private uint _queueHandle;
    private uint _rendererHandle;
    private bool _isSuspended;
    private bool _lifecycleSubscribed;
    private bool _surfaceConfigured;
    private PixelSize _configuredPixelSize;
    private WebGpuTextureFormat _surfaceFormat = WebGpuTextureFormat.Undefined;
    private bool _initializationFailed;
    private bool _hasReportedReady;
    private readonly Queue<SceneLease> _pendingLeases = new();

    public VelloBrowserRenderTarget(ITopLevelImpl topLevel, VelloPlatformOptions options)
    {
        _topLevel = topLevel ?? throw new ArgumentNullException(nameof(topLevel));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _presentMode = MapPresentMode(_options.PresentMode);

        WebGpuRuntime.EnsureInitialized();

        var descriptor = ResolveSurfaceDescriptor(topLevel);
        _swapChainSurface = new VelloBrowserSwapChainSurface(topLevel, descriptor);

        VelloBrowserDispatcherLifecycle.EnsureInitialized();
        _isSuspended = !VelloBrowserDispatcherLifecycle.IsVisible;
        VelloBrowserDispatcherLifecycle.VisibilityChanged += OnVisibilityChanged;
        _lifecycleSubscribed = true;

        if (_isSuspended)
        {
            _swapChainSurface.Suspend();
        }
        else
        {
            StartAdapterRequestIfNeeded();
        }
    }

    public bool IsCorrupted => false;

    public RenderTargetProperties Properties => s_properties;

    public IDrawingContextImpl CreateDrawingContext(bool useScaledDrawing)
    {
        var pixelSize = ResolvePixelSize();
        return CreateDrawingContext(pixelSize, out _);
    }

    public IDrawingContextImpl CreateDrawingContext(
        PixelSize expectedPixelSize,
        out RenderTargetDrawingContextProperties properties)
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(VelloBrowserRenderTarget));
            }
        }

        var scene = new Scene();
        scene.Reset();

        properties = new RenderTargetDrawingContextProperties
        {
            PreviousFrameIsRetained = false,
        };

        return new VelloDrawingContextImpl(
            scene,
            ResolvePixelSize(),
            _options,
            OnContextCompleted,
            skipInitialClip: !properties.PreviousFrameIsRetained,
            graphicsDevice: null);
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        if (_lifecycleSubscribed)
        {
            VelloBrowserDispatcherLifecycle.VisibilityChanged -= OnVisibilityChanged;
            _lifecycleSubscribed = false;
        }

        _swapChainSurface.Dispose();
        while (_pendingLeases.Count > 0)
        {
            _pendingLeases.Dequeue().Dispose();
        }

        try
        {
            if (_rendererHandle != 0)
            {
                WebGpuRuntime.DestroyRenderer(_rendererHandle);
                _rendererHandle = 0;
            }

            if (_queueHandle != 0)
            {
                WebGpuRuntime.DestroyQueue(_queueHandle);
                _queueHandle = 0;
            }

            if (_deviceHandle != 0)
            {
                WebGpuRuntime.DestroyDevice(_deviceHandle);
                _deviceHandle = 0;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Vello][Browser] Failed to release WebGPU resources: {ex.Message}");
        }
    }

    private void OnVisibilityChanged(bool isVisible)
    {
        if (isVisible)
        {
            ResumeRendering();
        }
        else
        {
            SuspendRendering();
        }
    }

    private void SuspendRendering()
    {
        List<SceneLease>? leasesToDispose = null;
        uint rendererHandle = 0;
        var shouldSuspendSurface = false;

        lock (_syncRoot)
        {
            if (_disposed || _isSuspended)
            {
                return;
            }

            _isSuspended = true;
            _hasReportedReady = false;

            if (_pendingLeases.Count > 0)
            {
                leasesToDispose = new List<SceneLease>(_pendingLeases.Count);
                while (_pendingLeases.Count > 0)
                {
                    leasesToDispose.Add(_pendingLeases.Dequeue());
                }
            }

            if (_rendererHandle != 0)
            {
                rendererHandle = _rendererHandle;
                _rendererHandle = 0;
            }

            _surfaceConfigured = false;
            _surfaceFormat = WebGpuTextureFormat.Undefined;
            shouldSuspendSurface = true;
        }

        if (leasesToDispose is not null)
        {
            foreach (var lease in leasesToDispose)
            {
                lease.Dispose();
            }
        }

        if (rendererHandle != 0)
        {
            try
            {
                WebGpuRuntime.DestroyRenderer(rendererHandle);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Vello][Browser] Failed to destroy renderer during suspension: {ex.Message}");
            }
        }

        if (shouldSuspendSurface)
        {
            try
            {
                _swapChainSurface.Suspend();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Vello][Browser] Failed to suspend WebGPU surface: {ex.Message}");
            }
        }
    }

    private void ResumeRendering()
    {
        lock (_syncRoot)
        {
            if (_disposed || !_isSuspended)
            {
                return;
            }

            _isSuspended = false;
        }

        try
        {
            _swapChainSurface.Resume();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Vello][Browser] Failed to resume WebGPU surface: {ex.Message}");
            return;
        }

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _swapChainSurface.ForceSync();
            StartAdapterRequestIfNeeded();
            DrainPendingLeases();
        }
    }

    private void OnContextCompleted(VelloDrawingContextImpl context)
    {
        SceneLease? lease = null;
        var enqueued = false;

        try
        {
            lock (_syncRoot)
            {
                if (_disposed || _initializationFailed || _isSuspended)
                {
                    context.Scene.Dispose();
                    return;
                }

                lease = context.LeaseScene();
                _swapChainSurface.ForceSync();

                if (!TryPrepareFrame(lease.RenderParams, out var adjustedParams))
                {
                    _pendingLeases.Enqueue(lease);
                    enqueued = true;
                    return;
                }

                lease.UpdateRenderParams(adjustedParams);
                RenderAndPresentScene(lease, adjustedParams);
                _lastAcquireError = null;
                DrainPendingLeases();
            }
        }
        catch (Exception ex) when (HandleAcquirePresentFailure(ex))
        {
        }
        finally
        {
            if (!enqueued)
            {
                lease?.Dispose();
            }
        }
    }

    private bool TryPrepareFrame(RenderParams renderParams, out RenderParams adjustedParams)
    {
        adjustedParams = renderParams;

        if (_isSuspended)
        {
            return false;
        }

        if (!EnsureDeviceAndQueue())
        {
            return false;
        }

        if (!EnsureSurfaceConfigured(renderParams))
        {
            return false;
        }

        if (_rendererHandle == 0)
        {
            try
            {
                _rendererHandle = WebGpuRuntime.CreateRenderer(
                    _deviceHandle,
                    _queueHandle,
                    _options.RendererOptions);
            }
            catch (Exception ex)
            {
                _initializationFailed = true;
                HandleAcquirePresentFailure(ex);
                return false;
            }
        }

        adjustedParams = AdjustRenderParams(renderParams);
        return true;
    }

    private void RenderAndPresentScene(SceneLease lease, RenderParams renderParams)
    {
        using var surfaceTexture = _swapChainSurface.AcquireNextTexture();
        var textureViewHandle = WebGpuRuntime.CreateSurfaceTextureView(surfaceTexture.TextureHandle);

        try
        {
            WebGpuRuntime.RenderSurface(
                _rendererHandle,
                lease.Scene.Handle,
                textureViewHandle,
                renderParams,
                _surfaceFormat);

            surfaceTexture.Present();
            if (!_hasReportedReady)
            {
                VelloBrowserDiagnostics.ReportAvailability(true, null);
                _hasReportedReady = true;
            }
        }
        finally
        {
            WebGpuRuntime.DestroyTextureView(textureViewHandle);
        }
    }

    private bool EnsureDeviceAndQueue()
    {
        if (_initializationFailed)
        {
            return false;
        }

        StartAdapterRequestIfNeeded();
        if (!TryFinalizeAdapterRequest())
        {
            return false;
        }

        StartDeviceRequestIfNeeded();
        return TryFinalizeDeviceRequest();
    }

    private void StartAdapterRequestIfNeeded()
    {
        if (_initializationFailed || _adapterHandle != 0 || _adapterRequestTask is not null)
        {
            return;
        }

        _adapterRequestTask = WebGpuRuntime.RequestAdapterAsync();
    }

    private void StartDeviceRequestIfNeeded()
    {
        if (_initializationFailed || _adapterHandle == 0 || _deviceHandle != 0 || _deviceRequestTask is not null)
        {
            return;
        }

        _deviceRequestTask = WebGpuRuntime.RequestDeviceAsync(_adapterHandle);
    }

    private bool TryFinalizeAdapterRequest()
    {
        if (_adapterHandle != 0)
        {
            return true;
        }

        var task = _adapterRequestTask;
        if (task is null)
        {
            return false;
        }

        if (!task.IsCompleted)
        {
            return false;
        }

        try
        {
            if (task.IsFaulted)
            {
                _initializationFailed = true;
                HandleAcquirePresentFailure(task.Exception?.GetBaseException() ?? new InvalidOperationException("WebGPU adapter request failed."));
                return false;
            }

            if (task.IsCanceled)
            {
                _initializationFailed = true;
                HandleAcquirePresentFailure(new InvalidOperationException("WebGPU adapter request was canceled."));
                return false;
            }

            var handle = task.Result;
            if (!handle.HasValue || handle.Value == 0)
            {
                _initializationFailed = true;
                HandleAcquirePresentFailure(new InvalidOperationException("No compatible WebGPU adapter was found."));
                return false;
            }

            _adapterHandle = handle.Value;
            _adapterRequestTask = null;
            return true;
        }
        catch (Exception ex)
        {
            _initializationFailed = true;
            HandleAcquirePresentFailure(ex);
            return false;
        }
    }

    private bool TryFinalizeDeviceRequest()
    {
        if (_deviceHandle != 0 && _queueHandle != 0)
        {
            return true;
        }

        var task = _deviceRequestTask;
        if (task is null)
        {
            return false;
        }

        if (!task.IsCompleted)
        {
            return false;
        }

        try
        {
            if (task.IsFaulted)
            {
                _initializationFailed = true;
                HandleAcquirePresentFailure(task.Exception?.GetBaseException() ?? new InvalidOperationException("WebGPU device request failed."));
                return false;
            }

            if (task.IsCanceled)
            {
                _initializationFailed = true;
                HandleAcquirePresentFailure(new InvalidOperationException("WebGPU device request was canceled."));
                return false;
            }

            var handles = task.Result;
            if (!handles.HasValue || handles.Value.DeviceHandle == 0 || handles.Value.QueueHandle == 0)
            {
                _initializationFailed = true;
                HandleAcquirePresentFailure(new InvalidOperationException("WebGPU device could not be created."));
                return false;
            }

            _deviceHandle = handles.Value.DeviceHandle;
            _queueHandle = handles.Value.QueueHandle;
            _deviceRequestTask = null;
            DrainPendingLeases();
            return true;
        }
        catch (Exception ex)
        {
            _initializationFailed = true;
            HandleAcquirePresentFailure(ex);
            return false;
        }
    }

    private bool EnsureSurfaceConfigured(RenderParams renderParams)
    {
        var width = renderParams.Width > 0 ? renderParams.Width : 1u;
        var height = renderParams.Height > 0 ? renderParams.Height : 1u;

        if (width == 0 || height == 0)
        {
            return false;
        }

        if (!_surfaceConfigured ||
            _configuredPixelSize.Width != (int)width ||
            _configuredPixelSize.Height != (int)height)
        {
            var configuration = new WebGpuSurfaceConfiguration(width, height, _presentMode);

            try
            {
                WebGpuRuntime.ConfigureSurface(
                    _swapChainSurface.Handle,
                    _adapterHandle,
                    _deviceHandle,
                    configuration);
            }
            catch (Exception ex)
            {
                HandleAcquirePresentFailure(ex);
                return false;
            }

            _surfaceConfigured = true;
            _configuredPixelSize = new PixelSize((int)width, (int)height);

            try
            {
                _surfaceFormat = _swapChainSurface.SurfaceFormat;
            }
            catch (Exception ex)
            {
                _surfaceConfigured = false;
                HandleAcquirePresentFailure(ex);
                return false;
            }

            if (_surfaceFormat == WebGpuTextureFormat.Undefined)
            {
                _surfaceConfigured = false;
                HandleAcquirePresentFailure(new InvalidOperationException("WebGPU surface reported an undefined texture format."));
                return false;
            }
        }

        return true;
    }

    private void DrainPendingLeases()
    {
        if (_pendingLeases.Count == 0)
        {
            return;
        }

        if (_isSuspended)
        {
            return;
        }

        while (_pendingLeases.Count > 0)
        {
            var lease = _pendingLeases.Peek();

            if (!TryPrepareFrame(lease.RenderParams, out var adjustedParams))
            {
                break;
            }

            lease.UpdateRenderParams(adjustedParams);

            try
            {
                RenderAndPresentScene(lease, adjustedParams);
                _lastAcquireError = null;
            }
            catch (Exception ex) when (HandleAcquirePresentFailure(ex))
            {
                lease.Dispose();
                _pendingLeases.Dequeue();
                continue;
            }

            lease.Dispose();
            _pendingLeases.Dequeue();
        }
    }

    private RenderParams AdjustRenderParams(RenderParams renderParams)
    {
        var antialiasing = _options.ResolveAntialiasing(renderParams.Antialiasing);
        var format = DetermineRenderFormat(_surfaceFormat);
        return new RenderParams(
            renderParams.Width,
            renderParams.Height,
            renderParams.BaseColor,
            antialiasing,
            format);
    }

    private static RenderFormat DetermineRenderFormat(WebGpuTextureFormat format) => format switch
    {
        WebGpuTextureFormat.Bgra8Unorm or WebGpuTextureFormat.Bgra8UnormSrgb => RenderFormat.Bgra8,
        WebGpuTextureFormat.Rgba8Unorm or WebGpuTextureFormat.Rgba8UnormSrgb => RenderFormat.Rgba8,
        _ => RenderFormat.Rgba8,
    };

    private bool HandleAcquirePresentFailure(Exception exception)
    {
        var message = exception.Message;
        if (string.Equals(message, _lastAcquireError, StringComparison.Ordinal))
        {
            return true;
        }

        _lastAcquireError = message;
        _hasReportedReady = false;
        Debug.WriteLine($"[Vello][Browser] Surface acquire/present failed: {message}");
        VelloBrowserDiagnostics.ReportAvailability(false, message);
        return true;
    }

    private PixelSize ResolvePixelSize()
    {
        var logicalSize = _topLevel.ClientSize;
        var scaling = _topLevel.RenderScaling;

        var width = Math.Max(1, (int)Math.Round(logicalSize.Width * scaling));
        var height = Math.Max(1, (int)Math.Round(logicalSize.Height * scaling));
        return new PixelSize(width, height);
    }

    private static WebGpuPresentMode MapPresentMode(PresentMode presentMode) => presentMode switch
    {
        PresentMode.Fifo => WebGpuPresentMode.Fifo,
        PresentMode.Immediate => WebGpuPresentMode.Immediate,
        _ => WebGpuPresentMode.Auto,
    };

    private static WebGpuSurfaceDescriptor ResolveSurfaceDescriptor(ITopLevelImpl topLevel)
    {
        if (topLevel.Handle is JSObjectPlatformHandle jsHandle)
        {
            if (TryGetContainerId(jsHandle.Object, out var containerId))
            {
                var canvasId = $"canvas{containerId}";
                var selector = $"#{canvasId}";
                return WebGpuSurfaceDescriptor.FromCssSelector(selector);
            }
        }

        throw new NotSupportedException("Unable to resolve browser canvas identifier for the WebGPU surface.");
    }

    private static bool TryGetContainerId(JSObject jsObject, out string? containerId)
    {
        using var dataset = jsObject.GetPropertyAsJSObject("dataset");
        containerId = dataset?.GetPropertyAsString("containerId");
        if (!string.IsNullOrWhiteSpace(containerId))
        {
            return true;
        }

        return false;
    }
}




