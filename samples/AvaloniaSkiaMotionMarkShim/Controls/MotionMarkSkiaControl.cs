extern alias VelloSkia;

using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaSkiaMotionMarkShim.Scenes;
using SKCanvas = VelloSkia::SkiaSharp.SKCanvas;
using SKColor = VelloSkia::SkiaSharp.SKColor;
using SKColors = VelloSkia::SkiaSharp.SKColors;
using SKRect = VelloSkia::SkiaSharp.SKRect;
using SKSurface = VelloSkia::SkiaSharp.SKSurface;
using SKImageInfo = VelloSkia::SkiaSharp.SKImageInfo;
using SKColorType = VelloSkia::SkiaSharp.SKColorType;
using SKAlphaType = VelloSkia::SkiaSharp.SKAlphaType;
using VelloSharp;
using VelloSharp.Integration.Rendering;

namespace AvaloniaSkiaMotionMarkShim.Controls;

public sealed class MotionMarkSkiaControl : Control
{
    public const int MinComplexity = 1;
    public const int MaxComplexity = 64;

    public static readonly StyledProperty<int> ComplexityProperty =
        AvaloniaProperty.Register<MotionMarkSkiaControl, int>(
            nameof(Complexity),
            MinComplexity,
            coerce: (_, value) => Math.Clamp(value, MinComplexity, MaxComplexity));

    public static readonly StyledProperty<bool> IsAnimationEnabledProperty =
        AvaloniaProperty.Register<MotionMarkSkiaControl, bool>(nameof(IsAnimationEnabled), true);

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly MotionMarkScene _scene = new();
    private TimeSpan _lastFrameTimestamp;
    private bool _animationFrameRequested;
    private Renderer? _renderer;
    private RenderParams _renderParams = new(1, 1, RgbaColor.FromBytes(0, 0, 0, 0))
    {
        Format = RenderFormat.Bgra8,
    };
    private WriteableBitmap? _bitmap;
    private uint _rendererWidth = 1;
    private uint _rendererHeight = 1;
    private SKSurface? _surface;
    private uint _surfaceWidth = 1;
    private uint _surfaceHeight = 1;
    private bool _disposed;

    static MotionMarkSkiaControl()
    {
        AffectsRender<MotionMarkSkiaControl>(ComplexityProperty, IsAnimationEnabledProperty);
    }

    public event EventHandler<FrameRenderedEventArgs>? FrameRendered;

    public int Complexity
    {
        get => GetValue(ComplexityProperty);
        set => SetValue(ComplexityProperty, value);
    }

    public bool IsAnimationEnabled
    {
        get => GetValue(IsAnimationEnabledProperty);
        set => SetValue(IsAnimationEnabledProperty, value);
    }

    public void IncreaseComplexity() => Complexity = Math.Min(Complexity + 1, MaxComplexity);
    public void DecreaseComplexity() => Complexity = Math.Max(Complexity - 1, MinComplexity);
    public void ResetComplexity() => Complexity = MinComplexity;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _disposed = false;
        if (IsAnimationEnabled)
        {
            ScheduleAnimationFrame();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animationFrameRequested = false;
        DisposeResources();
        _disposed = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsAnimationEnabledProperty && change.GetNewValue<bool>())
        {
            ScheduleAnimationFrame();
        }
        else if (change.Property == ComplexityProperty && !IsAnimationEnabled)
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_disposed)
        {
            return;
        }

        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var now = _stopwatch.Elapsed;
        var delta = _lastFrameTimestamp == TimeSpan.Zero ? TimeSpan.Zero : now - _lastFrameTimestamp;
        _lastFrameTimestamp = now;

        var scaling = VisualRoot?.RenderScaling ?? 1.0;
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(width * scaling));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(height * scaling));

        EnsureRenderer((uint)pixelWidth, (uint)pixelHeight);
        EnsureSurface((uint)pixelWidth, (uint)pixelHeight);
        EnsureBitmap(pixelWidth, pixelHeight, scaling);

        if (_renderer is not null && _surface is not null && _bitmap is not null)
        {
            var canvas = _surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            canvas.Save();
            canvas.Scale((float)scaling);
            RenderSkia(canvas, width, height, Complexity);
            canvas.Restore();

            using var frame = _bitmap.Lock();
            var descriptor = new RenderTargetDescriptor(
                (uint)frame.Size.Width,
                (uint)frame.Size.Height,
                RenderFormat.Bgra8,
                frame.RowBytes);

            unsafe
            {
                var span = new Span<byte>((void*)frame.Address, descriptor.RequiredBufferSize);
                VelloRenderPath.Render(_renderer, _surface.Scene, span, _renderParams, descriptor);
            }

            var sourceRect = new Rect(0, 0, _bitmap.PixelSize.Width, _bitmap.PixelSize.Height);
            context.DrawImage(_bitmap, sourceRect, Bounds);
        }

        FrameRendered?.Invoke(this, new FrameRenderedEventArgs(delta.TotalMilliseconds));

        if (IsAnimationEnabled)
        {
            ScheduleAnimationFrame();
        }
    }

    private void RenderSkia(SKCanvas canvas, double width, double height, int complexity)
    {
        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, (float)width, (float)height));
        canvas.Clear(new SKColor(0x12, 0x12, 0x14));

        const float sceneWidth = 1600f;
        const float sceneHeight = 1200f;
        var scale = Math.Min((float)(width / sceneWidth), (float)(height / sceneHeight));
        if (!float.IsFinite(scale) || scale <= 0f)
        {
            scale = 1f;
        }

        var scaledWidth = sceneWidth * scale;
        var scaledHeight = sceneHeight * scale;
        var offsetX = (float)((width - scaledWidth) * 0.5f);
        var offsetY = (float)((height - scaledHeight) * 0.5f);

        canvas.Translate(offsetX, offsetY);
        canvas.Scale(scale);

        _scene.Render(canvas, complexity);
        canvas.Restore();
    }

    private void EnsureRenderer(uint width, uint height)
    {
        if (_renderer is null)
        {
            _renderer = new Renderer(width, height);
            _rendererWidth = width;
            _rendererHeight = height;
        }
        else if (_rendererWidth != width || _rendererHeight != height)
        {
            _renderer.Resize(width, height);
            _rendererWidth = width;
            _rendererHeight = height;
        }

        _renderParams = _renderParams with
        {
            Width = width,
            Height = height,
            Format = RenderFormat.Bgra8,
        };
    }

    private void EnsureSurface(uint width, uint height)
    {
        if (_surface is not null && _surfaceWidth == width && _surfaceHeight == height)
        {
            return;
        }

        _surface?.Dispose();
        var info = new SKImageInfo((int)width, (int)height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _surface = SKSurface.Create(info);
        _surfaceWidth = width;
        _surfaceHeight = height;
    }

    private void EnsureBitmap(int width, int height, double scaling)
    {
        if (_bitmap is not null &&
            _bitmap.PixelSize.Width == width &&
            _bitmap.PixelSize.Height == height)
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

    private void ScheduleAnimationFrame()
    {
        if (_animationFrameRequested || !IsAnimationEnabled || _disposed)
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

    private void OnAnimationFrame(TimeSpan _)
    {
        _animationFrameRequested = false;

        if (!IsAnimationEnabled || _disposed)
        {
            return;
        }

        InvalidateVisual();
        ScheduleAnimationFrame();
    }

    private void DisposeResources()
    {
        _bitmap?.Dispose();
        _bitmap = null;

        _surface?.Dispose();
        _surface = null;
        _surfaceWidth = 1;
        _surfaceHeight = 1;

        _renderer?.Dispose();
        _renderer = null;
        _rendererWidth = 1;
        _rendererHeight = 1;
    }

}

public sealed class FrameRenderedEventArgs : EventArgs
{
    public FrameRenderedEventArgs(double frameTimeMilliseconds)
    {
        FrameTimeMilliseconds = frameTimeMilliseconds;
    }

    public double FrameTimeMilliseconds { get; }
}
