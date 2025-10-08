using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using VelloSharp.TreeDataGrid.Rendering;

namespace VelloSharp.TreeDataGrid.Templates;

public sealed class TreeTemplateRuntime : IDisposable
{
    private readonly ConcurrentDictionary<TreeTemplateCacheKey, RuntimeEntry> _entries = new();
    private readonly ITreeTemplateBackend _defaultBackend;
    private bool _disposed;

    public TreeTemplateRuntime()
        : this(TreeTemplateManagedBackend.Shared)
    {
    }

    public TreeTemplateRuntime(ITreeTemplateBackend defaultBackend)
    {
        _defaultBackend = defaultBackend ?? throw new ArgumentNullException(nameof(defaultBackend));
    }

    public TreeTemplateRuntimeHandle BindTemplate(
        TreeCompiledTemplate template,
        in TreeTemplateRuntimeContext context)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(template);
        var backend = context.Backend ?? _defaultBackend;
        var key = template.CacheKey;

        if (_entries.TryGetValue(key, out var existing) && existing.Generation == template.Generation)
        {
            return existing.Handle;
        }

        var handle = backend.Realize(
            key,
            template.Generation,
            template.InstructionSpan,
            context);

        var entry = new RuntimeEntry(template.Generation, handle, backend);
        _entries.AddOrUpdate(key, entry, (_, previous) =>
        {
            previous.Backend.Release(previous.Handle);
            return entry;
        });

        return handle;
    }

    public void Execute(
        TreeCompiledTemplate template,
        TreeSceneGraph sceneGraph,
        in TreeTemplateRuntimeContext context)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(sceneGraph);

        var key = template.CacheKey;
        var handle = BindTemplate(template, context);
        if (!_entries.TryGetValue(key, out var entry) || entry.Generation != template.Generation)
        {
            throw new InvalidOperationException("Template runtime handle not found after binding.");
        }

        entry.Backend.Execute(handle, template, sceneGraph, context);
    }

    public void Invalidate(TreeCompiledTemplate template)
    {
        ThrowIfDisposed();
        if (template is null)
        {
            return;
        }

        if (_entries.TryRemove(template.CacheKey, out var entry))
        {
            entry.Backend.Release(entry.Handle);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var entry in _entries.Values)
        {
            entry.Backend.Release(entry.Handle);
        }

        _entries.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TreeTemplateRuntime));
        }
    }

    private readonly record struct RuntimeEntry(
        int Generation,
        TreeTemplateRuntimeHandle Handle,
        ITreeTemplateBackend Backend);
}

public readonly record struct TreeTemplateRuntimeHandle(Guid HandleId, IntPtr NativeHandle)
{
    public bool IsValid => HandleId != Guid.Empty || NativeHandle != IntPtr.Zero;
}

public interface ITreeTemplateBackend
{
    TreeTemplateRuntimeHandle Realize(
        TreeTemplateCacheKey key,
        int generation,
        ReadOnlySpan<TreeTemplateInstruction> instructions,
        in TreeTemplateRuntimeContext context);

    void Execute(
        TreeTemplateRuntimeHandle handle,
        TreeCompiledTemplate template,
        TreeSceneGraph sceneGraph,
        in TreeTemplateRuntimeContext context);

    void Release(TreeTemplateRuntimeHandle handle);
}

public sealed class TreeTemplateManagedBackend : ITreeTemplateBackend
{
    private readonly ConcurrentDictionary<TreeTemplateCacheKey, BackendEntry> _entries = new();

    public static TreeTemplateManagedBackend Shared { get; } = new();

    private TreeTemplateManagedBackend()
    {
    }

    public TreeTemplateRuntimeHandle Realize(
        TreeTemplateCacheKey key,
        int generation,
        ReadOnlySpan<TreeTemplateInstruction> instructions,
        in TreeTemplateRuntimeContext context)
    {
        var copy = instructions.ToArray();
        var handle = new TreeTemplateRuntimeHandle(Guid.NewGuid(), IntPtr.Zero);
        var entry = new BackendEntry(handle, generation, copy);
        _entries[key] = entry;
        return handle;
    }

    public void Execute(
        TreeTemplateRuntimeHandle handle,
        TreeCompiledTemplate template,
        TreeSceneGraph sceneGraph,
        in TreeTemplateRuntimeContext context)
    {
        // Managed placeholder: mark batches dirty so tests can observe activity.
        foreach (var batch in context.PaneBatches.EnumerateActive())
        {
            var spans = batch.GetSpans();
            if (spans.Length == 0)
            {
                continue;
            }

            var minX = spans[0].Offset;
            var maxX = spans[^1].Offset + spans[^1].Width;
            sceneGraph.MarkRowDirty(batch.NodeId, minX, maxX, 0.0, 24.0);
        }
    }

    public void Release(TreeTemplateRuntimeHandle handle)
    {
        foreach (var (key, entry) in _entries)
        {
            if (entry.Handle == handle)
            {
                _entries.TryRemove(key, out _);
                break;
            }
        }
    }

    public bool TryGetInstructions(TreeTemplateCacheKey key, out ReadOnlyMemory<TreeTemplateInstruction> instructions)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            instructions = entry.Instructions;
            return true;
        }

        instructions = default;
        return false;
    }

    private readonly record struct BackendEntry(
        TreeTemplateRuntimeHandle Handle,
        int Generation,
        TreeTemplateInstruction[] Instructions);
}

public readonly record struct TreeTemplateBindings(IReadOnlyDictionary<string, object?> Values)
{
    public static TreeTemplateBindings Empty { get; } =
        new TreeTemplateBindings(new Dictionary<string, object?>(StringComparer.Ordinal));

    public bool TryGetValue(string key, out object? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Values.TryGetValue(key, out value);
    }
}

public readonly record struct TreeTemplateRuntimeContext(
    TreeTemplateBindings Bindings,
    TreePaneSceneBatchSet PaneBatches,
    ITreeTemplateBackend? Backend)
{
    public static TreeTemplateRuntimeContext Default { get; } =
        new TreeTemplateRuntimeContext(TreeTemplateBindings.Empty, default, null);

    public TreeTemplateRuntimeContext WithBackend(ITreeTemplateBackend backend)
        => new TreeTemplateRuntimeContext(Bindings, PaneBatches, backend);
}
