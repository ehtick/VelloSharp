using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using VelloSharp;
using VelloSharp.Avalonia.Vello.Rendering;

namespace AvaloniaVelloCommon.Controls;

public sealed class MotionMarkLeaseControl : Control
{
    public const int MinComplexity = 1;
    public const int MaxComplexity = 64;

    private const float SceneWidth = 1600f;
    private const float SceneHeight = 1200f;
    private const int GridWidth = 80;
    private const int GridHeight = 40;
    private const int WidthCells = 1600;
    private const int HeightCells = 900;

    private static readonly (int X, int Y)[] Offsets =
    {
        (-4, 0),
        (2, 0),
        (1, -2),
        (1, 2),
    };

    private static readonly RgbaColor[] Colors =
    {
        RgbaColor.FromBytes(0x10, 0x10, 0x10),
        RgbaColor.FromBytes(0x80, 0x80, 0x80),
        RgbaColor.FromBytes(0xC0, 0xC0, 0xC0),
        RgbaColor.FromBytes(0x10, 0x10, 0x10),
        RgbaColor.FromBytes(0x80, 0x80, 0x80),
        RgbaColor.FromBytes(0xC0, 0xC0, 0xC0),
        RgbaColor.FromBytes(0xE0, 0x10, 0x40),
    };

    private static readonly RgbaColor BackgroundColor = RgbaColor.FromBytes(0x12, 0x12, 0x14);

    public static readonly StyledProperty<int> ComplexityProperty =
        AvaloniaProperty.Register<MotionMarkLeaseControl, int>(
            nameof(Complexity),
            MinComplexity,
            coerce: (_, value) => Math.Clamp(value, MinComplexity, MaxComplexity));

    public static readonly StyledProperty<bool> IsAnimationEnabledProperty =
        AvaloniaProperty.Register<MotionMarkLeaseControl, bool>(nameof(IsAnimationEnabled), true);

    private readonly Avalonia.Media.SolidColorBrush _fallbackBackgroundBrush = new(Color.FromRgb(0x12, 0x12, 0x14));
    private readonly List<Element> _elements = new();
    private readonly Random _random = new(1);
    private readonly PathBuilder _backgroundPath = new PathBuilder()
        .MoveTo(0, 0)
        .LineTo(SceneWidth, 0)
        .LineTo(SceneWidth, SceneHeight)
        .LineTo(0, SceneHeight)
        .Close();
    private readonly StrokeStyle _stroke = new()
    {
        LineJoin = LineJoin.Bevel,
        StartCap = LineCap.Round,
        EndCap = LineCap.Round,
        MiterLimit = 4.0,
    };

    private bool _animationFrameRequested;
    private bool _leaseAvailable;
    private int _lastElementCount;
    private Matrix3x2 _lastLocalTransform = Matrix3x2.Identity;

    static MotionMarkLeaseControl()
    {
        AffectsRender<MotionMarkLeaseControl>(ComplexityProperty, IsAnimationEnabledProperty);
    }

    public MotionMarkLeaseControl()
    {
        ClipToBounds = true;
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
        context.DrawText(text, new Point(basePoint.X, basePoint.Y));
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

    private Matrix3x2 CreateSceneTransform(Rect bounds)
    {
        var width = (float)bounds.Width;
        var height = (float)bounds.Height;
        var scale = MathF.Min(width / SceneWidth, height / SceneHeight);
        if (!float.IsFinite(scale) || scale <= 0f)
        {
            scale = 1f;
        }

        var scaledWidth = SceneWidth * scale;
        var scaledHeight = SceneHeight * scale;
        var offsetX = (width - scaledWidth) * 0.5f + (float)bounds.X;
        var offsetY = (height - scaledHeight) * 0.5f + (float)bounds.Y;

        var transform = Matrix3x2.CreateScale(scale);
        transform.Translation = new Vector2(offsetX, offsetY);
        return transform;
    }

    private int RenderScene(Scene scene, Matrix3x2 transform, int complexity)
    {
        var target = EnsureElements(complexity);

        var builder = new PathBuilder();
        var pathStarted = false;

        for (var i = 0; i < _elements.Count; i++)
        {
            var element = _elements[i];

            if (!pathStarted)
            {
                builder.MoveTo(element.Start.X, element.Start.Y);
                pathStarted = true;
            }

            element.AppendTo(builder);

            var isLast = i == _elements.Count - 1;
            if (element.IsSplit || isLast)
            {
                _stroke.Width = element.Width;
                scene.StrokePath(builder, _stroke, transform, element.Color);
                builder.Clear();
                pathStarted = false;
            }

            if (_random.NextDouble() > 0.995)
            {
                element.IsSplit = !element.IsSplit;
            }

            _elements[i] = element;
        }

        return target;
    }

    private int EnsureElements(int complexity)
    {
        var target = ComputeElementTarget(complexity);

        if (_elements.Count > target)
        {
            _elements.RemoveRange(target, _elements.Count - target);
            return target;
        }

        var last = _elements.Count > 0 ? _elements[^1].GridPoint : new GridPoint(GridWidth / 2, GridHeight / 2);
        while (_elements.Count < target)
        {
            var element = Element.CreateRandom(last, _random);
            _elements.Add(element);
            last = element.GridPoint;
        }

        return target;
    }

    private static int ComputeElementTarget(int complexity)
    {
        var clamped = Math.Max(1, complexity);
        return clamped < 10
            ? (clamped + 1) * 1000
            : Math.Min((clamped - 8) * 10000, 120_000);
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

            var globalTransform = ToMatrix3x2(lease.Transform);
            var localTransform = _owner.CreateSceneTransform(_bounds);
            var compositeTransform = Matrix3x2.Multiply(localTransform, globalTransform);

            scene.FillPath(_owner._backgroundPath, VelloSharp.FillRule.NonZero, compositeTransform, BackgroundColor);

            var target = _owner.RenderScene(scene, compositeTransform, _complexity);

            _owner.UpdateLastRender(localTransform, target);
            _owner.UpdateLeaseAvailability(true);
        }
    }

    private enum SegmentType
    {
        Line,
        Quad,
        Cubic,
    }

    private readonly struct GridPoint
    {
        public GridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public static GridPoint Next(GridPoint last, Random random)
        {
            var offset = Offsets[random.Next(Offsets.Length)];
            var x = last.X + offset.X;
            if (x < 0 || x > GridWidth)
            {
                x -= offset.X * 2;
            }

            var y = last.Y + offset.Y;
            if (y < 0 || y > GridHeight)
            {
                y -= offset.Y * 2;
            }

            return new GridPoint(x, y);
        }

        public Vector2 ToCoordinate()
        {
            var scaleX = WidthCells / (GridWidth + 1f);
            var scaleY = HeightCells / (GridHeight + 1f);
            return new Vector2(
                (float)((X + 0.5f) * scaleX),
                100f + (float)((Y + 0.5f) * scaleY));
        }
    }

    private struct Element
    {
        public SegmentType Type;
        public Vector2 Start;
        public Vector2 Control1;
        public Vector2 Control2;
        public Vector2 End;
        public RgbaColor Color;
        public float Width;
        public bool IsSplit;
        public GridPoint GridPoint;

        public void AppendTo(PathBuilder builder)
        {
            switch (Type)
            {
                case SegmentType.Line:
                    builder.LineTo(End.X, End.Y);
                    break;
                case SegmentType.Quad:
                    builder.QuadraticTo(Control1.X, Control1.Y, End.X, End.Y);
                    break;
                case SegmentType.Cubic:
                    builder.CubicTo(Control1.X, Control1.Y, Control2.X, Control2.Y, End.X, End.Y);
                    break;
            }
        }

        public static Element CreateRandom(GridPoint last, Random random)
        {
            var next = GridPoint.Next(last, random);
            SegmentType type;
            var start = last.ToCoordinate();
            var end = next.ToCoordinate();
            var control1 = end;
            var control2 = end;
            var gridPoint = next;

            var choice = random.Next(4);
            if (choice < 2)
            {
                type = SegmentType.Line;
            }
            else if (choice < 3)
            {
                type = SegmentType.Quad;
                gridPoint = GridPoint.Next(next, random);
                control1 = next.ToCoordinate();
                end = gridPoint.ToCoordinate();
            }
            else
            {
                type = SegmentType.Cubic;
                control1 = next.ToCoordinate();
                var mid = GridPoint.Next(next, random);
                control2 = mid.ToCoordinate();
                gridPoint = GridPoint.Next(next, random);
                end = gridPoint.ToCoordinate();
            }

            var color = Colors[random.Next(Colors.Length)];
            var width = (float)(Math.Pow(random.NextDouble(), 5) * 20.0 + 1.0);
            var isSplit = random.Next(2) == 0;

            return new Element
            {
                Type = type,
                Start = start,
                Control1 = control1,
                Control2 = control2,
                End = end,
                Color = color,
                Width = width,
                IsSplit = isSplit,
                GridPoint = gridPoint,
            };
        }
    }
}
