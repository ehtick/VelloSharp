using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using VelloSharp;

namespace AvaloniaWinitDemo.Controls;

public sealed class MotionMarkDrawingContextControl : Control
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

    public static readonly StyledProperty<int> ComplexityProperty =
        AvaloniaProperty.Register<MotionMarkDrawingContextControl, int>(
            nameof(Complexity),
            MinComplexity,
            coerce: (_, value) => Math.Clamp(value, MinComplexity, MaxComplexity));

    public static readonly StyledProperty<bool> IsAnimationEnabledProperty =
        AvaloniaProperty.Register<MotionMarkDrawingContextControl, bool>(nameof(IsAnimationEnabled), true);

    private readonly Avalonia.Media.SolidColorBrush _backgroundBrush = new(Color.FromRgb(0x12, 0x12, 0x14));
    private readonly PenCache _penCache = new();
    private readonly List<Element> _elements = new();
    private readonly Random _random = new(1);

    private bool _animationFrameRequested;
    private int _lastElementCount;
    private Matrix3x2 _lastLocalTransform = Matrix3x2.Identity;

    static MotionMarkDrawingContextControl()
    {
        AffectsRender<MotionMarkDrawingContextControl>(ComplexityProperty, IsAnimationEnabledProperty);
    }

    public MotionMarkDrawingContextControl()
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
            context.FillRectangle(_backgroundBrush, bounds);

            var localTransform = CreateSceneTransform(bounds, out var scale);
            using (context.PushTransform(ToAvaloniaMatrix(localTransform)))
            {
                var target = RenderScene(context, scale, Complexity);
                UpdateLastRender(localTransform, target);
            }

            if (_lastElementCount > 0)
            {
                var text = new FormattedText(
                    $"mmark test: {_lastElementCount} path elements",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter"),
                    32,
                    Brushes.White);

                var basePoint = Vector2.Transform(new Vector2(100f, 1100f), localTransform);
                context.DrawText(text, new Point(basePoint.X, basePoint.Y));
            }
        }

        if (IsAnimationEnabled)
        {
            ScheduleAnimationFrame();
        }
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

    private void UpdateLastRender(Matrix3x2 transform, int elementCount)
    {
        _lastLocalTransform = transform;
        _lastElementCount = elementCount;
    }

    private Matrix3x2 CreateSceneTransform(Rect bounds, out double scale)
    {
        var width = (float)bounds.Width;
        var height = (float)bounds.Height;
        var s = MathF.Min(width / SceneWidth, height / SceneHeight);
        if (!float.IsFinite(s) || s <= 0f)
        {
            s = 1f;
        }

        var scaledWidth = SceneWidth * s;
        var scaledHeight = SceneHeight * s;
        var offsetX = (width - scaledWidth) * 0.5f + (float)bounds.X;
        var offsetY = (height - scaledHeight) * 0.5f + (float)bounds.Y;

        var transform = Matrix3x2.CreateScale(s);
        transform.Translation = new Vector2(offsetX, offsetY);

        scale = s;
        return transform;
    }

    private int RenderScene(DrawingContext context, double scale, int complexity)
    {
        var target = EnsureElements(complexity);

        StreamGeometry? geometry = null;
        StreamGeometryContext? geometryContext = null;
        var pathStarted = false;

        for (var i = 0; i < _elements.Count; i++)
        {
            var element = _elements[i];

            if (!pathStarted)
            {
                geometry = new StreamGeometry();
                geometryContext = geometry.Open();
                geometryContext.BeginFigure(ToPoint(element.Start), isFilled: false);
                pathStarted = true;
            }

            switch (element.Type)
            {
                case SegmentType.Line:
                    geometryContext!.LineTo(ToPoint(element.End));
                    break;
                case SegmentType.Quad:
                    geometryContext!.QuadraticBezierTo(ToPoint(element.Control1), ToPoint(element.End));
                    break;
                case SegmentType.Cubic:
                    geometryContext!.CubicBezierTo(ToPoint(element.Control1), ToPoint(element.Control2), ToPoint(element.End));
                    break;
            }

            var isLast = i == _elements.Count - 1;
            if (element.IsSplit || isLast)
            {
                geometryContext!.EndFigure(false);
                geometryContext.Dispose();
                geometryContext = null;

                var pen = _penCache.GetPen(ToAvaloniaColor(element.Color), element.Width * scale);
                context.DrawGeometry(null, pen, geometry!);

                geometry = null;
                pathStarted = false;
            }

            if (_random.NextDouble() > 0.995)
            {
                element.IsSplit = !element.IsSplit;
            }

            _elements[i] = element;
        }

        geometryContext?.Dispose();

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

    private static Matrix ToAvaloniaMatrix(Matrix3x2 matrix) => new(
        matrix.M11,
        matrix.M12,
        matrix.M21,
        matrix.M22,
        matrix.M31,
        matrix.M32);

    private static Point ToPoint(Vector2 value) => new(value.X, value.Y);

    private static Color ToAvaloniaColor(RgbaColor color)
    {
        static byte ToByte(float value)
        {
            var scaled = (int)MathF.Round(value * 255f);
            return (byte)Math.Clamp(scaled, 0, 255);
        }

        return Color.FromArgb(
            ToByte(color.A),
            ToByte(color.R),
            ToByte(color.G),
            ToByte(color.B));
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

    private sealed class PenCache
    {
        private readonly Dictionary<PenKey, Pen> _cache = new();

        public Pen GetPen(Color color, double width)
        {
            var key = new PenKey(color, width);
            if (_cache.TryGetValue(key, out var pen))
            {
                return pen;
            }

            pen = new Pen(new Avalonia.Media.SolidColorBrush(color), width)
            {
                LineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Bevel,
                MiterLimit = 4.0,
            };

            _cache[key] = pen;
            return pen;
        }

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
    }
}
