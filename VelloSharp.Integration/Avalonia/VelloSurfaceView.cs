using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;

namespace VelloSharp.Integration.Avalonia;

public class VelloSurfaceView : ContentControl, IDisposable
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private RendererOptions _rendererOptions = new();
    private RenderParams _renderParams = new RenderParams(1, 1, RgbaColor.FromBytes(0, 0, 0));
    private WgpuInstance? _wgpuInstance;
    private WgpuAdapter? _wgpuAdapter;
    private WgpuDevice? _wgpuDevice;
    private WgpuQueue? _wgpuQueue;
    private WgpuSurface? _wgpuSurface;
    private WgpuRenderer? _wgpuRenderer;
    private WgpuTextureFormat _surfaceFormat = WgpuTextureFormat.Bgra8Unorm;
    private Scene? _scene = new();
    private uint _surfaceWidth = 1;
    private uint _surfaceHeight = 1;
    private bool _surfaceConfigured;
    private TimeSpan _lastFrameTimestamp = TimeSpan.Zero;
    private bool _isLoopEnabled = true;
    private bool _animationFrameRequested;
    private bool _disposed;
    private bool _useFallback;
    private VelloView? _fallback;

    private enum GpuInitResult
    {
        Success,
        Pending,
        Failed,
    }

    private void PostContentUpdate(Action action)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed)
            {
                return;
            }

            action();
        }, DispatcherPriority.Background);
    }

    public VelloSurfaceView()
    {
        ClipToBounds = true;
    }

    public event Action<VelloRenderFrameContext>? RenderFrame;

    protected virtual void OnRenderFrame(VelloRenderFrameContext context) => RenderFrame?.Invoke(context);

    public RendererOptions RendererOptions
    {
        get => _rendererOptions;
        set
        {
            _rendererOptions = value;
            if (_useFallback)
            {
                if (_fallback is not null)
                {
                    _fallback.RendererOptions = value;
                }
                return;
            }
            RecreateRenderer();
        }
    }

    public RenderParams RenderParameters
    {
        get => _renderParams;
        set
        {
            _renderParams = value;
            if (_fallback is not null)
            {
                _fallback.RenderParameters = value;
            }
        }
    }

    public bool IsLoopEnabled
    {
        get => _isLoopEnabled;
        set
        {
            if (_isLoopEnabled == value)
            {
                return;
            }

            _isLoopEnabled = value;

            if (_isLoopEnabled)
            {
                ScheduleAnimationFrame();
            }
            else
            {
                _animationFrameRequested = false;
            }

            if (_fallback is not null)
            {
                _fallback.IsLoopEnabled = value;
            }
        }
    }

    public void RequestRender()
    {
        if (!_disposed)
        {
            InvalidateVisual();
            _fallback?.RequestRender();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_disposed)
        {
            return;
        }

        var scaling = VisualRoot?.RenderScaling ?? 1.0;
        var width = (uint)Math.Max(1, (int)Math.Ceiling(Bounds.Width * scaling));
        var height = (uint)Math.Max(1, (int)Math.Ceiling(Bounds.Height * scaling));
        if (width == 0 || height == 0)
        {
            return;
        }

        if (_wgpuRenderer is null || _wgpuSurface is null)
        {
            var gpuInit = TryEnsureGpu(width, height);
            switch (gpuInit)
            {
                case GpuInitResult.Pending:
                    return;
                case GpuInitResult.Failed:
                    EnsureFallback();
                    return;
            }
        }

        if (_wgpuRenderer is null || _wgpuSurface is null || _wgpuDevice is null)
        {
            return;
        }

        if (_useFallback)
        {
            return;
        }

        if (!_surfaceConfigured || width != _surfaceWidth || height != _surfaceHeight)
        {
            try
            {
                if (!ConfigureSurface(width, height))
                {
                    throw new InvalidOperationException("Surface configuration failed");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VelloSurface resize failed: {ex.Message}");
                EnsureFallback();
                return;
            }
        }

        var scene = _scene;
        if (scene is null)
        {
            return;
        }

        scene.Reset();

        var now = _stopwatch.Elapsed;
        var delta = _lastFrameTimestamp == TimeSpan.Zero ? TimeSpan.Zero : now - _lastFrameTimestamp;
        _lastFrameTimestamp = now;

        var frameContext = new VelloRenderFrameContext(scene, width, height, scaling, delta, now);
        OnRenderFrame(frameContext);

        WgpuSurfaceTexture? surfaceTexture = null;
        try
        {
            surfaceTexture = _wgpuSurface.AcquireNextTexture();
            using var textureView = surfaceTexture.CreateView();

            var renderParams = _renderParams with
            {
                Width = width,
                Height = height,
            };

            _wgpuRenderer.Render(scene, textureView, renderParams);
            surfaceTexture.Present();
            surfaceTexture = null;
        }
        catch (Exception ex)
        {
            surfaceTexture?.Dispose();
            Debug.WriteLine($"VelloSurface render failed: {ex.Message}");
            EnsureFallback();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RequestRender();
        if (_isLoopEnabled)
        {
            ScheduleAnimationFrame();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Dispose();
    }

    private void OnAnimationFrame(TimeSpan _)
    {
        _animationFrameRequested = false;

        if (_disposed || !_isLoopEnabled)
        {
            return;
        }

        RequestRender();
        ScheduleAnimationFrame();
    }

    private void ScheduleAnimationFrame()
    {
        if (_animationFrameRequested || !_isLoopEnabled || _disposed)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        _animationFrameRequested = true;
        topLevel.RequestAnimationFrame(OnAnimationFrame);
    }

    private GpuInitResult TryEnsureGpu(uint width, uint height)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var platformHandle = topLevel?.TryGetPlatformHandle();
        if (platformHandle is null)
        {
            return GpuInitResult.Pending;
        }

        if (platformHandle.Handle == IntPtr.Zero)
        {
            return GpuInitResult.Pending;
        }

        if (!TryCreateWindowHandle(platformHandle, out var handle))
        {
            return GpuInitResult.Failed;
        }

        try
        {
            _wgpuInstance ??= new WgpuInstance();
            _wgpuSurface ??= WgpuSurface.Create(_wgpuInstance, new SurfaceDescriptor
            {
                Width = width,
                Height = height,
                PresentMode = PresentMode.AutoVsync,
                Handle = handle,
            });

            _wgpuAdapter ??= _wgpuInstance.RequestAdapter(new WgpuRequestAdapterOptions
            {
                PowerPreference = WgpuPowerPreference.HighPerformance,
                CompatibleSurface = _wgpuSurface,
            });

            _wgpuDevice ??= _wgpuAdapter.RequestDevice(new WgpuDeviceDescriptor
            {
                Limits = WgpuLimitsPreset.Default,
                RequiredFeatures = WgpuFeature.None,
            });

            _wgpuQueue ??= _wgpuDevice.GetQueue();
            _wgpuRenderer ??= new WgpuRenderer(_wgpuDevice, _rendererOptions);

            if (!ConfigureSurface(width, height))
            {
                DisposeGpuResources();
                return GpuInitResult.Failed;
            }

            PostContentUpdate(() =>
            {
                var hadFallback = _fallback is not null || Content is VelloView;

                if (_fallback is not null)
                {
                    _fallback.RenderFrame -= OnFallbackRenderFrame;
                    _fallback.Dispose();
                    _fallback = null;
                }

                if (Content is VelloView)
                {
                    Content = null;
                }

                _useFallback = false;
                if (hadFallback)
                {
                    RequestRender();
                }
            });

            return GpuInitResult.Success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize Vello surface: {ex.Message}");
            DisposeGpuResources();
            return GpuInitResult.Failed;
        }
    }

    private static bool TryCreateWindowHandle(IPlatformHandle? platformHandle, out SurfaceHandle handle)
    {
        handle = default;
        if (platformHandle is null)
        {
            return false;
        }

        var descriptor = platformHandle.HandleDescriptor ?? string.Empty;
        if (descriptor.Equals("HWND", StringComparison.OrdinalIgnoreCase))
        {
            handle = SurfaceHandle.FromWin32(platformHandle.Handle);
            return true;
        }

        if (descriptor.Equals("NSView", StringComparison.OrdinalIgnoreCase))
        {
            handle = SurfaceHandle.FromAppKit(platformHandle.Handle);
            return true;
        }

        return false;
    }

    private bool ConfigureSurface(uint width, uint height)
    {
        if (_wgpuSurface is null || _wgpuDevice is null || _wgpuAdapter is null)
        {
            return false;
        }

        var format = _surfaceFormat;
        if (!_surfaceConfigured)
        {
            format = _wgpuSurface.GetPreferredFormat(_wgpuAdapter);
        }

        try
        {
            var config = new WgpuSurfaceConfiguration
            {
                Usage = WgpuTextureUsage.RenderAttachment,
                Format = format,
                Width = width,
                Height = height,
                PresentMode = PresentMode.AutoVsync,
                AlphaMode = WgpuCompositeAlphaMode.Auto,
                ViewFormats = Array.Empty<WgpuTextureFormat>(),
            };

            _wgpuSurface.Configure(_wgpuDevice, config);
            _surfaceWidth = width;
            _surfaceHeight = height;
            _surfaceFormat = format;
            _surfaceConfigured = true;

            var renderFormat = MapRenderFormat(format);
            RenderParameters = _renderParams with { Format = renderFormat };
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to configure wgpu surface: {ex.Message}");
            return false;
        }
    }

    private static RenderFormat MapRenderFormat(WgpuTextureFormat format)
        => format switch
        {
            WgpuTextureFormat.Bgra8Unorm => RenderFormat.Bgra8,
            WgpuTextureFormat.Bgra8UnormSrgb => RenderFormat.Bgra8,
            _ => RenderFormat.Rgba8,
        };

    private void EnsureFallback()
    {
        if (_useFallback)
        {
            return;
        }

        DisposeGpuResources();
        _useFallback = true;
        PostContentUpdate(() =>
        {
            if (_fallback is not null)
            {
                return;
            }

            var fallback = new VelloView
            {
                RendererOptions = _rendererOptions,
                RenderParameters = _renderParams,
            };
            fallback.IsLoopEnabled = IsLoopEnabled;
            fallback.RenderFrame += OnFallbackRenderFrame;
            _fallback = fallback;
            Content = fallback;
            fallback.RequestRender();
        });
    }

    private void OnFallbackRenderFrame(VelloRenderFrameContext context)
    {
        OnRenderFrame(context);
    }

    private void RecreateRenderer()
    {
        if (_wgpuDevice is null)
        {
            return;
        }

        try
        {
            _wgpuRenderer?.Dispose();
            _wgpuRenderer = new WgpuRenderer(_wgpuDevice, _rendererOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to recreate renderer: {ex.Message}");
            EnsureFallback();
        }
    }

    private void DisposeGpuResources()
    {
        _wgpuRenderer?.Dispose();
        _wgpuRenderer = null;
        _wgpuSurface?.Dispose();
        _wgpuSurface = null;
        _wgpuQueue?.Dispose();
        _wgpuQueue = null;
        _wgpuDevice?.Dispose();
        _wgpuDevice = null;
        _wgpuAdapter?.Dispose();
        _wgpuAdapter = null;
        _wgpuInstance?.Dispose();
        _wgpuInstance = null;
        _surfaceWidth = 1;
        _surfaceHeight = 1;
        _surfaceConfigured = false;
        _surfaceFormat = WgpuTextureFormat.Bgra8Unorm;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _isLoopEnabled = false;
        _animationFrameRequested = false;

        DisposeGpuResources();

        if (_fallback is not null)
        {
            _fallback.RenderFrame -= OnFallbackRenderFrame;
            _fallback.Dispose();
            _fallback = null;
        }

        _scene?.Dispose();
        _scene = null;

        Content = null;
        _useFallback = false;

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~VelloSurfaceView()
    {
        Dispose();
    }
}
