using System;

namespace VelloSharp.Composition.Controls;

public class TemplatedControl : CompositionElement
{
    private CompositionTemplate? _template;
    private CompositionElement? _templateRoot;

    public event EventHandler? TemplateApplied;

    public CompositionTemplate? Template
    {
        get => _template;
        set
        {
            if (ReferenceEquals(_template, value))
            {
                return;
            }

            _template = value;
            IsTemplateApplied = false;
        }
    }

    public CompositionElement? TemplateRoot => _templateRoot;

    public bool IsTemplateApplied { get; protected set; }

    public VisualTreeVirtualizer Virtualizer { get; } = new();

    public override void Measure(in LayoutConstraints constraints)
    {
        EnsureTemplate();
        base.Measure(constraints);
        if (_templateRoot is null)
        {
            DesiredSize = new LayoutSize(
                Math.Clamp(constraints.Width.Preferred, constraints.Width.Min, constraints.Width.Max),
                Math.Clamp(constraints.Height.Preferred, constraints.Height.Min, constraints.Height.Max));
            return;
        }

        _templateRoot.Measure(constraints);
        DesiredSize = _templateRoot.DesiredSize;
    }

    public override void Arrange(in LayoutRect rect)
    {
        base.Arrange(rect);
        _templateRoot?.Arrange(rect);
    }

    public override void Mount()
    {
        base.Mount();
        if (EnsureTemplate() && _templateRoot is not null)
        {
            _templateRoot.Mount();
        }
    }

    public override void Unmount()
    {
        _templateRoot?.Unmount();
        base.Unmount();
    }

    protected virtual void OnApplyTemplate()
    {
    }

    public bool ApplyTemplate()
    {
        if (Template is null)
        {
            _templateRoot = null;
            IsTemplateApplied = false;
            return false;
        }

        var newRoot = Template.Build(this);
        if (ReferenceEquals(_templateRoot, newRoot))
        {
            IsTemplateApplied = true;
            return true;
        }

        _templateRoot?.Unmount();
        _templateRoot = newRoot;
        if (IsMounted)
        {
            _templateRoot?.Mount();
        }

        IsTemplateApplied = _templateRoot is not null;
        OnApplyTemplate();
        TemplateApplied?.Invoke(this, EventArgs.Empty);
        return IsTemplateApplied;
    }

    protected bool EnsureTemplate()
    {
        if (IsTemplateApplied)
        {
            return true;
        }

        return ApplyTemplate();
    }

    public VisualTreeVirtualizer.VirtualizationPlan CaptureVirtualizationPlan(RowViewportMetrics rowMetrics, ColumnViewportMetrics columnMetrics) =>
        Virtualizer.CapturePlan(rowMetrics, columnMetrics);

    public void UpdateVirtualization(ReadOnlySpan<VirtualRowMetric> rows, ReadOnlySpan<VirtualColumnStrip> columns)
    {
        Virtualizer.UpdateRows(rows);
        Virtualizer.UpdateColumns(columns);
    }
}
