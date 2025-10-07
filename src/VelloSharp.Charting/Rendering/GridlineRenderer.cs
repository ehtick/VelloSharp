using System;
using VelloSharp.Charting.Axis;
using VelloSharp.Charting.Layout;
using VelloSharp.Charting.Styling;
using VScene = VelloSharp.Scene;
using ChartRgbaColor = VelloSharp.Charting.Styling.RgbaColor;

namespace VelloSharp.Charting.Rendering;

/// <summary>
/// Renders gridlines derived from axis ticks onto the Vello scene.
/// </summary>
internal sealed class GridlineRenderer
{

    public void Render(VScene scene, AxisRenderResult result, LayoutRect plotArea, ChartTheme theme)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(theme);

        var color = theme.Palette.GridLine;

        foreach (var axis in result.Axes)
        {
            if (axis.Model.Orientation == AxisOrientation.Left)
            {
                DrawHorizontalGrid(scene, plotArea, axis.Model, color);
            }
            else if (axis.Model.Orientation == AxisOrientation.Bottom)
            {
                DrawVerticalGrid(scene, plotArea, axis.Model, color);
            }
        }
    }

    private void DrawHorizontalGrid(VScene scene, LayoutRect plotArea, AxisRenderModel model, ChartRgbaColor color)
    {
        foreach (var tick in model.Ticks)
        {
            if (IsEdgeTick(tick.UnitPosition))
            {
                continue;
            }

            var y = plotArea.Y + (1.0 - tick.UnitPosition) * plotArea.Height;
            SceneDrawing.DrawLine(scene, plotArea.X, y, plotArea.Right, y, color, 1.0);
        }
    }

    private void DrawVerticalGrid(VScene scene, LayoutRect plotArea, AxisRenderModel model, ChartRgbaColor color)
    {
        foreach (var tick in model.Ticks)
        {
            if (IsEdgeTick(tick.UnitPosition))
            {
                continue;
            }

            var x = plotArea.X + tick.UnitPosition * plotArea.Width;
            SceneDrawing.DrawLine(scene, x, plotArea.Y, x, plotArea.Bottom, color, 1.0);
        }
    }

    private static bool IsEdgeTick(double unit) => unit <= 0.0001 || unit >= 0.9999;
}
