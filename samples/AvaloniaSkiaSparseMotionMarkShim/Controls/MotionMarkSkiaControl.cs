extern alias VelloSkia;

using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using AvaloniaSkiaSparseMotionMarkShim.Scenes;
using VelloCanvas = VelloSkia::SkiaSharp.SKCanvas;
using VelloColor = VelloSkia::SkiaSharp.SKColor;
using VelloColors = VelloSkia::SkiaSharp.SKColors;
using VelloRect = VelloSkia::SkiaSharp.SKRect;
using VelloSurface = VelloSkia::SkiaSharp.SKSurface;
using VelloImageInfo = VelloSkia::SkiaSharp.SKImageInfo;
using VelloColorType = VelloSkia::SkiaSharp.SKColorType;
using VelloAlphaType = VelloSkia::SkiaSharp.SKAlphaType;
using SkiaGRContext = global::SkiaSharp.GRContext;
using SkiaSurface = global::SkiaSharp.SKSurface;
using SkiaImageInfo = global::SkiaSharp.SKImageInfo;
using SkiaColorType = global::SkiaSharp.SKColorType;
using SkiaAlphaType = global::SkiaSharp.SKAlphaType;
using SkiaRect = global::SkiaSharp.SKRect;
using VelloSharp;
using VelloSharp.Rendering;
using VelloSharp.Integration.Skia;

namespace AvaloniaSkiaSparseMotionMarkShim.Controls;

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
    private uint _rendererWidth = 1;
    private uint _rendererHeight = 1;
    private VelloSurface? _velloSurface;
    private uint _velloSurfaceWidth = 1;
    private uint _velloSurfaceHeight = 1;
    private SkiaSurface? _skiaSurface;
    private int _skiaSurfaceWidth;
    private int _skiaSurfaceHeight;
    private IntPtr _skiaSurfaceGrContextHandle;
    private int _currentComplexity = MinComplexity;
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
        else if (change.Property == ComplexityProperty)
        {
            _currentComplexity = change.GetNewValue<int>();
            if (!IsAnimationEnabled)
            {
                InvalidateVisual();
            }
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

        context.Custom(new VelloDrawOperation(new Rect(Bounds.Size), this));

        if (IsAnimationEnabled)
        {
            ScheduleAnimationFrame();
        }
    }

    private void RenderScene(VelloCanvas canvas, double width, double height, int complexity)
    {
        canvas.Save();
        canvas.ClipRect(new VelloRect(0, 0, (float)width, (float)height));
        canvas.Clear(new VelloColor(0x12, 0x12, 0x14));

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

    private void EnsureRenderer(uint width, uint height, RenderFormat format = RenderFormat.Bgra8)
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
            Format = format,
        };
    }

    private void EnsureVelloSurface(uint width, uint height)
    {
        if (_velloSurface is not null && _velloSurfaceWidth == width && _velloSurfaceHeight == height)
        {
            return;
        }

        _velloSurface?.Dispose();
        var info = new VelloImageInfo((int)width, (int)height, VelloColorType.Bgra8888, VelloAlphaType.Premul);
        _velloSurface = VelloSurface.Create(info);
        _velloSurfaceWidth = width;
        _velloSurfaceHeight = height;
    }

    private void EnsureSkiaSurface(SkiaGRContext? grContext, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            DisposeSkiaSurface();
            return;
        }

        var contextHandle = grContext?.Handle ?? IntPtr.Zero;
        if (_skiaSurface is not null &&
            _skiaSurfaceWidth == width &&
            _skiaSurfaceHeight == height &&
            _skiaSurfaceGrContextHandle == contextHandle)
        {
            return;
        }

        DisposeSkiaSurface();

        var info = new SkiaImageInfo(width, height, SkiaColorType.Bgra8888, SkiaAlphaType.Premul);
        _skiaSurface = grContext is null
            ? SkiaSurface.Create(info)
            : SkiaSurface.Create(grContext, false, info);

        _skiaSurfaceWidth = width;
        _skiaSurfaceHeight = height;
        _skiaSurfaceGrContextHandle = contextHandle;
    }

    private void DisposeSkiaSurface()
    {
        _skiaSurface?.Dispose();
        _skiaSurface = null;
        _skiaSurfaceWidth = 0;
        _skiaSurfaceHeight = 0;
        _skiaSurfaceGrContextHandle = IntPtr.Zero;
    }

    private sealed class VelloDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly MotionMarkSkiaControl _owner;

        public VelloDrawOperation(Rect bounds, MotionMarkSkiaControl owner)
        {
            _bounds = bounds;
            _owner = owner;
        }

        public Rect Bounds => _bounds;

        public void Dispose()
        {
        }

        public bool HitTest(Point p) => _bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);

        public void Render(ImmediateDrawingContext context)
        {
            if (!context.TryGetFeature<ISkiaSharpApiLeaseFeature>(out var feature) || feature is null)
            {
                return;
            }

            using var lease = feature.Lease();
            if (lease?.SkSurface is null)
            {
                return;
            }

            var owner = _owner;
            var bounds = _bounds;
            var width = bounds.Width;
            var height = bounds.Height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var scaling = owner.VisualRoot?.RenderScaling ?? 1.0;
            var pixelWidth = Math.Max(1, (int)Math.Ceiling(width * scaling));
            var pixelHeight = Math.Max(1, (int)Math.Ceiling(height * scaling));
            const RenderFormat format = RenderFormat.Bgra8;

            owner.EnsureRenderer((uint)pixelWidth, (uint)pixelHeight, format);
            owner.EnsureVelloSurface((uint)pixelWidth, (uint)pixelHeight);
            owner.EnsureSkiaSurface(lease.GrContext, pixelWidth, pixelHeight);

            var renderer = owner._renderer;
            var velloSurface = owner._velloSurface;
            var skiaSurface = owner._skiaSurface;
            if (renderer is null || velloSurface is null || skiaSurface is null)
            {
                return;
            }

            var deltaMs = owner.BeginFrame();

            var canvas = velloSurface.Canvas;
            canvas.Clear(VelloColors.Transparent);
            canvas.Save();
            canvas.Scale((float)scaling);
            owner.RenderScene(canvas, width, height, owner._currentComplexity);
            canvas.Restore();

            SkiaRenderBridge.Render(skiaSurface, renderer, velloSurface.Scene, owner._renderParams);
            skiaSurface.Canvas.Flush();

            var targetCanvas = lease.SkCanvas;
            var clipRect = new SkiaRect(
                (float)(bounds.X * scaling),
                (float)(bounds.Y * scaling),
                (float)((bounds.X + bounds.Width) * scaling),
                (float)((bounds.Y + bounds.Height) * scaling));

            targetCanvas.Save();
            targetCanvas.ClipRect(clipRect);
            targetCanvas.DrawSurface(skiaSurface, clipRect.Left, clipRect.Top);
            targetCanvas.Restore();

            owner.OnFrameRendered(deltaMs);
        }
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
        _velloSurface?.Dispose();
        _velloSurface = null;
        _velloSurfaceWidth = 1;
        _velloSurfaceHeight = 1;

        _renderer?.Dispose();
        _renderer = null;
        _rendererWidth = 1;
        _rendererHeight = 1;

        DisposeSkiaSurface();
    }

    private double BeginFrame()
    {
        var now = _stopwatch.Elapsed;
        var delta = _lastFrameTimestamp == TimeSpan.Zero ? 0.0 : (now - _lastFrameTimestamp).TotalMilliseconds;
        _lastFrameTimestamp = now;
        return delta;
    }

    private void OnFrameRendered(double deltaMilliseconds)
    {
        FrameRendered?.Invoke(this, new FrameRenderedEventArgs(deltaMilliseconds));
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
