using System;
using VelloSharp.Composition;

namespace VelloSharp.TreeDataGrid.Rendering;

/// <summary>
/// Thin wrapper over the shared scene cache tailored for TreeDataGrid virtualization scenarios.
/// </summary>
public sealed class TreeSceneGraph : IDisposable
{
    private readonly SceneCache _sceneCache = new();
    private bool _disposed;

    public uint CreateNode(uint? parentId = null)
    {
        ThrowIfDisposed();
        return _sceneCache.CreateNode(parentId);
    }

    public void DisposeNode(uint nodeId)
    {
        ThrowIfDisposed();
        _sceneCache.DisposeNode(nodeId);
    }

    public void MarkCellDirty(uint nodeId, double x, double y)
    {
        ThrowIfDisposed();
        _sceneCache.MarkDirty(nodeId, x, y);
    }

    public void MarkRowDirty(uint nodeId, double minX, double maxX, double minY, double maxY)
    {
        ThrowIfDisposed();
        _sceneCache.MarkDirtyBounds(nodeId, minX, maxX, minY, maxY);
    }

    public bool TryTakeDirty(uint nodeId, out DirtyRegion region)
    {
        ThrowIfDisposed();
        return _sceneCache.TakeDirty(nodeId, out region);
    }

    public void Clear(uint nodeId)
    {
        ThrowIfDisposed();
        _sceneCache.Clear(nodeId);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TreeSceneGraph));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _sceneCache.Dispose();
        _disposed = true;
    }
}
