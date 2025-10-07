using VelloSharp.ChartEngine;
using VelloSharp.Charting.Styling;
using ChartRgbaColor = VelloSharp.Charting.Styling.RgbaColor;

namespace VelloSharp.Charting.Legend;

public readonly record struct LegendItemVisual(
    string Label,
    ChartRgbaColor Color,
    ChartSeriesKind Kind,
    double StrokeWidth,
    double FillOpacity,
    double MarkerX,
    double MarkerY,
    double MarkerSize,
    double TextX,
    double TextY,
    ChartTypography Typography);
