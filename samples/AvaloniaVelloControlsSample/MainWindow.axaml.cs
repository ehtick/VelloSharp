using System;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VelloSharp;
using VelloSharp.Avalonia.Controls;

namespace AvaloniaVelloControlsSample;

public partial class MainWindow : Window
{
    private static readonly GradientStop[] CanvasGradientStops =
    [
        new GradientStop(0f, RgbaColor.FromBytes(96, 214, 255)),
        new GradientStop(0.5f, RgbaColor.FromBytes(126, 125, 255)),
        new GradientStop(1f, RgbaColor.FromBytes(216, 95, 255)),
    ];

    private static readonly GradientStop[] RibbonStops =
    [
        new GradientStop(0f, RgbaColor.FromBytes(58, 197, 255)),
        new GradientStop(0.45f, RgbaColor.FromBytes(153, 111, 255)),
        new GradientStop(1f, RgbaColor.FromBytes(255, 93, 217)),
    ];

    private static readonly GradientStop[] OrbStops =
    [
        new GradientStop(0f, RgbaColor.FromBytes(255, 255, 255)),
        new GradientStop(1f, RgbaColor.FromBytes(118, 185, 255, 32)),
    ];

    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCanvasDraw(object? sender, VelloDrawEventArgs e)
    {
        var bounds = e.Bounds;
        var scene = e.Scene;
        var transform = e.GlobalTransform;

        var backdrop = CreateRectPath(bounds);
        scene.FillPath(backdrop, FillRule.NonZero, transform, new SolidColorBrush(RgbaColor.FromBytes(12, 18, 32)));

        var capsule = new Rect(
            bounds.Width * 0.18,
            bounds.Height * 0.28,
            bounds.Width * 0.64,
            bounds.Height * 0.44);

        var capsulePath = CreateRoundedRectPath(capsule, Math.Min(capsule.Width, capsule.Height) * 0.36);
        var gradient = new LinearGradientBrush(
            new Vector2((float)capsule.Left, (float)capsule.Bottom),
            new Vector2((float)capsule.Right, (float)capsule.Top),
            CanvasGradientStops);
        scene.FillPath(capsulePath, FillRule.NonZero, transform, gradient);

        var outlineRadius = (float)(Math.Min(bounds.Width, bounds.Height) * 0.42);
        var outlineCenter = new Vector2((float)(bounds.X + bounds.Width / 2), (float)(bounds.Y + bounds.Height / 2));
        var outline = CreateEllipsePath(outlineCenter, outlineRadius, outlineRadius);

        var strokeStyle = new StrokeStyle
        {
            Width = Math.Max(bounds.Width, bounds.Height) * 0.01,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        scene.StrokePath(outline, strokeStyle, transform, new SolidColorBrush(RgbaColor.FromBytes(255, 255, 255, 60)));

        var sparkRadius = outlineRadius * 0.35f;
        var spark = new Vector2(outlineCenter.X + sparkRadius, outlineCenter.Y);
        var sparkPath = CreateEllipsePath(spark, outlineRadius * 0.07f, outlineRadius * 0.07f);
        scene.FillPath(sparkPath, FillRule.NonZero, transform, new SolidColorBrush(RgbaColor.FromBytes(255, 255, 255, 180)));

        var spokeStroke = new StrokeStyle
        {
            Width = Math.Max(bounds.Width, bounds.Height) * 0.0015,
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        var spokeBrush = new SolidColorBrush(RgbaColor.FromBytes(255, 255, 255, 24));

        const int Spokes = 12;
        for (var i = 0; i < Spokes; i++)
        {
            var angle = (float)(i * Math.PI * 2 / Spokes);
            var endpoint = new Vector2(
                outlineCenter.X + MathF.Cos(angle) * outlineRadius,
                outlineCenter.Y + MathF.Sin(angle) * outlineRadius);

            var spoke = new PathBuilder();
            spoke.MoveTo(outlineCenter.X, outlineCenter.Y);
            spoke.LineTo(endpoint.X, endpoint.Y);
            scene.StrokePath(spoke, spokeStroke, transform, spokeBrush);
        }

        for (var ringIndex = 1; ringIndex <= 3; ringIndex++)
        {
            var ringRadius = outlineRadius * ringIndex / 3f;
            var ring = CreateEllipsePath(outlineCenter, ringRadius, ringRadius);
            scene.StrokePath(ring, spokeStroke, transform, spokeBrush);
        }
    }

    private void OnAnimatedDraw(object? sender, VelloDrawEventArgs e)
    {
        var bounds = e.Bounds;
        var scene = e.Scene;
        var transform = e.GlobalTransform;
        var time = (float)e.TotalTime.TotalSeconds;

        var animatedBackdrop = CreateRectPath(bounds);
        scene.FillPath(animatedBackdrop, FillRule.NonZero, transform, new SolidColorBrush(RgbaColor.FromBytes(10, 14, 26)));

        var center = new Vector2((float)(bounds.X + bounds.Width / 2f), (float)(bounds.Y + bounds.Height / 2f));
        var baseRadius = MathF.Min((float)bounds.Width, (float)bounds.Height) * 0.32f;

        var ribbon = CreateRibbonPath(center, baseRadius, time);
        var ribbonBrush = new LinearGradientBrush(
            center + new Vector2(-baseRadius, -baseRadius),
            center + new Vector2(baseRadius, baseRadius),
            RibbonStops);
        var ribbonTransform = Matrix3x2.Multiply(Matrix3x2.CreateRotation(time * 0.4f, center), transform);
        scene.FillPath(ribbon, FillRule.NonZero, ribbonTransform, ribbonBrush);

        var orbitRadius = baseRadius * 1.15f;
        var orbit = CreateEllipsePath(center, orbitRadius, orbitRadius);
        scene.StrokePath(orbit, new StrokeStyle
        {
            Width = baseRadius * 0.08,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        }, transform, new SolidColorBrush(RgbaColor.FromBytes(255, 255, 255, 32)));

        var orbRadius = baseRadius * 0.38f;
        var orbCenter = new Vector2(
            center.X + MathF.Cos(time * 1.2f) * orbitRadius * 0.55f,
            center.Y + MathF.Sin(time * 1.2f) * orbitRadius * 0.55f);

        var orb = CreateEllipsePath(orbCenter, orbRadius, orbRadius);
        var orbBrush = new RadialGradientBrush(
            startCenter: orbCenter,
            startRadius: 0f,
            endCenter: orbCenter,
            endRadius: orbRadius,
            stops: OrbStops);
        scene.FillPath(orb, FillRule.NonZero, transform, orbBrush);
    }

    private void OnResetAnimationClick(object? sender, RoutedEventArgs e)
    {
        AnimatedCanvas.Reset();
    }

    private static PathBuilder CreateRectPath(Rect rect)
    {
        var path = new PathBuilder();
        path.MoveTo(rect.X, rect.Y);
        path.LineTo(rect.Right, rect.Y);
        path.LineTo(rect.Right, rect.Bottom);
        path.LineTo(rect.X, rect.Bottom);
        path.Close();
        return path;
    }

    private static PathBuilder CreateRoundedRectPath(Rect rect, double radius)
    {
        var path = new PathBuilder();
        var rx = Math.Min(radius, rect.Width / 2);
        var ry = Math.Min(radius, rect.Height / 2);
        const double Kappa = 0.5522847498307936;
        var kx = rx * Kappa;
        var ky = ry * Kappa;
        var left = rect.X;
        var top = rect.Y;
        var right = rect.Right;
        var bottom = rect.Bottom;

        path.MoveTo(left + rx, top);
        path.LineTo(right - rx, top);
        path.CubicTo(right - rx + kx, top, right, top + ry - ky, right, top + ry);
        path.LineTo(right, bottom - ry);
        path.CubicTo(right, bottom - ry + ky, right - rx + kx, bottom, right - rx, bottom);
        path.LineTo(left + rx, bottom);
        path.CubicTo(left + rx - kx, bottom, left, bottom - ry + ky, left, bottom - ry);
        path.LineTo(left, top + ry);
        path.CubicTo(left, top + ry - ky, left + rx - kx, top, left + rx, top);
        path.Close();
        return path;
    }

    private static PathBuilder CreateEllipsePath(Vector2 center, float radiusX, float radiusY)
    {
        const double Kappa = 0.5522847498307936;
        var path = new PathBuilder();
        var cx = center.X;
        var cy = center.Y;
        var kappaX = radiusX * (float)Kappa;
        var kappaY = radiusY * (float)Kappa;

        path.MoveTo(cx, cy - radiusY);
        path.CubicTo(cx + kappaX, cy - radiusY, cx + radiusX, cy - kappaY, cx + radiusX, cy);
        path.CubicTo(cx + radiusX, cy + kappaY, cx + kappaX, cy + radiusY, cx, cy + radiusY);
        path.CubicTo(cx - kappaX, cy + radiusY, cx - radiusX, cy + kappaY, cx - radiusX, cy);
        path.CubicTo(cx - radiusX, cy - kappaY, cx - kappaX, cy - radiusY, cx, cy - radiusY);
        path.Close();
        return path;
    }

    private static PathBuilder CreateRibbonPath(Vector2 center, float radius, float time)
    {
        var path = new PathBuilder();
        const int Segments = 320;
        for (var i = 0; i <= Segments; i++)
        {
            var t = i / (float)Segments;
            var angle = t * MathF.PI * 2f + time * 0.85f;
            var wobble = MathF.Sin(t * 14f + time * 1.3f) * radius * 0.18f;
            var currentRadius = radius + wobble;
            var x = center.X + MathF.Cos(angle) * currentRadius;
            var y = center.Y + MathF.Sin(angle) * currentRadius;
            if (i == 0)
            {
                path.MoveTo(x, y);
            }
            else
            {
                path.LineTo(x, y);
            }
        }

        path.Close();
        return path;
    }
}
