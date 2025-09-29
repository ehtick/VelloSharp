extern alias VelloSkia;

using System;
using System.Collections.Generic;
using SKCanvas = VelloSkia::SkiaSharp.SKCanvas;
using SKColor = VelloSkia::SkiaSharp.SKColor;
using SKColors = VelloSkia::SkiaSharp.SKColors;
using SKPaint = VelloSkia::SkiaSharp.SKPaint;
using SKPaintStyle = VelloSkia::SkiaSharp.SKPaintStyle;
using SKPath = VelloSkia::SkiaSharp.SKPath;
using SKPoint = VelloSkia::SkiaSharp.SKPoint;
using SKStrokeCap = VelloSkia::SkiaSharp.SKStrokeCap;
using SKStrokeJoin = VelloSkia::SkiaSharp.SKStrokeJoin;

namespace AvaloniaSkiaMotionMarkShim.Scenes;

public sealed class MotionMarkScene
{
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

    private static readonly SKColor[] Colors =
    {
        new SKColor(0x10, 0x10, 0x10),
        new SKColor(0x80, 0x80, 0x80),
        new SKColor(0xC0, 0xC0, 0xC0),
        new SKColor(0x10, 0x10, 0x10),
        new SKColor(0x80, 0x80, 0x80),
        new SKColor(0xC0, 0xC0, 0xC0),
        new SKColor(0xE0, 0x10, 0x40),
    };

    private readonly List<Element> _elements = new();
    private readonly Random _random = new(1);

    public void Render(SKCanvas canvas, int complexity)
    {
        var clamped = Math.Max(1, complexity);
        var target = clamped < 10
            ? (clamped + 1) * 1000
            : Math.Min((clamped - 8) * 10000, 120_000);

        Resize(target);

        using var stroke = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Bevel,
            IsAntialias = true,
        };

        using var path = new SKPath();
        var pathStarted = false;

        for (var i = 0; i < _elements.Count; i++)
        {
            var element = _elements[i];
            if (!pathStarted)
            {
                path.MoveTo(element.Start);
                pathStarted = true;
            }

            element.AppendTo(path);

            var isLast = i == _elements.Count - 1;
            if (element.IsSplit || isLast)
            {
                stroke.Color = element.Color;
                stroke.StrokeWidth = element.Width;
                canvas.DrawPath(path, stroke);
                path.Reset();
                pathStarted = false;
            }

            if (_random.NextDouble() > 0.995)
            {
                element.IsSplit = !element.IsSplit;
            }

            _elements[i] = element;
        }

        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 40f,
            IsAntialias = true,
        };

        canvas.DrawText(
            $"mmark test: {target} path elements",
            100f,
            1100f,
            textPaint);
    }

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

    private enum SegmentType
    {
        Line,
        Quad,
        Cubic,
    }

    private struct Element
    {
        public SegmentType Type;
        public SKPoint Start;
        public SKPoint Control1;
        public SKPoint Control2;
        public SKPoint End;
        public SKColor Color;
        public float Width;
        public bool IsSplit;
        public GridPoint GridPoint;

        public void AppendTo(SKPath path)
        {
            switch (Type)
            {
                case SegmentType.Line:
                    path.LineTo(End);
                    break;
                case SegmentType.Quad:
                    path.QuadTo(Control1, End);
                    break;
                case SegmentType.Cubic:
                    path.CubicTo(Control1, Control2, End);
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

        public SKPoint ToCoordinate()
        {
            var scaleX = Width / (GridWidth + 1f);
            var scaleY = Height / (GridHeight + 1f);
            return new SKPoint((float)((X + 0.5f) * scaleX), 100f + (float)((Y + 0.5f) * scaleY));
        }
    }
}
