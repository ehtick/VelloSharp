extern alias VelloSkia;

using System;
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using AvaloniaSkiaSparseMotionMarkShim.Scenes;
using VelloSharp;
using VelloSharp.Integration.Skia;
using VelloSharp.Rendering;
using VelloCanvas = VelloSkia::SkiaSharp.SKCanvas;
using VelloColor = VelloSkia::SkiaSharp.SKColor;
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

    public static readonly StyledProperty<bool> UseSparseRendererProperty =
        AvaloniaProperty.Register<MotionMarkSkiaControl, bool>(nameof(UseSparseRenderer), true);

    private static readonly SparseRenderContextOptions s_defaultSparseOptions;
    private static readonly RgbaColor s_backgroundColor = RgbaColor.FromBytes(0x12, 0x12, 0x14);

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly MotionMarkScene _scene = new();
    private TimeSpan _lastFrameTimestamp;
    private bool _animationFrameRequested;
    private SparseRenderContext? _sparseContext;
    private byte[]? _cpuBuffer;
    private int _bufferWidth;
    private int _bufferHeight;
    private int _bufferStride;
    private int _currentComplexity = MinComplexity;
    private bool _disposed;
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

    static MotionMarkSkiaControl()
    {
        s_defaultSparseOptions = ConfigureSparseBackend();
        AffectsRender<MotionMarkSkiaControl>(ComplexityProperty, IsAnimationEnabledProperty, UseSparseRendererProperty);
    }

    private static SparseRenderContextOptions ConfigureSparseBackend()
    {
        try
        {
            var options = SparseRenderContextOptions.CreateForCurrentMachine();
            SkiaSharp.CpuSkiaBackendConfiguration.SparseRenderOptions = options.Clone();
            return options;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to configure sparse backend: {ex.Message}");
            var fallback = new SparseRenderContextOptions();
            SkiaSharp.CpuSkiaBackendConfiguration.SparseRenderOptions = fallback.Clone();
            return fallback;
        }
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

    public bool UseSparseRenderer
    {
        get => GetValue(UseSparseRendererProperty);
        set => SetValue(UseSparseRendererProperty, value);
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
        else if (change.Property == UseSparseRendererProperty)
        {
            if (!change.GetNewValue<bool>())
            {
                DisposeResources();
            }

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

        if (UseSparseRenderer)
        {
            context.Custom(new VelloDrawOperation(new Rect(Bounds.Size), this));
        }
        else
        {
            context.Custom(new SkiaDrawOperation(new Rect(Bounds.Size), this, width, height, _currentComplexity));
        }

        if (IsAnimationEnabled)
        {
            ScheduleAnimationFrame();
        }
    }

    private void EnsureSparseContext(int pixelWidth, int pixelHeight)
    {
        var width = (ushort)Math.Clamp(pixelWidth, 1, ushort.MaxValue);
        var height = (ushort)Math.Clamp(pixelHeight, 1, ushort.MaxValue);

        if (_sparseContext is { } context && context.Width == width && context.Height == height)
        {
            return;
        }

        _sparseContext?.Dispose();
        _sparseContext = new SparseRenderContext(width, height, s_defaultSparseOptions.Clone());
    }

    private void EnsureCpuBuffer(int pixelWidth, int pixelHeight)
    {
        var stride = pixelWidth * 4;
        var required = stride * pixelHeight;

        if (_cpuBuffer is null || _cpuBuffer.Length < required)
        {
            _cpuBuffer = new byte[required];
        }

        _bufferWidth = pixelWidth;
        _bufferHeight = pixelHeight;
        _bufferStride = stride;
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

            var renderScaling = owner.VisualRoot?.RenderScaling ?? 1.0;
            var logicalWidth = width;
            var logicalHeight = height;
            var pixelWidth = Math.Max(1, (int)Math.Ceiling(logicalWidth * renderScaling));
            var pixelHeight = Math.Max(1, (int)Math.Ceiling(logicalHeight * renderScaling));

            owner.EnsureSparseContext(pixelWidth, pixelHeight);
            owner.EnsureCpuBuffer(pixelWidth, pixelHeight);

            var sparseContext = owner._sparseContext;
            if (sparseContext is null || owner._cpuBuffer is null)
            {
                return;
            }

            var deltaMs = owner.BeginFrame();

            sparseContext.Reset();
            sparseContext.FillRect(0, 0, pixelWidth, pixelHeight, s_backgroundColor);

            var sceneWidth = MotionMarkScene.CanvasWidth;
            var sceneHeight = MotionMarkScene.CanvasHeight;
            var logicalScale = (float)Math.Min(
                (float)(logicalWidth / sceneWidth),
                (float)(logicalHeight / sceneHeight));
            if (!float.IsFinite(logicalScale) || logicalScale <= 0f)
            {
                logicalScale = 1f;
            }

            var physicalScale = logicalScale * (float)renderScaling;
            var offsetXLogical = (float)((logicalWidth - sceneWidth * logicalScale) * 0.5f);
            var offsetYLogical = (float)((logicalHeight - sceneHeight * logicalScale) * 0.5f);
            var translationPixels = new Vector2(
                offsetXLogical * (float)renderScaling,
                offsetYLogical * (float)renderScaling);

            var transform = Matrix3x2.CreateScale(physicalScale);
            transform.Translation = translationPixels;

            owner._scene.RenderSparse(sparseContext, owner._currentComplexity, transform);

            sparseContext.RenderTo(owner._cpuBuffer.AsSpan(0, owner._bufferStride * owner._bufferHeight));

            var targetCanvas = lease.SkCanvas;
            var clipRect = new global::SkiaSharp.SKRect(
                (float)bounds.X,
                (float)bounds.Y,
                (float)(bounds.X + bounds.Width),
                (float)(bounds.Y + bounds.Height));

            var imageInfo = new global::SkiaSharp.SKImageInfo(pixelWidth, pixelHeight, global::SkiaSharp.SKColorType.Bgra8888, global::SkiaSharp.SKAlphaType.Unpremul);

            unsafe
            {
                fixed (byte* bufferPtr = owner._cpuBuffer)
                {
                    using var image = global::SkiaSharp.SKImage.FromPixels(imageInfo, (IntPtr)bufferPtr, owner._bufferStride);

                    targetCanvas.Save();
                    targetCanvas.ClipRect(clipRect);
                    var destRect = new global::SkiaSharp.SKRect(
                        (float)(bounds.X + offsetXLogical),
                        (float)(bounds.Y + offsetYLogical),
                        (float)(bounds.X + offsetXLogical + sceneWidth * logicalScale),
                        (float)(bounds.Y + offsetYLogical + sceneHeight * logicalScale));
                    targetCanvas.DrawImage(image, destRect);
                    targetCanvas.Restore();
                }
            }

            var targetElements = MotionMarkScene.ComputeElementTarget(owner._currentComplexity);
            targetCanvas.Save();
            targetCanvas.ClipRect(clipRect);
            targetCanvas.Translate(
                (float)(bounds.X + offsetXLogical),
                (float)(bounds.Y + offsetYLogical));
            targetCanvas.Scale(logicalScale);
            using (var textPaint = new global::SkiaSharp.SKPaint
            {
                Color = new global::SkiaSharp.SKColor(0xFF, 0xFF, 0xFF),
                TextSize = 40f,
                IsAntialias = true,
            })
            {
                targetCanvas.DrawText($"mmark test: {targetElements} path elements", 100f, 1100f, textPaint);
            }
            targetCanvas.Restore();

            owner.OnFrameRendered(deltaMs);
        }
    }

    private sealed class SkiaDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly MotionMarkSkiaControl _owner;
        private readonly double _width;
        private readonly double _height;
        private readonly int _complexity;

        public SkiaDrawOperation(Rect bounds, MotionMarkSkiaControl owner, double width, double height, int complexity)
        {
            _bounds = bounds;
            _owner = owner;
            _width = width;
            _height = height;
            _complexity = complexity;
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
            if (lease is null || lease.SkSurface is null)
            {
                return;
            }

            _owner.RenderSkia(lease, _bounds, _width, _height, _complexity);
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
        _sparseContext?.Dispose();
        _sparseContext = null;
        _cpuBuffer = null;
        _bufferWidth = 0;
        _bufferHeight = 0;
        _bufferStride = 0;

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

    private void RenderSkia(ISkiaSharpApiLease lease, Rect bounds, double width, double height, int complexity)
    {
        var scaling = VisualRoot?.RenderScaling ?? 1.0;
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(width * scaling));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(height * scaling));

        EnsureRenderer((uint)pixelWidth, (uint)pixelHeight, RenderFormat.Bgra8);
        EnsureVelloSurface((uint)pixelWidth, (uint)pixelHeight);
        EnsureSkiaSurface(lease.GrContext, pixelWidth, pixelHeight);

        var renderer = _renderer;
        var velloSurface = _velloSurface;
        var skiaSurface = _skiaSurface;
        if (renderer is null || velloSurface is null || skiaSurface is null)
        {
            return;
        }

        var delta = BeginFrame();

        var velloCanvas = velloSurface.Canvas;
        velloCanvas.Clear(new VelloColor(0x12, 0x12, 0x14));
        velloCanvas.Save();
        velloCanvas.Scale((float)scaling);
        RenderScene(velloCanvas, width, height, complexity);
        velloCanvas.Restore();

        SkiaRenderBridge.Render(skiaSurface, renderer, velloSurface.Scene, _renderParams);

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

        skiaSurface.Canvas.Flush();

        OnFrameRendered(delta);
    }

    private void RenderScene(VelloCanvas canvas, double width, double height, int complexity)
    {
        canvas.Save();
        canvas.ClipRect(new VelloRect(0, 0, (float)width, (float)height));
        canvas.Clear(new VelloColor(0x12, 0x12, 0x14));

        const float sceneWidth = MotionMarkScene.CanvasWidth;
        const float sceneHeight = MotionMarkScene.CanvasHeight;
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

    private void EnsureRenderer(uint width, uint height, RenderFormat format)
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

}

public sealed class FrameRenderedEventArgs : EventArgs
{
    public FrameRenderedEventArgs(double frameTimeMilliseconds)
    {
        FrameTimeMilliseconds = frameTimeMilliseconds;
    }

    public double FrameTimeMilliseconds { get; }
}
