using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering.SceneGraph;
using AvaloniaWinitDemo.Rendering;
using AvaloniaWinitDemo.Scenes;
using VelloSharp;

namespace AvaloniaWinitDemo.Controls;

public sealed class MotionMarkImmediateControl : Control
{
    public const int MinComplexity = 1;
    public const int MaxComplexity = 64;

    public static readonly StyledProperty<int> ComplexityProperty =
        AvaloniaProperty.Register<MotionMarkImmediateControl, int>(
            nameof(Complexity),
            MinComplexity,
            coerce: (_, value) => Math.Clamp(value, MinComplexity, MaxComplexity));

    public static readonly StyledProperty<bool> IsAnimationEnabledProperty =
        AvaloniaProperty.Register<MotionMarkImmediateControl, bool>(nameof(IsAnimationEnabled), true);

    private static readonly RgbaColor s_backgroundColor = RgbaColor.FromBytes(0x12, 0x12, 0x14);
    private static readonly ImmutableSolidColorBrush s_backgroundImmutable =
        new(MotionMarkRenderHelpers.ToAvaloniaColor(s_backgroundColor));

    private readonly Avalonia.Media.SolidColorBrush _fallbackBackground = new(Color.FromRgb(0x12, 0x12, 0x14));
    private readonly MotionMarkScene _scene = new();
    private readonly ImmediateRenderer _renderer = new();

    private bool _animationFrameRequested;
    private int _lastElementCount;
    private Matrix3x2 _lastLocalTransform = Matrix3x2.Identity;

    public MotionMarkImmediateControl()
    {
        ClipToBounds = true;
    }

    static MotionMarkImmediateControl()
    {
        AffectsRender<MotionMarkImmediateControl>(ComplexityProperty, IsAnimationEnabledProperty);
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
            context.FillRectangle(_fallbackBackground, bounds);

            context.Custom(new ImmediateDrawOperation(new Rect(bounds.Size), this, Complexity));

            if (_lastElementCount > 0)
            {
                var text = new FormattedText(
                    $"mmark test: {_lastElementCount} path elements",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter"),
                    32,
                    Brushes.White);

                var basePoint = Vector2.Transform(new Vector2(100f, 1100f), _lastLocalTransform);
                context.DrawText(text, new Point(basePoint.X, basePoint.Y));
            }
        }

        if (IsAnimationEnabled)
        {
            ScheduleAnimationFrame();
        }
    }

    private void UpdateLastRender(Matrix3x2 transform, int elementCount)
    {
        _lastLocalTransform = transform;
        _lastElementCount = elementCount;
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

    private readonly struct ImmediateDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly MotionMarkImmediateControl _owner;
        private readonly int _complexity;

        public ImmediateDrawOperation(Rect bounds, MotionMarkImmediateControl owner, int complexity)
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
            other is ImmediateDrawOperation op && ReferenceEquals(op._owner, _owner) && op._bounds.Equals(_bounds);

        public void Render(ImmediateDrawingContext context)
        {
            var localTransform = MotionMarkRenderHelpers.CreateSceneTransform(_bounds);
            var scale = localTransform.M11;

            var backgroundRect = new Rect(
                localTransform.M31,
                localTransform.M32,
                MotionMarkScene.CanvasWidth * scale,
                MotionMarkScene.CanvasHeight * scale);

            context.FillRectangle(s_backgroundImmutable, backgroundRect);

            _owner._renderer.Begin(context, localTransform, scale);
            var target = _owner._scene.Render(_complexity, _owner._renderer);
            _owner._renderer.End();

            _owner.UpdateLastRender(localTransform, target);
        }
    }

    private sealed class ImmediateRenderer : MotionMarkScene.IRenderer
    {
        private readonly PenCache _penCache = new();
        private readonly List<Point> _points = new();

        private ImmediateDrawingContext? _context;
        private Matrix3x2 _transform;
        private double _scale;
        private Vector2 _current;

        public void Begin(ImmediateDrawingContext context, Matrix3x2 transform, double scale)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _transform = transform;
            _scale = scale;
            _points.Clear();
            _current = default;
        }

        public void BeginPath(in MotionMarkScene.Element element)
        {
            _points.Clear();
            _current = element.Start;
            _points.Add(TransformPoint(element.Start));
        }

        public void Append(in MotionMarkScene.Element element)
        {
            switch (element.Type)
            {
                case MotionMarkScene.SegmentType.Line:
                    AppendLine(element.End);
                    break;
                case MotionMarkScene.SegmentType.Quad:
                    AppendQuadratic(element.Control1, element.End);
                    break;
                case MotionMarkScene.SegmentType.Cubic:
                    AppendCubic(element.Control1, element.Control2, element.End);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _current = element.End;
        }

        public void CompletePath(in MotionMarkScene.Element element)
        {
            if (_context is null || _points.Count < 2)
            {
                return;
            }

            var color = MotionMarkRenderHelpers.ToAvaloniaColor(element.Color);
            var thickness = element.Width * _scale;
            var pen = _penCache.GetPen(color, thickness);

            for (var i = 1; i < _points.Count; i++)
            {
                _context.DrawLine(pen, _points[i - 1], _points[i]);
            }

            _points.Clear();
        }

        public void End()
        {
            _points.Clear();
            _context = null;
        }

        private void AppendLine(Vector2 end)
        {
            _points.Add(TransformPoint(end));
        }

        private void AppendQuadratic(Vector2 control, Vector2 end)
        {
            const int subdivisions = 12;
            for (var i = 1; i <= subdivisions; i++)
            {
                var t = i / (float)subdivisions;
                var point = Quadratic(_current, control, end, t);
                _points.Add(TransformPoint(point));
            }
        }

        private void AppendCubic(Vector2 control1, Vector2 control2, Vector2 end)
        {
            const int subdivisions = 18;
            for (var i = 1; i <= subdivisions; i++)
            {
                var t = i / (float)subdivisions;
                var point = Cubic(_current, control1, control2, end, t);
                _points.Add(TransformPoint(point));
            }
        }

        private Point TransformPoint(Vector2 point)
        {
            var transformed = Vector2.Transform(point, _transform);
            return new Point(transformed.X, transformed.Y);
        }

        private static Vector2 Quadratic(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            var oneMinusT = 1f - t;
            return oneMinusT * oneMinusT * p0 + 2f * oneMinusT * t * p1 + t * t * p2;
        }

        private static Vector2 Cubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var oneMinusT = 1f - t;
            return oneMinusT * oneMinusT * oneMinusT * p0
                   + 3f * oneMinusT * oneMinusT * t * p1
                   + 3f * oneMinusT * t * t * p2
                   + t * t * t * p3;
        }
    }

    private sealed class PenCache
    {
        private readonly struct PenKey : IEquatable<PenKey>
        {
            public PenKey(Color color, double width)
            {
                Color = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
                WidthBits = BitConverter.DoubleToInt64Bits(width);
            }

            public uint Color { get; }
            public long WidthBits { get; }

            public bool Equals(PenKey other) => Color == other.Color && WidthBits == other.WidthBits;

            public override bool Equals(object? obj) => obj is PenKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Color, WidthBits);
        }

        private readonly Dictionary<PenKey, ImmutablePen> _cache = new();

        public ImmutablePen GetPen(Color color, double width)
        {
            var key = new PenKey(color, width);
            if (_cache.TryGetValue(key, out var pen))
            {
                return pen;
            }

            pen = new ImmutablePen(
                new ImmutableSolidColorBrush(color),
                width,
                dashStyle: null,
                lineCap: PenLineCap.Round,
                lineJoin: PenLineJoin.Bevel,
                miterLimit: 4.0);

            _cache[key] = pen;
            return pen;
        }
    }
}
