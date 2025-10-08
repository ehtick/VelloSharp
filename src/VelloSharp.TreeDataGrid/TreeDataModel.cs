using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VelloSharp.TreeDataGrid;

public enum TreeRowKind
{
    Data = 0,
    GroupHeader = 1,
    Summary = 2,
}

public enum TreeModelDiffKind
{
    Inserted = 0,
    Removed = 1,
    Expanded = 2,
    Collapsed = 3,
}

public enum TreeSelectionMode
{
    Replace = 0,
    Add = 1,
    Toggle = 2,
    Range = 3,
}

public readonly record struct TreeNodeDescriptor(
    ulong Key,
    TreeRowKind RowKind,
    float Height,
    bool HasChildren);

public readonly record struct TreeModelDiff(
    uint NodeId,
    uint? ParentId,
    uint Index,
    uint Depth,
    TreeRowKind RowKind,
    TreeModelDiffKind Kind,
    float Height,
    bool HasChildren,
    bool IsExpanded,
    ulong Key);

public readonly record struct TreeSelectionDiff(uint NodeId, bool IsSelected);

public readonly record struct TreeNodeMetadata(
    ulong Key,
    uint Depth,
    float Height,
    TreeRowKind RowKind,
    bool IsExpanded,
    bool IsSelected,
    bool HasChildren);

internal sealed class TreeDataModelHandle : SafeHandle
{
    private TreeDataModelHandle()
        : base(IntPtr.Zero, ownsHandle: true)
    {
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.vello_tdg_model_destroy(handle);
        SetHandle(IntPtr.Zero);
        return true;
    }

    public static TreeDataModelHandle Create()
    {
        var ptr = NativeMethods.vello_tdg_model_create();
        if (ptr == nint.Zero)
        {
            throw TreeInterop.CreateException("Failed to create tree data model");
        }

        var handle = new TreeDataModelHandle();
        handle.SetHandle(ptr);
        return handle;
    }
}

public sealed class TreeDataModel : IDisposable
{
    private const int StackThreshold = 16;
    private readonly TreeDataModelHandle _handle;

    public TreeDataModel()
    {
        _handle = TreeDataModelHandle.Create();
    }

    public void AttachRoots(ReadOnlySpan<TreeNodeDescriptor> descriptors)
    {
        AttachChildrenInternal(nint.Zero, descriptors, isRoot: true);
    }

    public void AttachChildren(uint parentNodeId, ReadOnlySpan<TreeNodeDescriptor> descriptors)
    {
        AttachChildrenInternal((nint)parentNodeId, descriptors, isRoot: false);
    }

    public void SetExpanded(uint nodeId, bool expanded)
    {
        Invoke(handle =>
        {
            TreeInterop.ThrowIfFalse(
                NativeMethods.vello_tdg_model_set_expanded(handle, nodeId, expanded ? 1u : 0u),
                $"Failed to set expanded state for node {nodeId}");
        });
    }

    public void SetSelected(uint nodeId, TreeSelectionMode mode)
    {
        var nativeMode = mode switch
        {
            TreeSelectionMode.Replace => NativeMethods.VelloTdgSelectionMode.Replace,
            TreeSelectionMode.Add => NativeMethods.VelloTdgSelectionMode.Add,
            TreeSelectionMode.Toggle => NativeMethods.VelloTdgSelectionMode.Toggle,
            TreeSelectionMode.Range => NativeMethods.VelloTdgSelectionMode.Range,
            _ => NativeMethods.VelloTdgSelectionMode.Replace,
        };

        Invoke(handle =>
        {
            TreeInterop.ThrowIfFalse(
                NativeMethods.vello_tdg_model_set_selected(handle, nodeId, nativeMode),
                $"Failed to update selection state for node {nodeId}");
        });
    }

    public void SelectRange(uint anchorNodeId, uint focusNodeId)
    {
        Invoke(handle =>
        {
            TreeInterop.ThrowIfFalse(
                NativeMethods.vello_tdg_model_select_range(handle, anchorNodeId, focusNodeId),
                "Failed to apply range selection");
        });
    }

    public IReadOnlyList<TreeModelDiff> DrainModelDiffs()
    {
        return Invoke(handle =>
        {
            var count = (int)NativeMethods.vello_tdg_model_diff_count(handle);
            if (count == 0)
            {
                return Array.Empty<TreeModelDiff>();
            }

            var rented = ArrayPool<NativeMethods.VelloTdgModelDiff>.Shared.Rent(count);
            try
            {
                var span = rented.AsSpan(0, count);
                unsafe
                {
                    fixed (NativeMethods.VelloTdgModelDiff* ptr = span)
                    {
                        NativeMethods.vello_tdg_model_copy_diffs(
                            handle,
                            ptr,
                            (nuint)count);
                    }
                }

                var results = new TreeModelDiff[count];
                for (var i = 0; i < count; i++)
                {
                    results[i] = Convert(in span[i]);
                }
                return results;
            }
            finally
            {
                ArrayPool<NativeMethods.VelloTdgModelDiff>.Shared.Return(rented);
            }
        });
    }

    public IReadOnlyList<TreeSelectionDiff> DrainSelectionDiffs()
    {
        return Invoke(handle =>
        {
            var count = (int)NativeMethods.vello_tdg_model_selection_diff_count(handle);
            if (count == 0)
            {
                return Array.Empty<TreeSelectionDiff>();
            }

            var rented = ArrayPool<NativeMethods.VelloTdgSelectionDiff>.Shared.Rent(count);
            try
            {
                var span = rented.AsSpan(0, count);
                unsafe
                {
                    fixed (NativeMethods.VelloTdgSelectionDiff* ptr = span)
                    {
                        NativeMethods.vello_tdg_model_copy_selection_diffs(
                            handle,
                            ptr,
                            (nuint)count);
                    }
                }

                var results = new TreeSelectionDiff[count];
                for (var i = 0; i < count; i++)
                {
                    results[i] = new TreeSelectionDiff(span[i].NodeId, span[i].IsSelected != 0);
                }
                return results;
            }
            finally
            {
                ArrayPool<NativeMethods.VelloTdgSelectionDiff>.Shared.Return(rented);
            }
        });
    }

    public bool TryDequeueMaterialization(out uint nodeId)
    {
        bool added = false;
        try
        {
            _handle.DangerousAddRef(ref added);
            var handle = _handle.DangerousGetHandle();
            unsafe
            {
                uint* idPtr = stackalloc uint[1];
                if (!NativeMethods.vello_tdg_model_dequeue_materialization(handle, idPtr))
                {
                    nodeId = 0;
                    return false;
                }

                nodeId = idPtr[0];
                return true;
            }
        }
        finally
        {
            if (added)
            {
                _handle.DangerousRelease();
            }
        }
    }

    public TreeNodeMetadata GetMetadata(uint nodeId)
    {
        return Invoke(handle =>
        {
            unsafe
            {
                NativeMethods.VelloTdgNodeMetadata metadata;
                if (!NativeMethods.vello_tdg_model_node_metadata(handle, nodeId, &metadata))
                {
                    throw TreeInterop.CreateException($"No metadata available for node {nodeId}");
                }

                return new TreeNodeMetadata(
                    metadata.Key,
                    metadata.Depth,
                    metadata.Height,
                    FromNative(metadata.RowKind),
                    metadata.IsExpanded != 0,
                    metadata.IsSelected != 0,
                    metadata.HasChildren != 0);
            }
        });
    }

    public void Clear()
    {
        Invoke(handle => NativeMethods.vello_tdg_model_clear(handle));
    }

    private void AttachChildrenInternal(
        nint parentId,
        ReadOnlySpan<TreeNodeDescriptor> descriptors,
        bool isRoot)
    {
        var count = descriptors.Length;
        if (count == 0)
        {
            bool added = false;
            try
            {
                _handle.DangerousAddRef(ref added);
                var handle = _handle.DangerousGetHandle();
                unsafe
                {
                    if (isRoot)
                    {
                        TreeInterop.ThrowIfFalse(
                            NativeMethods.vello_tdg_model_attach_roots(handle, null, 0),
                            "Failed to attach root nodes");
                    }
                    else
                    {
                        TreeInterop.ThrowIfFalse(
                            NativeMethods.vello_tdg_model_attach_children(handle, (uint)parentId, null, 0),
                            $"Failed to attach children for node {parentId}");
                    }
                }
            }
            finally
            {
                if (added)
                {
                    _handle.DangerousRelease();
                }
            }
            return;
        }

        NativeMethods.VelloTdgNodeDescriptor[]? rented = null;
        Span<NativeMethods.VelloTdgNodeDescriptor> buffer = count <= StackThreshold
            ? stackalloc NativeMethods.VelloTdgNodeDescriptor[StackThreshold]
            : rented = ArrayPool<NativeMethods.VelloTdgNodeDescriptor>.Shared.Rent(count);
        var span = buffer[..count];

        for (var i = 0; i < count; i++)
        {
            ref readonly var descriptor = ref descriptors[i];
            span[i] = new NativeMethods.VelloTdgNodeDescriptor
            {
                Key = descriptor.Key,
                RowKind = ToNative(descriptor.RowKind),
                Height = descriptor.Height,
                HasChildren = descriptor.HasChildren ? 1u : 0u,
            };
        }

        try
        {
            unsafe
            {
                fixed (NativeMethods.VelloTdgNodeDescriptor* ptr = span)
                {
                    bool added = false;
                    try
                    {
                        _handle.DangerousAddRef(ref added);
                        var handle = _handle.DangerousGetHandle();
                        if (isRoot)
                        {
                            TreeInterop.ThrowIfFalse(
                                NativeMethods.vello_tdg_model_attach_roots(handle, ptr, (nuint)count),
                                "Failed to attach root nodes");
                        }
                        else
                        {
                            TreeInterop.ThrowIfFalse(
                                NativeMethods.vello_tdg_model_attach_children(
                                    handle,
                                    (uint)parentId,
                                    ptr,
                                    (nuint)count),
                                $"Failed to attach children for node {parentId}");
                        }
                    }
                    finally
                    {
                        if (added)
                        {
                            _handle.DangerousRelease();
                        }
                    }
                }
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<NativeMethods.VelloTdgNodeDescriptor>.Shared.Return(rented);
            }
        }
    }

    private T Invoke<T>(Func<nint, T> action)
    {
        bool added = false;
        try
        {
            _handle.DangerousAddRef(ref added);
            return action(_handle.DangerousGetHandle());
        }
        finally
        {
            if (added)
            {
                _handle.DangerousRelease();
            }
        }
    }

    private void Invoke(Action<nint> action)
    {
        bool added = false;
        try
        {
            _handle.DangerousAddRef(ref added);
            action(_handle.DangerousGetHandle());
        }
        finally
        {
            if (added)
            {
                _handle.DangerousRelease();
            }
        }
    }

    public void Dispose()
    {
        _handle.Dispose();
        GC.SuppressFinalize(this);
    }

    private static TreeModelDiff Convert(in NativeMethods.VelloTdgModelDiff diff)
    {
        uint? parent = diff.ParentId == uint.MaxValue ? null : diff.ParentId;
        return new TreeModelDiff(
            diff.NodeId,
            parent,
            diff.Index,
            diff.Depth,
            FromNative(diff.RowKind),
            FromNative(diff.Kind),
            diff.Height,
            diff.HasChildren != 0,
            diff.IsExpanded != 0,
            diff.Key);
    }

    private static TreeRowKind FromNative(NativeMethods.VelloTdgRowKind value)
        => value switch
        {
            NativeMethods.VelloTdgRowKind.GroupHeader => TreeRowKind.GroupHeader,
            NativeMethods.VelloTdgRowKind.Summary => TreeRowKind.Summary,
            _ => TreeRowKind.Data,
        };

    private static TreeModelDiffKind FromNative(NativeMethods.VelloTdgModelDiffKind value)
        => value switch
        {
            NativeMethods.VelloTdgModelDiffKind.Removed => TreeModelDiffKind.Removed,
            NativeMethods.VelloTdgModelDiffKind.Expanded => TreeModelDiffKind.Expanded,
            NativeMethods.VelloTdgModelDiffKind.Collapsed => TreeModelDiffKind.Collapsed,
            _ => TreeModelDiffKind.Inserted,
        };

    private static NativeMethods.VelloTdgRowKind ToNative(TreeRowKind value)
        => value switch
        {
            TreeRowKind.GroupHeader => NativeMethods.VelloTdgRowKind.GroupHeader,
            TreeRowKind.Summary => NativeMethods.VelloTdgRowKind.Summary,
            _ => NativeMethods.VelloTdgRowKind.Data,
        };
}



