using System;
using System.Linq;
using VelloSharp.TreeDataGrid;
using VelloSharp.TreeDataGrid.Templates;
using Xunit;

namespace VelloSharp.Charting.Tests.TreeDataGrid;

public sealed class TreeTemplateBuilderTests
{
    [Fact]
    public void Builder_ProducesRowAndColumnBindings()
    {
        var definition = TreeTemplateBuilder.Row<PersonRow, ColumnMeta>(row =>
            row.PrimaryPane(pane =>
                pane.Cell("name", cell =>
                    cell.Stack(stack =>
                    {
                        stack.Text(text => text.BindRowContent(r => r.Name));
                        stack.Text(text => text.Content(":"));
                        stack.Text(text => text.BindColumnContent(c => c.Key));
                        stack.Spacing(6.0);
                    })
                )
            ));

        var compiler = new TreeTemplateCompiler();
        var compiled = definition.Compile(
            compiler,
            new TreeTemplateCompileOptions("Row:Primary", TreeFrozenKind.None, 1));

        Assert.True(compiled.Instructions.Length > 0);

        var instructions = compiled.Instructions.Span.ToArray();
        Assert.Contains(
            instructions,
            instruction => instruction.OpCode == TreeTemplateOpCode.BindProperty &&
                           string.Equals(instruction.Value.BindingPath, "Row.Name", StringComparison.Ordinal));
        Assert.Contains(
            instructions,
            instruction => instruction.OpCode == TreeTemplateOpCode.BindProperty &&
                           string.Equals(instruction.Value.BindingPath, "Column.Key", StringComparison.Ordinal));
    }

    [Fact]
    public void Builder_ReusesCompiledTemplateForSameGeneration()
    {
        var definition = TreeTemplateBuilder.Row<PersonRow>(row =>
            row.PrimaryPane(pane =>
                pane.Cell("value", cell =>
                    cell.Text(text => text.BindRowContent(r => r.Value)))));

        var compiler = new TreeTemplateCompiler();
        var options = new TreeTemplateCompileOptions("Row:Primary", TreeFrozenKind.None, 7);

        var first = definition.Compile(compiler, options);
        var second = definition.Compile(compiler, options);

        Assert.Same(first, second);
    }

    private sealed record PersonRow(string Name, double Value);

    private sealed record ColumnMeta(string Key, bool IsNumeric);
}
