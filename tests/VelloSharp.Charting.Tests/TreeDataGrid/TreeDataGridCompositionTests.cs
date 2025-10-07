using System;
using System.Collections.Generic;
using VelloSharp.Composition;
using VelloSharp.TreeDataGrid.Composition;
using VelloSharp.TreeDataGrid.Rendering;
using Xunit;

namespace VelloSharp.Charting.Tests.TreeDataGrid;

public sealed class TreeDataGridCompositionTests
{
    [Fact]
    public void ColumnLayout_UsesSharedSolver()
    {
        var engine = new TreeNodeLayoutEngine();
        var definitions = new[]
        {
            new TreeColumnDefinition(80.0, 120.0, 240.0, 1.0, 8.0, 8.0),
            new TreeColumnDefinition(100.0, 200.0, 400.0, 2.0, 8.0, 8.0),
            new TreeColumnDefinition(60.0, 90.0, 150.0, 1.0, 4.0, 4.0),
        };

        var slots = engine.ArrangeColumns(definitions, 800.0, 12.0);
        Assert.Equal(3, slots.Count);
        Assert.Equal(8.0, slots[0].Offset, 6);
        Assert.Equal(187.448276, slots[0].Width, 6);
        Assert.Equal(223.448276, slots[1].Offset, 6);
        Assert.Equal(400.0, slots[1].Width, 6);
        Assert.Equal(647.448276, slots[2].Offset, 6);
        Assert.Equal(123.724138, slots[2].Width, 6);
    }

    [Fact]
    public void SceneGraph_AggregatesTreeDirtyBounds()
    {
        using var graph = new TreeSceneGraph();
        var root = graph.CreateNode();
        var branch = graph.CreateNode(root);
        var leaf = graph.CreateNode(branch);

        graph.MarkCellDirty(branch, 12.0, 18.0);
        graph.MarkRowDirty(branch, 8.0, 24.0, 32.0, 48.0);
        graph.MarkRowDirty(leaf, -4.0, 6.0, 28.0, 52.0);

        Assert.True(graph.TryTakeDirty(root, out var region));
        Assert.Equal(-4.0, region.MinX, 6);
        Assert.Equal(24.0, region.MaxX, 6);
        Assert.Equal(18.0, region.MinY, 6);
        Assert.Equal(52.0, region.MaxY, 6);
        Assert.False(graph.TryTakeDirty(root, out _));
    }
}
