using System;
using System.Collections.Generic;
using VelloSharp.ChartEngine;
using VelloSharp.Charting.Annotations;
using VelloSharp.Charting.Layout;
using VelloSharp.Charting.Styling;
using VScene = VelloSharp.Scene;
using ChartRgbaColor = VelloSharp.Charting.Styling.RgbaColor;

namespace VelloSharp.Charting.Rendering;

/// <summary>
/// Renders chart annotations using theme-aware styling.
/// </summary>
internal sealed class AnnotationRenderer
{
    public void Render(
        VScene scene,
        IReadOnlyList<ChartAnnotation> annotations,
        LayoutRect plotArea,
        ChartFrameMetadata metadata,
        ChartTheme theme)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(annotations);
        ArgumentNullException.ThrowIfNull(theme);

        if (annotations.Count == 0)
        {
            return;
        }

        var color = theme.Palette.AxisLine;

        foreach (var annotation in annotations)
        {
            switch (annotation.Kind)
            {
                case AnnotationKind.HorizontalLine:
                {
                    var unit = (annotation.Value - metadata.ValueMin) /
                               (metadata.ValueMax - metadata.ValueMin + double.Epsilon);
                    unit = Math.Clamp(unit, 0.0, 1.0);
                    var y = plotArea.Y + (1.0 - unit) * plotArea.Height;
                    SceneDrawing.DrawLine(scene, plotArea.X, y, plotArea.Right, y, color, 1.5);
                    break;
                }
                case AnnotationKind.VerticalLine:
                {
                    var unit = (annotation.Value - metadata.RangeStartSeconds) /
                               (metadata.RangeEndSeconds - metadata.RangeStartSeconds + double.Epsilon);
                    unit = Math.Clamp(unit, 0.0, 1.0);
                    var x = plotArea.X + unit * plotArea.Width;
                    SceneDrawing.DrawLine(scene, x, plotArea.Y, x, plotArea.Bottom, color, 1.5);
                    break;
                }
            }
        }
    }
}
