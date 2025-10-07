using System;
using System.Collections.Generic;
using VelloSharp.ChartEngine;
using VelloSharp.ChartEngine.Annotations;
using VelloSharp.Charting.Axis;
using VelloSharp.Charting.Layout;
using VelloSharp.Charting.Legend;
using VelloSharp.Charting.Rendering.Text;
using VelloSharp.Charting.Scales;
using VelloSharp.Charting.Styling;
using VelloSharp.Charting.Ticks;
using static VelloSharp.Charting.Rendering.SceneDrawing;
using VScene = VelloSharp.Scene;
using ChartRgbaColor = VelloSharp.Charting.Styling.RgbaColor;

namespace VelloSharp.Charting.Rendering;

public sealed class ChartOverlayRenderer : IDisposable
{
    private readonly AxisTickGeneratorRegistry _tickRegistry = AxisTickGeneratorRegistry.CreateDefault();
    private readonly AxisRenderer _axisRenderer = new();
    private readonly LegendRenderer _legendRenderer = new();
    private readonly TextRenderer _textRenderer = new();
    private readonly GridlineRenderer _gridlineRenderer = new();
    private readonly AnnotationRenderer _annotationRenderer = new();

    private readonly record struct PaneView(
        int Index,
        string Id,
        LayoutRect Bounds,
        double ValueMin,
        double ValueMax,
        bool ShareXAxisWithPrimary,
        IReadOnlyList<ChartFrameMetadata.AxisTickMetadata> ValueTicks);

    public void Render(
        VScene scene,
        ChartFrameMetadata metadata,
        double width,
        double height,
        double devicePixelRatio,
        ChartTheme theme,
        LegendDefinition? legendDefinition = null,
        IReadOnlyList<ChartAnnotation>? annotations = null,
        bool renderAxes = true)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(theme);

        if (width <= 0 || height <= 0)
        {
            return;
        }

        var plotArea = new LayoutRect(metadata.PlotLeft, metadata.PlotTop, metadata.PlotWidth, metadata.PlotHeight);
        var paneViews = BuildPaneViews(metadata);

        var leftThickness = Math.Max(metadata.PlotLeft, 0d);
        var bottomThickness = Math.Max(height - (metadata.PlotTop + metadata.PlotHeight), 0d);

        var timeStart = DateTimeOffset.FromUnixTimeMilliseconds(
            (long)Math.Round(metadata.RangeStartSeconds * 1000.0));
        var timeEnd = DateTimeOffset.FromUnixTimeMilliseconds(
            (long)Math.Round(metadata.RangeEndSeconds * 1000.0));
        if (timeEnd <= timeStart)
        {
            timeEnd = timeStart.AddSeconds(1);
        }

        var timeScale = new TimeScale(timeStart, timeEnd);
        var timeTickTarget = Math.Max(metadata.TimeTicks.Count, 4);

        var bottomAxis = new AxisDefinition<DateTimeOffset>(
            "axis-time",
            AxisOrientation.Bottom,
            Math.Max(bottomThickness, 0d),
            timeScale,
            theme.Axis,
            new TickGenerationOptions<DateTimeOffset> { TargetTickCount = timeTickTarget });

        var bottomLayoutRect = new LayoutRect(
            plotArea.X,
            plotArea.Y + plotArea.Height,
            plotArea.Width,
            Math.Max(bottomThickness, 0d));

        var bottomLayout = new AxisLayout(
            AxisOrientation.Bottom,
            bottomLayoutRect,
            Math.Max(bottomThickness, 0d));

        var timeAxisSurface = new AxisRenderSurface(
            plotArea,
            new[] { bottomAxis.Build(bottomLayout, _tickRegistry) });

        var timeAxisResult = _axisRenderer.Render(timeAxisSurface);

        if (renderAxes)
        {
            _gridlineRenderer.Render(scene, timeAxisResult, plotArea, theme);
            DrawAxes(scene, timeAxisResult);
            DrawAxisLabels(scene, timeAxisResult);
        }

        var seriesByPane = new Dictionary<int, List<ChartFrameMetadata.SeriesMetadata>>(paneViews.Count);
        foreach (var pane in paneViews)
        {
            seriesByPane[pane.Index] = new List<ChartFrameMetadata.SeriesMetadata>();
        }

        if (paneViews.Count > 0)
        {
            var fallbackIndex = paneViews[0].Index;
            foreach (var series in metadata.Series)
            {
                var targetIndex = seriesByPane.ContainsKey(series.PaneIndex)
                    ? series.PaneIndex
                    : fallbackIndex;
                seriesByPane[targetIndex].Add(series);
            }
        }

        foreach (var pane in paneViews)
        {
            var valueScale = new LinearScale(pane.ValueMin, pane.ValueMax);
            var valueTickTarget = Math.Max(pane.ValueTicks.Count, 4);

            var valueAxis = new AxisDefinition<double>(
                $"axis-value-{pane.Index}",
                AxisOrientation.Left,
                Math.Max(leftThickness, 0d),
                valueScale,
                theme.Axis,
                new TickGenerationOptions<double> { TargetTickCount = valueTickTarget });

            var leftLayoutRect = new LayoutRect(
                Math.Max(pane.Bounds.X - leftThickness, 0d),
                pane.Bounds.Y,
                Math.Max(leftThickness, 0d),
                pane.Bounds.Height);

            var leftLayout = new AxisLayout(
                AxisOrientation.Left,
                leftLayoutRect,
                Math.Max(leftThickness, 0d));

            var paneSurface = new AxisRenderSurface(
                pane.Bounds,
                new[] { valueAxis.Build(leftLayout, _tickRegistry) });

            var paneResult = _axisRenderer.Render(paneSurface);

            if (renderAxes)
            {
                _gridlineRenderer.Render(scene, paneResult, pane.Bounds, theme);
                DrawAxes(scene, paneResult);
                DrawAxisLabels(scene, paneResult);
            }

            if (legendDefinition is null &&
                seriesByPane.TryGetValue(pane.Index, out var paneSeries) &&
                paneSeries.Count > 0)
            {
                var legend = CreateLegendDefinition($"legend-{pane.Id}", paneSeries);
                if (legend.Items.Count > 0)
                {
                    var legendSurface = new AxisRenderSurface(
                        pane.Bounds,
                        Array.Empty<AxisRenderModel>());
                    var legendVisual = _legendRenderer.Render(legend, legendSurface, theme);
                    DrawLegend(scene, legendVisual, theme);
                }
            }
        }

        if (legendDefinition is not null)
        {
            var legendSurface = new AxisRenderSurface(
                plotArea,
                Array.Empty<AxisRenderModel>());
            var legendVisual = _legendRenderer.Render(legendDefinition, legendSurface, theme);
            DrawLegend(scene, legendVisual, theme);
        }

        if (annotations is { Count: > 0 })
        {
            _annotationRenderer.Render(scene, annotations, plotArea, metadata, theme);
        }
    }

    private static List<PaneView> BuildPaneViews(ChartFrameMetadata metadata)
    {
        var panes = new List<PaneView>();
        if (metadata.Panes.Count > 0)
        {
            for (var i = 0; i < metadata.Panes.Count; i++)
            {
                var pane = metadata.Panes[i];
                var rect = new LayoutRect(pane.PlotLeft, pane.PlotTop, pane.PlotWidth, pane.PlotHeight);
                panes.Add(new PaneView(
                    i,
                    string.IsNullOrEmpty(pane.Id) ? $"pane-{i}" : pane.Id,
                    rect,
                    pane.ValueMin,
                    pane.ValueMax,
                    pane.ShareXAxisWithPrimary,
                    pane.ValueTicks));
            }
        }
        else
        {
            panes.Add(new PaneView(
                0,
                "pane-primary",
                new LayoutRect(metadata.PlotLeft, metadata.PlotTop, metadata.PlotWidth, metadata.PlotHeight),
                metadata.ValueMin,
                metadata.ValueMax,
                true,
                metadata.ValueTicks));
        }

        return panes;
    }

    private void DrawAxes(VScene scene, AxisRenderResult result)
    {
        foreach (var axis in result.Axes)
        {
            if (axis.Model.Orientation is not AxisOrientation.Left and not AxisOrientation.Bottom)
            {
                continue;
            }

            DrawLine(scene, axis.AxisLine.X1, axis.AxisLine.Y1, axis.AxisLine.X2, axis.AxisLine.Y2, axis.AxisLine.Color, 1.0);

            foreach (var tick in axis.Ticks)
            {
                DrawLine(scene, tick.X1, tick.Y1, tick.X2, tick.Y2, tick.Color, 1.0);
            }
        }
    }

    private void DrawAxisLabels(VScene scene, AxisRenderResult result)
    {
        foreach (var axis in result.Axes)
        {
            if (axis.Model.Orientation is not AxisOrientation.Left and not AxisOrientation.Bottom)
            {
                continue;
            }

            foreach (var label in axis.Labels)
            {
                var fontSize = (float)label.Typography.FontSize;
                _textRenderer.Draw(scene, label, fontSize);
            }
        }
    }

    private static LegendDefinition CreateLegendDefinition(
        string legendId,
        IEnumerable<ChartFrameMetadata.SeriesMetadata> seriesMetadata)
    {
        var items = new List<LegendItem>();
        foreach (var series in seriesMetadata)
        {
            var color = ChartRgbaColor.FromChartColor(series.Color);
            var label = string.IsNullOrWhiteSpace(series.Label) ? $"Series {series.SeriesId}" : series.Label;
            items.Add(new LegendItem(
                label,
                color,
                series.Kind,
                series.StrokeWidth,
                series.FillOpacity,
                series.MarkerSize));
        }

        return new LegendDefinition(
            legendId,
            LegendOrientation.Vertical,
            LegendPosition.InsideTopRight,
            items);
    }

    private void DrawLegend(VScene scene, LegendVisual legend, ChartTheme theme)
    {
        var style = theme.Legend;
        FillRectangle(scene, legend.Bounds, style.Background);
        if (style.BorderThickness > 0.0)
        {
            StrokeRectangle(scene, legend.Bounds, style.Border, style.BorderThickness);
        }

        foreach (var item in legend.Items)
        {
            DrawLegendMarker(scene, item, style);
            var labelVisual = new AxisLabelVisual(
                item.TextX,
                item.TextY,
                item.Label,
                style.LabelTypography,
                AxisOrientation.Bottom,
                TextAlignment.Start,
                TextAlignment.Center,
                style.LabelTypography.FontWeight?.Equals("bold", StringComparison.OrdinalIgnoreCase) == true
                    ? item.Color
                    : theme.Palette.Foreground);

            var fontSize = (float)style.LabelTypography.FontSize;
            _textRenderer.Draw(scene, labelVisual, fontSize);
        }
    }

    private static void DrawLegendMarker(VScene scene, LegendItemVisual item, LegendStyle style)
    {
        var size = item.MarkerSize > 0 ? item.MarkerSize : style.MarkerSize;
        var markerRect = new LayoutRect(item.MarkerX, item.MarkerY, size, size);

        switch (item.Kind)
        {
            case ChartSeriesKind.Line:
                if (item.FillOpacity > 0)
                {
                    var fill = item.Color.WithAlpha(ToByteOpacity(item.FillOpacity));
                    FillRectangle(scene, markerRect, fill);
                }

                var centerY = item.MarkerY + size / 2d;
                DrawLine(scene, item.MarkerX, centerY, item.MarkerX + size, centerY, item.Color, Math.Max(1.0, item.StrokeWidth));
                break;

            case ChartSeriesKind.Area:
                var areaFill = item.Color.WithAlpha(ToByteOpacity(item.FillOpacity <= 0 ? 0.45 : item.FillOpacity));
                FillRectangle(scene, markerRect, areaFill);
                DrawLine(scene, item.MarkerX, item.MarkerY, item.MarkerX + size, item.MarkerY, item.Color, Math.Max(1.0, item.StrokeWidth));
                break;

            case ChartSeriesKind.Scatter:
                FillRectangle(scene, markerRect, item.Color);
                StrokeRectangle(scene, markerRect, item.Color, Math.Max(1.0, item.StrokeWidth));
                break;

            case ChartSeriesKind.Bar:
                FillRectangle(scene, markerRect, item.Color);
                break;

            case ChartSeriesKind.Band:
            {
                var bandOpacity = item.FillOpacity <= 0 ? 0.25 : item.FillOpacity;
                var bandFill = item.Color.WithAlpha(ToByteOpacity(bandOpacity));
                FillRectangle(scene, markerRect, bandFill);

                var topY = item.MarkerY;
                var bottomY = item.MarkerY + size;
                DrawLine(scene, item.MarkerX, topY, item.MarkerX + size, topY, item.Color, Math.Max(1.0, item.StrokeWidth));

                var lowerOpacity = Math.Clamp(bandOpacity * 0.6 + 0.2, 0d, 1d);
                var lowerColor = item.Color.WithAlpha(ToByteOpacity(lowerOpacity));
                DrawLine(scene, item.MarkerX, bottomY, item.MarkerX + size, bottomY, lowerColor, Math.Max(1.0, item.StrokeWidth * 0.8));
                break;
            }

            case ChartSeriesKind.Heatmap:
            {
                var heatmapOpacity = item.FillOpacity <= 0 ? 0.85 : item.FillOpacity;
                var heatFill = item.Color.WithAlpha(ToByteOpacity(heatmapOpacity));
                FillRectangle(scene, markerRect, heatFill);

                var borderOpacity = Math.Clamp(heatmapOpacity * 0.5 + 0.2, 0d, 1d);
                var borderColor = item.Color.WithAlpha(ToByteOpacity(borderOpacity));
                StrokeRectangle(scene, markerRect, borderColor, 1.0);
                break;
            }

            default:
                FillRectangle(scene, markerRect, item.Color);
                break;
        }
    }

    private static byte ToByteOpacity(double opacity)
    {
        var clamped = Math.Clamp(opacity, 0d, 1d);
        return (byte)Math.Round(clamped * 255d);
    }

    public void Dispose()
    {
        _textRenderer.Dispose();
    }
}
