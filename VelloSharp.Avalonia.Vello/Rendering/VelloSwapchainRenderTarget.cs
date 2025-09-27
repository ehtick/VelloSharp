using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Winit;
using VelloSharp;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class VelloSwapchainRenderTarget : IRenderTargetWithProperties
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
        return CreateDrawingContext(useScaledDrawing, out _);
    }

    public IDrawingContextImpl CreateDrawingContext(
        bool useScaledDrawing,
        out RenderTargetDrawingContextProperties properties)
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(VelloSwapchainRenderTarget));
            }
        }

        var clampedSize = ClampSize(_surfaceProvider.SurfacePixelSize);
        var scene = new Scene();
        scene.Reset();

        properties = new RenderTargetDrawingContextProperties
        {
            PreviousFrameIsRetained = false,
        };

        return new VelloDrawingContextImpl(scene, clampedSize, _options, OnContextCompleted);
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
                if (!EnsureSurface(resources, context.RenderParams))
                {
                    return;
                }

                RenderScene(resources, context.Scene, context.RenderParams);
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

        if (!_surfaceHandle.HasValue ||
            !_surfaceHandle.Value.Equals(surfaceHandle) ||
            !ReferenceEquals(_currentInstance, resources.Instance))
        {
            DisposeSurface_NoLock();

            var descriptor = new SurfaceDescriptor
            {
                Width = width,
                Height = height,
                PresentMode = _presentMode,
                Handle = surfaceHandle,
            };

            _wgpuSurface = WgpuSurface.Create(resources.Instance, descriptor);
            _surfaceHandle = surfaceHandle;
            _currentInstance = resources.Instance;
            _currentAdapter = resources.Adapter;
            _currentDevice = resources.Device;
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
            _surfaceFormat = _wgpuSurface.GetPreferredFormat(resources.Adapter);
            _requiresSurfaceBlit = RequiresSurfaceBlit(_surfaceFormat);
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
        RenderParams renderParams)
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
        var antialiasing = _options.ResolveAntialiasing(renderParams.Antialiasing);

        return renderParams with
        {
            Format = format,
            Antialiasing = antialiasing,
        };
    }

    private static RenderFormat DetermineRenderFormat(WgpuTextureFormat format) => format switch
    {
        WgpuTextureFormat.Rgba8Unorm or WgpuTextureFormat.Rgba8UnormSrgb => RenderFormat.Rgba8,
        WgpuTextureFormat.Bgra8Unorm or WgpuTextureFormat.Bgra8UnormSrgb => RenderFormat.Bgra8,
        _ => RenderFormat.Bgra8,
    };

    private static bool RequiresSurfaceBlit(WgpuTextureFormat format) => format switch
    {
        WgpuTextureFormat.Rgba8Unorm or WgpuTextureFormat.Rgba8UnormSrgb => false,
        _ => true,
    };

    private void DisposeSurface_NoLock()
    {
        _wgpuSurface?.Dispose();
        _wgpuSurface = null;
        _surfaceHandle = null;
        _surfaceConfigured = false;
        _currentInstance = null;
        _currentAdapter = null;
        _currentDevice = null;
    }

    private static PixelSize ClampSize(PixelSize size)
    {
        return new PixelSize(
            Math.Max(1, size.Width),
            Math.Max(1, size.Height));
    }
}
