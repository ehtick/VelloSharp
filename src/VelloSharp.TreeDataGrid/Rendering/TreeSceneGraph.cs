using System;
using VelloSharp.Composition;
using VelloSharp.TreeDataGrid.Composition;

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

    public void EncodeRow(uint nodeId, in TreeRowVisual visual, ReadOnlySpan<TreeColumnSpan> columns)
    {
        ThrowIfDisposed();
        Span<NativeMethods.VelloTdgColumnPlan> buffer = columns.Length <= 16
            ? stackalloc NativeMethods.VelloTdgColumnPlan[16]
            : new NativeMethods.VelloTdgColumnPlan[columns.Length];
        var span = buffer[..columns.Length];
        FillColumnPlans(columns, span);

        var nativeVisual = visual.ToNative();
        bool added = false;
        try
        {
            _sceneCache.DangerousAddRef(ref added);
            var handle = _sceneCache.DangerousGetHandle();
            unsafe
            {
                fixed (NativeMethods.VelloTdgColumnPlan* plansPtr = span)
                {
                    var visualCopy = nativeVisual;
                    TreeInterop.ThrowIfFalse(
                        NativeMethods.vello_tdg_scene_encode_row(
                            handle,
                            nodeId,
                            &visualCopy,
                            plansPtr,
                            (nuint)span.Length),
                        $"Failed to encode row scene for node {nodeId}");
                }
            }
        }
        finally
        {
            if (added)
            {
                _sceneCache.DangerousRelease();
            }
        }
    }

    public void EncodeGroupHeader(uint nodeId, in TreeGroupHeaderVisual visual, ReadOnlySpan<TreeColumnSpan> columns)
    {
        ThrowIfDisposed();
        Span<NativeMethods.VelloTdgColumnPlan> buffer = columns.Length <= 16
            ? stackalloc NativeMethods.VelloTdgColumnPlan[16]
            : new NativeMethods.VelloTdgColumnPlan[columns.Length];
        var span = buffer[..columns.Length];
        FillColumnPlans(columns, span);

        var nativeVisual = visual.ToNative();
        bool added = false;
        try
        {
            _sceneCache.DangerousAddRef(ref added);
            var handle = _sceneCache.DangerousGetHandle();
            unsafe
            {
                fixed (NativeMethods.VelloTdgColumnPlan* plansPtr = span)
                {
                    var visualCopy = nativeVisual;
                    TreeInterop.ThrowIfFalse(
                        NativeMethods.vello_tdg_scene_encode_group_header(
                            handle,
                            nodeId,
                            &visualCopy,
                            plansPtr,
                            (nuint)span.Length),
                        $"Failed to encode group header scene for node {nodeId}");
                }
            }
        }
        finally
        {
            if (added)
            {
                _sceneCache.DangerousRelease();
            }
        }
    }

    public void EncodeSummary(uint nodeId, in TreeSummaryVisual visual, ReadOnlySpan<TreeColumnSpan> columns)
    {
        ThrowIfDisposed();
        Span<NativeMethods.VelloTdgColumnPlan> buffer = columns.Length <= 16
            ? stackalloc NativeMethods.VelloTdgColumnPlan[16]
            : new NativeMethods.VelloTdgColumnPlan[columns.Length];
        var span = buffer[..columns.Length];
        FillColumnPlans(columns, span);

        var nativeVisual = visual.ToNative();
        bool added = false;
        try
        {
            _sceneCache.DangerousAddRef(ref added);
            var handle = _sceneCache.DangerousGetHandle();
            unsafe
            {
                fixed (NativeMethods.VelloTdgColumnPlan* plansPtr = span)
                {
                    var visualCopy = nativeVisual;
                    TreeInterop.ThrowIfFalse(
                        NativeMethods.vello_tdg_scene_encode_summary(
                            handle,
                            nodeId,
                            &visualCopy,
                            plansPtr,
                            (nuint)span.Length),
                        $"Failed to encode summary scene for node {nodeId}");
                }
            }
        }
        finally
        {
            if (added)
            {
                _sceneCache.DangerousRelease();
            }
        }
    }

    public void EncodeChrome(uint nodeId, in TreeChromeVisual visual, ReadOnlySpan<TreeColumnSpan> columns)
    {
        ThrowIfDisposed();
        Span<NativeMethods.VelloTdgColumnPlan> buffer = columns.Length <= 16
            ? stackalloc NativeMethods.VelloTdgColumnPlan[16]
            : new NativeMethods.VelloTdgColumnPlan[columns.Length];
        var span = buffer[..columns.Length];
        FillColumnPlans(columns, span);

        var nativeVisual = visual.ToNative();
        bool added = false;
        try
        {
            _sceneCache.DangerousAddRef(ref added);
            var handle = _sceneCache.DangerousGetHandle();
            unsafe
            {
                fixed (NativeMethods.VelloTdgColumnPlan* plansPtr = span)
                {
                    var visualCopy = nativeVisual;
                    TreeInterop.ThrowIfFalse(
                        NativeMethods.vello_tdg_scene_encode_chrome(
                            handle,
                            nodeId,
                            &visualCopy,
                            plansPtr,
                            (nuint)span.Length),
                        $"Failed to encode chrome scene for node {nodeId}");
                }
            }
        }
        finally
        {
            if (added)
            {
                _sceneCache.DangerousRelease();
            }
        }
    }

    public bool EncodeChromeIfChanged(
        uint nodeId,
        in TreeChromeVisual visual,
        ReadOnlySpan<TreeColumnSpan> columns,
        in TreeColumnPaneDiff paneDiff)
    {
        if (!paneDiff.LeadingChanged && !paneDiff.TrailingChanged)
        {
            return false;
        }

        EncodeChrome(nodeId, visual, columns);
        return true;
    }

    private static void FillColumnPlans(
        ReadOnlySpan<TreeColumnSpan> columns,
        Span<NativeMethods.VelloTdgColumnPlan> target)
    {
        for (var i = 0; i < columns.Length; i++)
        {
            target[i] = new NativeMethods.VelloTdgColumnPlan
            {
                Offset = columns[i].Offset,
                Width = columns[i].Width,
                Frozen = columns[i].Frozen switch
                {
                    TreeFrozenKind.Leading => NativeMethods.VelloTdgFrozenKind.Leading,
                    TreeFrozenKind.Trailing => NativeMethods.VelloTdgFrozenKind.Trailing,
                    _ => NativeMethods.VelloTdgFrozenKind.None,
                },
            };
        }
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
