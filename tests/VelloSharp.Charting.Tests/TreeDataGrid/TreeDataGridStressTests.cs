using System;
using System.Collections.Generic;
using System.Linq;
using VelloSharp.TreeDataGrid;
using Xunit;

namespace VelloSharp.Charting.Tests.TreeDataGrid;

public sealed class TreeDataGridStressTests
{
    private static TreeRowMetric[] CreateRowMetrics(int count, float baseHeight = 24f)
    {
        var rows = new TreeRowMetric[count];
        for (var i = 0; i < count; i++)
        {
            var height = baseHeight + (i % 5);
            rows[i] = new TreeRowMetric((uint)(i + 1), height);
        }

        return rows;
    }

    [Fact]
    public void VirtualizationTelemetry_SupportsLargeWindows()
    {
        const int rowCount = 50_000;
        var rows = CreateRowMetrics(rowCount);

        using var scheduler = new TreeVirtualizationScheduler();
        scheduler.SetRows(rows);
        scheduler.SetColumns(new[]
        {
            new TreeColumnMetric(0.0, 160.0, TreeFrozenKind.Leading),
            new TreeColumnMetric(160.0, 220.0, TreeFrozenKind.None),
            new TreeColumnMetric(380.0, 200.0, TreeFrozenKind.None),
            new TreeColumnMetric(580.0, 180.0, TreeFrozenKind.Trailing),
        });

        var viewport = new TreeViewportMetrics(
            RowScrollOffset: 0.0,
            RowViewportHeight: 720.0,
            RowOverscan: 160.0,
            ColumnScrollOffset: 0.0,
            ColumnViewportWidth: 960.0,
            ColumnOverscan: 120.0);

        var initialPlan = scheduler.Plan(viewport);
        var initialTelemetry = scheduler.GetTelemetry();

        Assert.Equal((uint)rowCount, initialTelemetry.RowsTotal);
        Assert.Equal((uint)initialPlan.ActiveRows.Count, initialTelemetry.WindowLength);
        Assert.Equal((uint)initialPlan.ActiveRows.Count, initialTelemetry.Allocated);
        Assert.Equal(0u, initialTelemetry.Reused);
        Assert.Equal(0u, initialTelemetry.Adopted);
        Assert.Equal((uint)initialPlan.RecycledRows.Count, initialTelemetry.Recycled);
        Assert.Equal((uint)initialPlan.ActiveRows.Count, initialTelemetry.ActiveBuffers);
        Assert.Equal(0u, initialTelemetry.Evicted);

        var scrolledPlan = scheduler.Plan(viewport with { RowScrollOffset = 720.0 });
        var scrolledTelemetry = scheduler.GetTelemetry();

        var reuseCount = scrolledPlan.ActiveRows.Count(row => row.Action == TreeRowAction.Reuse);
        var adoptCount = scrolledPlan.ActiveRows.Count(row => row.Action == TreeRowAction.Adopt);
        var allocateCount = scrolledPlan.ActiveRows.Count(row => row.Action == TreeRowAction.Allocate);
        var recycledCount = scrolledPlan.RecycledRows.Count;

        Assert.Equal((uint)reuseCount, scrolledTelemetry.Reused);
        Assert.Equal((uint)adoptCount, scrolledTelemetry.Adopted);
        Assert.Equal((uint)allocateCount, scrolledTelemetry.Allocated);
        Assert.Equal((uint)recycledCount, scrolledTelemetry.Recycled);
        Assert.True(
            scrolledTelemetry.ActiveBuffers >= scrolledTelemetry.WindowLength,
            $"Active={scrolledTelemetry.ActiveBuffers} Window={scrolledTelemetry.WindowLength}");
        Assert.True(
            scrolledTelemetry.FreeBuffers >= scrolledTelemetry.Recycled,
            $"Free={scrolledTelemetry.FreeBuffers} Recycled={scrolledTelemetry.Recycled}");
        Assert.Equal((uint)rowCount, scrolledTelemetry.RowsTotal);
        Assert.True(scrolledTelemetry.Reused > 0, $"Reused={scrolledTelemetry.Reused}");
        if (recycledCount > 0)
        {
            Assert.True(scrolledTelemetry.Adopted > 0, $"Recycled={recycledCount} Adopted={scrolledTelemetry.Adopted}");
        }
        Assert.True(scrolledTelemetry.Evicted <= scrolledTelemetry.FreeBuffers);
    }

    [Fact]
    public void VirtualizationTelemetry_EvictsStaleBuffers()
    {
        using var scheduler = new TreeVirtualizationScheduler();

        var largeRows = CreateRowMetrics(2_048);
        scheduler.SetRows(largeRows);
        scheduler.SetColumns(new[]
        {
            new TreeColumnMetric(0.0, 160.0, TreeFrozenKind.Leading),
            new TreeColumnMetric(160.0, 200.0, TreeFrozenKind.None),
            new TreeColumnMetric(360.0, 220.0, TreeFrozenKind.None),
        });

        var largeViewport = new TreeViewportMetrics(
            RowScrollOffset: 0.0,
            RowViewportHeight: 600.0,
            RowOverscan: 200.0,
            ColumnScrollOffset: 0.0,
            ColumnViewportWidth: 860.0,
            ColumnOverscan: 120.0);

        scheduler.Plan(largeViewport);

        var smallRows = CreateRowMetrics(64);
        scheduler.SetRows(smallRows);

        var smallViewport = new TreeViewportMetrics(
            RowScrollOffset: 0.0,
            RowViewportHeight: 120.0,
            RowOverscan: 24.0,
            ColumnScrollOffset: 0.0,
            ColumnViewportWidth: 320.0,
            ColumnOverscan: 24.0);

        scheduler.Plan(smallViewport);
        var telemetry = scheduler.GetTelemetry();

        const uint MinReserve = 128;
        const uint RetentionMultiplier = 6;
        var maxExpected = Math.Max((int)(telemetry.WindowLength * RetentionMultiplier), (int)MinReserve);

        Assert.True(telemetry.FreeBuffers <= (uint)maxExpected, $"Free={telemetry.FreeBuffers} MaxExpected={maxExpected}");
    }

    [Fact]
    public void DataModel_HandlesLargeRangeSelection()
    {
        const int rootCount = 20_000;
        using var model = new TreeDataModel();

        var descriptors = new TreeNodeDescriptor[rootCount];
        var expandableIndices = new List<int>();
        for (var i = 0; i < rootCount; i++)
        {
            var key = (ulong)(i + 1);
            var hasChildren = i % 500 == 0;
            if (hasChildren)
            {
                expandableIndices.Add(i);
            }

            descriptors[i] = new TreeNodeDescriptor(
                key,
                i % 7 == 0 ? TreeRowKind.GroupHeader : TreeRowKind.Data,
                Height: 22f + (i % 4),
                HasChildren: hasChildren);
        }

        model.AttachRoots(descriptors);
        var rootDiffs = model.DrainModelDiffs();
        Assert.Equal(rootCount, rootDiffs.Count);

        var rootLookup = rootDiffs.ToDictionary(diff => diff.NodeId);
        var expandedNodes = new HashSet<uint>();
        foreach (var index in expandableIndices.Take(10))
        {
            var nodeId = rootDiffs[index].NodeId;
            model.SetExpanded(nodeId, true);
            expandedNodes.Add(nodeId);
        }

        while (model.TryDequeueMaterialization(out var nodeId))
        {
            if (!expandedNodes.Contains(nodeId))
            {
                continue;
            }

            const int childCount = 256;
            if (!rootLookup.TryGetValue(nodeId, out var parentDiff))
            {
                continue;
            }

            var parentKey = parentDiff.Key;
            var children = new TreeNodeDescriptor[childCount];
            for (var i = 0; i < childCount; i++)
            {
                children[i] = new TreeNodeDescriptor(
                    Key: parentKey * 1_000_000UL + (ulong)(i + 1),
                    RowKind: TreeRowKind.Data,
                    Height: 20f + (i % 3),
                    HasChildren: false);
            }

            model.AttachChildren(nodeId, children);
        }

        var childDiffs = model.DrainModelDiffs();
        Assert.True(childDiffs.Count >= 10 * 200);
        Assert.All(
            childDiffs.Where(diff => diff.ParentId.HasValue),
            diff => Assert.True(expandedNodes.Contains(diff.ParentId!.Value)));

        var firstNode = rootDiffs[0].NodeId;
        var lastNode = rootDiffs[^1].NodeId;
        model.SetSelected(firstNode, TreeSelectionMode.Replace);
        model.SelectRange(firstNode, lastNode);
        var selectionDiffs = model.DrainSelectionDiffs();

        Assert.True(selectionDiffs.Count >= (rootCount - 1));
        Assert.True(selectionDiffs.Count(diff => diff.IsSelected) >= (rootCount - 1));
    }
}
