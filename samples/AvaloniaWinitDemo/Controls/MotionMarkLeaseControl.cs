using System;
using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Platform;
using AvaloniaWinitDemo.Rendering;
using AvaloniaWinitDemo.Scenes;
using VelloSharp;
using VelloSharp.Avalonia.Vello.Rendering;

namespace AvaloniaWinitDemo.Controls;

public sealed class MotionMarkLeaseControl : Control
{
    public const int MinComplexity = 1;
    public const int MaxComplexity = 64;

    public static readonly StyledProperty<int> ComplexityProperty =
        AvaloniaProperty.Register<MotionMarkLeaseControl, int>(
            nameof(Complexity),
            MinComplexity,
            coerce: (_, value) => Math.Clamp(value, MinComplexity, MaxComplexity));

    public static readonly StyledProperty<bool> IsAnimationEnabledProperty =
        AvaloniaProperty.Register<MotionMarkLeaseControl, bool>(nameof(IsAnimationEnabled), true);

    private static readonly RgbaColor s_backgroundColor = RgbaColor.FromBytes(0x12, 0x12, 0x14);
    private readonly Avalonia.Media.SolidColorBrush _fallbackBackgroundBrush = new(Color.FromRgb(0x12, 0x12, 0x14));
    private readonly MotionMarkScene _scene = new();
    private readonly VelloRenderer _renderer = new();
    private readonly PathBuilder _backgroundPath = new PathBuilder()
        .MoveTo(0, 0)
        .LineTo(MotionMarkScene.CanvasWidth, 0)
        .LineTo(MotionMarkScene.CanvasWidth, MotionMarkScene.CanvasHeight)
        .LineTo(0, MotionMarkScene.CanvasHeight)
        .Close();

    private bool _animationFrameRequested;
    private bool _leaseAvailable;
    private int _lastElementCount;
    private Matrix3x2 _lastLocalTransform = Matrix3x2.Identity;

    public MotionMarkLeaseControl()
    {
        ClipToBounds = true;
    }

    static MotionMarkLeaseControl()
    {
        AffectsRender<MotionMarkLeaseControl>(ComplexityProperty, IsAnimationEnabledProperty);
    }

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

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using (context.PushClip(bounds))
        {
            context.FillRectangle(_fallbackBackgroundBrush, bounds);

            context.Custom(new LeaseDrawOperation(new Rect(bounds.Size), this, Complexity));

            DrawOverlay(context, bounds);
        }

        if (IsAnimationEnabled)
        {
            ScheduleAnimationFrame();
        }
    }

    private void DrawOverlay(DrawingContext context, Rect bounds)
    {
        if (!_leaseAvailable)
        {
            var message = new FormattedText(
                "IVelloApiLeaseFeature unavailable.",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter"),
                16,
                Brushes.White)
            {
                TextAlignment = TextAlignment.Center,
            };

            var origin = new Point(
                bounds.X + bounds.Width / 2 - message.WidthIncludingTrailingWhitespace / 2,
                bounds.Y + bounds.Height / 2 - message.Height / 2);

            context.DrawText(message, origin);
            return;
        }

        if (_lastElementCount <= 0)
        {
            return;
        }

        var text = new FormattedText(
            $"mmark test: {_lastElementCount} path elements",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            32,
            Brushes.White);

        var basePoint = Vector2.Transform(new Vector2(100f, 1100f), _lastLocalTransform);
        var textPosition = new Point(basePoint.X, basePoint.Y);
        context.DrawText(text, textPosition);
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

    private void UpdateLeaseAvailability(bool available) => _leaseAvailable = available;

    private void UpdateLastRender(Matrix3x2 localTransform, int elementCount)
    {
        _lastLocalTransform = localTransform;
        _lastElementCount = elementCount;
    }

    private static Matrix3x2 ToMatrix3x2(Matrix matrix)
    {
        return new Matrix3x2(
            (float)matrix.M11,
            (float)matrix.M12,
            (float)matrix.M21,
            (float)matrix.M22,
            (float)matrix.M31,
            (float)matrix.M32);
    }

    private readonly struct LeaseDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly MotionMarkLeaseControl _owner;
        private readonly int _complexity;

        public LeaseDrawOperation(Rect bounds, MotionMarkLeaseControl owner, int complexity)
        {
            _bounds = bounds;
            _owner = owner;
            _complexity = complexity;
        }

        public Rect Bounds => _bounds;

        public void Dispose()
        {
        }

        public bool HitTest(Point p) => _bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) =>
            other is LeaseDrawOperation op && ReferenceEquals(op._owner, _owner) && op._bounds.Equals(_bounds);

        public void Render(ImmediateDrawingContext context)
        {
            if (!context.TryGetFeature<IVelloApiLeaseFeature>(out var feature) || feature is null)
            {
                _owner.UpdateLeaseAvailability(false);
                return;
            }

            using var lease = feature.Lease();
            if (lease is null)
            {
                _owner.UpdateLeaseAvailability(false);
                return;
            }

            var scene = lease.Scene;
            if (scene is null)
            {
                _owner.UpdateLeaseAvailability(false);
                return;
            }

            scene.Reset();

            var globalTransform = ToMatrix3x2(lease.Transform);
            var localTransform = MotionMarkRenderHelpers.CreateSceneTransform(_bounds);
            var compositeTransform = Matrix3x2.Multiply(localTransform, globalTransform);

            scene.FillPath(_owner._backgroundPath, VelloSharp.FillRule.NonZero, compositeTransform, s_backgroundColor);

            _owner._renderer.Reset(scene, compositeTransform);
            var target = _owner._scene.Render(_complexity, _owner._renderer);

            _owner.UpdateLastRender(localTransform, target);
            _owner.UpdateLeaseAvailability(true);
        }
    }

    private sealed class VelloRenderer : MotionMarkScene.IRenderer
    {
        private readonly PathBuilder _path = new();
        private readonly StrokeStyle _stroke = new()
        {
            LineJoin = LineJoin.Bevel,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            MiterLimit = 4.0,
        };

        private Scene? _scene;
        private Matrix3x2 _transform;

        public void Reset(Scene scene, Matrix3x2 transform)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _transform = transform;
        }

        public void BeginPath(in MotionMarkScene.Element element)
        {
            _path.Clear();
            _path.MoveTo(element.Start.X, element.Start.Y);
        }

        public void Append(in MotionMarkScene.Element element)
        {
            element.AppendTo(_path);
        }

        public void CompletePath(in MotionMarkScene.Element element)
        {
            if (_scene is null || _path.Count <= 1)
            {
                return;
            }

            _stroke.Width = element.Width;
            _scene.StrokePath(_path, _stroke, _transform, element.Color);
        }
    }
}
