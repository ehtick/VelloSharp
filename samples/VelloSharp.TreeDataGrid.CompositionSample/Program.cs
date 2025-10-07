using System;
using System.Linq;
using VelloSharp.Composition;
using VelloSharp.TreeDataGrid.Composition;
using VelloSharp.TreeDataGrid.Rendering;

Console.WriteLine("VelloSharp TreeDataGrid Composition Sample");
Console.WriteLine("=========================================\n");

var columns = new[]
{
    new TreeColumnDefinition(minWidth: 96, preferredWidth: 140, maxWidth: 280, weight: 1.0, leadingMargin: 16, trailingMargin: 8),
    new TreeColumnDefinition(minWidth: 72, preferredWidth: 120, maxWidth: 240, weight: 1.6),
    new TreeColumnDefinition(minWidth: 100, preferredWidth: 180, maxWidth: 360, weight: 2.2, trailingMargin: 12),
    new TreeColumnDefinition(minWidth: 80, preferredWidth: 120, maxWidth: 220, weight: 1.2),
};

var layoutEngine = new TreeNodeLayoutEngine();
var viewportWidths = new[] { 960.0, 640.0, 420.0 };
IReadOnlyList<TreeColumnSlot>? primaryLayout = null;

foreach (var width in viewportWidths)
{
    var slots = layoutEngine.ArrangeColumns(columns, width, spacing: 12);
    primaryLayout ??= slots;

    Console.WriteLine($"Viewport width: {width:F0}px");
    if (slots.Count == 0)
    {
        Console.WriteLine("  No columns could be arranged.\n");
        continue;
    }

    for (var i = 0; i < slots.Count; i++)
    {
        var slot = slots[i];
        Console.WriteLine($"  Column {i:D2}: offset={slot.Offset,7:F2} width={slot.Width,7:F2}");
    }

    Console.WriteLine();
}

if (primaryLayout is null || primaryLayout.Count == 0)
{
    Console.WriteLine("No column layout produced; aborting dirty-region demo.");
    return;
}

using var sceneGraph = new TreeSceneGraph();
var rootNode = sceneGraph.CreateNode();
var rowNode = sceneGraph.CreateNode(rootNode);

// Mark a row span and individual cell updates to exercise dirty-region aggregation.
var rowTop = 32.0;
var rowBottom = 32.0 + 28.0;
var rowMinX = primaryLayout.First().Offset;
var rowMaxX = primaryLayout.Last().Offset + primaryLayout.Last().Width;

sceneGraph.MarkRowDirty(rowNode, rowMinX, rowMaxX, rowTop, rowBottom);
sceneGraph.MarkCellDirty(rowNode, primaryLayout[1].Offset + 12.0, rowTop + 8.0);
sceneGraph.MarkCellDirty(rowNode, primaryLayout[2].Offset + 36.0, rowTop + 12.0);

if (sceneGraph.TryTakeDirty(rowNode, out DirtyRegion region))
{
    Console.WriteLine("Aggregated dirty region:");
    Console.WriteLine($"  X range: {region.MinX:F2} – {region.MaxX:F2}");
    Console.WriteLine($"  Y range: {region.MinY:F2} – {region.MaxY:F2}");
}
else
{
    Console.WriteLine("Scene graph reported no dirty region.");
}

var cleared = !sceneGraph.TryTakeDirty(rowNode, out _);
Console.WriteLine($"\nDirty region consumed: {(cleared ? "yes" : "no")}");

sceneGraph.DisposeNode(rowNode);
sceneGraph.DisposeNode(rootNode);
Console.WriteLine("\nSample complete.");
