using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace VelloSharp.Avalonia.Vello.Geometry;

internal static class GeometryUtilities
{
    internal readonly struct CubicBezierSegment
    {
        public CubicBezierSegment(Point controlPoint1, Point controlPoint2, Point endPoint)
        {
            ControlPoint1 = controlPoint1;
            ControlPoint2 = controlPoint2;
            EndPoint = endPoint;
        }

        public Point ControlPoint1 { get; }
        public Point ControlPoint2 { get; }
        public Point EndPoint { get; }
    }

    public static IReadOnlyList<CubicBezierSegment> ArcToBezier(
        Point start,
        Point end,
        Size radius,
        double xAxisRotation,
        bool isLargeArc,
        SweepDirection sweepDirection)
    {
        var segments = new List<CubicBezierSegment>();

        var rx = Math.Abs(radius.Width);
        var ry = Math.Abs(radius.Height);

        if (rx < double.Epsilon || ry < double.Epsilon || start == end)
        {
            segments.Add(new CubicBezierSegment(start, end, end));
            return segments;
        }

        var phi = xAxisRotation * Math.PI / 180.0;
        var cosPhi = Math.Cos(phi);
        var sinPhi = Math.Sin(phi);

        var dx = (start.X - end.X) / 2.0;
        var dy = (start.Y - end.Y) / 2.0;

        var x1p = cosPhi * dx + sinPhi * dy;
        var y1p = -sinPhi * dx + cosPhi * dy;

        var rxSq = rx * rx;
        var rySq = ry * ry;
        var x1pSq = x1p * x1p;
        var y1pSq = y1p * y1p;

        var radiiCheck = x1pSq / rxSq + y1pSq / rySq;
        if (radiiCheck > 1)
        {
            var scale = Math.Sqrt(radiiCheck);
            rx *= scale;
            ry *= scale;
            rxSq = rx * rx;
            rySq = ry * ry;
        }

        var sign = (isLargeArc == (sweepDirection == SweepDirection.Clockwise)) ? -1 : 1;
        var sq = ((rxSq * rySq) - (rxSq * y1pSq) - (rySq * x1pSq)) /
                 ((rxSq * y1pSq) + (rySq * x1pSq));
        sq = sq < 0 ? 0 : sq;
        var coef = sign * Math.Sqrt(sq);

        var cxp = coef * (rx * y1p) / ry;
        var cyp = coef * -(ry * x1p) / rx;

        var cx = cosPhi * cxp - sinPhi * cyp + (start.X + end.X) / 2.0;
        var cy = sinPhi * cxp + cosPhi * cyp + (start.Y + end.Y) / 2.0;

        var startVector = new Vector((x1p - cxp) / rx, (y1p - cyp) / ry);
        var endVector = new Vector((-x1p - cxp) / rx, (-y1p - cyp) / ry);

        var startAngle = AngleBetween(new Vector(1, 0), startVector);
        var sweepAngle = AngleBetween(startVector, endVector);

        if (!isLargeArc && sweepAngle > Math.PI)
        {
            sweepAngle -= 2 * Math.PI;
        }
        else if (isLargeArc && sweepAngle < Math.PI)
        {
            sweepAngle += 2 * Math.PI;
        }

        if (sweepDirection == SweepDirection.CounterClockwise && sweepAngle > 0)
        {
            sweepAngle -= 2 * Math.PI;
        }
        else if (sweepDirection == SweepDirection.Clockwise && sweepAngle < 0)
        {
            sweepAngle += 2 * Math.PI;
        }

        var numSegments = (int)Math.Ceiling(Math.Abs(sweepAngle / (Math.PI / 2)));
        var delta = sweepAngle / numSegments;
        var t = 8.0 / 3.0 * Math.Sin(delta / 4) * Math.Sin(delta / 4) / Math.Sin(delta / 2);

        var currentAngle = startAngle;
        var currentPoint = start;

        for (int i = 0; i < numSegments; i++)
        {
            var nextAngle = currentAngle + delta;
            var sinCurrent = Math.Sin(currentAngle);
            var cosCurrent = Math.Cos(currentAngle);
            var sinNext = Math.Sin(nextAngle);
            var cosNext = Math.Cos(nextAngle);

            var endpoint = CalculatePoint(cx, cy, rx, ry, phi, cosNext, sinNext);

            var cp1 = currentPoint + new Vector(
                t * (-rx * sinCurrent * cosPhi - ry * cosCurrent * sinPhi),
                t * (-rx * sinCurrent * sinPhi + ry * cosCurrent * cosPhi));

            var cp2 = endpoint + new Vector(
                t * (rx * sinNext * cosPhi + ry * cosNext * sinPhi),
                t * (rx * sinNext * sinPhi - ry * cosNext * cosPhi));

            segments.Add(new CubicBezierSegment(cp1, cp2, endpoint));

            currentPoint = endpoint;
            currentAngle = nextAngle;
        }

        return segments;
    }

    private static double AngleBetween(Vector u, Vector v)
    {
        var dot = u.X * v.X + u.Y * v.Y;
        var len = Math.Sqrt(u.SquaredLength * v.SquaredLength);
        if (len == 0)
        {
            return 0;
        }

        var cos = Math.Clamp(dot / len, -1.0, 1.0);
        var sign = Math.Sign(u.X * v.Y - u.Y * v.X);
        return sign * Math.Acos(cos);
    }

    private static Point CalculatePoint(double cx, double cy, double rx, double ry, double phi, double cosAngle, double sinAngle)
    {
        var x = cx + rx * cosAngle * Math.Cos(phi) - ry * sinAngle * Math.Sin(phi);
        var y = cy + rx * cosAngle * Math.Sin(phi) + ry * sinAngle * Math.Cos(phi);
        return new Point(x, y);
    }
}
