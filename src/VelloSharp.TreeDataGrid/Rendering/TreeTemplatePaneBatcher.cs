using System;
using System.Collections.Generic;
using VelloSharp.TreeDataGrid.Composition;

namespace VelloSharp.TreeDataGrid.Rendering;

/// <summary>
/// Creates reusable scene batches for the leading, primary, and trailing panes
/// based on a <see cref="TreeColumnPaneSnapshot"/> slice. Consumers can use the
/// batches to render template fragments without re-slicing the column cache.
/// </summary>
public sealed class TreeTemplatePaneBatcher : IDisposable
{
    private readonly TreeSceneGraph _sceneGraph;
    private readonly uint _parentNodeId;
    private uint _leadingNodeId;
    private uint _primaryNodeId;
    private uint _trailingNodeId;
    private bool _disposed;

    public TreeTemplatePaneBatcher(TreeSceneGraph sceneGraph, uint parentNodeId)
    {
        _sceneGraph = sceneGraph ?? throw new ArgumentNullException(nameof(sceneGraph));
        if (parentNodeId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parentNodeId), "Template batches require a parent scene node.");
        }

        _parentNodeId = parentNodeId;
    }

    public TreePaneSceneBatchSet Build(in TreeColumnStripSnapshot stripSnapshot)
    {
        ThrowIfDisposed();
        var leading = CreateBatch(TreeFrozenKind.Leading, stripSnapshot.LeadingPane, ref _leadingNodeId);
        var primary = CreateBatch(TreeFrozenKind.None, stripSnapshot.PrimaryPane, ref _primaryNodeId);
        var trailing = CreateBatch(TreeFrozenKind.Trailing, stripSnapshot.TrailingPane, ref _trailingNodeId);

        return new TreePaneSceneBatchSet(leading, primary, trailing);
    }

    public void Reset()
    {
        ThrowIfDisposed();
        ClearNode(_leadingNodeId);
        ClearNode(_primaryNodeId);
        ClearNode(_trailingNodeId);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DisposeNode(ref _leadingNodeId);
        DisposeNode(ref _primaryNodeId);
        DisposeNode(ref _trailingNodeId);

        _disposed = true;
    }

    private TreePaneSceneBatch CreateBatch(
        TreeFrozenKind pane,
        in TreeColumnPaneSnapshot snapshot,
        ref uint nodeId)
    {
        if (snapshot.Count == 0)
        {
            if (nodeId != 0)
            {
                _sceneGraph.Clear(nodeId);
            }

            return TreePaneSceneBatch.Empty(pane);
        }

        if (nodeId == 0)
        {
            nodeId = _sceneGraph.CreateNode(_parentNodeId);
        }

        return new TreePaneSceneBatch(pane, nodeId, snapshot.Spans, snapshot.Metrics);
    }

    private void ClearNode(uint nodeId)
    {
        if (nodeId != 0)
        {
            _sceneGraph.Clear(nodeId);
        }
    }

    private void DisposeNode(ref uint nodeId)
    {
        if (nodeId != 0)
        {
            _sceneGraph.DisposeNode(nodeId);
            nodeId = 0;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TreeTemplatePaneBatcher));
        }
    }
}

public readonly record struct TreePaneSceneBatch(
    TreeFrozenKind Pane,
    uint NodeId,
    ReadOnlyMemory<TreeColumnSpan> Spans,
    ReadOnlyMemory<TreeColumnMetric> Metrics)
{
    public bool IsEmpty => NodeId == 0 || Spans.IsEmpty;
    public ReadOnlySpan<TreeColumnSpan> GetSpans() => Spans.Span;
    public ReadOnlySpan<TreeColumnMetric> GetMetrics() => Metrics.Span;

    public static TreePaneSceneBatch Empty(TreeFrozenKind pane)
        => new(pane, 0, ReadOnlyMemory<TreeColumnSpan>.Empty, ReadOnlyMemory<TreeColumnMetric>.Empty);
}

public readonly record struct TreePaneSceneBatchSet(
    TreePaneSceneBatch Leading,
    TreePaneSceneBatch Primary,
    TreePaneSceneBatch Trailing)
{
    public IEnumerable<TreePaneSceneBatch> EnumerateActive()
    {
        if (!Leading.IsEmpty)
        {
            yield return Leading;
        }

        if (!Primary.IsEmpty)
        {
            yield return Primary;
        }

        if (!Trailing.IsEmpty)
        {
            yield return Trailing;
        }
    }

    public bool TryGet(TreeFrozenKind pane, out TreePaneSceneBatch batch)
    {
        batch = pane switch
        {
            TreeFrozenKind.Leading => Leading,
            TreeFrozenKind.Trailing => Trailing,
            _ => Primary,
        };

        return !batch.IsEmpty;
    }
}
