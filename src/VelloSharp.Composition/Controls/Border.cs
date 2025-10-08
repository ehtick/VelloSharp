using System;

namespace VelloSharp.Composition.Controls;

public class Border : Decorator
{
    public LayoutThickness BorderThickness { get; set; } = LayoutThickness.Zero;

    public LayoutThickness Padding { get; set; } = LayoutThickness.Zero;

    public CompositionColor Background { get; set; }

    public CompositionColor BorderBrush { get; set; }

    public double CornerRadius { get; set; }

    public override void Measure(in LayoutConstraints constraints)
    {
        base.Measure(constraints);
        var child = Child;
        if (child is null)
        {
            DesiredSize = new LayoutSize(
                BorderThickness.Horizontal + Padding.Horizontal,
                BorderThickness.Vertical + Padding.Vertical);
            return;
        }

        var innerWidth = Math.Max(0, constraints.Width.Max - BorderThickness.Horizontal - Padding.Horizontal);
        var innerHeight = Math.Max(0, constraints.Height.Max - BorderThickness.Vertical - Padding.Vertical);

        var childConstraints = new LayoutConstraints(
            new ScalarConstraint(0, innerWidth, innerWidth),
            new ScalarConstraint(0, innerHeight, innerHeight));

        child.Measure(childConstraints);
        var desired = child.DesiredSize;
        DesiredSize = new LayoutSize(
            desired.Width + BorderThickness.Horizontal + Padding.Horizontal,
            desired.Height + BorderThickness.Vertical + Padding.Vertical);
    }

    public override void Arrange(in LayoutRect rect)
    {
        base.Arrange(rect);
        var child = Child;
        if (child is null)
        {
            return;
        }

        var offsetX = BorderThickness.Left + Padding.Left;
        var offsetY = BorderThickness.Top + Padding.Top;
        var width = Math.Max(0, rect.Width - BorderThickness.Horizontal - Padding.Horizontal);
        var height = Math.Max(0, rect.Height - BorderThickness.Vertical - Padding.Vertical);

        var childRect = new LayoutRect(
            rect.X + offsetX,
            rect.Y + offsetY,
            width,
            height,
            rect.PrimaryOffset,
            rect.PrimaryLength);
        child.Arrange(childRect);
    }
}
