using System;
using System.Collections.Generic;
using VelloSharp.ChartEngine;
using VelloSharp.Charting.Axis;
using VelloSharp.Charting.Layout;
using VelloSharp.Charting.Rendering;
using VelloSharp.Charting.Styling;

namespace VelloSharp.Charting.Legend;

/// <summary>
/// Computes legend visuals relative to the chart surface.
/// </summary>
public sealed class LegendRenderer
{
    private const double PlacementMargin = 8d;

    public LegendVisual Render(LegendDefinition definition, AxisRenderSurface surface, ChartTheme theme)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(theme);

        var style = theme.Legend;
        var lineHeight = ResolveLineHeight(style);
        var (width, height, itemVisuals) = definition.Orientation switch
        {
            LegendOrientation.Vertical => LayoutVertical(definition, style, lineHeight),
            LegendOrientation.Horizontal => LayoutHorizontal(definition, style, lineHeight),
            _ => throw new ArgumentOutOfRangeException(nameof(definition.Orientation), definition.Orientation, null),
        };

        var bounds = PositionLegend(definition.Position, surface.PlotArea, width, height);
        var offsetItems = OffsetItems(itemVisuals, bounds);

        return new LegendVisual(definition, bounds, style, offsetItems);
    }

    private static double ResolveLineHeight(LegendStyle style)
    {
        return style.LabelTypography.LineHeight
            ?? style.LabelTypography.FontSize * 1.4d;
    }

    private static (double Width, double Height, List<LegendItemVisual> Items) LayoutVertical(
        LegendDefinition definition,
        LegendStyle style,
        double lineHeight)
    {
        var items = new List<LegendItemVisual>(definition.Items.Count);
        var maxLabelWidth = 0d;
        foreach (var item in definition.Items)
        {
            var estimated = EstimateLabelWidth(item.Label, style.LabelTypography);
            maxLabelWidth = Math.Max(maxLabelWidth, estimated);
        }

        var maxMarkerSize = ResolveMaxMarkerSize(definition.Items, style);
        var itemHeight = Math.Max(lineHeight, maxMarkerSize);
        var width = style.Padding * 2d + maxMarkerSize + style.LabelSpacing + maxLabelWidth;
        var height = style.Padding * 2d
                     + definition.Items.Count * itemHeight
                     + Math.Max(0, definition.Items.Count - 1) * style.ItemSpacing;

        var currentY = style.Padding;
        foreach (var item in definition.Items)
        {
            var markerSize = ResolveMarkerSize(item, style);
            var markerX = style.Padding;
            var markerY = currentY + (itemHeight - markerSize) / 2d;
            var textX = markerX + markerSize + style.LabelSpacing;
            var textY = currentY + itemHeight / 2d;
            items.Add(new LegendItemVisual(
                item.Label,
                item.Color,
                item.Kind,
                item.StrokeWidth,
                item.FillOpacity,
                markerX,
                markerY,
                markerSize,
                textX,
                textY,
                style.LabelTypography));
            currentY += itemHeight + style.ItemSpacing;
        }

        return (width, height, items);
    }

    private static (double Width, double Height, List<LegendItemVisual> Items) LayoutHorizontal(
        LegendDefinition definition,
        LegendStyle style,
        double lineHeight)
    {
        var items = new List<LegendItemVisual>(definition.Items.Count);
        var currentX = style.Padding;
        var maxMarkerSize = ResolveMaxMarkerSize(definition.Items, style);
        var maxHeight = Math.Max(lineHeight, maxMarkerSize);

        if (definition.Items.Count == 0)
        {
            var widthEmpty = style.Padding * 2d;
            var heightEmpty = style.Padding * 2d + maxHeight;
            return (widthEmpty, heightEmpty, items);
        }

        foreach (var item in definition.Items)
        {
            var labelWidth = EstimateLabelWidth(item.Label, style.LabelTypography);
            var markerSize = ResolveMarkerSize(item, style);
            var markerX = currentX;
            var markerY = style.Padding + (maxHeight - markerSize) / 2d;
            var textX = markerX + markerSize + style.LabelSpacing;
            var textY = style.Padding + maxHeight / 2d;
            items.Add(new LegendItemVisual(
                item.Label,
                item.Color,
                item.Kind,
                item.StrokeWidth,
                item.FillOpacity,
                markerX,
                markerY,
                markerSize,
                textX,
                textY,
                style.LabelTypography));

            currentX = textX + labelWidth + style.ItemSpacing;
        }

        var width = currentX - style.ItemSpacing + style.Padding;
        var height = style.Padding * 2d + maxHeight;

        return (width, height, items);
    }

    private static LayoutRect PositionLegend(LegendPosition position, LayoutRect plotArea, double legendWidth, double legendHeight)
    {
        return position switch
        {
            LegendPosition.InsideTopLeft => new LayoutRect(
                plotArea.X + PlacementMargin,
                plotArea.Y + PlacementMargin,
                legendWidth,
                legendHeight),
            LegendPosition.InsideTopRight => new LayoutRect(
                plotArea.Right - legendWidth - PlacementMargin,
                plotArea.Y + PlacementMargin,
                legendWidth,
                legendHeight),
            LegendPosition.InsideBottomLeft => new LayoutRect(
                plotArea.X + PlacementMargin,
                plotArea.Bottom - legendHeight - PlacementMargin,
                legendWidth,
                legendHeight),
            LegendPosition.InsideBottomRight => new LayoutRect(
                plotArea.Right - legendWidth - PlacementMargin,
                plotArea.Bottom - legendHeight - PlacementMargin,
                legendWidth,
                legendHeight),
            LegendPosition.OutsideTop => new LayoutRect(
                plotArea.X + (plotArea.Width - legendWidth) / 2d,
                Math.Max(0d, plotArea.Y - legendHeight - PlacementMargin),
                legendWidth,
                legendHeight),
            LegendPosition.OutsideBottom => new LayoutRect(
                plotArea.X + (plotArea.Width - legendWidth) / 2d,
                plotArea.Bottom + PlacementMargin,
                legendWidth,
                legendHeight),
            _ => new LayoutRect(plotArea.Right - legendWidth - PlacementMargin, plotArea.Y + PlacementMargin, legendWidth, legendHeight),
        };
    }

    private static List<LegendItemVisual> OffsetItems(List<LegendItemVisual> items, LayoutRect bounds)
    {
        var offsetItems = new List<LegendItemVisual>(items.Count);
        foreach (var item in items)
        {
            offsetItems.Add(item with
            {
                MarkerX = item.MarkerX + bounds.X,
                MarkerY = item.MarkerY + bounds.Y,
                TextX = item.TextX + bounds.X,
                TextY = item.TextY + bounds.Y,
            });
        }

        return offsetItems;
    }

    private static double ResolveMarkerSize(LegendItem item, LegendStyle style)
    {
        return item.Kind switch
        {
            ChartSeriesKind.Scatter => Math.Max(style.MarkerSize, Math.Max(0.0, item.MarkerSize)),
            ChartSeriesKind.Bar => Math.Max(style.MarkerSize * 0.8, style.MarkerSize),
            _ => style.MarkerSize,
        };
    }

    private static double ResolveMaxMarkerSize(IReadOnlyList<LegendItem> items, LegendStyle style)
    {
        var max = style.MarkerSize;
        foreach (var item in items)
        {
            max = Math.Max(max, ResolveMarkerSize(item, style));
        }

        return max;
    }

    private static double EstimateLabelWidth(string label, ChartTypography typography)
    {
        if (string.IsNullOrEmpty(label))
        {
            return 0d;
        }

        const double glyphWidthFactor = 0.6d;
        return typography.FontSize * glyphWidthFactor * label.Length;
    }
}
