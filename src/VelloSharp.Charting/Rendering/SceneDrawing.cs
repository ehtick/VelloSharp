using System;
using System.Numerics;
using VelloSharp.Charting.Layout;
using VelloSharp.Charting.Styling;
using VPathBuilder = VelloSharp.PathBuilder;
using VStrokeStyle = VelloSharp.StrokeStyle;
using VFillRule = VelloSharp.FillRule;
using VScene = VelloSharp.Scene;
using ChartRgbaColor = VelloSharp.Charting.Styling.RgbaColor;

namespace VelloSharp.Charting.Rendering;

internal static class SceneDrawing
{
    public static void FillRectangle(VScene scene, LayoutRect rect, ChartRgbaColor color)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var path = new VPathBuilder()
            .MoveTo(rect.X, rect.Y)
            .LineTo(rect.Right, rect.Y)
            .LineTo(rect.Right, rect.Bottom)
            .LineTo(rect.X, rect.Bottom)
            .Close();

        scene.FillPath(path, VFillRule.NonZero, Matrix3x2.Identity, color.ToVelloColor());
    }

    public static void StrokeRectangle(VScene scene, LayoutRect rect, ChartRgbaColor color, double width)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var path = new VPathBuilder()
            .MoveTo(rect.X, rect.Y)
            .LineTo(rect.Right, rect.Y)
            .LineTo(rect.Right, rect.Bottom)
            .LineTo(rect.X, rect.Bottom)
            .Close();

        var stroke = new VStrokeStyle { Width = width };
        scene.StrokePath(path, stroke, Matrix3x2.Identity, color.ToVelloColor());
    }

    public static void DrawLine(VScene scene, double x1, double y1, double x2, double y2, ChartRgbaColor color, double width)
    {
        var stroke = new VStrokeStyle { Width = width };
        DrawLine(scene, x1, y1, x2, y2, color, stroke);
    }

    public static void DrawLine(VScene scene, double x1, double y1, double x2, double y2, ChartRgbaColor color, VStrokeStyle stroke)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var path = new VPathBuilder()
            .MoveTo(x1, y1)
            .LineTo(x2, y2);

        scene.StrokePath(path, stroke, Matrix3x2.Identity, color.ToVelloColor());
    }
}
