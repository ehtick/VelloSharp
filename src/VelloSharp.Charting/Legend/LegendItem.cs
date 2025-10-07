using VelloSharp.ChartEngine;
using VelloSharp.Charting.Styling;

namespace VelloSharp.Charting.Legend;

public readonly record struct LegendItem(
    string Label,
    RgbaColor Color,
    ChartSeriesKind Kind,
    double StrokeWidth,
    double FillOpacity,
    double MarkerSize);
