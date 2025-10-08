using System;
using System.Linq;
using System.Threading;
using VelloSharp.ChartDiagnostics;
using VelloSharp.Composition;
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
        Assert.Equal((uint)plan.ActiveRows.Count, plan.BufferDiagnostics.Total);
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
    public void Virtualizer_UpdateColumns_UsesInternalAnimator()
    {
        using var scheduler = new TreeVirtualizationScheduler();
        scheduler.SetRows(new[]
        {
            new TreeRowMetric(1, 24f),
            new TreeRowMetric(2, 24f),
        });

        var definitions = new[]
        {
            new TreeColumnDefinition(100, 140, 260, Weight: 1.0, Sizing: TreeColumnSizingMode.Star, Key: 1),
            new TreeColumnDefinition(120, 160, 280, Weight: 1.0, Sizing: TreeColumnSizingMode.Auto, Key: 2),
        };

        var snapshot = scheduler.UpdateColumns(definitions, availableWidth: 512.0, spacing: 12.0);
        Assert.Equal(definitions.Length, snapshot.Metrics.Length);

        var repeated = scheduler.UpdateColumns(definitions, availableWidth: 532.0, spacing: 12.0);
        Assert.Equal(snapshot.Metrics.Length, repeated.Metrics.Length);
    }

    [Fact]
    public void RenderLoop_ProducesFrameStats()
    {
        using var collector = new FrameDiagnosticsCollector();
        using var loop = new TreeRenderLoop(120f, collector);
        if (loop.BeginFrame())
        {
            loop.RecordGpuSummary(new TreeGpuTimestampSummary(0.9f, 0.25f, 4));
            var stats = loop.EndFrame(0f, 0f);
            Assert.True(stats.FrameIntervalMs >= 0f);
            Assert.Equal(4u, stats.GpuSampleCount);
            Assert.True(stats.GpuTimeMs >= 0f);
            Assert.True(collector.TryGetRecent(out var recorded));
            Assert.True(recorded.Timestamp > DateTimeOffset.MinValue);
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
        using var animator = new TreeColumnLayoutAnimator(stiffness: 120.0, damping: 20.0);
        var definitions = new[]
        {
            new TreeColumnDefinition(100, 140, 260, Weight: 1.0, Sizing: TreeColumnSizingMode.Star, Key: 1),
            new TreeColumnDefinition(100, 140, 260, Weight: 2.0, Sizing: TreeColumnSizingMode.Star, Key: 2),
        };

        var initial = animator.Animate(definitions, 480.0, 12.0).ToArray();
        var target = animator.Animate(definitions, 800.0, 12.0);
        Assert.Equal(initial.Length, target.Length);

        var advanced = false;
        for (var i = 0; i < 12; i++)
        {
            Thread.Sleep(16);
            if (animator.TryAdvance(out var slots))
            {
                advanced = true;
                foreach (var slot in slots)
                {
                    Assert.True(double.IsFinite(slot.Width));
                    Assert.True(double.IsFinite(slot.Offset));
                }
                break;
            }
        }

        Assert.True(advanced || !initial.SequenceEqual(target.ToArray()), "Expected timeline to advance during polling.");
    }

    [Fact]
    public void RowAnimator_ProducesSnapshotsForExpansion()
    {
        using var scheduler = new TreeVirtualizationScheduler();
        scheduler.SetRows(new[]
        {
            new TreeRowMetric(1, 24f),
            new TreeRowMetric(2, 26f),
        });

        scheduler.SetColumns(new[]
        {
            new TreeColumnMetric(0.0, 160.0, TreeFrozenKind.Leading),
            new TreeColumnMetric(160.0, 220.0, TreeFrozenKind.None),
        });

        var viewport = new TreeViewportMetrics(
            RowScrollOffset: 0.0,
            RowViewportHeight: 160.0,
            RowOverscan: 24.0,
            ColumnScrollOffset: 0.0,
            ColumnViewportWidth: 640.0,
            ColumnOverscan: 48.0);

        // Warm up and ensure baseline state.
        var baseline = scheduler.Plan(viewport);
        Assert.All(baseline.RowAnimations, snapshot =>
        {
            Assert.Equal(1f, snapshot.HeightFactor, 3);
            Assert.Equal(0f, snapshot.SelectionGlow);
            Assert.Equal(0f, snapshot.CaretRotationDegrees);
        });

        scheduler.NotifyRowExpansion(1, true);
        Thread.Sleep(20);

        var expanded = scheduler.Plan(viewport);
        var expansionSnapshot = Assert.Single(expanded.RowAnimations, anim => anim.NodeId == 1);
        Assert.InRange(expansionSnapshot.HeightFactor, 0.6f, 1f);
        Assert.InRange(expansionSnapshot.SelectionGlow, 0f, 1f);
        Thread.Sleep(60);
        var settled = scheduler.Plan(viewport);
        var settledSnapshot = Assert.Single(settled.RowAnimations, anim => anim.NodeId == 1);
        Assert.True(settledSnapshot.HeightFactor <= 1f + 0.01f);
        Assert.True(settledSnapshot.HeightFactor >= 0.99f);
        Assert.True(settledSnapshot.CaretRotationDegrees <= 90f + 0.5f, $"Caret={settledSnapshot.CaretRotationDegrees}");
    }

    [Fact]
    public void RowAnimator_ResetsOnCollapse()
    {
        using var scheduler = new TreeVirtualizationScheduler();
        scheduler.SetRows(new[]
        {
            new TreeRowMetric(10, 24f),
        });

        scheduler.SetColumns(new[]
        {
            new TreeColumnMetric(0.0, 160.0, TreeFrozenKind.None),
        });

        var viewport = new TreeViewportMetrics(
            RowScrollOffset: 0.0,
            RowViewportHeight: 80.0,
            RowOverscan: 16.0,
            ColumnScrollOffset: 0.0,
            ColumnViewportWidth: 320.0,
            ColumnOverscan: 16.0);

        scheduler.Plan(viewport);
        scheduler.NotifyRowExpansion(10, true);
        Thread.Sleep(25);
        scheduler.Plan(viewport);

        scheduler.NotifyRowExpansion(10, false);
        Thread.Sleep(25);

        var collapsed = scheduler.Plan(viewport);
        var snapshot = Assert.Single(collapsed.RowAnimations);
        Assert.Equal(10u, snapshot.NodeId);
        Assert.True(snapshot.CaretRotationDegrees <= 5f);
        Assert.True(snapshot.HeightFactor <= 1f + 0.01f);
    }

    [Fact]
    public void RowAnimator_HonoursReducedMotionProfile()
    {
        using var scheduler = new TreeVirtualizationScheduler();
        scheduler.ConfigureRowAnimations(TreeRowAnimationProfile.Default with { ReducedMotionEnabled = true });

        scheduler.SetRows(new[]
        {
            new TreeRowMetric(42, 28f),
        });

        scheduler.SetColumns(new[]
        {
            new TreeColumnMetric(0.0, 160.0, TreeFrozenKind.None),
        });

        var viewport = new TreeViewportMetrics(
            RowScrollOffset: 0.0,
            RowViewportHeight: 96.0,
            RowOverscan: 12.0,
            ColumnScrollOffset: 0.0,
            ColumnViewportWidth: 240.0,
            ColumnOverscan: 24.0);

        scheduler.Plan(viewport);
        scheduler.NotifyRowExpansion(42, true);

        var plan = scheduler.Plan(viewport);
        var snapshot = Assert.Single(plan.RowAnimations, anim => anim.NodeId == 42);
        Assert.Equal(1f, snapshot.HeightFactor, 3);
        Assert.True(snapshot.SelectionGlow <= 0.01f);
        Assert.Equal(scheduler.RowAnimationProfile.CaretExpandedDegrees, snapshot.CaretRotationDegrees, 1);
    }

    [Fact]
    public void RowAnimator_DisabledCaretTimelineSnapsToTarget()
    {
        using var scheduler = new TreeVirtualizationScheduler();
        scheduler.ConfigureRowAnimations(TreeRowAnimationProfile.Default with
        {
            CaretRotation = new TreeAnimationTimeline(TimeSpan.Zero, TimelineEasing.Linear),
        });

        scheduler.SetRows(new[]
        {
            new TreeRowMetric(77, 24f),
        });

        scheduler.SetColumns(new[]
        {
            new TreeColumnMetric(0.0, 160.0, TreeFrozenKind.None),
        });

        var viewport = new TreeViewportMetrics(
            RowScrollOffset: 0.0,
            RowViewportHeight: 96.0,
            RowOverscan: 16.0,
            ColumnScrollOffset: 0.0,
            ColumnViewportWidth: 240.0,
            ColumnOverscan: 24.0);

        scheduler.Plan(viewport);
        scheduler.NotifyRowExpansion(77, true);

        var plan = scheduler.Plan(viewport);
        var snapshot = Assert.Single(plan.RowAnimations, anim => anim.NodeId == 77);
        Assert.Equal(scheduler.RowAnimationProfile.CaretExpandedDegrees, snapshot.CaretRotationDegrees, 1);
    }

    [Fact]
    public void RowAnimator_RespectsMinimumHeightFactor()
    {
        using var scheduler = new TreeVirtualizationScheduler();
        var profile = TreeRowAnimationProfile.Default with
        {
            MinHeightFactor = 0.4f,
            ExpandStartFactor = 0.0f,
        };
        scheduler.ConfigureRowAnimations(profile);

        scheduler.SetRows(new[]
        {
            new TreeRowMetric(91, 20f),
        });

        scheduler.SetColumns(new[]
        {
            new TreeColumnMetric(0.0, 160.0, TreeFrozenKind.None),
        });

        var viewport = new TreeViewportMetrics(
            RowScrollOffset: 0.0,
            RowViewportHeight: 80.0,
            RowOverscan: 12.0,
            ColumnScrollOffset: 0.0,
            ColumnViewportWidth: 220.0,
            ColumnOverscan: 18.0);

        scheduler.Plan(viewport);
        scheduler.NotifyRowExpansion(91, true);

        var plan = scheduler.Plan(viewport);
        var snapshot = Assert.Single(plan.RowAnimations, anim => anim.NodeId == 91);
        Assert.True(snapshot.HeightFactor >= 0.39f, $"HeightFactor={snapshot.HeightFactor}");
    }

    [Fact]
    public void Virtualizer_Plan_PollsColumnAnimator()
    {
        using var scheduler = new TreeVirtualizationScheduler();
        scheduler.SetRows(new[]
        {
            new TreeRowMetric(1, 24f),
        });

        var definitions = new[]
        {
            new TreeColumnDefinition(100, 140, 260, Weight: 1.0, Sizing: TreeColumnSizingMode.Star, Key: 1),
            new TreeColumnDefinition(120, 180, 320, Weight: 1.6, Sizing: TreeColumnSizingMode.Star, Key: 2),
        };

        scheduler.UpdateColumns(definitions, availableWidth: 480.0, spacing: 12.0);

        var viewport = new TreeViewportMetrics(
            RowScrollOffset: 0.0,
            RowViewportHeight: 240.0,
            RowOverscan: 48.0,
            ColumnScrollOffset: 0.0,
            ColumnViewportWidth: 640.0,
            ColumnOverscan: 48.0);

        scheduler.Plan(viewport);

        scheduler.UpdateColumns(definitions, availableWidth: 820.0, spacing: 12.0);
        var firstPlan = scheduler.Plan(viewport);
        Assert.True(firstPlan.PaneDiff.Any);

        var sawAnimatedDiff = firstPlan.PaneDiff.Any;
        var settled = false;
        for (var i = 0; i < 60; i++)
        {
            Thread.Sleep(16);
            var plan = scheduler.Plan(viewport);
            if (plan.PaneDiff.Any)
            {
                sawAnimatedDiff = true;
            }
            else if (sawAnimatedDiff)
            {
                settled = true;
                break;
            }
        }

        Assert.True(sawAnimatedDiff, "Column animation never progressed during plan polling.");
        Assert.True(settled, "Column animation failed to settle after polling.");
    }
}
