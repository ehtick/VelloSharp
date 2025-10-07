using System.Collections.Generic;
using VelloSharp.Charting.Layout;
using VelloSharp.Charting.Styling;
using ChartRgbaColor = VelloSharp.Charting.Styling.RgbaColor;

namespace VelloSharp.Charting.Legend;

public sealed class LegendVisual
{
    public LegendVisual(LegendDefinition definition, LayoutRect bounds, LegendStyle style, IReadOnlyList<LegendItemVisual> items)
    {
        Definition = definition;
        Bounds = bounds;
        Style = style;
        Items = items;
    }

    public LegendDefinition Definition { get; }

    public LayoutRect Bounds { get; }

    public LegendStyle Style { get; }

    public IReadOnlyList<LegendItemVisual> Items { get; }
}
