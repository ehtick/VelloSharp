using VelloSharp.ChartEngine;
using ChartRgbaColor = VelloSharp.Charting.Styling.RgbaColor;

namespace VelloSharp.Charting.Legend;

public readonly record struct LegendItem(
    string Label,
    ChartRgbaColor Color,
    ChartSeriesKind Kind,
    double StrokeWidth,
    double FillOpacity,
    double MarkerSize);
