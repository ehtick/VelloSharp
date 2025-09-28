using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using AvaloniaSkiaMotionMark.Scenes;

namespace AvaloniaSkiaMotionMark.Controls;

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
        if (IsAnimationEnabled)
        {
            ScheduleAnimationFrame();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animationFrameRequested = false;
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

        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var now = _stopwatch.Elapsed;
        var delta = _lastFrameTimestamp == TimeSpan.Zero ? TimeSpan.Zero : now - _lastFrameTimestamp;
        _lastFrameTimestamp = now;

        var drawOperation = new SkiaDrawOperation(new Rect(Bounds.Size), this, width, height, Complexity);
        context.Custom(drawOperation);

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

    private void ScheduleAnimationFrame()
    {
        if (_animationFrameRequested || !IsAnimationEnabled)
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

        if (!IsAnimationEnabled)
        {
            return;
        }

        InvalidateVisual();
        ScheduleAnimationFrame();
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
            var surface = lease?.SkSurface;
            if (surface is null)
            {
                return;
            }

            var canvas = surface.Canvas;
            canvas.Save();
            _owner.RenderSkia(canvas, _width, _height, _complexity);
            canvas.Restore();
        }
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
