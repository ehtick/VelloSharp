using System;
using System.Linq;
using VelloSharp.ChartDiagnostics;
using VelloSharp.Composition;
using VelloSharp.Composition.Controls;
using VelloSharp.TreeDataGrid;
using VelloSharp.TreeDataGrid.Composition;
using VelloSharp.TreeDataGrid.Rendering;

Console.WriteLine("VelloSharp TreeDataGrid Phase 2 Sample");
Console.WriteLine("=======================================\n");

using var dataModel = new TreeDataModel();
using var virtualizer = new TreeVirtualizationScheduler();
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
virtualizer.NotifyRowExpansion(headerNode, isExpanded: true);

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

using var animator = new TreeColumnLayoutAnimator();
var animatedSlots = animator.Animate(layoutDefinitions, availableWidth: 720.0, spacing: 12.0).ToArray();
var columnSnapshot = virtualizer.UpdateColumns(layoutDefinitions, availableWidth: 720.0, spacing: 12.0);
var columnSpans = columnSnapshot.Spans.Span.ToArray();
var totalWidth = columnSpans.Sum(span => span.Width);

var viewportMetrics = new TreeViewportMetrics(
    RowScrollOffset: 0.0,
    RowViewportHeight: 48.0,
    RowOverscan: 16.0,
    ColumnScrollOffset: 0.0,
    ColumnViewportWidth: 480.0,
    ColumnOverscan: 48.0);
var plan = virtualizer.Plan(viewportMetrics);

Console.WriteLine("\nVirtualization plan:");
foreach (var row in plan.ActiveRows)
{
    Console.WriteLine($"  row node={row.NodeId} buffer={row.BufferId} action={row.Action} top={row.Top:F1}");
}
Console.WriteLine($"Column slice primary start={plan.ColumnSlice.PrimaryStart} count={plan.ColumnSlice.PrimaryCount}");
Console.WriteLine($"Pane diff: leading={plan.PaneDiff.LeadingChanged}, primary={plan.PaneDiff.PrimaryChanged}, trailing={plan.PaneDiff.TrailingChanged}");
Console.WriteLine($"Buffer diagnostics: reused={plan.BufferDiagnostics.Reused} adopted={plan.BufferDiagnostics.Adopted} allocated={plan.BufferDiagnostics.Allocated} adoptionRate={plan.BufferDiagnostics.AdoptionRate:P1}");

Console.WriteLine("\nAnimated column slots:");
for (var i = 0; i < animatedSlots.Length; i++)
{
    Console.WriteLine($"  column {i} offset={animatedSlots[i].Offset:F2} width={animatedSlots[i].Width:F2}");
}

Console.WriteLine($"Column pane diff -> leading={columnSnapshot.PaneDiff.LeadingChanged}, primary={columnSnapshot.PaneDiff.PrimaryChanged}, trailing={columnSnapshot.PaneDiff.TrailingChanged}");
Console.WriteLine($"Leading pane columns: {columnSnapshot.LeadingPane.Count}, trailing pane columns: {columnSnapshot.TrailingPane.Count}");

Console.WriteLine("\nTemplated control validation:");
var compositionRows = TreeSampleConverters.CreateVirtualRowMetrics(rowMetrics);
var compositionColumns = TreeSampleConverters.CreateVirtualColumnStrips(columnSnapshot.Metrics.Span);
var rowViewport = new RowViewportMetrics(viewportMetrics.RowScrollOffset, viewportMetrics.RowViewportHeight, viewportMetrics.RowOverscan);
var columnViewport = new ColumnViewportMetrics(viewportMetrics.ColumnScrollOffset, viewportMetrics.ColumnViewportWidth, viewportMetrics.ColumnOverscan);

var templatedHost = new TreeVirtualizedPanelControl
{
    Template = TreeTemplateFactory.CreateRowHostTemplate(),
};
templatedHost.TemplateApplied += (_, _) => Console.WriteLine("  Template applied for composition host.");
templatedHost.Mount();

double templatedHeight = 0;
if (!templatedHost.ApplyTemplate())
{
    Console.WriteLine("  Failed to apply templated control template.");
}
else
{
    templatedHost.UpdateVirtualization(compositionRows, compositionColumns);
    using (var compositionPlan = templatedHost.CaptureVirtualizationPlan(rowViewport, columnViewport))
    {
        templatedHost.SyncRows(compositionPlan.Active);

        foreach (var entry in compositionPlan.Active)
        {
            templatedHeight += entry.Height;
        }

        Console.WriteLine($"  Active rows={compositionPlan.Active.Length} recycled={compositionPlan.Recycled.Length}");
        Console.WriteLine($"  Window start={compositionPlan.Window.StartIndex} end={compositionPlan.Window.EndIndex} totalHeight={compositionPlan.Window.TotalHeight:F1}");
        Console.WriteLine($"  Column slice -> primaryStart={compositionPlan.Columns.PrimaryStart} count={compositionPlan.Columns.PrimaryCount} frozenLeading={compositionPlan.Columns.FrozenLeading} frozenTrailing={compositionPlan.Columns.FrozenTrailing}");
        Console.WriteLine($"  Telemetry -> reused={compositionPlan.Telemetry.Reused} adopted={compositionPlan.Telemetry.Adopted} allocated={compositionPlan.Telemetry.Allocated} recycled={compositionPlan.Telemetry.Recycled}");
    }

    templatedHeight = templatedHeight <= 0
        ? viewportMetrics.RowViewportHeight
        : templatedHeight;

    var hostConstraints = new LayoutConstraints(
        new ScalarConstraint(0, totalWidth, totalWidth),
        new ScalarConstraint(0, templatedHeight, templatedHeight));

    templatedHost.Measure(hostConstraints);
    templatedHost.Arrange(new LayoutRect(0, 0, templatedHost.DesiredSize.Width, templatedHost.DesiredSize.Height, 0, templatedHost.DesiredSize.Height));

    Console.WriteLine($"  Panel rows materialized={templatedHost.RowCount} desired={templatedHost.DesiredSize.Width:F1}x{templatedHost.DesiredSize.Height:F1}");

    var buttonSample = new Button
    {
        Text = "Apply Template",
        Background = new CompositionColor(0.26f, 0.35f, 0.55f, 0.92f),
        BorderBrush = new CompositionColor(0.38f, 0.46f, 0.64f, 1f),
        Padding = new LayoutThickness(12, 6, 12, 6),
    };
    buttonSample.Mount();
    buttonSample.Measure(new LayoutConstraints(
        new ScalarConstraint(0, 180, 180),
        new ScalarConstraint(0, 60, 60)));
    Console.WriteLine($"  Button sample -> desired {buttonSample.DesiredSize.Width:F1}x{buttonSample.DesiredSize.Height:F1}");
    buttonSample.Unmount();

    var dropDownSample = new DropDown
    {
        PlaceholderText = "Select pane",
    };
    dropDownSample.Items.Add(new TextBlock { Text = "Primary", FontSize = 13f });
    dropDownSample.Items.Add(new TextBlock { Text = "Leading", FontSize = 13f });
    dropDownSample.Items.Add(new TextBlock { Text = "Trailing", FontSize = 13f });
    dropDownSample.SelectedIndex = 1;
    dropDownSample.Mount();
    dropDownSample.Measure(new LayoutConstraints(
        new ScalarConstraint(0, 220, 220),
        new ScalarConstraint(0, 64, 64)));
    var selectedItem = dropDownSample.SelectedItem as TextBlock;
    Console.WriteLine($"  DropDown sample -> selected='{selectedItem?.Text}' width={dropDownSample.DesiredSize.Width:F1}");
    dropDownSample.Unmount();

    var tabSample = new TabControl();
    tabSample.Items.Add(new TabItem
    {
        Header = "Summary",
        Content = new TextBlock { Text = $"Rows: {compositionPlan.Active.Length}", FontSize = 12f },
    });
    tabSample.Items.Add(new TabItem
    {
        Header = "Columns",
        Content = new TextBlock { Text = $"Leading {columnSnapshot.LeadingPane.Count} / Trailing {columnSnapshot.TrailingPane.Count}", FontSize = 12f },
    });
    tabSample.SelectedIndex = 0;
    tabSample.Mount();
    tabSample.Measure(new LayoutConstraints(
        new ScalarConstraint(0, 320, 320),
        new ScalarConstraint(0, 180, 180)));
    Console.WriteLine($"  TabControl sample -> tabs={tabSample.Items.Count} desired={tabSample.DesiredSize.Width:F1}");
    tabSample.Unmount();
}

templatedHost.Unmount();

using var sceneGraph = new TreeSceneGraph();
var rootNode = sceneGraph.CreateNode();
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

using var renderLoop = new TreeRenderLoop(targetFps: 120f, telemetrySink: DashboardTelemetrySink.Instance);
if (renderLoop.BeginFrame())
{
    renderLoop.RecordGpuSummary(new TreeGpuTimestampSummary(0.9f, 0.2f, 5));
    var stats = renderLoop.EndFrame(gpuTimeMs: 0f, queueTimeMs: 0f);
    Console.WriteLine($"\nFrame stats: cpu={stats.CpuTimeMs:F2}ms gpu={stats.GpuTimeMs:F2}ms samples={stats.GpuSampleCount} interval={stats.FrameIntervalMs:F2}ms");
    if (renderLoop.Diagnostics.TryGetRecent(out FrameStats diagnostics))
    {
        Console.WriteLine($"Diagnostics collector -> cpu={diagnostics.CpuTime.TotalMilliseconds:F2}ms gpu={diagnostics.GpuTime.TotalMilliseconds:F2}ms queue={diagnostics.QueueLatency.TotalMilliseconds:F2}ms");
    }
}

Console.WriteLine("\nSample complete.");

internal static class TreeSampleConverters
{
    public static VirtualRowMetric[] CreateVirtualRowMetrics(ReadOnlySpan<TreeRowMetric> rows)
    {
        if (rows.IsEmpty)
        {
            return Array.Empty<VirtualRowMetric>();
        }

        var result = new VirtualRowMetric[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            ref readonly var metric = ref rows[i];
            result[i] = new VirtualRowMetric(metric.NodeId, metric.Height);
        }

        return result;
    }

    public static VirtualColumnStrip[] CreateVirtualColumnStrips(ReadOnlySpan<TreeColumnMetric> columns)
    {
        if (columns.IsEmpty)
        {
            return Array.Empty<VirtualColumnStrip>();
        }

        var result = new VirtualColumnStrip[columns.Length];
        for (int i = 0; i < columns.Length; i++)
        {
            ref readonly var column = ref columns[i];
            result[i] = new VirtualColumnStrip(column.Offset, column.Width, Map(column.Frozen), column.Key);
        }

        return result;
    }

    private static FrozenKind Map(TreeFrozenKind kind) => kind switch
    {
        TreeFrozenKind.Leading => FrozenKind.Leading,
        TreeFrozenKind.Trailing => FrozenKind.Trailing,
        _ => FrozenKind.None,
    };
}

internal sealed class TreeVirtualizedPanelControl : TemplatedControl
{
    private Panel? _rowPanel;

    public int RowCount => _rowPanel?.Children.Count ?? 0;

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _rowPanel = (TemplateRoot as Border)?.Child as Panel;
    }

    public void SyncRows(ReadOnlySpan<RowPlanEntry> plan)
    {
        if (_rowPanel is null)
        {
            Console.WriteLine("  ! Row panel not found in template.");
            return;
        }

        var children = _rowPanel.Children;

        while (children.Count < plan.Length)
        {
            children.Add(new VirtualizedRowElement(children.Count));
        }

        while (children.Count > plan.Length)
        {
            children.RemoveAt(children.Count - 1);
        }

        for (int i = 0; i < plan.Length; i++)
        {
            if (children[i] is VirtualizedRowElement row)
            {
                row.Update(plan[i]);
            }
            else
            {
                var replacement = new VirtualizedRowElement(i);
                replacement.Update(plan[i]);
                children[i] = replacement;
            }
        }
    }
}

internal sealed class VirtualizedRowElement : CompositionElement
{
    private readonly string _slotName;
    private RowPlanEntry _entry;

    public VirtualizedRowElement(int slotIndex)
    {
        _slotName = $"slot_{slotIndex:D2}";
    }

    public void Update(RowPlanEntry entry)
    {
        _entry = entry;
    }

    public override void Measure(in LayoutConstraints constraints)
    {
        base.Measure(constraints);
        double width = Math.Clamp(constraints.Width.Preferred, constraints.Width.Min, constraints.Width.Max);
        double height = Math.Max(_entry.Height, Math.Max(constraints.Height.Min, 0));
        DesiredSize = new LayoutSize(width, height);
    }

    public override void Arrange(in LayoutRect rect)
    {
        base.Arrange(rect);
        Console.WriteLine($"    {_slotName} -> node={_entry.NodeId} buffer={_entry.BufferId} action={_entry.Action} top={rect.Y:F1} height={rect.Height:F1}");
    }
}

internal static class TreeTemplateFactory
{
    public static CompositionTemplate CreateRowHostTemplate() =>
        CompositionTemplate.Create(_ =>
        {
            var border = new Border
            {
                BorderThickness = new LayoutThickness(1, 1, 1, 1),
                Padding = new LayoutThickness(6, 6, 6, 6),
                BorderBrush = new CompositionColor(0.32f, 0.42f, 0.72f, 0.8f),
                Background = new CompositionColor(0.10f, 0.10f, 0.12f, 0.95f),
            };

            var panel = new Panel
            {
                Orientation = LayoutOrientation.Vertical,
                Spacing = 3,
                Padding = new LayoutThickness(4, 4, 4, 4),
                CrossAlignment = LayoutAlignment.Stretch,
            };

            border.Child = panel;
            return border;
        });
}
