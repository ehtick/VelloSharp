using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace AvaloniaVelloBrowserDemo.Controls;

/// <summary>
/// Draws a GPU-backed animated scene using high-frequency Vello primitives to exercise the browser renderer.
/// </summary>
public class AnimatedVelloSceneView : Control
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<AnimatedVelloSceneView, double>(nameof(Progress), 0d);

    public static readonly StyledProperty<double> DevicePixelRatioProperty =
        AvaloniaProperty.Register<AnimatedVelloSceneView, double>(nameof(DevicePixelRatio), 1d);

    private static readonly Typeface LabelTypeface = new(Typeface.Default.FontFamily, Typeface.Default.Style, FontWeight.SemiBold);
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    static AnimatedVelloSceneView()
    {
        AffectsRender<AnimatedVelloSceneView>(ProgressProperty, DevicePixelRatioProperty);
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public double DevicePixelRatio
    {
        get => GetValue(DevicePixelRatioProperty);
        set => SetValue(DevicePixelRatioProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var progress = NormalizeProgress(Progress);
        var dpr = Math.Max(DevicePixelRatio, 0.5);

        DrawBackground(context, bounds, progress);
        DrawGrid(context, bounds, dpr);
        DrawRibbons(context, bounds, progress, dpr);
        DrawOrbitingParticles(context, bounds, progress, dpr);
        DrawIndicators(context, bounds, progress, dpr);
    }

    private static void DrawBackground(DrawingContext context, Rect bounds, double progress)
    {
        var phase = progress * Math.Tau;
        var startColor = Color.FromArgb(
            0xFF,
            ToByte(24 + 120 * (Math.Sin(phase) + 1) * 0.5),
            ToByte(32 + 140 * (Math.Sin(phase + 2.1) + 1) * 0.5),
            ToByte(48 + 150 * (Math.Sin(phase + 4.2) + 1) * 0.5));

        var endColor = Color.FromArgb(
            0xFF,
            ToByte(32 + 100 * (Math.Sin(phase + 0.9) + 1) * 0.5),
            ToByte(34 + 120 * (Math.Sin(phase + 3.3) + 1) * 0.5),
            ToByte(54 + 160 * (Math.Sin(phase + 5.1) + 1) * 0.5));

        var gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(startColor, 0),
                new GradientStop(endColor, 1),
            },
        };

        context.FillRectangle(gradient, bounds);

        var halo = new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            Radius = 0.65,
            GradientStops = new GradientStops
            {
                new GradientStop(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF), 0),
                new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 1),
            },
        };

        context.FillRectangle(halo, bounds);
    }

    private static void DrawGrid(DrawingContext context, Rect bounds, double dpr)
    {
        var spacing = Math.Clamp(48 / Math.Max(dpr, 0.1), 24, 80);
        var gridBrush = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
        var pen = new Pen(gridBrush, Math.Max(0.75, 0.75 / dpr));

        for (var x = bounds.Left; x <= bounds.Right; x += spacing)
        {
            context.DrawLine(pen, new Point(x, bounds.Top), new Point(x, bounds.Bottom));
        }

        for (var y = bounds.Top; y <= bounds.Bottom; y += spacing)
        {
            context.DrawLine(pen, new Point(bounds.Left, y), new Point(bounds.Right, y));
        }
    }

    private static void DrawRibbons(DrawingContext context, Rect bounds, double progress, double dpr)
    {
        const int RibbonCount = 5;
        var radius = Math.Min(bounds.Width, bounds.Height) * 0.45;

        for (var i = 0; i < RibbonCount; i++)
        {
            var ribbonProgress = NormalizeProgress(progress + i * 0.18);
            var geometry = BuildRibbonGeometry(bounds, radius, ribbonProgress, i, RibbonCount);

            try
            {
                var fillStart = Color.FromArgb(
                    0xB0,
                    ToByte(120 + 18 * i),
                    ToByte(60 + 90 * ribbonProgress),
                    ToByte(150 + 18 * i));

                var fillEnd = Color.FromArgb(
                    0xE0,
                    ToByte(190 + 24 * ribbonProgress - 8 * i),
                    ToByte(150 + 70 * ribbonProgress),
                    ToByte(230 - 12 * i + 15 * ribbonProgress));

                var fill = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                    GradientStops = new GradientStops
                    {
                        new GradientStop(fillStart, 0),
                        new GradientStop(fillEnd, 1),
                    },
                };

                var outline = new SolidColorBrush(Color.FromArgb(0xAA, 0x10, 0x18, 0x22));
                var thickness = Math.Max(1.5, 2.5 * dpr * (0.6 + i * 0.12));
                var pen = new Pen(outline, thickness)
                {
                    LineJoin = PenLineJoin.Round,
                    LineCap = PenLineCap.Round,
                };

                context.DrawGeometry(fill, pen, geometry);
            }
            finally
            {
                if (geometry is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }

    private static StreamGeometry BuildRibbonGeometry(Rect bounds, double baseRadius, double progress, int index, int count)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.SetFillRule(FillRule.EvenOdd);

        var center = bounds.Center;
        var segments = 320;
        var basePhase = progress * Math.Tau;
        var radialBias = 0.14 * index;

        for (var step = 0; step <= segments; step++)
        {
            var t = step / (double)segments;
            var angle = basePhase + t * Math.Tau;
            var turbulence = 0.1 * Math.Sin(angle * (1.5 + index * 0.5) + Math.Tau * progress * 1.2);
            var breathing = 0.04 * Math.Cos((angle * 2.0) + index);
            var radius = baseRadius * (0.55 + radialBias + turbulence + breathing);

            var point = new Point(
                center.X + Math.Cos(angle) * radius,
                center.Y + Math.Sin(angle) * radius);

            if (step == 0)
            {
                ctx.BeginFigure(point, true);
            }
            else
            {
                ctx.LineTo(point);
            }
        }

        ctx.EndFigure(true);
        return geometry;
    }

    private static void DrawOrbitingParticles(DrawingContext context, Rect bounds, double progress, double dpr)
    {
        var center = bounds.Center;
        var baseRadius = Math.Min(bounds.Width, bounds.Height) * 0.48;
        var basePhase = progress * Math.Tau;

        var particleCount = 48;
        for (var i = 0; i < particleCount; i++)
        {
            var orbitPhase = basePhase + i * (Math.Tau / particleCount);
            var radialPulse = baseRadius * (0.55 + 0.1 * Math.Sin(orbitPhase * 3 + basePhase * 0.5));
            var position = new Point(
                center.X + Math.Cos(orbitPhase) * radialPulse,
                center.Y + Math.Sin(orbitPhase) * radialPulse);

            var sparklePhase = NormalizeProgress(progress + i / (double)particleCount);
            var radius = Math.Max(2.2, 2.2 * dpr * (0.8 + 0.9 * sparklePhase));
            var color = Color.FromArgb(
                0xD0,
                ToByte(210 + 35 * Math.Sin(orbitPhase)),
                ToByte(200 + 45 * Math.Cos(orbitPhase + 1.3)),
                ToByte(220 + 35 * Math.Sin(orbitPhase + 0.8)));

            var brush = new SolidColorBrush(color);
            context.DrawEllipse(brush, null, position, radius, radius);
        }
    }

    private static void DrawIndicators(DrawingContext context, Rect bounds, double progress, double dpr)
    {
        var center = bounds.Center;
        var crossPen = new Pen(new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)), Math.Max(1, 1.5 * dpr))
        {
            LineCap = PenLineCap.Round,
        };

        var crossSize = Math.Min(bounds.Width, bounds.Height) * 0.08;
        context.DrawLine(crossPen, new Point(center.X - crossSize, center.Y), new Point(center.X + crossSize, center.Y));
        context.DrawLine(crossPen, new Point(center.X, center.Y - crossSize), new Point(center.X, center.Y + crossSize));

        var caption = string.Format(Culture, "progress {0:P0}", progress);
        using var textLayout = new TextLayout(caption, LabelTypeface, 14, Brushes.White);

        var labelBackground = new SolidColorBrush(Color.FromArgb(0x60, 0x00, 0x00, 0x00));
        var padding = new Thickness(10, 6);
        var labelSize = new Size(
            textLayout.Width + padding.Left + padding.Right,
            textLayout.Height + padding.Top + padding.Bottom);
        var labelOrigin = new Point(bounds.Left + 12, bounds.Bottom - labelSize.Height - 12);
        var labelRect = new Rect(labelOrigin, labelSize);

        context.FillRectangle(labelBackground, labelRect);
        textLayout.Draw(context, labelRect.TopLeft + new Avalonia.Vector(padding.Left, padding.Top));
    }

    private static double NormalizeProgress(double value)
        => value - Math.Floor(value);

    private static byte ToByte(double value)
        => (byte)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
}
