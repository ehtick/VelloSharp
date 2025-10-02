using System;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Threading;
using Avalonia.Winit;
using VelloSharp;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class VelloSwapchainRenderTarget : IRenderTarget2
{
    private readonly VelloGraphicsDevice _graphicsDevice;
    private readonly VelloPlatformOptions _options;
    private readonly IVelloWinitSurfaceProvider _surfaceProvider;
    private readonly object _syncRoot = new();

    private readonly PresentMode _presentMode;
    private static readonly RenderTargetProperties s_properties = new()
    {
        RetainsPreviousFrameContents = true,
        IsSuitableForDirectRendering = true,
    };

    private WgpuSurface? _wgpuSurface;
    private SurfaceHandle? _surfaceHandle;
    private WgpuSurfaceConfiguration _surfaceConfiguration;
    private WgpuTextureFormat _surfaceFormat;
    private bool _surfaceConfigured;
    private bool _requiresSurfaceBlit;
    private bool _disposed;
    private WgpuInstance? _currentInstance;
    private WgpuAdapter? _currentAdapter;
    private WgpuDevice? _currentDevice;
    private bool _surfaceCreationPending;
    private SurfaceDescriptor _pendingSurfaceDescriptor;
    private bool _hasPendingSurfaceDescriptor;
    private WgpuInstance? _pendingSurfaceInstance;
    private Exception? _surfaceCreationError;
    private long _surfaceCreationRequestId;

    public VelloSwapchainRenderTarget(
        VelloGraphicsDevice graphicsDevice,
        VelloPlatformOptions options,
        IVelloWinitSurfaceProvider surfaceProvider)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _surfaceProvider = surfaceProvider ?? throw new ArgumentNullException(nameof(surfaceProvider));
        _presentMode = options.PresentMode;
    }

    public bool IsCorrupted => false;

    public RenderTargetProperties Properties => s_properties;

    public IDrawingContextImpl CreateDrawingContext(bool useScaledDrawing)
    {
        var pixelSize = ClampSize(_surfaceProvider.SurfacePixelSize);
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
                throw new ObjectDisposedException(nameof(VelloSwapchainRenderTarget));
            }
        }

        var clampedSize = ResolvePixelSize(expectedPixelSize);
        var scene = new Scene();
        scene.Reset();

        properties = new RenderTargetDrawingContextProperties
        {
            PreviousFrameIsRetained = false,
        };

        return new VelloDrawingContextImpl(
            scene,
            clampedSize,
            _options,
            OnContextCompleted,
            skipInitialClip: !properties.PreviousFrameIsRetained,
            graphicsDevice: _graphicsDevice);
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
            DisposeSurface_NoLock();
        }
    }

    private void OnContextCompleted(VelloDrawingContextImpl context)
    {
        try
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                var resources = _graphicsDevice.Acquire(_options.RendererOptions);
                var surfaceCallbacks = context.TakeWgpuSurfaceRenderCallbacks();
                if (!EnsureSurface(resources, context.RenderParams))
                {
                    return;
                }

                RenderScene(resources, context.Scene, context.RenderParams, surfaceCallbacks);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Vello] Swapchain rendering failed: {ex}");
            lock (_syncRoot)
            {
                DisposeSurface_NoLock();
            }

            _surfaceProvider.RequestRedraw();
        }
        finally
        {
            context.Scene.Dispose();
        }
    }

    private bool EnsureSurface(
        (WgpuInstance Instance, WgpuAdapter Adapter, WgpuDevice Device, WgpuQueue Queue, WgpuRenderer Renderer) resources,
        RenderParams renderParams)
    {
        var width = Math.Max(1u, renderParams.Width);
        var height = Math.Max(1u, renderParams.Height);

        if (width == 0 || height == 0)
        {
            return false;
        }

        var surfaceHandle = _surfaceProvider.CreateSurfaceHandle();

        var descriptor = new SurfaceDescriptor
        {
            Width = width,
            Height = height,
            PresentMode = _presentMode,
            Handle = surfaceHandle,
        };

        if (OperatingSystem.IsMacOS() && _surfaceCreationPending)
        {
            if (PendingSurfaceMatches(resources.Instance, descriptor))
            {
                return false;
            }
        }

        if (!_surfaceHandle.HasValue ||
            !_surfaceHandle.Value.Equals(surfaceHandle) ||
            !ReferenceEquals(_currentInstance, resources.Instance))
        {
            if (OperatingSystem.IsMacOS())
            {
                DisposeSurface_NoLock();
                ScheduleSurfaceCreation(resources.Instance, descriptor);
                return false;
            }

            DisposeSurface_NoLock();

            _wgpuSurface = WgpuSurface.Create(resources.Instance, descriptor);
            _surfaceHandle = surfaceHandle;
            _currentInstance = resources.Instance;
            _currentAdapter = resources.Adapter;
            _currentDevice = resources.Device;
            _surfaceConfigured = false;
            _surfaceFormat = default;
            _requiresSurfaceBlit = false;
            _surfaceConfiguration = default;
        }

        if (_surfaceCreationPending)
        {
            return false;
        }

        if (_surfaceCreationError is Exception creationError)
        {
            _surfaceCreationError = null;
            throw new InvalidOperationException("Failed to create wgpu surface.", creationError);
        }

        if (!ReferenceEquals(_currentAdapter, resources.Adapter) ||
            !ReferenceEquals(_currentDevice, resources.Device))
        {
            _surfaceConfigured = false;
            _currentAdapter = resources.Adapter;
            _currentDevice = resources.Device;
        }

        if (_wgpuSurface is null)
        {
            return false;
        }

        if (!_surfaceConfigured ||
            _surfaceConfiguration.Width != width ||
            _surfaceConfiguration.Height != height ||
            _surfaceConfiguration.PresentMode != _presentMode)
        {
            var preferredFormat = _wgpuSurface.GetPreferredFormat(resources.Adapter);
            _surfaceFormat = NormalizeSurfaceFormat(preferredFormat);
            _requiresSurfaceBlit = RequiresSurfaceBlit(preferredFormat);
            _surfaceConfiguration = new WgpuSurfaceConfiguration
            {
                Usage = WgpuTextureUsage.RenderAttachment,
                Format = _surfaceFormat,
                Width = width,
                Height = height,
                PresentMode = _presentMode,
                AlphaMode = WgpuCompositeAlphaMode.Auto,
                ViewFormats = null,
            };

            _wgpuSurface.Configure(resources.Device, _surfaceConfiguration);
            _surfaceConfigured = true;
        }

        return true;
    }

    private void RenderScene(
        (WgpuInstance Instance, WgpuAdapter Adapter, WgpuDevice Device, WgpuQueue Queue, WgpuRenderer Renderer) resources,
        Scene scene,
        RenderParams renderParams,
        IReadOnlyList<Action<WgpuSurfaceRenderContext>>? surfaceCallbacks)
    {
        if (_wgpuSurface is null)
        {
            return;
        }

        _surfaceProvider.PrePresent();

        WgpuSurfaceTexture? surfaceTexture = null;
        try
        {
            surfaceTexture = _wgpuSurface.AcquireNextTexture();
            using var textureView = surfaceTexture.CreateView();

            var adjustedParams = AdjustRenderParams(renderParams);

            if (_requiresSurfaceBlit)
            {
                resources.Renderer.RenderSurface(scene, textureView, adjustedParams, _surfaceFormat);
            }
            else
            {
                resources.Renderer.Render(scene, textureView, adjustedParams);
            }

            if (surfaceCallbacks is { Count: > 0 })
            {
                var callbackContext = new WgpuSurfaceRenderContext(
                    resources.Device,
                    resources.Queue,
                    textureView,
                    adjustedParams,
                    _surfaceFormat);

                foreach (var callback in surfaceCallbacks)
                {
                    try
                    {
                        callback(callbackContext);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Vello] WGPU surface callback failed: {ex}");
                    }
                }
            }

            surfaceTexture.Present();
        }
        finally
        {
            surfaceTexture?.Dispose();
        }
    }

    private RenderParams AdjustRenderParams(RenderParams renderParams)
    {
        var format = _requiresSurfaceBlit
            ? RenderFormat.Rgba8
            : DetermineRenderFormat(_surfaceFormat);
        var antialiasing = AntialiasingMode.Area;

        return new RenderParams(renderParams.Width, renderParams.Height, renderParams.BaseColor, antialiasing, format);
    }

    private static RenderFormat DetermineRenderFormat(WgpuTextureFormat format) => format switch
    {
        WgpuTextureFormat.Rgba8Unorm or WgpuTextureFormat.Rgba8UnormSrgb => RenderFormat.Rgba8,
        WgpuTextureFormat.Bgra8Unorm or WgpuTextureFormat.Bgra8UnormSrgb => RenderFormat.Bgra8,
        _ => RenderFormat.Bgra8,
    };

    private static bool RequiresSurfaceBlit(WgpuTextureFormat format) => format switch
    {
        WgpuTextureFormat.Rgba8Unorm => false,
        _ => true,
    };

    private static WgpuTextureFormat NormalizeSurfaceFormat(WgpuTextureFormat format) => format switch
    {
        WgpuTextureFormat.Rgba8UnormSrgb => WgpuTextureFormat.Rgba8Unorm,
        WgpuTextureFormat.Bgra8UnormSrgb => WgpuTextureFormat.Bgra8Unorm,
        _ => format,
    };

    private bool PendingSurfaceMatches(WgpuInstance instance, in SurfaceDescriptor descriptor)
    {
        if (!_hasPendingSurfaceDescriptor)
        {
            return false;
        }

        return ReferenceEquals(_pendingSurfaceInstance, instance)
            && _pendingSurfaceDescriptor.Width == descriptor.Width
            && _pendingSurfaceDescriptor.Height == descriptor.Height
            && _pendingSurfaceDescriptor.PresentMode == descriptor.PresentMode
            && _pendingSurfaceDescriptor.Handle.Equals(descriptor.Handle);
    }

    private void ScheduleSurfaceCreation(WgpuInstance instance, SurfaceDescriptor descriptor)
    {
        _surfaceCreationPending = true;
        _surfaceCreationError = null;
        _hasPendingSurfaceDescriptor = true;
        _pendingSurfaceDescriptor = descriptor;
        _pendingSurfaceInstance = instance;
        var requestId = ++_surfaceCreationRequestId;

        Dispatcher.UIThread.Post(() =>
        {
            WgpuSurface? createdSurface = null;
            Exception? error = null;

            try
            {
                createdSurface = WgpuSurface.Create(instance, descriptor);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            lock (_syncRoot)
            {
                if (_disposed || requestId != _surfaceCreationRequestId)
                {
                    createdSurface?.Dispose();
                    return;
                }

                if (error is null && createdSurface is not null)
                {
                    _wgpuSurface = createdSurface;
                    _surfaceHandle = descriptor.Handle;
                    _currentInstance = instance;
                    _currentAdapter = null;
                    _currentDevice = null;
                    _surfaceConfigured = false;
                    _surfaceFormat = default;
                    _requiresSurfaceBlit = false;
                    _surfaceConfiguration = default;
                }
                else
                {
                    createdSurface?.Dispose();
                    _surfaceCreationError = error;
                }

                _surfaceCreationPending = false;
                _hasPendingSurfaceDescriptor = false;
                _pendingSurfaceInstance = null;
            }

            if (error is not null)
            {
                Debug.WriteLine($"[Vello] Failed to create wgpu surface: {error}");
            }

            _surfaceProvider.RequestRedraw();
        });
    }

    private void DisposeSurface_NoLock()
    {
        _wgpuSurface?.Dispose();
        _wgpuSurface = null;
        _surfaceHandle = null;
        _surfaceConfigured = false;
        _currentInstance = null;
        _currentAdapter = null;
        _currentDevice = null;
        _surfaceCreationPending = false;
        _hasPendingSurfaceDescriptor = false;
        _pendingSurfaceInstance = null;
        _surfaceCreationError = null;
        _surfaceConfiguration = default;
        _surfaceFormat = default;
        _requiresSurfaceBlit = false;
        _surfaceCreationRequestId++;
    }

    private PixelSize ResolvePixelSize(PixelSize expected)
    {
        if (expected.Width > 0 && expected.Height > 0)
        {
            return ClampSize(expected);
        }

        return ClampSize(_surfaceProvider.SurfacePixelSize);
    }

    private static PixelSize ClampSize(PixelSize size)
    {
        return new PixelSize(
            Math.Max(1, size.Width),
            Math.Max(1, size.Height));
    }
}
