using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace MotionMark.SceneShared;

public sealed class MotionMarkScene
{
    public const float CanvasWidth = 1600f;
    public const float CanvasHeight = 1200f;

    private const int Width = 1600;
    private const int Height = 900;
    private const int GridWidth = 80;
    private const int GridHeight = 40;

    private static readonly (int X, int Y)[] Offsets =
    {
        (-4, 0),
        (2, 0),
        (1, -2),
        (1, 2),
    };

    private static readonly Color[] Colors =
    {
        Color.FromArgb(0x10, 0x10, 0x10),
        Color.FromArgb(0x80, 0x80, 0x80),
        Color.FromArgb(0xC0, 0xC0, 0xC0),
        Color.FromArgb(0x10, 0x10, 0x10),
        Color.FromArgb(0x80, 0x80, 0x80),
        Color.FromArgb(0xC0, 0xC0, 0xC0),
        Color.FromArgb(0xE0, 0x10, 0x40),
    };

    private readonly List<Element> _elements = new();
    private readonly Random _random;

    public MotionMarkScene()
        : this(seed: 1)
    {
    }

    public MotionMarkScene(int seed)
    {
        _random = new Random(seed);
    }

    public static int ComputeElementTarget(int complexity)
    {
        var clamped = Math.Max(1, complexity);
        return clamped < 10
            ? (clamped + 1) * 1000
            : Math.Min((clamped - 8) * 10000, 120_000);
    }

    public int PrepareFrame(int complexity)
    {
        var target = ComputeElementTarget(complexity);
        Resize(target);
        UpdateSplits();
        return target;
    }

    public ReadOnlySpan<Element> Elements => CollectionsMarshal.AsSpan(_elements);

    private void Resize(int target)
    {
        if (_elements.Count > target)
        {
            _elements.RemoveRange(target, _elements.Count - target);
            return;
        }

        var last = _elements.Count > 0 ? _elements[^1].GridPoint : new GridPoint(GridWidth / 2, GridHeight / 2);
        while (_elements.Count < target)
        {
            var element = Element.CreateRandom(last, _random);
            _elements.Add(element);
            last = element.GridPoint;
        }
    }

    private void UpdateSplits()
    {
        for (var i = 0; i < _elements.Count; i++)
        {
            var element = _elements[i];
            if (_random.NextDouble() > 0.995)
            {
                element.IsSplit = !element.IsSplit;
                _elements[i] = element;
            }
        }
    }

    public enum ElementType
    {
        Line,
        Quadratic,
        Cubic,
    }

    public struct Element
    {
        public Element(
            ElementType type,
            PointF start,
            PointF control1,
            PointF control2,
            PointF end,
            Color color,
            float width,
            bool isSplit,
            GridPoint gridPoint)
        {
            Type = type;
            Start = start;
            Control1 = control1;
            Control2 = control2;
            End = end;
            Color = color;
            Width = width;
            IsSplit = isSplit;
            GridPoint = gridPoint;
        }

        public ElementType Type { get; set; }
        public PointF Start { get; set; }
        public PointF Control1 { get; set; }
        public PointF Control2 { get; set; }
        public PointF End { get; set; }
        public Color Color { get; set; }
        public float Width { get; set; }
        public bool IsSplit { get; set; }
        public GridPoint GridPoint { get; set; }

        public static Element CreateRandom(GridPoint last, Random random)
        {
            var next = GridPoint.Next(last, random);
            ElementType type;
            var start = last.ToCoordinate();
            var end = next.ToCoordinate();
            var control1 = end;
            var control2 = end;
            var gridPoint = next;

            var choice = random.Next(4);
            if (choice < 2)
            {
                type = ElementType.Line;
            }
            else if (choice < 3)
            {
                type = ElementType.Quadratic;
                gridPoint = GridPoint.Next(next, random);
                control1 = next.ToCoordinate();
                end = gridPoint.ToCoordinate();
            }
            else
            {
                type = ElementType.Cubic;
                control1 = next.ToCoordinate();
                var mid = GridPoint.Next(next, random);
                control2 = mid.ToCoordinate();
                gridPoint = GridPoint.Next(next, random);
                end = gridPoint.ToCoordinate();
            }

            var color = Colors[random.Next(Colors.Length)];
            var width = (float)(Math.Pow(random.NextDouble(), 5) * 20.0 + 1.0);
            var isSplit = random.Next(2) == 0;

            return new Element(
                type,
                start,
                control1,
                control2,
                end,
                color,
                width,
                isSplit,
                gridPoint);
        }
    }

    public readonly struct GridPoint
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

        public PointF ToCoordinate()
        {
            var scaleX = Width / (GridWidth + 1f);
            var scaleY = Height / (GridHeight + 1f);
            return new PointF((float)((X + 0.5f) * scaleX), 100f + (float)((Y + 0.5f) * scaleY));
        }
    }
}
