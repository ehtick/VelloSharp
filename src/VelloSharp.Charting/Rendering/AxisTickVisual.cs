using VelloSharp.Charting.Styling;
using ChartRgbaColor = VelloSharp.Charting.Styling.RgbaColor;

namespace VelloSharp.Charting.Rendering;

public readonly record struct AxisTickVisual(double X1, double Y1, double X2, double Y2, ChartRgbaColor Color);
