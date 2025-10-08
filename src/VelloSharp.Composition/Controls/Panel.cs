using System;
using System.Collections.Generic;

namespace VelloSharp.Composition.Controls;

public class Panel : CompositionElement
{
    private readonly List<CompositionElement> _children = new();
    private LayoutRect[] _layoutSlots = Array.Empty<LayoutRect>();

    public IList<CompositionElement> Children => _children;

    public LayoutOrientation Orientation { get; set; } = LayoutOrientation.Vertical;

    public double Spacing { get; set; }

    public LayoutThickness Padding { get; set; } = LayoutThickness.Zero;

    public LayoutAlignment CrossAlignment { get; set; } = LayoutAlignment.Stretch;

    public override void Measure(in LayoutConstraints constraints)
    {
        base.Measure(constraints);
        if (_children.Count == 0)
        {
            DesiredSize = new LayoutSize(Padding.Horizontal, Padding.Vertical);
            _layoutSlots = Array.Empty<LayoutRect>();
            return;
        }

        var available = new LayoutSize(constraints.Width.Max, constraints.Height.Max).ClampNonNegative();

        Span<StackLayoutChild> stackChildren = _children.Count <= 16
            ? stackalloc StackLayoutChild[16]
            : new StackLayoutChild[_children.Count];
        stackChildren = stackChildren[.._children.Count];

        for (int i = 0; i < _children.Count; i++)
        {
            var child = _children[i];
            // Allow child to use the same constraints minus padding.
            var childConstraints = new LayoutConstraints(
                constraints.Width,
                constraints.Height);
            child.Measure(childConstraints);
            var desired = child.DesiredSize;
            var tightConstraints = new LayoutConstraints(
                new ScalarConstraint(desired.Width, desired.Width, desired.Width),
                new ScalarConstraint(desired.Height, desired.Height, desired.Height));

            stackChildren[i] = new StackLayoutChild(tightConstraints, 1.0, LayoutThickness.Zero, CrossAlignment);
        }

        Span<LayoutRect> layoutBuffer = _children.Count <= 16
            ? stackalloc LayoutRect[16]
            : new LayoutRect[_children.Count];
        layoutBuffer = layoutBuffer[.._children.Count];

        var options = new StackLayoutOptions(Orientation, Spacing, Padding, CrossAlignment);
        int produced = CompositionInterop.SolveStackLayout(stackChildren, options, available, layoutBuffer);

        if (_layoutSlots.Length != produced)
        {
            _layoutSlots = new LayoutRect[produced];
        }

        double width = 0;
        double height = 0;
        for (int i = 0; i < produced; i++)
        {
            var slot = layoutBuffer[i];
            _layoutSlots[i] = slot;
            width = Math.Max(width, slot.X + slot.Width);
            height = Math.Max(height, slot.Y + slot.Height);
        }

        DesiredSize = new LayoutSize(width, height);
    }

    public override void Arrange(in LayoutRect rect)
    {
        base.Arrange(rect);
        int count = Math.Min(_children.Count, _layoutSlots.Length);
        for (int i = 0; i < count; i++)
        {
            var child = _children[i];
            var slot = _layoutSlots[i];
            var arranged = new LayoutRect(
                rect.X + slot.X,
                rect.Y + slot.Y,
                slot.Width,
                slot.Height,
                slot.PrimaryOffset,
                slot.PrimaryLength,
                slot.LineIndex);
            child.Arrange(arranged);
        }
    }

    public override IEnumerable<CompositionElement> GetChildren()
    {
        return _children;
    }
}
