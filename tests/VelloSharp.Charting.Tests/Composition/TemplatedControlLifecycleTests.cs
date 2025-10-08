using System;
using System.Linq;
using VelloSharp.Composition;
using VelloSharp.Composition.Controls;
using Xunit;

namespace VelloSharp.Charting.Tests.Composition;

public sealed class TemplatedControlLifecycleTests
{
    [Fact]
    public void ApplyTemplate_MountsTemplateRoot()
    {
        var control = new TemplatedControl();
        var element = new TrackingElement();
        control.Template = CompositionTemplate.Create(_ => element);

        control.Mount();
        Assert.True(control.IsTemplateApplied);
        Assert.Same(element, control.TemplateRoot);
        Assert.Equal(2, element.MountCount);

        Assert.True(control.ApplyTemplate());
        Assert.Equal(2, element.MountCount);

        control.Unmount();
        Assert.Equal(1, element.UnmountCount);
    }

    [Fact]
    public void Measure_EnsuresTemplateApplied()
    {
        var control = new TemplatedControl
        {
            Template = CompositionTemplate.Create(_ => new TrackingElement()),
        };

        var constraints = new LayoutConstraints(
            new ScalarConstraint(0, 120, 120),
            new ScalarConstraint(0, 40, 40));

        control.Measure(constraints);

        Assert.True(control.IsTemplateApplied);
        Assert.NotNull(control.TemplateRoot);
        Assert.InRange(control.DesiredSize.Width, 119.9, 120.1);
        Assert.InRange(control.DesiredSize.Height, 39.9, 40.1);
    }

    [Fact]
    public void Virtualizer_CapturesPlanAfterUpdate()
    {
        var control = new TemplatedControl
        {
            Template = CompositionTemplate.Create(_ => new TrackingElement()),
        };

        control.Mount();
        Assert.True(control.ApplyTemplate());

        var rows = new[]
        {
            new VirtualRowMetric(101, 24),
            new VirtualRowMetric(102, 28),
        };

        var columns = new[]
        {
            new VirtualColumnStrip(0, 120, FrozenKind.None, 1),
            new VirtualColumnStrip(120, 140, FrozenKind.Trailing, 2),
        };

        control.UpdateVirtualization(rows, columns);

        using (var plan = control.CaptureVirtualizationPlan(
                   new RowViewportMetrics(0, 200, 0),
                   new ColumnViewportMetrics(0, 400, 0)))
        {
            Assert.Equal(rows.Length, plan.Active.Length);
            Assert.True(plan.Recycled.IsEmpty);
            Assert.Equal((uint)rows.Length, plan.Telemetry.RowsTotal);
            Assert.Equal(0u, plan.Columns.FrozenLeading);
            Assert.Equal((uint)1, plan.Columns.FrozenTrailing);
            Assert.True(plan.Window.TotalHeight >= rows.Sum(r => r.Height) - 1e-3);

            foreach (var entry in plan.Active)
            {
                Assert.NotEqual(RowAction.Recycle, entry.Action);
            }
        }

        control.Unmount();
    }

    private sealed class TrackingElement : CompositionElement
    {
        public int MountCount { get; private set; }
        public int UnmountCount { get; private set; }

        public override void Mount()
        {
            base.Mount();
            MountCount++;
        }

        public override void Unmount()
        {
            UnmountCount++;
            base.Unmount();
        }
    }
}
