using VelloSharp.Charting.Layout;
using VelloSharp.Charting.Styling;
using ChartRgbaColor = VelloSharp.Charting.Styling.RgbaColor;

namespace VelloSharp.Charting.Rendering;

public readonly record struct AxisLabelVisual(
    double X,
    double Y,
    string Text,
    ChartTypography Typography,
    AxisOrientation Orientation,
    TextAlignment HorizontalAlignment,
    TextAlignment VerticalAlignment,
    ChartRgbaColor Color);
