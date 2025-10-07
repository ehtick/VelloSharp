using System;
using System.Collections.Generic;
using System.Linq;

namespace VelloSharp.Charting.Layout;

/// <summary>
/// Computes layout regions for chart axes and plot panels while respecting device pixel density.
/// </summary>
public sealed class ChartLayoutEngine
{
    public ChartLayoutResult Arrange(ChartLayoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var width = request.ViewportWidth;
        var height = request.ViewportHeight;
        var dpr = request.DevicePixelRatio;

        var axes = new List<AxisLayout>();
        var axisByOrientation = request.Axes
            .GroupBy(a => a.Orientation)
            .ToDictionary(g => g.Key, g => g.First());

        var left = GetRoundedThickness(axisByOrientation, AxisOrientation.Left, dpr);
        var right = GetRoundedThickness(axisByOrientation, AxisOrientation.Right, dpr);
        var top = GetRoundedThickness(axisByOrientation, AxisOrientation.Top, dpr);
        var bottom = GetRoundedThickness(axisByOrientation, AxisOrientation.Bottom, dpr);

        var plotWidth = Math.Max(0d, width - left - right);
        var plotHeight = Math.Max(0d, height - top - bottom);
        var plotArea = new LayoutRect(left, top, plotWidth, plotHeight);

        if (axisByOrientation.ContainsKey(AxisOrientation.Left))
        {
            axes.Add(new AxisLayout(
                AxisOrientation.Left,
                new LayoutRect(0d, top, left, plotHeight),
                left));
        }

        if (axisByOrientation.ContainsKey(AxisOrientation.Right))
        {
            axes.Add(new AxisLayout(
                AxisOrientation.Right,
                new LayoutRect(width - right, top, right, plotHeight),
                right));
        }

        if (axisByOrientation.ContainsKey(AxisOrientation.Top))
        {
            axes.Add(new AxisLayout(
                AxisOrientation.Top,
                new LayoutRect(left, 0d, plotWidth, top),
                top));
        }

        if (axisByOrientation.ContainsKey(AxisOrientation.Bottom))
        {
            axes.Add(new AxisLayout(
                AxisOrientation.Bottom,
                new LayoutRect(left, height - bottom, plotWidth, bottom),
                bottom));
        }

        return new ChartLayoutResult(plotArea, axes);
    }

    private static double GetRoundedThickness(
        IReadOnlyDictionary<AxisOrientation, AxisLayoutRequest> requests,
        AxisOrientation orientation,
        double dpr)
    {
        if (!requests.TryGetValue(orientation, out var request))
        {
            return 0d;
        }

        var thickness = AlignToPixel(request.Thickness, dpr);

        if (request.MinThickness is { } min)
        {
            thickness = Math.Max(thickness, AlignToPixel(min, dpr));
        }

        if (request.MaxThickness is { } max)
        {
            thickness = Math.Min(thickness, AlignToPixel(max, dpr));
        }

        return thickness;
    }

    private static double AlignToPixel(double value, double dpr)
    {
        var pixels = Math.Ceiling(value * dpr);
        return pixels / dpr;
    }
}
