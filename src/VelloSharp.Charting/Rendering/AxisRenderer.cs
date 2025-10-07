using System;
using System.Collections.Generic;
using VelloSharp.Charting.Axis;
using VelloSharp.Charting.Layout;
using VelloSharp.Charting.Styling;
using ChartRgbaColor = VelloSharp.Charting.Styling.RgbaColor;
using VelloSharp.Charting.Ticks;

namespace VelloSharp.Charting.Rendering;

/// <summary>
/// Converts axis render surface data into drawable visuals.
/// </summary>
public sealed class AxisRenderer
{
    public AxisRenderResult Render(AxisRenderSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var visuals = new List<AxisVisual>(surface.Axes.Count);
        foreach (var axis in surface.Axes)
        {
            visuals.Add(RenderAxis(axis));
        }

        return new AxisRenderResult(surface.PlotArea, visuals);
    }

    private static AxisVisual RenderAxis(AxisRenderModel model)
    {
        var layout = model.Layout.Bounds;
        var style = model.Style;
        var axisLine = BuildAxisLine(model.Orientation, layout, style.LineColor);
        var ticks = new List<AxisTickVisual>(model.Ticks.Count);
        var labels = new List<AxisLabelVisual>(model.Ticks.Count);

        foreach (var tick in model.Ticks)
        {
            var (tickVisual, labelVisual) = BuildTickVisual(model.Orientation, layout, style, tick);
            ticks.Add(tickVisual);
            labels.Add(labelVisual);
        }

        return new AxisVisual(model, axisLine, ticks, labels);
    }

    private static AxisLineVisual BuildAxisLine(AxisOrientation orientation, LayoutRect bounds, ChartRgbaColor color)
    {
        return orientation switch
        {
            AxisOrientation.Left => new AxisLineVisual(bounds.Right, bounds.Y, bounds.Right, bounds.Bottom, color),
            AxisOrientation.Right => new AxisLineVisual(bounds.X, bounds.Y, bounds.X, bounds.Bottom, color),
            AxisOrientation.Top => new AxisLineVisual(bounds.X, bounds.Bottom, bounds.Right, bounds.Bottom, color),
            AxisOrientation.Bottom => new AxisLineVisual(bounds.X, bounds.Y, bounds.Right, bounds.Y, color),
            _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, null),
        };
    }

    private static (AxisTickVisual Tick, AxisLabelVisual Label) BuildTickVisual(
        AxisOrientation orientation,
        LayoutRect bounds,
        AxisStyle style,
        AxisTickInfo tick)
    {
        var tickColor = style.TickColor;
        var typography = style.LabelTypography;
        var labelColor = style.TickColor;
        switch (orientation)
        {
            case AxisOrientation.Left:
            {
                var y = bounds.Bottom - tick.UnitPosition * bounds.Height;
                var x = bounds.Right;
                var tickVisual = new AxisTickVisual(x, y, x - style.TickLength, y, tickColor);
                var labelX = x - style.TickLength - style.LabelMargin;
                var label = new AxisLabelVisual(
                    labelX,
                    y,
                    tick.Label,
                    typography,
                    orientation,
                    TextAlignment.End,
                    TextAlignment.Center,
                    labelColor);
                return (tickVisual, label);
            }
            case AxisOrientation.Right:
            {
                var y = bounds.Bottom - tick.UnitPosition * bounds.Height;
                var x = bounds.X;
                var tickVisual = new AxisTickVisual(x, y, x + style.TickLength, y, tickColor);
                var labelX = x + style.TickLength + style.LabelMargin;
                var label = new AxisLabelVisual(
                    labelX,
                    y,
                    tick.Label,
                    typography,
                    orientation,
                    TextAlignment.Start,
                    TextAlignment.Center,
                    labelColor);
                return (tickVisual, label);
            }
            case AxisOrientation.Top:
            {
                var x = bounds.X + tick.UnitPosition * bounds.Width;
                var y = bounds.Bottom;
                var tickVisual = new AxisTickVisual(x, y, x, y - style.TickLength, tickColor);
                var labelY = y - style.TickLength - style.LabelMargin;
                var label = new AxisLabelVisual(
                    x,
                    labelY,
                    tick.Label,
                    typography,
                    orientation,
                    TextAlignment.Center,
                    TextAlignment.End,
                    labelColor);
                return (tickVisual, label);
            }
            case AxisOrientation.Bottom:
            {
                var x = bounds.X + tick.UnitPosition * bounds.Width;
                var y = bounds.Y;
                var tickVisual = new AxisTickVisual(x, y, x, y + style.TickLength, tickColor);
                var labelY = y + style.TickLength + style.LabelMargin;
                var label = new AxisLabelVisual(
                    x,
                    labelY,
                    tick.Label,
                    typography,
                    orientation,
                    TextAlignment.Center,
                    TextAlignment.Start,
                    labelColor);
                return (tickVisual, label);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(orientation), orientation, null);
        }
    }
}
