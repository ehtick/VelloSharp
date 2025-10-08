using System;
using System.Linq;
using VelloSharp.TreeDataGrid;
using VelloSharp.TreeDataGrid.Composition;
using VelloSharp.TreeDataGrid.Rendering;
using Xunit;

namespace VelloSharp.Charting.Tests.TreeDataGrid;

public sealed class TreeDataGridPhase2Tests
{
    [Fact]
    public void DataModel_AttachRoots_ProducesDiffs()
    {
        using var model = new TreeDataModel();
        model.AttachRoots(new[]
        {
            new TreeNodeDescriptor(1, TreeRowKind.GroupHeader, 28f, true),
            new TreeNodeDescriptor(2, TreeRowKind.Data, 24f, false),
        });

        var diffs = model.DrainModelDiffs();
        Assert.Equal(2, diffs.Count);
        Assert.Contains(diffs, diff => diff.Kind == TreeModelDiffKind.Inserted && diff.Key == 1);
    }

    [Fact]
    public void Virtualizer_Plan_ReturnsRows()
    {
        using var scheduler = new TreeVirtualizationScheduler();
        scheduler.SetRows(new[]
        {
            new TreeRowMetric(1, 24f),
            new TreeRowMetric(2, 24f),
            new TreeRowMetric(3, 24f),
        });
        scheduler.SetColumns(new[]
        {
            new TreeColumnMetric(0.0, 160.0, TreeFrozenKind.Leading),
            new TreeColumnMetric(160.0, 200.0, TreeFrozenKind.None),
        });

        var plan = scheduler.Plan(new TreeViewportMetrics(0.0, 32.0, 8.0, 0.0, 320.0, 24.0));
        Assert.NotEmpty(plan.ActiveRows);
        Assert.True(plan.RowWindow.EndIndex > plan.RowWindow.StartIndex);
    }

    [Fact]
    public void Virtualizer_Plan_ReportsPaneDiff()
    {
        using var scheduler = new TreeVirtualizationScheduler();
        scheduler.SetRows(new[]
        {
            new TreeRowMetric(1, 24f),
            new TreeRowMetric(2, 24f),
        });
        var definitions = new[]
        {
            new TreeColumnDefinition(0, 120, 200, Sizing: TreeColumnSizingMode.Pixel, PixelWidth: 120, Key: 1, Frozen: TreeFrozenKind.Leading),
            new TreeColumnDefinition(0, 160, 220, Weight: 1.0, Sizing: TreeColumnSizingMode.Star, Key: 2),
            new TreeColumnDefinition(0, 140, 200, Weight: 1.0, Sizing: TreeColumnSizingMode.Auto, Key: 3, Frozen: TreeFrozenKind.Trailing),
        };
        var slots = new[]
        {
            new TreeColumnSlot(0.0, 120.0),
            new TreeColumnSlot(120.0, 200.0),
            new TreeColumnSlot(320.0, 180.0),
        };

        var snapshot = scheduler.UpdateColumns(definitions, slots);
        Assert.True(snapshot.PaneDiff.Any);

        var viewport = new TreeViewportMetrics(0.0, 48.0, 8.0, 0.0, 320.0, 24.0);
        var plan = scheduler.Plan(viewport);
        Assert.Equal(snapshot.PaneDiff, plan.PaneDiff);

        var repeatPlan = scheduler.Plan(viewport);
        Assert.False(repeatPlan.PaneDiff.Any);
    }

    [Fact]
    public void Virtualizer_Clear_ResetsState()
    {
        using var scheduler = new TreeVirtualizationScheduler();
        scheduler.SetRows(new[]
        {
            new TreeRowMetric(1, 24f),
            new TreeRowMetric(2, 24f),
        });
        scheduler.SetColumns(new[]
        {
            new TreeColumnMetric(0.0, 160.0, TreeFrozenKind.Leading),
            new TreeColumnMetric(160.0, 200.0, TreeFrozenKind.None),
        });

        var plan = scheduler.Plan(new TreeViewportMetrics(0.0, 32.0, 8.0, 0.0, 320.0, 24.0));
        Assert.NotEmpty(plan.ActiveRows);

        scheduler.Clear();
        var cleared = scheduler.Plan(new TreeViewportMetrics(0.0, 32.0, 8.0, 0.0, 320.0, 24.0));
        Assert.Empty(cleared.ActiveRows);
        Assert.Equal(0u, cleared.RowWindow.StartIndex);
        Assert.Equal(0u, cleared.RowWindow.EndIndex);
    }

    [Fact]
    public void RenderLoop_ProducesFrameStats()
    {
        using var loop = new TreeRenderLoop(120f);
        if (loop.BeginFrame())
        {
            loop.RecordGpuSummary(new TreeGpuTimestampSummary(0.9f, 0.25f, 4));
            var stats = loop.EndFrame(0f, 0f);
            Assert.True(stats.FrameIntervalMs >= 0f);
            Assert.Equal(4u, stats.GpuSampleCount);
            Assert.True(stats.GpuTimeMs >= 0f);
        }
    }

    [Fact]
    public void SceneGraph_EncodesRow()
    {
        using var sceneGraph = new TreeSceneGraph();
        var node = sceneGraph.CreateNode();
        var visual = new TreeRowVisual(
            Width: 320.0,
            Height: 24.0,
            Depth: 0,
            Indent: 16.0,
            Background: TreeColor.FromRgb(0.18f, 0.18f, 0.2f),
            HoverBackground: TreeColor.FromRgb(0.24f, 0.24f, 0.26f, 0.4f),
            SelectionFill: TreeColor.FromRgb(0.2f, 0.4f, 0.75f, 0.35f),
            Outline: TreeColor.FromRgb(0.32f, 0.52f, 0.9f),
            OutlineWidth: 1.0f,
            Stripe: TreeColor.FromRgb(1f, 1f, 1f, 0.05f),
            StripeWidth: 1.0f,
            IsSelected: false,
            IsHovered: false);

        sceneGraph.EncodeRow(node, visual, new[]
        {
            new TreeColumnSpan(0.0, 160.0, TreeFrozenKind.Leading),
            new TreeColumnSpan(160.0, 160.0, TreeFrozenKind.None),
        });

        sceneGraph.MarkRowDirty(node, 0.0, 320.0, 0.0, 24.0);
        Assert.True(sceneGraph.TryTakeDirty(node, out _));
    }

    [Fact]
    public void ColumnAnimator_SoftensTransition()
    {
        var animator = new TreeColumnLayoutAnimator(0.5);
        var definitions = new[]
        {
            new TreeColumnDefinition(100, 140, 260, Weight: 1.0, Sizing: TreeColumnSizingMode.Star, Key: 1),
            new TreeColumnDefinition(100, 140, 260, Weight: 2.0, Sizing: TreeColumnSizingMode.Star, Key: 2),
        };

        var first = animator.Animate(definitions, 480.0, 12.0);
        var secondDefinitions = definitions
            .Select((def, index) => index == 0
                ? def with { PixelWidth = 200.0, Sizing = TreeColumnSizingMode.Pixel }
                : def)
            .ToArray();

        var second = animator.Animate(secondDefinitions, 480.0, 12.0);
        Assert.Equal(first.Count, second.Count);
        Assert.NotEqual(first[0].Width, second[0].Width);
    }
}
