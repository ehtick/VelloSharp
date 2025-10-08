using System.Collections.Generic;

namespace VelloSharp.Composition.Controls;

public class Decorator : CompositionElement
{
    private CompositionElement? _child;

    public CompositionElement? Child
    {
        get => _child;
        set
        {
            if (ReferenceEquals(_child, value))
            {
                return;
            }

            _child?.Unmount();
            _child = value;
            if (IsMounted)
            {
                _child?.Mount();
            }
        }
    }

    public override void Mount()
    {
        base.Mount();
        _child?.Mount();
    }

    public override void Unmount()
    {
        _child?.Unmount();
        base.Unmount();
    }

    public override void Measure(in LayoutConstraints constraints)
    {
        base.Measure(constraints);
        if (_child is null)
        {
            DesiredSize = new LayoutSize(
                Math.Clamp(constraints.Width.Preferred, constraints.Width.Min, constraints.Width.Max),
                Math.Clamp(constraints.Height.Preferred, constraints.Height.Min, constraints.Height.Max));
            return;
        }

        _child.Measure(constraints);
        DesiredSize = _child.DesiredSize;
    }

    public override void Arrange(in LayoutRect rect)
    {
        base.Arrange(rect);
        _child?.Arrange(rect);
    }

    public override IEnumerable<CompositionElement> GetChildren()
    {
        if (_child is not null)
        {
            yield return _child;
        }
    }
}
