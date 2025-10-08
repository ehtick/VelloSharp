using System;
using System.Collections.Generic;

namespace VelloSharp.Composition.Controls;

public abstract class CompositionElement
{
    private LayoutRect _arrangedBounds;
    private LayoutConstraints _measureConstraints;

    public LayoutRect ArrangedBounds => _arrangedBounds;

    public LayoutConstraints MeasureConstraints => _measureConstraints;

    public LayoutSize DesiredSize { get; protected set; } = new(0, 0);

    public bool IsMounted { get; private set; }

    public virtual void Measure(in LayoutConstraints constraints)
    {
        _measureConstraints = constraints;
        DesiredSize = new LayoutSize(
            Math.Clamp(constraints.Width.Preferred, constraints.Width.Min, constraints.Width.Max),
            Math.Clamp(constraints.Height.Preferred, constraints.Height.Min, constraints.Height.Max));
    }

    public virtual void Arrange(in LayoutRect rect)
    {
        _arrangedBounds = rect;
    }

    public virtual void Mount()
    {
        IsMounted = true;
    }

    public virtual void Unmount()
    {
        IsMounted = false;
    }

    public virtual IEnumerable<CompositionElement> GetChildren()
    {
        yield break;
    }
}
