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

    static MotionMarkSkiaControl()
    {
        s_defaultSparseOptions = ConfigureSparseBackend();
        AffectsRender<MotionMarkSkiaControl>(ComplexityProperty, IsAnimationEnabledProperty);
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
