using System;
using System.Collections.Generic;
using System.Linq;
using VelloSharp.Composition;
using VelloSharp.TreeDataGrid;
using VelloSharp.TreeDataGrid.Composition;
using VelloSharp.TreeDataGrid.Rendering;
using Xunit;

namespace VelloSharp.Charting.Tests.TreeDataGrid;

public sealed class TreeDataGridIntegrationTests
{
    [Fact]
    public void VirtualizationSceneHarness_ReusesSceneNodes_AndSkipsChromeWhenUnchanged()
    {
        var rows = Enumerable.Range(0, 256)
            .Select(i => new TreeRowMetric((uint)(i + 1), 24f + (i % 4)))
            .ToArray();
        var definitions = new[]
        {
            new TreeColumnDefinition(120, 160, 240, Sizing: TreeColumnSizingMode.Pixel, PixelWidth: 160, Key: 1, Frozen: TreeFrozenKind.Leading),
            new TreeColumnDefinition(90, 140, 260, Weight: 1.5, Sizing: TreeColumnSizingMode.Star, Key: 2),
            new TreeColumnDefinition(120, 180, 320, Weight: 1.2, Sizing: TreeColumnSizingMode.Auto, Key: 3, Frozen: TreeFrozenKind.Trailing),
        };
        var slots = new[]
        {
            new TreeColumnSlot(0.0, 160.0),
            new TreeColumnSlot(160.0, 180.0),
            new TreeColumnSlot(340.0, 220.0),
        };

        using var harness = new VirtualizationSceneHarness(rows);
        var paneDiff = harness.UpdateColumns(definitions, slots);
        Assert.True(paneDiff.Any);

        var viewport = new TreeViewportMetrics(
            RowScrollOffset: 0.0,
            RowViewportHeight: 512.0,
            RowOverscan: 128.0,
            ColumnScrollOffset: 0.0,
            ColumnViewportWidth: 720.0,
            ColumnOverscan: 96.0);

        var initial = harness.ApplyPlan(viewport);
        Assert.True(
            initial.Telemetry.Allocated >= initial.Telemetry.WindowLength,
            $"Allocated={initial.Telemetry.Allocated} Window={initial.Telemetry.WindowLength}");
        Assert.True(
            (uint)initial.ActiveRowCount == initial.Telemetry.WindowLength,
            $"ActiveCount={initial.ActiveRowCount} WindowLength={initial.Telemetry.WindowLength}");
        Assert.True(
            initial.DirtyRegionCount >= initial.ActiveRowCount,
            $"Dirty={initial.DirtyRegionCount} Active={initial.ActiveRowCount}");
        Assert.True(initial.CreatedNodeCount >= initial.ActiveRowCount);
        Assert.True(initial.ChromeEncoded);

        var scrolled = harness.ApplyPlan(viewport with { RowScrollOffset = 256.0 });
        var recycledCount = scrolled.RecycleCount;
        Assert.True(
            scrolled.CreatedNodeCount >= initial.CreatedNodeCount,
            $"InitialNodes={initial.CreatedNodeCount} ScrolledNodes={scrolled.CreatedNodeCount}");
        Assert.True(scrolled.Telemetry.Reused > 0, $"Reused={scrolled.Telemetry.Reused}");
        if (recycledCount > 0)
        {
            Assert.True(scrolled.Telemetry.Recycled > 0, $"Recycled={scrolled.Telemetry.Recycled}");
        }
        else
        {
            Assert.Equal(0u, scrolled.Telemetry.Recycled);
        }
        Assert.True(scrolled.DirtyRegionCount >= scrolled.ActiveRowCount, $"Dirty={scrolled.DirtyRegionCount} Active={scrolled.ActiveRowCount}");
        Assert.True(
            scrolled.Telemetry.Recycled == (uint)scrolled.RecycleCount,
            $"TelemetryRecycled={scrolled.Telemetry.Recycled} RecycleCount={scrolled.RecycleCount}");
        Assert.True(
            scrolled.Telemetry.Reused == scrolled.ReuseCount,
            $"TelemetryReused={scrolled.Telemetry.Reused} ReuseCount={scrolled.ReuseCount}");
        if (recycledCount > 0)
        {
            Assert.True(scrolled.Telemetry.Adopted > 0, $"Adopted={scrolled.Telemetry.Adopted} Recycled={recycledCount}");
        }
        Assert.True(
            scrolled.Telemetry.Adopted == scrolled.AdoptCount,
            $"TelemetryAdopted={scrolled.Telemetry.Adopted} AdoptCount={scrolled.AdoptCount}");
        Assert.True(
            scrolled.Telemetry.Allocated == scrolled.AllocateCount,
            $"TelemetryAlloc={scrolled.Telemetry.Allocated} AllocCount={scrolled.AllocateCount}");
        Assert.False(scrolled.ChromeEncoded);

        slots[1] = new TreeColumnSlot(160.0, 210.0);
        definitions[1] = definitions[1] with { Frozen = TreeFrozenKind.Trailing };
        var diff = harness.UpdateColumns(definitions, slots);
        Assert.True(diff.TrailingChanged);

        var updated = harness.ApplyPlan(viewport);
        Assert.True(updated.ChromeEncoded);
    }

    private sealed class VirtualizationSceneHarness : IDisposable
    {
        private readonly TreeVirtualizationScheduler _scheduler = new();
        private readonly TreeSceneGraph _sceneGraph = new();
        private readonly Dictionary<uint, uint> _bufferToSceneNode = new();
        private readonly HashSet<uint> _dirtyCandidates = new();
        private TreeColumnSpan[] _columnSpans = Array.Empty<TreeColumnSpan>();
        private double _totalWidth;
        private TreeChromeVisual _chromeVisual;
        private readonly uint _chromeNode;

        public VirtualizationSceneHarness(ReadOnlySpan<TreeRowMetric> rows)
        {
            _scheduler.SetRows(rows);
            _chromeNode = _sceneGraph.CreateNode();
        }

        public TreeColumnPaneDiff UpdateColumns(
            ReadOnlySpan<TreeColumnDefinition> definitions,
            ReadOnlySpan<TreeColumnSlot> slots)
        {
            var snapshot = _scheduler.UpdateColumns(definitions, slots);
            _columnSpans = snapshot.Spans.Span.ToArray();
            _totalWidth = _columnSpans.Sum(span => span.Width);

            _chromeVisual = new TreeChromeVisual(
                Width: _totalWidth,
                Height: 32.0,
                GridColor: TreeColor.FromRgb(0.24f, 0.24f, 0.28f),
                GridWidth: 1.0f,
                FrozenLeading: snapshot.FrozenLeading,
                FrozenTrailing: snapshot.FrozenTrailing,
                FrozenFill: TreeColor.FromRgb(0.08f, 0.08f, 0.08f, 0.45f));

            return snapshot.PaneDiff;
        }

        public PlanResult ApplyPlan(in TreeViewportMetrics metrics)
        {
            _dirtyCandidates.Clear();
            var plan = _scheduler.Plan(metrics);

            var chromeEncoded = false;
            if (plan.PaneDiff.Any)
            {
                chromeEncoded = _sceneGraph.EncodeChromeIfChanged(_chromeNode, _chromeVisual, _columnSpans, plan.PaneDiff);
            }

            foreach (var recycle in plan.RecycledRows)
            {
                if (_bufferToSceneNode.TryGetValue(recycle.BufferId, out var nodeId))
                {
                    _sceneGraph.MarkRowDirty(nodeId, 0.0, _totalWidth, recycle.Top, recycle.Top + recycle.Height);
                    _dirtyCandidates.Add(nodeId);
                }
            }

            uint reuse = 0;
            uint adopt = 0;
            uint allocate = 0;

            foreach (var entry in plan.ActiveRows)
            {
                var nodeId = ResolveSceneNode(entry, ref reuse, ref adopt, ref allocate);
                var visual = new TreeRowVisual(
                    Width: _totalWidth,
                    Height: entry.Height,
                    Depth: 0,
                    Indent: 18.0,
                    Background: TreeColor.FromRgb(0.12f, 0.12f, 0.14f),
                    HoverBackground: TreeColor.FromRgb(0.16f, 0.16f, 0.2f, 0.4f),
                    SelectionFill: TreeColor.FromRgb(0.2f, 0.45f, 0.8f, 0.3f),
                    Outline: TreeColor.FromRgb(0.32f, 0.54f, 0.92f),
                    OutlineWidth: 1.0f,
                    Stripe: TreeColor.FromRgb(1f, 1f, 1f, 0.04f),
                    StripeWidth: 1.0f,
                    IsSelected: false,
                    IsHovered: false);

                _sceneGraph.EncodeRow(nodeId, visual, _columnSpans);
                _sceneGraph.MarkRowDirty(nodeId, 0.0, _totalWidth, entry.Top, entry.Top + entry.Height);
                _dirtyCandidates.Add(nodeId);
            }

            var dirtyCount = 0;
            foreach (var nodeId in _dirtyCandidates)
            {
                if (_sceneGraph.TryTakeDirty(nodeId, out DirtyRegion _))
                {
                    dirtyCount++;
                }
            }

            var telemetry = _scheduler.GetTelemetry();
            return new PlanResult(
                ActiveRowCount: plan.ActiveRows.Count,
                RecycleCount: plan.RecycledRows.Count,
                DirtyRegionCount: dirtyCount,
                CreatedNodeCount: _bufferToSceneNode.Count,
                ReuseCount: reuse,
                AdoptCount: adopt,
                AllocateCount: allocate,
                Telemetry: telemetry,
                ChromeEncoded: chromeEncoded);
        }

        private uint ResolveSceneNode(in TreeRowPlanEntry entry, ref uint reuse, ref uint adopt, ref uint allocate)
        {
            switch (entry.Action)
            {
                case TreeRowAction.Reuse:
                    reuse++;
                    return _bufferToSceneNode[entry.BufferId];
                case TreeRowAction.Adopt:
                    adopt++;
                    return _bufferToSceneNode[entry.BufferId];
                case TreeRowAction.Allocate:
                    allocate++;
                    var nodeId = _sceneGraph.CreateNode();
                    _bufferToSceneNode[entry.BufferId] = nodeId;
                    return nodeId;
                default:
                    throw new InvalidOperationException($"Unexpected row action {entry.Action}");
            }
        }

        public void Dispose()
        {
            _sceneGraph.Dispose();
            _scheduler.Dispose();
        }
    }

    private readonly record struct PlanResult(
        int ActiveRowCount,
        int RecycleCount,
        int DirtyRegionCount,
        int CreatedNodeCount,
        uint ReuseCount,
        uint AdoptCount,
        uint AllocateCount,
        TreeVirtualizationTelemetry Telemetry,
        bool ChromeEncoded);
}
