using System;
using Avalonia;
using Avalonia.Controls.Platform;
using Avalonia.Platform;
using VelloSharp;
using System.Runtime.Versioning;
using WebGpuSurfaceDescriptor = VelloSharp.WebGpuRuntime.WebGpuSurfaceDescriptor;
using WebGpuTextureFormat = VelloSharp.WebGpuRuntime.WebGpuTextureFormat;
using WindowResizeReason = Avalonia.Controls.WindowResizeReason;

namespace VelloSharp.Avalonia.Browser;

[SupportedOSPlatform("browser")]
internal sealed class VelloBrowserSwapChainSurface : IDisposable
{
    private readonly ITopLevelImpl _topLevel;
    private readonly WebGpuSurfaceDescriptor _descriptor;
    private readonly object _syncRoot = new();
    private uint _surfaceHandle;
    private bool _disposed;
    private float _lastLogicalWidth = -1f;
    private float _lastLogicalHeight = -1f;
    private float _lastDevicePixelRatio = -1f;
    private uint _activeTextureHandle;

    public VelloBrowserSwapChainSurface(ITopLevelImpl topLevel, WebGpuSurfaceDescriptor descriptor)
    {
        _topLevel = topLevel ?? throw new ArgumentNullException(nameof(topLevel));
        _descriptor = descriptor;
        _surfaceHandle = WebGpuRuntime.CreateSurface(descriptor);

        SubscribeTopLevelCallbacks();
        SyncCanvasSize(_topLevel.ClientSize, _topLevel.RenderScaling);
    }

    public uint Handle => _surfaceHandle;
    public WebGpuSurfaceDescriptor Descriptor => _descriptor;

    public WebGpuTextureFormat SurfaceFormat
    {
        get
        {
            uint surfaceHandle;
            lock (_syncRoot)
            {
                ThrowIfDisposed();
                surfaceHandle = EnsureSurfaceHandle();
            }

            return WebGpuRuntime.GetSurfaceTextureFormat(surfaceHandle);
        }
    }

    public static VelloBrowserSwapChainSurface FromCanvasId(ITopLevelImpl topLevel, string canvasId) =>
        new(topLevel, WebGpuSurfaceDescriptor.FromCanvasId(canvasId));

    public static VelloBrowserSwapChainSurface FromCssSelector(ITopLevelImpl topLevel, string selector) =>
        new(topLevel, WebGpuSurfaceDescriptor.FromCssSelector(selector));

    public void ForceSync() => SyncCanvasSize(_topLevel.ClientSize, _topLevel.RenderScaling);

    internal void Suspend()
    {
        uint surfaceHandle = 0;
        uint textureHandle = 0;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            textureHandle = _activeTextureHandle;
            _activeTextureHandle = 0;
            surfaceHandle = _surfaceHandle;
            _surfaceHandle = 0;
            _lastLogicalWidth = -1f;
            _lastLogicalHeight = -1f;
            _lastDevicePixelRatio = -1f;
        }

        if (textureHandle != 0)
        {
            WebGpuRuntime.DestroySurfaceTexture(textureHandle);
        }

        if (surfaceHandle != 0)
        {
            WebGpuRuntime.DestroySurface(surfaceHandle);
        }
    }

    internal void Resume()
    {
        var shouldSync = false;

        lock (_syncRoot)
        {
            ThrowIfDisposed();

            if (_surfaceHandle != 0)
            {
                return;
            }

            _surfaceHandle = WebGpuRuntime.CreateSurface(_descriptor);
            _lastLogicalWidth = -1f;
            _lastLogicalHeight = -1f;
            _lastDevicePixelRatio = -1f;
            shouldSync = true;
        }

        if (shouldSync)
        {
            SyncCanvasSize(_topLevel.ClientSize, _topLevel.RenderScaling);
        }
    }

    public VelloBrowserSurfaceTexture AcquireNextTexture()
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            var surfaceHandle = EnsureSurfaceHandle();

            if (_activeTextureHandle != 0)
            {
                throw new InvalidOperationException("A surface texture is already in flight for this surface.");
            }

            var textureHandle = WebGpuRuntime.AcquireSurfaceTexture(surfaceHandle);
            _activeTextureHandle = textureHandle;
            return new VelloBrowserSurfaceTexture(this, textureHandle);
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    ~VelloBrowserSwapChainSurface() => Dispose(disposing: false);

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        uint surfaceHandle = 0;
        uint textureHandle = 0;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            textureHandle = _activeTextureHandle;
            _activeTextureHandle = 0;
            surfaceHandle = _surfaceHandle;
            _surfaceHandle = 0;
        }

        if (disposing)
        {
            UnsubscribeTopLevelCallbacks();
        }

        if (textureHandle != 0)
        {
            WebGpuRuntime.DestroySurfaceTexture(textureHandle);
        }

        if (surfaceHandle != 0)
        {
            WebGpuRuntime.DestroySurface(surfaceHandle);
        }
    }

    private void SyncCanvasSize(Size logicalSize, double scaling)
    {
        uint surfaceHandle;
        float logicalWidth;
        float logicalHeight;
        float devicePixelRatio;

        lock (_syncRoot)
        {
            if (_disposed || _surfaceHandle == 0)
            {
                return;
            }

            if (logicalSize.Width <= 0 || logicalSize.Height <= 0 || scaling <= 0)
            {
                return;
            }

            logicalWidth = (float)logicalSize.Width;
            logicalHeight = (float)logicalSize.Height;
            devicePixelRatio = (float)scaling;

            if (!IsResizeRequired(logicalWidth, logicalHeight, devicePixelRatio))
            {
                return;
            }

            surfaceHandle = _surfaceHandle;
            _lastLogicalWidth = logicalWidth;
            _lastLogicalHeight = logicalHeight;
            _lastDevicePixelRatio = devicePixelRatio;
        }

        if (surfaceHandle == 0)
        {
            return;
        }

        WebGpuRuntime.ResizeSurfaceCanvas(surfaceHandle, logicalWidth, logicalHeight, devicePixelRatio);
    }

    private static bool RequiresUpdate(float current, float previous, float epsilon) =>
        MathF.Abs(current - previous) > epsilon;

    private bool IsResizeRequired(float width, float height, float devicePixelRatio)
    {
        const float SizeEpsilon = 0.05f;
        const float ScaleEpsilon = 0.005f;

        return RequiresUpdate(width, _lastLogicalWidth, SizeEpsilon)
            || RequiresUpdate(height, _lastLogicalHeight, SizeEpsilon)
            || RequiresUpdate(devicePixelRatio, _lastDevicePixelRatio, ScaleEpsilon);
    }

    private void SubscribeTopLevelCallbacks()
    {
        _topLevel.Resized = (Action<Size, WindowResizeReason>?)Delegate.Combine(
            _topLevel.Resized,
            (Action<Size, WindowResizeReason>)OnResized);

        _topLevel.ScalingChanged = (Action<double>?)Delegate.Combine(
            _topLevel.ScalingChanged,
            (Action<double>)OnScalingChanged);
    }

    private void UnsubscribeTopLevelCallbacks()
    {
        _topLevel.Resized = (Action<Size, WindowResizeReason>?)Delegate.Remove(
            _topLevel.Resized,
            (Action<Size, WindowResizeReason>)OnResized);

        _topLevel.ScalingChanged = (Action<double>?)Delegate.Remove(
            _topLevel.ScalingChanged,
            (Action<double>)OnScalingChanged);
    }

    private uint EnsureSurfaceHandle()
    {
        if (_surfaceHandle == 0)
        {
            throw new InvalidOperationException("The WebGPU surface has already been released.");
        }

        return _surfaceHandle;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VelloBrowserSwapChainSurface));
        }
    }

    private void OnResized(Size size, WindowResizeReason _)
    {
        SyncCanvasSize(size, _topLevel.RenderScaling);
    }

    private void OnScalingChanged(double scaling)
    {
        SyncCanvasSize(_topLevel.ClientSize, scaling);
    }

    internal void OnTexturePresented(uint textureHandle)
    {
        uint surfaceHandle;
        lock (_syncRoot)
        {
            ThrowIfDisposed();

            surfaceHandle = EnsureSurfaceHandle();

            if (_activeTextureHandle != textureHandle)
            {
                throw new InvalidOperationException("The provided texture handle does not match the active swapchain frame.");
            }
        }

        var presented = false;
        try
        {
            WebGpuRuntime.PresentSurfaceTexture(surfaceHandle, textureHandle);
            presented = true;
        }
        finally
        {
            lock (_syncRoot)
            {
                if (presented || _activeTextureHandle != textureHandle)
                {
                    _activeTextureHandle = 0;
                }
            }
        }
    }

    internal void OnTextureReleased(uint textureHandle)
    {
        var shouldRelease = false;

        lock (_syncRoot)
        {
            if (_activeTextureHandle == textureHandle)
            {
                _activeTextureHandle = 0;
                shouldRelease = true;
            }
        }

        if (shouldRelease && textureHandle != 0)
        {
            WebGpuRuntime.DestroySurfaceTexture(textureHandle);
        }
    }
}
