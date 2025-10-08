using System;

namespace VelloSharp.Composition.Controls;

public abstract class Shape : CompositionElement
{
    public double? Width { get; set; }

    public double? Height { get; set; }

    public double StrokeThickness { get; set; } = 1.0;

    public CompositionColor Stroke { get; set; }

    public CompositionColor Fill { get; set; }

    public override void Measure(in LayoutConstraints constraints)
    {
        base.Measure(constraints);
        double width = Width ?? constraints.Width.Preferred;
        double height = Height ?? constraints.Height.Preferred;
        width = Math.Clamp(width, constraints.Width.Min, constraints.Width.Max);
        height = Math.Clamp(height, constraints.Height.Min, constraints.Height.Max);
        DesiredSize = new LayoutSize(width, height);
    }
}
