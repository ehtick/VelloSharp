using System;
using System.Collections.Generic;
using System.Linq;
using VelloSharp.TreeDataGrid;
using VelloSharp.TreeDataGrid.Rendering;
using VelloSharp.TreeDataGrid.Templates;
using Xunit;

namespace VelloSharp.Charting.Tests.TreeDataGrid;

public sealed class TreeTemplateCompilerTests
{
    private const string SampleXaml = """
<RowTemplate xmlns="http://schemas.vello.dev/tdg">
  <Stack Orientation="Horizontal">
    <Text Content="{Binding Path=Data.Name}" />
  </Stack>
</RowTemplate>
""";

    [Fact]
    public void Compile_ReturnsInstructions()
    {
        var compiler = new TreeTemplateCompiler();
        var options = new TreeTemplateCompileOptions("Row:Primary", TreeFrozenKind.None, 1);

        var compiled = compiler.Compile(SampleXaml, options);

        Assert.NotNull(compiled);
        Assert.True(compiled.Instructions.Length > 0);
    }

    [Fact]
    public void Compile_UsesCacheWithinGeneration()
    {
        var compiler = new TreeTemplateCompiler();
        var options = new TreeTemplateCompileOptions("Row:Primary", TreeFrozenKind.None, 42);

        var first = compiler.Compile(SampleXaml, options);
        var second = compiler.Compile(SampleXaml, options);

        Assert.Same(first, second);
    }

    [Fact]
    public void Compile_RebuildsOnGenerationChange()
    {
        var compiler = new TreeTemplateCompiler();
        var options = new TreeTemplateCompileOptions("Row:Primary", TreeFrozenKind.None, 1);
        var first = compiler.Compile(SampleXaml, options);

        var updated = compiler.Compile(SampleXaml, options with { Generation = 2 });

        Assert.NotSame(first, updated);
        Assert.NotEqual(first.Generation, updated.Generation);
    }

    [Fact]
    public void Runtime_RealizesTemplateOnce()
    {
        var compiler = new TreeTemplateCompiler();
        var options = new TreeTemplateCompileOptions("Row:Primary", TreeFrozenKind.None, 1);
        var compiled = compiler.Compile(SampleXaml, options);

        using var backend = new FakeBackend();
        using var runtime = new TreeTemplateRuntime(backend);
        var context = new TreeTemplateRuntimeContext(TreeTemplateBindings.Empty, default, backend);

        var first = compiled.Bind(runtime, context);
        var second = compiled.Bind(runtime, context);

        Assert.Equal(1, backend.RealizeCount);
        Assert.True(first.IsValid);
        Assert.Equal(first, second);
    }

    [Fact]
    public void TextBlockBinding_ExposesBindingPath()
    {
        var definition = TreeTemplateBuilder.Row<PersonRow>(row =>
            row.PrimaryPane(pane =>
                pane.Cell("value", cell =>
                    cell.TextBlock(text => text.BindRowContent(r => r.Name)))));

        var compiler = new TreeTemplateCompiler();
        var options = new TreeTemplateCompileOptions("Row:Primary", TreeFrozenKind.None, 3);
        var compiled = definition.Compile(compiler, options);

        var instructions = compiled.Instructions.Span.ToArray();
        Assert.Contains(instructions, instruction =>
            instruction.OpCode == TreeTemplateOpCode.BindProperty &&
            string.Equals(instruction.PropertyName, "Content", StringComparison.Ordinal) &&
            string.Equals(instruction.Value.BindingPath, "Row.Name", StringComparison.Ordinal));
    }

    private sealed record PersonRow(string Name);

    private sealed class FakeBackend : ITreeTemplateBackend, IDisposable
    {
        private readonly Dictionary<TreeTemplateCacheKey, TreeTemplateRuntimeHandle> _handles = new();

        public int RealizeCount { get; private set; }

        public TreeTemplateRuntimeHandle Realize(
            TreeTemplateCacheKey key,
            int generation,
            ReadOnlySpan<TreeTemplateInstruction> instructions,
            in TreeTemplateRuntimeContext context)
        {
            RealizeCount++;
            if (_handles.TryGetValue(key, out var handle))
            {
                return handle;
            }

            handle = new TreeTemplateRuntimeHandle(Guid.NewGuid(), IntPtr.Zero);
            _handles[key] = handle;
            return handle;
        }

        public void Execute(
            TreeTemplateRuntimeHandle handle,
            TreeCompiledTemplate template,
            TreeSceneGraph sceneGraph,
            in TreeTemplateRuntimeContext context)
        {
            // No-op for unit test backend.
        }

        public void Release(TreeTemplateRuntimeHandle handle)
        {
            foreach (var (key, value) in _handles)
            {
                if (value == handle)
                {
                    _handles.Remove(key);
                    break;
                }
            }
        }

        public void Dispose()
        {
            _handles.Clear();
        }
    }
}
