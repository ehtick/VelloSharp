#nullable enable
using System;
using VelloSharp.TreeDataGrid.Composition;
using System.Collections.Generic;
using VelloSharp.TreeDataGrid;
using VelloSharp.TreeDataGrid.Rendering;
using VelloSharp.TreeDataGrid.Templates;
using Xunit;

namespace VelloSharp.Charting.Tests.TreeDataGrid;

public sealed class TreeTemplateNativeBackendTests
{
    [Fact]
    public void NativeBackend_EncodesPaneAndMarksDirty()
    {
        var definition = TreeTemplateBuilder.Row<PersonRow>(row =>
            row.PrimaryPane(pane =>
                pane.Cell("value", cell =>
                {
                    cell.Rectangle(rect => rect.Background("#336699"));
                    cell.TextBlock(text => text
                        .BindRowContent(r => r.Name)
                        .Foreground("#E6EDF7"));
                    cell.AccessText(text => text.Content("_Details"));
                    cell.TextBox(text => text
                        .BindRowContent(r => r.Name)
                        .Background("#1F2430")
                        .Foreground("#F8FAFF"));
                })));

        var compiler = new TreeTemplateCompiler();
        var compiled = definition.Compile(
            compiler,
            new TreeTemplateCompileOptions("Row:Primary", TreeFrozenKind.None, 1));

        using var backend = new TreeTemplateNativeBackend();
        using var runtime = new TreeTemplateRuntime(backend);
        using var sceneGraph = new TreeSceneGraph();
        var rootNode = sceneGraph.CreateNode();
        var parentNode = sceneGraph.CreateNode(rootNode);
        using var batcher = new TreeTemplatePaneBatcher(sceneGraph, parentNode);
        var spans = new[]
        {
            new TreeColumnSpan(0.0, 120.0, TreeFrozenKind.None, Key: 1),
            new TreeColumnSpan(120.0, 160.0, TreeFrozenKind.None, Key: 2),
        };
        var metrics = new[]
        {
            new TreeColumnMetric(0.0, 120.0, TreeFrozenKind.None, Key: 1),
            new TreeColumnMetric(120.0, 160.0, TreeFrozenKind.None, Key: 2),
        };
        var snapshot = new TreeColumnStripSnapshot(
            spans,
            metrics,
            frozenLeading: 0,
            frozenTrailing: 0,
            paneDiff: new TreeColumnPaneDiff(true, true, true));

        var batches = batcher.Build(snapshot);
        var bindings = new TreeTemplateBindings(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Row.Name"] = "Alpha",
        });
        var context = new TreeTemplateRuntimeContext(bindings, batches, backend);

        runtime.Execute(compiled, sceneGraph, context);

        var primary = batches.Primary;
        Assert.NotEqual<uint>(0, primary.NodeId);
        Assert.True(sceneGraph.TryTakeDirty(primary.NodeId, out var region));
        Assert.True(region.MaxX > region.MinX);
    }

    [Fact]
    public void RenderHookRegistries_RegisterRoundTrip()
    {
        TreeShaderRegistry.Register(1, new TreeShaderDescriptor(
            TreeShaderKind.Solid,
            TreeColor.FromRgb(0.25f, 0.42f, 0.71f, 1f)));

        try
        {
            TreeMaterialRegistry.Register(5, new TreeMaterialDescriptor(1, 0.75f));
            try
            {
                TreeRenderHookRegistry.Register(
                    9,
                    new TreeRenderHookDescriptor(
                        TreeRenderHookKind.FillRounded,
                        5,
                        Inset: 2.0,
                        CornerRadius: 3.0));
            }
            finally
            {
                TreeRenderHookRegistry.Unregister(9);
                TreeMaterialRegistry.Unregister(5);
            }
        }
        finally
        {
            TreeShaderRegistry.Unregister(1);
        }
    }

    [Fact]
    public void MaterialRegistration_ThrowsWhenShaderMissing()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TreeMaterialRegistry.Register(11, new TreeMaterialDescriptor(9999)));
    }

    [Fact]
    public void RenderHookRegistration_ThrowsWhenMaterialMissing()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TreeRenderHookRegistry.Register(
                17,
                new TreeRenderHookDescriptor(TreeRenderHookKind.FillRounded, 1234)));
    }

    private sealed record PersonRow(string Name);
}
