using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using VelloSharp;
using VelloSharp.Rendering;

namespace VelloSharp.Integration.Avalonia;

public class VelloView : Control, IDisposable
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private RendererOptions _rendererOptions = new();
    private RenderParams _renderParams = new RenderParams(1, 1, RgbaColor.FromBytes(0, 0, 0, 0))
    {
        Format = RenderFormat.Bgra8,
    };
    private Renderer? _renderer;
    private Scene? _scene = new();
    private WriteableBitmap? _bitmap;
    private uint _rendererWidth = 1;
    private uint _rendererHeight = 1;
    private TimeSpan _lastFrameTimestamp = TimeSpan.Zero;
    private bool _isLoopEnabled = true;
    private bool _animationFrameRequested;
    private bool _disposed;

    public VelloView()
    {
        ClipToBounds = true;
    }

    public event Action<VelloRenderFrameContext>? RenderFrame;

    public RendererOptions RendererOptions
    {
        get => _rendererOptions;
        set
        {
            _rendererOptions = value;
            RecreateRenderer();
        }
    }

    public RenderParams RenderParameters
    {
        get => _renderParams;
        set => _renderParams = value;
    }

    public bool IsLoopEnabled
    {
        get => _isLoopEnabled;
        set
        {
            if (value == _isLoopEnabled)
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
        }
    }

    public void RequestRender()
    {
        if (!_disposed)
        {
            InvalidateVisual();
        }
    }

    protected virtual void OnRenderFrame(VelloRenderFrameContext context) => RenderFrame?.Invoke(context);

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_disposed || _scene is null)
        {
            return;
        }

        var scaling = VisualRoot?.RenderScaling ?? 1.0;
        var width = Math.Max(1, (int)Math.Ceiling(Bounds.Width * scaling));
        var height = Math.Max(1, (int)Math.Ceiling(Bounds.Height * scaling));

        if (width <= 0 || height <= 0)
        {
            return;
        }

        var widthU = (uint)width;
        var heightU = (uint)height;

        EnsureRenderer(widthU, heightU);
        EnsureBitmap(width, height, scaling);

        if (_renderer is null || _bitmap is null)
        {
            return;
        }

        _scene.Reset();

        var now = _stopwatch.Elapsed;
        var delta = _lastFrameTimestamp == TimeSpan.Zero ? TimeSpan.Zero : now - _lastFrameTimestamp;
        _lastFrameTimestamp = now;

        var frameContext = new VelloRenderFrameContext(_scene, widthU, heightU, scaling, delta, now);
        OnRenderFrame(frameContext);

        using var frame = _bitmap.Lock();
        unsafe
        {
            var descriptor = new RenderTargetDescriptor((uint)frame.Size.Width, (uint)frame.Size.Height, RenderFormat.Bgra8, frame.RowBytes);
            var span = new Span<byte>((void*)frame.Address, descriptor.RequiredBufferSize);
            VelloRenderPath.Render(_renderer, _scene, span, _renderParams, descriptor);
        }

        var sourceRect = new Rect(0, 0, _bitmap.PixelSize.Width, _bitmap.PixelSize.Height);
        context.DrawImage(_bitmap, sourceRect, Bounds);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _isLoopEnabled = false;
        _animationFrameRequested = false;

        _bitmap?.Dispose();
        _bitmap = null;

        _scene?.Dispose();
        _scene = null;

        _renderer?.Dispose();
        _renderer = null;

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Dispose();
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

    private void RecreateRenderer()
    {
        _renderer?.Dispose();
        _renderer = null;
        _rendererWidth = _rendererHeight = 1;
    }

    private void EnsureRenderer(uint width, uint height)
    {
        if (_renderer is null)
        {
            _renderer = new Renderer(width, height, _rendererOptions);
            _rendererWidth = width;
            _rendererHeight = height;
            return;
        }

        if (_rendererWidth != width || _rendererHeight != height)
        {
            _renderer.Resize(width, height);
            _rendererWidth = width;
            _rendererHeight = height;
        }
    }

    private void EnsureBitmap(int width, int height, double scaling)
    {
        if (_bitmap is { PixelSize.Width: var w, PixelSize.Height: var h } && w == width && h == height)
        {
            return;
        }

        _bitmap?.Dispose();

        var dpi = 96.0 * scaling;
        _bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(dpi, dpi),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
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
}
