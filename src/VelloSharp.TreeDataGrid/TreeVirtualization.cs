using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using VelloSharp.TreeDataGrid.Composition;

namespace VelloSharp.TreeDataGrid;

public enum TreeFrozenKind
{
    None = 0,
    Leading = 1,
    Trailing = 2,
}

public enum TreeRowAction
{
    Reuse = 0,
    Adopt = 1,
    Allocate = 2,
    Recycle = 3,
}

public readonly record struct TreeRowMetric(uint NodeId, float Height);

public readonly record struct TreeColumnMetric(double Offset, double Width, TreeFrozenKind Frozen);

public readonly record struct TreeViewportMetrics(
    double RowScrollOffset,
    double RowViewportHeight,
    double RowOverscan,
    double ColumnScrollOffset,
    double ColumnViewportWidth,
    double ColumnOverscan);

public readonly record struct TreeRowPlanEntry(
    uint NodeId,
    uint BufferId,
    double Top,
    float Height,
    TreeRowAction Action);

public readonly record struct TreeColumnSlice(
    uint PrimaryStart,
    uint PrimaryCount,
    uint FrozenLeading,
    uint FrozenTrailing);

public readonly record struct TreeRowWindow(uint StartIndex, uint EndIndex, double TotalHeight);

public readonly record struct TreeVirtualizationPlan(
    IReadOnlyList<TreeRowPlanEntry> ActiveRows,
    IReadOnlyList<TreeRowPlanEntry> RecycledRows,
    TreeColumnSlice ColumnSlice,
    TreeRowWindow RowWindow,
    TreeColumnPaneDiff PaneDiff);

public readonly record struct TreeVirtualizationTelemetry(
    uint RowsTotal,
    uint WindowLength,
    uint Reused,
    uint Adopted,
    uint Allocated,
    uint Recycled,
    uint ActiveBuffers,
    uint FreeBuffers,
    uint Evicted);

internal sealed class TreeVirtualizerHandle : SafeHandle
{
    private TreeVirtualizerHandle()
        : base(IntPtr.Zero, ownsHandle: true)
    {
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.vello_tdg_virtualizer_destroy(handle);
        SetHandle(IntPtr.Zero);
        return true;
    }

    public static TreeVirtualizerHandle Create()
    {
        var ptr = NativeMethods.vello_tdg_virtualizer_create();
        if (ptr == nint.Zero)
        {
            throw TreeInterop.CreateException("Failed to create virtualization scheduler");
        }

        var handle = new TreeVirtualizerHandle();
        handle.SetHandle(ptr);
        return handle;
    }
}

public sealed class TreeVirtualizationScheduler : IDisposable
{
    private const int StackThreshold = 32;
    private readonly TreeVirtualizerHandle _handle;
    private readonly List<TreeRowPlanEntry> _rowPlan = new();
    private readonly List<TreeRowPlanEntry> _recyclePlan = new();
    private readonly TreeColumnStripCache _columnCache = new();
    private TreeColumnSlice _columnSlice;
    private TreeRowWindow _rowWindow;
    private TreeColumnPaneDiff _pendingPaneDiff;

    public TreeVirtualizationScheduler()
    {
        _handle = TreeVirtualizerHandle.Create();
    }

    public TreeColumnStripSnapshot UpdateColumns(
        ReadOnlySpan<TreeColumnDefinition> definitions,
        ReadOnlySpan<TreeColumnSlot> slots)
    {
        var snapshot = _columnCache.Update(definitions, slots);
        SetColumns(snapshot.Metrics.Span);
        _pendingPaneDiff = _pendingPaneDiff.Union(snapshot.PaneDiff);
        return snapshot;
    }

    public unsafe void SetRows(ReadOnlySpan<TreeRowMetric> metrics)
    {
        NativeMethods.VelloTdgRowMetric[]? rented = null;
        Span<NativeMethods.VelloTdgRowMetric> buffer = metrics.Length <= StackThreshold
            ? stackalloc NativeMethods.VelloTdgRowMetric[StackThreshold]
            : rented = ArrayPool<NativeMethods.VelloTdgRowMetric>.Shared.Rent(metrics.Length);
        var span = buffer[..metrics.Length];

        for (var i = 0; i < metrics.Length; i++)
        {
            span[i] = new NativeMethods.VelloTdgRowMetric
            {
                NodeId = metrics[i].NodeId,
                Height = metrics[i].Height,
            };
        }

        try
        {
            unsafe
            {
                fixed (NativeMethods.VelloTdgRowMetric* ptr = span)
                {
                    bool added = false;
                    try
                    {
                        _handle.DangerousAddRef(ref added);
                        var handle = _handle.DangerousGetHandle();
                        TreeInterop.ThrowIfFalse(
                            NativeMethods.vello_tdg_virtualizer_set_rows(handle, ptr, (nuint)metrics.Length),
                            "Failed to update virtualization rows");
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
                ArrayPool<NativeMethods.VelloTdgRowMetric>.Shared.Return(rented);
            }
        }
    }

    public unsafe void SetColumns(ReadOnlySpan<TreeColumnMetric> columns)
    {
        NativeMethods.VelloTdgColumnMetric[]? rented = null;
        Span<NativeMethods.VelloTdgColumnMetric> buffer = columns.Length <= StackThreshold
            ? stackalloc NativeMethods.VelloTdgColumnMetric[StackThreshold]
            : rented = ArrayPool<NativeMethods.VelloTdgColumnMetric>.Shared.Rent(columns.Length);
        var span = buffer[..columns.Length];

        for (var i = 0; i < columns.Length; i++)
        {
            span[i] = new NativeMethods.VelloTdgColumnMetric
            {
                Offset = columns[i].Offset,
                Width = columns[i].Width,
                Frozen = ToNative(columns[i].Frozen),
            };
        }

        try
        {
            unsafe
            {
                fixed (NativeMethods.VelloTdgColumnMetric* ptr = span)
                {
                    bool added = false;
                    try
                    {
                        _handle.DangerousAddRef(ref added);
                        var handle = _handle.DangerousGetHandle();
                        TreeInterop.ThrowIfFalse(
                            NativeMethods.vello_tdg_virtualizer_set_columns(handle, ptr, (nuint)columns.Length),
                            "Failed to update virtualization columns");
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
                ArrayPool<NativeMethods.VelloTdgColumnMetric>.Shared.Return(rented);
            }
        }
    }

    public unsafe TreeVirtualizationPlan Plan(in TreeViewportMetrics metrics)
    {
        NativeMethods.VelloTdgColumnSlice slice;
        bool added = false;
        try
        {
            _handle.DangerousAddRef(ref added);
            var handle = _handle.DangerousGetHandle();
            unsafe
            {
                var nativeMetrics = new NativeMethods.VelloTdgViewportMetrics
                {
                    RowScrollOffset = metrics.RowScrollOffset,
                    RowViewportHeight = metrics.RowViewportHeight,
                    RowOverscan = metrics.RowOverscan,
                    ColumnScrollOffset = metrics.ColumnScrollOffset,
                    ColumnViewportWidth = metrics.ColumnViewportWidth,
                    ColumnOverscan = metrics.ColumnOverscan,
                };

                TreeInterop.ThrowIfFalse(
                    NativeMethods.vello_tdg_virtualizer_plan(handle, nativeMetrics, &slice),
                    "Virtualization plan failed");
            }
        }
        finally
        {
            if (added)
            {
                _handle.DangerousRelease();
            }
        }

        _columnSlice = new TreeColumnSlice(
            slice.PrimaryStart,
            slice.PrimaryCount,
            slice.FrozenLeading,
            slice.FrozenTrailing);
        var paneDiff = _pendingPaneDiff;
        _pendingPaneDiff = default;

        PopulatePlanLists();
        PopulateRecycleList();
        PopulateRowWindow();

        return new TreeVirtualizationPlan(
            _rowPlan.ToArray(),
            _recyclePlan.ToArray(),
            _columnSlice,
            _rowWindow,
            paneDiff);
    }

    public void Clear()
    {
        _rowPlan.Clear();
        _recyclePlan.Clear();
        _columnSlice = default;
        _rowWindow = default;
        _pendingPaneDiff = default;

        bool added = false;
        try
        {
            _handle.DangerousAddRef(ref added);
            var handle = _handle.DangerousGetHandle();
            NativeMethods.vello_tdg_virtualizer_clear(handle);
        }
        finally
        {
            if (added)
            {
                _handle.DangerousRelease();
            }
        }
    }

    public TreeVirtualizationTelemetry GetTelemetry()
    {
        bool added = false;
        try
        {
            _handle.DangerousAddRef(ref added);
            var handle = _handle.DangerousGetHandle();
            unsafe
            {
                NativeMethods.VelloTdgVirtualizerTelemetry telemetry;
                TreeInterop.ThrowIfFalse(
                    NativeMethods.vello_tdg_virtualizer_telemetry(handle, &telemetry),
                    "Failed to query virtualizer telemetry");
                return new TreeVirtualizationTelemetry(
                    telemetry.RowsTotal,
                    telemetry.WindowLen,
                    telemetry.Reused,
                    telemetry.Adopted,
                    telemetry.Allocated,
                    telemetry.Recycled,
                    telemetry.ActiveBuffers,
                    telemetry.FreeBuffers,
                    telemetry.Evicted);
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

    private unsafe void PopulatePlanLists()
    {
        bool added = false;
        try
        {
            _handle.DangerousAddRef(ref added);
            var handle = _handle.DangerousGetHandle();
            var count = (int)NativeMethods.vello_tdg_virtualizer_copy_plan(handle, null, 0);
            _rowPlan.Clear();
            if (count == 0)
            {
                return;
            }

            var rented = ArrayPool<NativeMethods.VelloTdgRowPlanEntry>.Shared.Rent(count);
            try
            {
                var span = rented.AsSpan(0, count);
                unsafe
                {
                    fixed (NativeMethods.VelloTdgRowPlanEntry* ptr = span)
                    {
                        NativeMethods.vello_tdg_virtualizer_copy_plan(handle, ptr, (nuint)count);
                    }
                }

                for (var i = 0; i < count; i++)
                {
                    _rowPlan.Add(Convert(span[i]));
                }
            }
            finally
            {
                ArrayPool<NativeMethods.VelloTdgRowPlanEntry>.Shared.Return(rented);
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

    private unsafe void PopulateRecycleList()
    {
        bool added = false;
        try
        {
            _handle.DangerousAddRef(ref added);
            var handle = _handle.DangerousGetHandle();
            var count = (int)NativeMethods.vello_tdg_virtualizer_copy_recycle(handle, null, 0);
            _recyclePlan.Clear();
            if (count == 0)
            {
                return;
            }

            var rented = ArrayPool<NativeMethods.VelloTdgRowPlanEntry>.Shared.Rent(count);
            try
            {
                var span = rented.AsSpan(0, count);
                unsafe
                {
                    fixed (NativeMethods.VelloTdgRowPlanEntry* ptr = span)
                    {
                        NativeMethods.vello_tdg_virtualizer_copy_recycle(handle, ptr, (nuint)count);
                    }
                }

                for (var i = 0; i < count; i++)
                {
                    _recyclePlan.Add(Convert(span[i]));
                }
            }
            finally
            {
                ArrayPool<NativeMethods.VelloTdgRowPlanEntry>.Shared.Return(rented);
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

    private unsafe void PopulateRowWindow()
    {
        bool added = false;
        try
        {
            _handle.DangerousAddRef(ref added);
            var handle = _handle.DangerousGetHandle();
            unsafe
            {
                NativeMethods.VelloTdgRowWindow window;
                if (NativeMethods.vello_tdg_virtualizer_window(handle, &window))
                {
                    _rowWindow = new TreeRowWindow(window.StartIndex, window.EndIndex, window.TotalHeight);
                }
                else
                {
                    _rowWindow = default;
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
    }

    public void Dispose()
    {
        _handle.Dispose();
        GC.SuppressFinalize(this);
    }

    private static TreeRowAction FromNative(NativeMethods.VelloTdgRowAction action)
        => action switch
        {
            NativeMethods.VelloTdgRowAction.Reuse => TreeRowAction.Reuse,
            NativeMethods.VelloTdgRowAction.Adopt => TreeRowAction.Adopt,
            NativeMethods.VelloTdgRowAction.Allocate => TreeRowAction.Allocate,
            NativeMethods.VelloTdgRowAction.Recycle => TreeRowAction.Recycle,
            _ => TreeRowAction.Reuse,
        };

    private static NativeMethods.VelloTdgFrozenKind ToNative(TreeFrozenKind kind)
        => kind switch
        {
            TreeFrozenKind.Leading => NativeMethods.VelloTdgFrozenKind.Leading,
            TreeFrozenKind.Trailing => NativeMethods.VelloTdgFrozenKind.Trailing,
            _ => NativeMethods.VelloTdgFrozenKind.None,
        };

    private static TreeRowPlanEntry Convert(NativeMethods.VelloTdgRowPlanEntry entry)
        => new(entry.NodeId, entry.BufferId, entry.Top, entry.Height, FromNative(entry.Action));



}







