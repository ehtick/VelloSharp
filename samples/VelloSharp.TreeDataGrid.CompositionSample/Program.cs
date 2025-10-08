using System;
using System.Linq;
using VelloSharp.TreeDataGrid;
using VelloSharp.TreeDataGrid.Composition;
using VelloSharp.TreeDataGrid.Rendering;

Console.WriteLine("VelloSharp TreeDataGrid Phase 2 Sample");
Console.WriteLine("=======================================\n");

using var dataModel = new TreeDataModel();
var roots = new[]
{
    new TreeNodeDescriptor(1, TreeRowKind.GroupHeader, 28f, hasChildren: true),
    new TreeNodeDescriptor(2, TreeRowKind.Data, 24f, hasChildren: false),
};
dataModel.AttachRoots(roots);

var diffs = dataModel.DrainModelDiffs();
foreach (var diff in diffs)
{
    Console.WriteLine($"Diff: {diff.Kind} node={diff.NodeId} depth={diff.Depth} kind={diff.RowKind}");
}

var headerNode = diffs.First().NodeId;
dataModel.SetExpanded(headerNode, expanded: true);

if (dataModel.TryDequeueMaterialization(out var materializationNode))
{
    var children = new[]
    {
        new TreeNodeDescriptor(11, TreeRowKind.Data, 24f, hasChildren: false),
        new TreeNodeDescriptor(12, TreeRowKind.Data, 26f, hasChildren: false),
    };
    dataModel.AttachChildren(materializationNode, children);
}

var metadata = dataModel.GetMetadata(headerNode);
Console.WriteLine($"Header node expanded={metadata.IsExpanded} height={metadata.Height}");

using var virtualizer = new TreeVirtualizationScheduler();
var rowMetrics = new[]
{
    new TreeRowMetric(headerNode, metadata.Height),
    new TreeRowMetric(headerNode + 1, 24f),
    new TreeRowMetric(headerNode + 2, 26f),
};
virtualizer.SetRows(rowMetrics);

var layoutDefinitions = new[]
{
    new TreeColumnDefinition(120, 160, 240, Weight: 1.0, LeadingMargin: 16, Sizing: TreeColumnSizingMode.Pixel, PixelWidth: 160, Key: 1, Frozen: TreeFrozenKind.Leading),
    new TreeColumnDefinition(90, 140, 260, Weight: 1.8, Sizing: TreeColumnSizingMode.Star, Key: 2),
    new TreeColumnDefinition(120, 180, 320, Weight: 1.2, Sizing: TreeColumnSizingMode.Auto, Key: 3, Frozen: TreeFrozenKind.Trailing),
};

var animator = new TreeColumnLayoutAnimator();
var animatedSlots = animator.Animate(layoutDefinitions, availableWidth: 720.0, spacing: 12.0).ToArray();
var columnSnapshot = virtualizer.UpdateColumns(layoutDefinitions, animatedSlots);

var plan = virtualizer.Plan(new TreeViewportMetrics(
    RowScrollOffset: 0.0,
    RowViewportHeight: 48.0,
    RowOverscan: 16.0,
    ColumnScrollOffset: 0.0,
    ColumnViewportWidth: 480.0,
    ColumnOverscan: 48.0));

Console.WriteLine("\nVirtualization plan:");
foreach (var row in plan.ActiveRows)
{
    Console.WriteLine($"  row node={row.NodeId} buffer={row.BufferId} action={row.Action} top={row.Top:F1}");
}
Console.WriteLine($"Column slice primary start={plan.ColumnSlice.PrimaryStart} count={plan.ColumnSlice.PrimaryCount}");
Console.WriteLine($"Pane diff: leading={plan.PaneDiff.LeadingChanged}, primary={plan.PaneDiff.PrimaryChanged}, trailing={plan.PaneDiff.TrailingChanged}");

Console.WriteLine("\nAnimated column slots:");
for (var i = 0; i < animatedSlots.Length; i++)
{
    Console.WriteLine($"  column {i} offset={animatedSlots[i].Offset:F2} width={animatedSlots[i].Width:F2}");
}

Console.WriteLine($"Column pane diff -> leading={columnSnapshot.PaneDiff.LeadingChanged}, primary={columnSnapshot.PaneDiff.PrimaryChanged}, trailing={columnSnapshot.PaneDiff.TrailingChanged}");
Console.WriteLine($"Leading pane columns: {columnSnapshot.LeadingPane.Count}, trailing pane columns: {columnSnapshot.TrailingPane.Count}");

using var sceneGraph = new TreeSceneGraph();
var rootNode = sceneGraph.CreateNode();
var columnSpans = columnSnapshot.Spans.Span.ToArray();
var totalWidth = columnSpans.Sum(span => span.Width);
var chromeNode = sceneGraph.CreateNode(rootNode);

var rowVisual = new TreeRowVisual(
    Width: totalWidth,
    Height: 28.0,
    Depth: 1,
    Indent: 18.0,
    Background: TreeColor.FromRgb(0.16f, 0.16f, 0.18f),
    HoverBackground: TreeColor.FromRgb(0.22f, 0.22f, 0.25f, 0.5f),
    SelectionFill: TreeColor.FromRgb(0.19f, 0.43f, 0.78f, 0.35f),
    Outline: TreeColor.FromRgb(0.32f, 0.54f, 0.92f),
    OutlineWidth: 1.0f,
    Stripe: TreeColor.FromRgb(1f, 1f, 1f, 0.05f),
    StripeWidth: 1.0f,
    IsSelected: true,
    IsHovered: false);

sceneGraph.EncodeRow(rootNode, rowVisual, columnSpans);
sceneGraph.MarkRowDirty(rootNode, 0.0, totalWidth, 0.0, 28.0);

var chromeVisual = new TreeChromeVisual(
    Width: totalWidth,
    Height: 28.0,
    GridColor: TreeColor.FromRgb(0.25f, 0.25f, 0.28f),
    GridWidth: 1.0f,
    FrozenLeading: columnSnapshot.FrozenLeading,
    FrozenTrailing: columnSnapshot.FrozenTrailing,
    FrozenFill: TreeColor.FromRgb(0.05f, 0.05f, 0.05f, 0.4f));

var chromeEncoded = sceneGraph.EncodeChromeIfChanged(chromeNode, chromeVisual, columnSpans, columnSnapshot.PaneDiff);
Console.WriteLine($"Chrome encoded? {chromeEncoded}");

if (sceneGraph.TryTakeDirty(rootNode, out var dirtyRegion))
{
    Console.WriteLine($"\nDirty region captured: X={dirtyRegion.MinX:F1}..{dirtyRegion.MaxX:F1} Y={dirtyRegion.MinY:F1}..{dirtyRegion.MaxY:F1}");
}

if (sceneGraph.TryTakeDirty(chromeNode, out var chromeRegion))
{
    Console.WriteLine($"Chrome region captured: X={chromeRegion.MinX:F1}..{chromeRegion.MaxX:F1} Y={chromeRegion.MinY:F1}..{chromeRegion.MaxY:F1}");
}

using var renderLoop = new TreeRenderLoop(targetFps: 120f);
if (renderLoop.BeginFrame())
{
    renderLoop.RecordGpuSummary(new TreeGpuTimestampSummary(0.9f, 0.2f, 5));
    var stats = renderLoop.EndFrame(gpuTimeMs: 0f, queueTimeMs: 0f);
    Console.WriteLine($"\nFrame stats: cpu={stats.CpuTimeMs:F2}ms gpu={stats.GpuTimeMs:F2}ms samples={stats.GpuSampleCount} interval={stats.FrameIntervalMs:F2}ms");
}

Console.WriteLine("\nSample complete.");
