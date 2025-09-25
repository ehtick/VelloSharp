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
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private RendererOptions _rendererOptions = new();
    private RenderParams _renderParams = new RenderParams(1, 1, RgbaColor.FromBytes(0, 0, 0));
    private VelloSurfaceContext? _context;
    private VelloSurface? _surface;
    private VelloSurfaceRenderer? _renderer;
    private Scene? _scene = new();
    private uint _surfaceWidth = 1;
    private uint _surfaceHeight = 1;
    private TimeSpan _lastFrameTimestamp = TimeSpan.Zero;
    private bool _disposed;
    private bool _useFallback;
    private VelloView? _fallback;

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
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTimerTick;
        _timer.Start();
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
        get => _timer.IsEnabled;
        set
        {
            if (value)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }
            if (_fallback is not null)
            {
                _fallback.IsLoopEnabled = value;
            }
        }
    }

    public TimeSpan FrameInterval
    {
        get => _timer.Interval;
        set
        {
            var interval = value > TimeSpan.Zero ? value : TimeSpan.FromMilliseconds(16);
            _timer.Interval = interval;
            if (_fallback is not null)
            {
                _fallback.FrameInterval = interval;
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

        if (_useFallback)
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

        if (_renderer is null || _surface is null)
        {
            if (!TryEnsureGpu(width, height))
            {
                EnsureFallback();
                return;
            }
        }

        if (_renderer is null || _surface is null)
        {
            return;
        }

        if (width != _surfaceWidth || height != _surfaceHeight)
        {
            try
            {
                _surface.Resize(width, height);
                _surfaceWidth = width;
                _surfaceHeight = height;
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

        try
        {
            var requestedAa = _renderParams.Antialiasing;
            var renderParams = _renderParams with
            {
                Width = width,
                Height = height,
                Antialiasing = AntialiasingMode.Area,
            };

            if (requestedAa != AntialiasingMode.Area)
            {
                Debug.WriteLine("VelloSurfaceView: forcing AntialiasingMode.Area for GPU rendering.");
            }

            _renderer.Render(_surface, scene, renderParams);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"VelloSurface render failed: {ex.Message}");
            EnsureFallback();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RequestRender();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Dispose();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            RequestRender();
        }
    }

    private bool TryEnsureGpu(uint width, uint height)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var platformHandle = topLevel?.TryGetPlatformHandle();
        if (platformHandle is null)
        {
            return false;
        }

        if (!TryCreateWindowHandle(platformHandle, out var handle))
        {
            return false;
        }

        try
        {
            _context ??= new VelloSurfaceContext();
            _surface = new VelloSurface(_context, new SurfaceDescriptor
            {
                Width = width,
                Height = height,
                PresentMode = PresentMode.AutoVsync,
                Handle = handle,
            });
            _renderer = new VelloSurfaceRenderer(_surface, _rendererOptions);
            _surfaceWidth = width;
            _surfaceHeight = height;
            _useFallback = false;

            PostContentUpdate(() =>
            {
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
            });

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize Vello surface: {ex.Message}");
            DisposeGpuResources();
            return false;
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
            fallback.FrameInterval = FrameInterval;
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
        if (_renderer is null || _surface is null)
        {
            return;
        }

        try
        {
            _renderer.Dispose();
            _renderer = new VelloSurfaceRenderer(_surface, _rendererOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to recreate renderer: {ex.Message}");
            EnsureFallback();
        }
    }

    private void DisposeGpuResources()
    {
        _renderer?.Dispose();
        _renderer = null;
        _surface?.Dispose();
        _surface = null;
        _context?.Dispose();
        _context = null;
        _surfaceWidth = 1;
        _surfaceHeight = 1;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTimerTick;

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
