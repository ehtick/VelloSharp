using System;
using System.Collections.Generic;
using VelloSharp.ChartEngine;
using VelloSharp.ChartEngine.Annotations;
using VelloSharp.Charting.Axis;
using VelloSharp.Charting.Layout;
using VelloSharp.Charting.Rendering.Text;
using VelloSharp.Charting.Styling;
using ChartRgbaColor = VelloSharp.Charting.Styling.RgbaColor;
using VScene = VelloSharp.Scene;

namespace VelloSharp.Charting.Rendering;

/// <summary>
/// Renders chart annotations using theme-aware styling.
/// </summary>
internal sealed class AnnotationRenderer : IDisposable
{
    private readonly TextRenderer _textRenderer = new();
    private bool _disposed;

    public void RenderPane(
        VScene scene,
        ChartFrameMetadata metadata,
        LayoutRect plotArea,
        string paneId,
        LayoutRect paneBounds,
        double valueMin,
        double valueMax,
        IReadOnlyList<ChartFrameMetadata.AxisTickMetadata> paneValueTicks,
        ChartTheme theme,
        IReadOnlyList<ChartAnnotation>? annotations)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(theme);

        if (annotations is null || annotations.Count == 0)
        {
            return;
        }

        EnsureNotDisposed();

        foreach (var annotation in annotations)
        {
            if (annotation is null)
            {
                continue;
            }

            if (annotation.TargetPaneId is { Length: > 0 } && !string.Equals(annotation.TargetPaneId, paneId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            switch (annotation)
            {
                case HorizontalLineAnnotation horizontal:
                    RenderHorizontalLine(scene, metadata, paneBounds, valueMin, valueMax, paneValueTicks, theme, horizontal);
                    break;

                case VerticalLineAnnotation vertical:
                    RenderVerticalLine(scene, metadata, plotArea, paneBounds, theme, vertical);
                    break;

                case ValueZoneAnnotation zone:
                    RenderValueZone(scene, metadata, paneBounds, valueMin, valueMax, paneValueTicks, theme, zone);
                    break;

                case GradientZoneAnnotation gradient:
                    RenderGradientZone(scene, metadata, paneBounds, valueMin, valueMax, paneValueTicks, theme, gradient);
                    break;

                case TimeRangeAnnotation timeRange:
                    RenderTimeRange(scene, metadata, plotArea, paneBounds, theme, timeRange);
                    break;

                case CalloutAnnotation callout:
                    RenderCallout(scene, metadata, plotArea, paneBounds, valueMin, valueMax, paneValueTicks, theme, callout);
                    break;
            }
        }
    }

    private void RenderHorizontalLine(
        VScene scene,
        ChartFrameMetadata metadata,
        LayoutRect paneBounds,
        double valueMin,
        double valueMax,
        IReadOnlyList<ChartFrameMetadata.AxisTickMetadata> paneValueTicks,
        ChartTheme theme,
        HorizontalLineAnnotation annotation)
    {
        var snappedValue = SnapValue(annotation.Value, annotation.SnapMode, valueMin, valueMax, paneValueTicks);
        var y = ValueToY(snappedValue, paneBounds, valueMin, valueMax);
        var color = ResolveLineColor(annotation.Color, theme);
        var thickness = Math.Max(0.5, annotation.Thickness);

        SceneDrawing.DrawLine(scene, paneBounds.X, y, paneBounds.Right, y, color, thickness);

        if (!string.IsNullOrWhiteSpace(annotation.Label))
        {
            DrawInlineLabel(scene, theme, annotation.Label, paneBounds.X + 6.0, y - 4.0, color);
        }
    }

    private void RenderVerticalLine(
        VScene scene,
        ChartFrameMetadata metadata,
        LayoutRect plotArea,
        LayoutRect paneBounds,
        ChartTheme theme,
        VerticalLineAnnotation annotation)
    {
        var snappedTime = SnapTime(annotation.TimestampSeconds, annotation.SnapMode, metadata);
        var x = TimeToX(snappedTime, plotArea, metadata);
        var color = ResolveLineColor(annotation.Color, theme);
        var thickness = Math.Max(0.5, annotation.Thickness);

        SceneDrawing.DrawLine(scene, x, paneBounds.Y, x, paneBounds.Bottom, color, thickness);

        if (!string.IsNullOrWhiteSpace(annotation.Label))
        {
            DrawInlineLabel(scene, theme, annotation.Label, x + 6.0, paneBounds.Y + 6.0, color);
        }
    }

    private void RenderValueZone(
        VScene scene,
        ChartFrameMetadata metadata,
        LayoutRect paneBounds,
        double valueMin,
        double valueMax,
        IReadOnlyList<ChartFrameMetadata.AxisTickMetadata> paneValueTicks,
        ChartTheme theme,
        ValueZoneAnnotation annotation)
    {
        var minValue = Math.Min(annotation.MinValue, annotation.MaxValue);
        var maxValue = Math.Max(annotation.MinValue, annotation.MaxValue);

        minValue = SnapValue(minValue, annotation.SnapMode, valueMin, valueMax, paneValueTicks);
        maxValue = SnapValue(maxValue, annotation.SnapMode, valueMin, valueMax, paneValueTicks);

        var top = ValueToY(maxValue, paneBounds, valueMin, valueMax);
        var bottom = ValueToY(minValue, paneBounds, valueMin, valueMax);
        var rect = new LayoutRect(paneBounds.X, top, paneBounds.Width, Math.Max(0.0, bottom - top));

        var fillColor = ResolveZoneFill(annotation.Fill, theme);
        SceneDrawing.FillRectangle(scene, rect, fillColor);

        var borderColor = ResolveZoneBorder(annotation.Border, theme);
        if (annotation.BorderThickness > 0.0)
        {
            SceneDrawing.StrokeRectangle(scene, rect, borderColor, annotation.BorderThickness);
        }

        if (!string.IsNullOrWhiteSpace(annotation.Label))
        {
            DrawInlineLabel(scene, theme, annotation.Label, rect.X + 6.0, rect.Y + 6.0, borderColor);
        }
    }

    private void RenderGradientZone(
        VScene scene,
        ChartFrameMetadata metadata,
        LayoutRect paneBounds,
        double valueMin,
        double valueMax,
        IReadOnlyList<ChartFrameMetadata.AxisTickMetadata> paneValueTicks,
        ChartTheme theme,
        GradientZoneAnnotation annotation)
    {
        var snappedMin = SnapValue(annotation.MinValue, annotation.SnapMode, valueMin, valueMax, paneValueTicks);
        var snappedMax = SnapValue(annotation.MaxValue, annotation.SnapMode, valueMin, valueMax, paneValueTicks);

        if (snappedMax < snappedMin)
        {
            (snappedMin, snappedMax) = (snappedMax, snappedMin);
        }

        var top = ValueToY(snappedMax, paneBounds, valueMin, valueMax);
        var bottom = ValueToY(snappedMin, paneBounds, valueMin, valueMax);
        var height = Math.Abs(bottom - top);
        if (height < 0.5)
        {
            height = 0.5;
        }

        var rect = new LayoutRect(paneBounds.X, Math.Min(top, bottom), paneBounds.Width, height);

        var opacity = Math.Clamp(annotation.FillOpacity, 0.0, 1.0);
        var startColor = ChartRgbaColor.FromChartColor(annotation.StartColor)
            .WithAlpha((byte)Math.Clamp(opacity * annotation.StartColor.A, 0, 255));
        var endColor = ChartRgbaColor.FromChartColor(annotation.EndColor);

        SceneDrawing.FillRectangle(scene, rect, startColor);

        if (annotation.BorderThickness > 0.0)
        {
            SceneDrawing.StrokeRectangle(scene, rect, endColor, annotation.BorderThickness);
        }

        if (!string.IsNullOrWhiteSpace(annotation.Label))
        {
            DrawInlineLabel(scene, theme, annotation.Label, rect.X + 6.0, rect.Y + 6.0, endColor);
        }
    }

    private void RenderTimeRange(
        VScene scene,
        ChartFrameMetadata metadata,
        LayoutRect plotArea,
        LayoutRect paneBounds,
        ChartTheme theme,
        TimeRangeAnnotation annotation)
    {
        var start = Math.Min(annotation.StartSeconds, annotation.EndSeconds);
        var end = Math.Max(annotation.StartSeconds, annotation.EndSeconds);

        start = SnapTime(start, annotation.SnapMode, metadata);
        end = SnapTime(end, annotation.SnapMode, metadata);

        var left = TimeToX(start, plotArea, metadata);
        var right = TimeToX(end, plotArea, metadata);
        var rect = new LayoutRect(Math.Min(left, right), paneBounds.Y, Math.Abs(right - left), paneBounds.Height);

        var fillColor = ResolveZoneFill(annotation.Fill, theme);
        SceneDrawing.FillRectangle(scene, rect, fillColor);

        var borderColor = ResolveZoneBorder(annotation.Border, theme);
        if (annotation.BorderThickness > 0.0)
        {
            SceneDrawing.StrokeRectangle(scene, rect, borderColor, annotation.BorderThickness);
        }

        if (!string.IsNullOrWhiteSpace(annotation.Label))
        {
            DrawInlineLabel(scene, theme, annotation.Label, rect.X + 6.0, rect.Y + 6.0, borderColor);
        }
    }

    private void RenderCallout(
        VScene scene,
        ChartFrameMetadata metadata,
        LayoutRect plotArea,
        LayoutRect paneBounds,
        double valueMin,
        double valueMax,
        IReadOnlyList<ChartFrameMetadata.AxisTickMetadata> paneValueTicks,
        ChartTheme theme,
        CalloutAnnotation annotation)
    {
        if (string.IsNullOrWhiteSpace(annotation.Label))
        {
            return;
        }

        var snappedTime = SnapTime(annotation.TimestampSeconds, annotation.SnapMode, metadata);
        var snappedValue = SnapValue(annotation.Value, annotation.SnapMode, valueMin, valueMax, paneValueTicks);

        var anchorX = TimeToX(snappedTime, plotArea, metadata);
        var anchorY = ValueToY(snappedValue, paneBounds, valueMin, valueMax);

        var textColor = annotation.TextColor.HasValue
            ? ChartRgbaColor.FromChartColor(annotation.TextColor.Value)
            : theme.Palette.Foreground;
        var borderColor = annotation.Border.HasValue
            ? ChartRgbaColor.FromChartColor(annotation.Border.Value)
            : ResolveLineColor(annotation.Color, theme);
        var background = annotation.Background.HasValue
            ? ChartRgbaColor.FromChartColor(annotation.Background.Value)
            : theme.Palette.LegendBackground.WithAlpha(220);

        var font = theme.TypographyVariants.TryGetValue("Body", out var bodyTypography)
            ? bodyTypography
            : new ChartTypography("Segoe UI", 12d);

        var metrics = _textRenderer.Measure(annotation.Label, (float)font.FontSize);
        if (metrics.IsEmpty)
        {
            return;
        }

        var padding = Math.Max(2.0, annotation.Padding);
        var pointerLength = Math.Max(0.0, annotation.PointerLength);

        var boxWidth = metrics.Width + padding * 2;
        var boxHeight = metrics.LineHeight + padding * 2;

        var placement = annotation.Placement == AnnotationCalloutPlacement.Auto
            ? ChoosePlacement(anchorX, anchorY, plotArea, paneBounds)
            : annotation.Placement;

        var rect = PositionCalloutRectangle(
            placement,
            anchorX,
            anchorY,
            boxWidth,
            boxHeight,
            pointerLength,
            plotArea,
            paneBounds);

        var pointerTarget = NearestPointOnRectangle(rect, anchorX, anchorY);
        SceneDrawing.DrawLine(scene, anchorX, anchorY, pointerTarget.X, pointerTarget.Y, borderColor, 1.0);

        SceneDrawing.FillRectangle(scene, rect, background);
        SceneDrawing.StrokeRectangle(scene, rect, borderColor, 1.0);

        var label = new AxisLabelVisual(
            rect.X + padding,
            rect.Y + padding,
            annotation.Label,
            font,
            AxisOrientation.Bottom,
            TextAlignment.Start,
            TextAlignment.Start,
            textColor);

        _textRenderer.Draw(scene, label, (float)font.FontSize);
    }

    private static AnnotationCalloutPlacement ChoosePlacement(
        double anchorX,
        double anchorY,
        LayoutRect plotArea,
        LayoutRect paneBounds)
    {
        var horizontal = anchorX > plotArea.X + plotArea.Width * 0.66
            ? AnnotationCalloutPlacement.TopLeft
            : AnnotationCalloutPlacement.TopRight;

        if (anchorY > paneBounds.Y + paneBounds.Height * 0.5)
        {
            horizontal = horizontal == AnnotationCalloutPlacement.TopLeft
                ? AnnotationCalloutPlacement.BottomLeft
                : AnnotationCalloutPlacement.BottomRight;
        }

        return horizontal;
    }

    private static LayoutRect PositionCalloutRectangle(
        AnnotationCalloutPlacement placement,
        double anchorX,
        double anchorY,
        double width,
        double height,
        double pointerLength,
        LayoutRect plotArea,
        LayoutRect paneBounds)
    {
        double x = anchorX;
        double y = anchorY;

        switch (placement)
        {
            case AnnotationCalloutPlacement.TopLeft:
                x = anchorX - pointerLength - width;
                y = anchorY - pointerLength - height;
                break;

            case AnnotationCalloutPlacement.TopRight:
                x = anchorX + pointerLength;
                y = anchorY - pointerLength - height;
                break;

            case AnnotationCalloutPlacement.BottomLeft:
                x = anchorX - pointerLength - width;
                y = anchorY + pointerLength;
                break;

            case AnnotationCalloutPlacement.BottomRight:
                x = anchorX + pointerLength;
                y = anchorY + pointerLength;
                break;

            default:
                x = anchorX + pointerLength;
                y = anchorY - pointerLength - height;
                break;
        }

        var minX = plotArea.X;
        var maxX = plotArea.Right - width;
        var minY = paneBounds.Y;
        var maxY = paneBounds.Bottom - height;

        if (maxX < minX)
        {
            maxX = minX;
        }

        if (maxY < minY)
        {
            maxY = minY;
        }

        x = Math.Clamp(x, minX, maxX);
        y = Math.Clamp(y, minY, maxY);

        return new LayoutRect(x, y, width, height);
    }

    private static (double X, double Y) NearestPointOnRectangle(LayoutRect rect, double x, double y)
    {
        var clampedX = Math.Clamp(x, rect.X, rect.Right);
        var clampedY = Math.Clamp(y, rect.Y, rect.Bottom);
        return (clampedX, clampedY);
    }

    private static ChartRgbaColor ResolveLineColor(ChartColor? color, ChartTheme theme)
    {
        if (color.HasValue)
        {
            return ChartRgbaColor.FromChartColor(color.Value);
        }

        return theme.Axis.LineColor;
    }

    private static ChartRgbaColor ResolveZoneFill(ChartColor? color, ChartTheme theme)
    {
        if (color.HasValue)
        {
            return ChartRgbaColor.FromChartColor(color.Value);
        }

        return theme.Palette.AxisLine.WithAlpha(48);
    }

    private static ChartRgbaColor ResolveZoneBorder(ChartColor? color, ChartTheme theme)
    {
        if (color.HasValue)
        {
            return ChartRgbaColor.FromChartColor(color.Value);
        }

        return theme.Palette.AxisLine.WithAlpha(128);
    }

    private void DrawInlineLabel(
        VScene scene,
        ChartTheme theme,
        string label,
        double x,
        double y,
        ChartRgbaColor color)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        var typography = theme.TypographyVariants.TryGetValue("AxisLabel", out var variant)
            ? variant
            : new ChartTypography("Segoe UI", 10d);

        var visual = new AxisLabelVisual(
            x,
            y,
            label,
            typography,
            AxisOrientation.Bottom,
            TextAlignment.Start,
            TextAlignment.Start,
            color);

        _textRenderer.Draw(scene, visual, (float)typography.FontSize);
    }

    private static double SnapValue(
        double value,
        AnnotationSnapMode snapMode,
        double valueMin,
        double valueMax,
        IReadOnlyList<ChartFrameMetadata.AxisTickMetadata> valueTicks)
    {
        if (!snapMode.HasFlag(AnnotationSnapMode.ValueToTicks) || valueTicks.Count == 0)
        {
            return value;
        }

        var range = valueMax - valueMin;
        if (Math.Abs(range) < double.Epsilon)
        {
            return value;
        }

        var targetUnit = (value - valueMin) / range;
        var bestUnit = targetUnit;
        var bestDistance = double.MaxValue;
        foreach (var tick in valueTicks)
        {
            var distance = Math.Abs(tick.Position - targetUnit);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestUnit = tick.Position;
            }
        }

        return valueMin + bestUnit * range;
    }

    private static double SnapTime(
        double value,
        AnnotationSnapMode snapMode,
        ChartFrameMetadata metadata)
    {
        if (!snapMode.HasFlag(AnnotationSnapMode.TimeToTicks) || metadata.TimeTicks.Count == 0)
        {
            return value;
        }

        var range = metadata.RangeEndSeconds - metadata.RangeStartSeconds;
        if (Math.Abs(range) < double.Epsilon)
        {
            return value;
        }

        var targetUnit = (value - metadata.RangeStartSeconds) / range;
        var bestUnit = targetUnit;
        var bestDistance = double.MaxValue;
        foreach (var tick in metadata.TimeTicks)
        {
            var distance = Math.Abs(tick.Position - targetUnit);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestUnit = tick.Position;
            }
        }

        return metadata.RangeStartSeconds + bestUnit * range;
    }

    private static double ValueToY(double value, LayoutRect paneBounds, double valueMin, double valueMax)
    {
        var range = valueMax - valueMin;
        if (Math.Abs(range) < double.Epsilon)
        {
            return paneBounds.Y + paneBounds.Height / 2d;
        }

        var unit = (value - valueMin) / range;
        unit = Math.Clamp(unit, 0d, 1d);
        return paneBounds.Y + (1d - unit) * paneBounds.Height;
    }

    private static double TimeToX(double value, LayoutRect plotArea, ChartFrameMetadata metadata)
    {
        var range = metadata.RangeEndSeconds - metadata.RangeStartSeconds;
        if (Math.Abs(range) < double.Epsilon)
        {
            return plotArea.X;
        }

        var unit = (value - metadata.RangeStartSeconds) / range;
        unit = Math.Clamp(unit, 0d, 1d);
        return plotArea.X + unit * plotArea.Width;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AnnotationRenderer));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _textRenderer.Dispose();
        _disposed = true;
    }
}


