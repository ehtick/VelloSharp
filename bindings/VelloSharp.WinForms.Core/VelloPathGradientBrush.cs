using System;
using System.Drawing;
using System.Numerics;
using VelloSharp;

namespace VelloSharp.WinForms;

public sealed class VelloPathGradientBrush : VelloBrush
{
    public VelloPathGradientBrush(PointF center, float radius, Color centerColor, Color surroundColor)
    {
        if (!float.IsFinite(radius) || radius <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        Center = center;
        Radius = radius;
        CenterColor = centerColor;
        SurroundColor = surroundColor;
    }

    public VelloPathGradientBrush(ReadOnlySpan<PointF> points, Color centerColor, Color surroundColor)
    {
        if (points.Length < 3)
        {
            throw new ArgumentException("At least three points are required to construct a path gradient brush.", nameof(points));
        }

        Center = ComputeCentroid(points);
        Radius = ComputeMaxDistance(points, Center);
        if (Radius <= 0f)
        {
            Radius = 1f;
        }

        CenterColor = centerColor;
        SurroundColor = surroundColor;
    }

    public PointF Center { get; set; }

    public float Radius { get; set; }

    public Color CenterColor { get; set; }

    public Color SurroundColor { get; set; }

    public ExtendMode ExtendMode { get; set; } = ExtendMode.Pad;

    protected override Brush CreateCoreBrushCore()
    {
        if (!float.IsFinite(Radius) || Radius <= 0f)
        {
            throw new InvalidOperationException("Radius must be positive.");
        }

        var center = new Vector2(Center.X, Center.Y);
        var stops = new[]
        {
            new GradientStop(0f, VelloColorHelpers.ToRgba(CenterColor)),
            new GradientStop(1f, VelloColorHelpers.ToRgba(SurroundColor)),
        };

        return new RadialGradientBrush(center, 0f, center, Radius, stops, ExtendMode);
    }

    private static PointF ComputeCentroid(ReadOnlySpan<PointF> points)
    {
        double sumX = 0;
        double sumY = 0;
        for (var i = 0; i < points.Length; i++)
        {
            sumX += points[i].X;
            sumY += points[i].Y;
        }

        return new PointF((float)(sumX / points.Length), (float)(sumY / points.Length));
    }

    private static float ComputeMaxDistance(ReadOnlySpan<PointF> points, PointF center)
    {
        var max = 0f;
        for (var i = 0; i < points.Length; i++)
        {
            var dx = points[i].X - center.X;
            var dy = points[i].Y - center.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance > max)
            {
                max = (float)distance;
            }
        }

        return max;
    }
}
