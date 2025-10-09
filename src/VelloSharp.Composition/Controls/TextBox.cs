using System;

namespace VelloSharp.Composition.Controls;

public class TextBox : TextBlock
{
    private LayoutThickness _padding = new(8, 4, 8, 4);
    private LayoutThickness _borderThickness = new(1, 1, 1, 1);
    private CompositionColor _background = new(0.16f, 0.19f, 0.26f, 0.95f);
    private CompositionColor _borderBrush = new(0.32f, 0.38f, 0.52f, 1f);

    public LayoutThickness Padding
    {
        get => _padding;
        set => _padding = value;
    }

    public LayoutThickness BorderThickness
    {
        get => _borderThickness;
        set => _borderThickness = value;
    }

    public CompositionColor Background
    {
        get => _background;
        set => _background = value;
    }

    public CompositionColor BorderBrush
    {
        get => _borderBrush;
        set => _borderBrush = value;
    }

    public double CornerRadius { get; set; } = 4.0;

    public bool IsReadOnly { get; set; }

    public bool AcceptsReturn { get; set; }

    public override void Measure(in LayoutConstraints constraints)
    {
        var horizontalChrome = _padding.Horizontal + _borderThickness.Horizontal;
        var verticalChrome = _padding.Vertical + _borderThickness.Vertical;

        var innerWidth = Math.Max(0, constraints.Width.Max - horizontalChrome);
        var innerHeight = Math.Max(0, constraints.Height.Max - verticalChrome);

        var innerConstraints = new LayoutConstraints(
            new ScalarConstraint(
                Math.Max(0, constraints.Width.Min - horizontalChrome),
                Math.Max(0, constraints.Width.Preferred - horizontalChrome),
                innerWidth),
            new ScalarConstraint(
                Math.Max(0, constraints.Height.Min - verticalChrome),
                Math.Max(0, constraints.Height.Preferred - verticalChrome),
                innerHeight));

        base.Measure(innerConstraints);

        var width = DesiredSize.Width + horizontalChrome;
        var height = DesiredSize.Height + verticalChrome;

        width = Math.Clamp(width, constraints.Width.Min, constraints.Width.Max);
        height = Math.Clamp(height, constraints.Height.Min, constraints.Height.Max);

        DesiredSize = new LayoutSize(width, height);
    }
}
