using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace VelloSharp.Composition;

public static class CompositionInterop
{
    private const int StackAllocThreshold = 256;
    private const int LinearLayoutStackThreshold = 8;
    private const int LayoutSpanStackThreshold = 16;

    public static PlotArea ComputePlotArea(double width, double height)
    {
        if (!NativeMethods.vello_composition_compute_plot_area(width, height, out var area))
        {
            throw new InvalidOperationException("vello_composition_compute_plot_area failed.");
        }

        return new PlotArea(area.Left, area.Top, area.Width, area.Height);
    }

    public static LabelMetrics MeasureLabel(string text, float fontSize = 14f)
    {
        ArgumentNullException.ThrowIfNull(text);
        return MeasureLabel(text.AsSpan(), fontSize);
    }

    public static LabelMetrics MeasureLabel(ReadOnlySpan<char> text, float fontSize = 14f)
    {
        if (text.IsEmpty)
        {
            return default;
        }

        int requiredLength = Encoding.UTF8.GetByteCount(text);
        if (requiredLength == 0)
        {
            return default;
        }

        if (requiredLength <= StackAllocThreshold)
        {
            Span<byte> buffer = stackalloc byte[StackAllocThreshold];
            Span<byte> utf8 = buffer[..requiredLength];
            Encoding.UTF8.GetBytes(text, utf8);
            return MeasureLabelCore(utf8, fontSize);
        }

        byte[] rented = ArrayPool<byte>.Shared.Rent(requiredLength);
        try
        {
            Span<byte> utf8 = rented.AsSpan(0, requiredLength);
            Encoding.UTF8.GetBytes(text, utf8);
            return MeasureLabelCore(utf8, fontSize);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static LabelMetrics MeasureLabelCore(Span<byte> utf8, float fontSize)
    {
        if (utf8.IsEmpty)
        {
            return default;
        }

        unsafe
        {
            fixed (byte* ptr = utf8)
            {
                if (!NativeMethods.vello_composition_measure_label(
                        ptr,
                        (nuint)utf8.Length,
                        fontSize,
                        out var metrics))
                {
                    return default;
                }

                return new LabelMetrics(metrics.Width, metrics.Height, metrics.Ascent);
            }
        }
    }

    public static int SolveLinearLayout(
        ReadOnlySpan<LinearLayoutChild> children,
        double available,
        double spacing,
        Span<LinearLayoutResult> results)
    {
        if (children.Length == 0)
        {
            return 0;
        }

        if (results.Length < children.Length)
        {
            throw new ArgumentException("Result span is too small.", nameof(results));
        }

        int count = children.Length;
        Span<VelloCompositionLinearLayoutItem> nativeItems = count <= LinearLayoutStackThreshold
            ? stackalloc VelloCompositionLinearLayoutItem[LinearLayoutStackThreshold]
            : new VelloCompositionLinearLayoutItem[count];
        nativeItems = nativeItems[..count];

        Span<VelloCompositionLinearLayoutSlot> nativeSlots = count <= LinearLayoutStackThreshold
            ? stackalloc VelloCompositionLinearLayoutSlot[LinearLayoutStackThreshold]
            : new VelloCompositionLinearLayoutSlot[count];
        nativeSlots = nativeSlots[..count];

        for (int i = 0; i < count; i++)
        {
            var child = children[i];
            nativeItems[i] = new VelloCompositionLinearLayoutItem
            {
                Min = child.Min,
                Preferred = child.Preferred,
                Max = child.Max,
                Weight = child.Weight,
                MarginLeading = child.MarginLeading,
                MarginTrailing = child.MarginTrailing,
            };
        }

        nuint solved;
        unsafe
        {
            fixed (VelloCompositionLinearLayoutItem* itemsPtr = nativeItems)
            fixed (VelloCompositionLinearLayoutSlot* slotsPtr = nativeSlots)
            {
                solved = NativeMethods.vello_composition_solve_linear_layout(
                    itemsPtr,
                    (nuint)count,
                    available,
                    spacing,
                    slotsPtr,
                    (nuint)count);
            }
        }

        if (solved == 0 || solved != (nuint)count)
        {
            return 0;
        }

        for (int i = 0; i < count; i++)
        {
            var slot = nativeSlots[i];
            results[i] = new LinearLayoutResult(slot.Offset, slot.Length);
        }

        return count;
    }

    public readonly record struct LinearLayoutChild(
        double Min,
        double Preferred,
        double Max,
        double Weight = 1.0,
        double MarginLeading = 0.0,
        double MarginTrailing = 0.0);

    public readonly record struct LinearLayoutResult(double Offset, double Length);

    private static VelloCompositionScalarConstraint ToNative(in ScalarConstraint constraint) => new()
    {
        Min = constraint.Min,
        Preferred = constraint.Preferred,
        Max = constraint.Max,
    };

    private static VelloCompositionLayoutConstraints ToNative(in LayoutConstraints constraints) => new()
    {
        Width = ToNative(constraints.Width),
        Height = ToNative(constraints.Height),
    };

    private static VelloCompositionLayoutThickness ToNative(in LayoutThickness thickness) => new()
    {
        Left = thickness.Left,
        Top = thickness.Top,
        Right = thickness.Right,
        Bottom = thickness.Bottom,
    };

    private static VelloCompositionLayoutAlignment ToNative(LayoutAlignment alignment) =>
        alignment switch
        {
            LayoutAlignment.Start => VelloCompositionLayoutAlignment.Start,
            LayoutAlignment.Center => VelloCompositionLayoutAlignment.Center,
            LayoutAlignment.End => VelloCompositionLayoutAlignment.End,
            LayoutAlignment.Stretch => VelloCompositionLayoutAlignment.Stretch,
            _ => VelloCompositionLayoutAlignment.Stretch,
        };

    private static VelloCompositionLayoutOrientation ToNative(LayoutOrientation orientation) =>
        orientation switch
        {
            LayoutOrientation.Horizontal => VelloCompositionLayoutOrientation.Horizontal,
            LayoutOrientation.Vertical => VelloCompositionLayoutOrientation.Vertical,
            _ => VelloCompositionLayoutOrientation.Vertical,
        };

    private static VelloCompositionStackLayoutChild ToNative(in StackLayoutChild child) => new()
    {
        Constraints = ToNative(child.Constraints),
        Weight = child.Weight,
        Margin = ToNative(child.Margin),
        CrossAlignment = ToNative(child.CrossAlignment),
    };

    private static VelloCompositionStackLayoutOptions ToNative(in StackLayoutOptions options) => new()
    {
        Orientation = ToNative(options.Orientation),
        Spacing = options.Spacing,
        Padding = ToNative(options.Padding),
        CrossAlignment = ToNative(options.CrossAlignment),
    };

    private static VelloCompositionWrapLayoutChild ToNative(in WrapLayoutChild child) => new()
    {
        Constraints = ToNative(child.Constraints),
        Margin = ToNative(child.Margin),
        LineBreak = child.LineBreak ? 1u : 0u,
    };

    private static VelloCompositionWrapLayoutOptions ToNative(in WrapLayoutOptions options) => new()
    {
        Orientation = ToNative(options.Orientation),
        ItemSpacing = options.ItemSpacing,
        LineSpacing = options.LineSpacing,
        Padding = ToNative(options.Padding),
        LineAlignment = ToNative(options.LineAlignment),
        CrossAlignment = ToNative(options.CrossAlignment),
    };

    private static VelloCompositionGridTrack ToNative(in GridTrack track) => new()
    {
        Kind = track.Kind switch
        {
            GridTrackKind.Fixed => VelloCompositionGridTrackKind.Fixed,
            GridTrackKind.Auto => VelloCompositionGridTrackKind.Auto,
            GridTrackKind.Star => VelloCompositionGridTrackKind.Star,
            _ => VelloCompositionGridTrackKind.Auto,
        },
        Value = track.Value,
        Min = track.Min,
        Max = double.IsPositiveInfinity(track.Max) ? double.PositiveInfinity : track.Max,
    };

    private static VelloCompositionGridLayoutChild ToNative(in GridLayoutChild child) => new()
    {
        Constraints = ToNative(child.Constraints),
        Column = child.Column,
        ColumnSpan = child.ColumnSpan,
        Row = child.Row,
        RowSpan = child.RowSpan,
        Margin = ToNative(child.Margin),
        HorizontalAlignment = ToNative(child.HorizontalAlignment),
        VerticalAlignment = ToNative(child.VerticalAlignment),
    };

    private static VelloCompositionGridLayoutOptions ToNative(in GridLayoutOptions options) => new()
    {
        Padding = ToNative(options.Padding),
        ColumnSpacing = options.ColumnSpacing,
        RowSpacing = options.RowSpacing,
    };

    private static VelloCompositionDockLayoutChild ToNative(in DockLayoutChild child) => new()
    {
        Constraints = ToNative(child.Constraints),
        Margin = ToNative(child.Margin),
        Side = child.Side switch
        {
            DockSide.Left => VelloCompositionDockSide.Left,
            DockSide.Top => VelloCompositionDockSide.Top,
            DockSide.Right => VelloCompositionDockSide.Right,
            DockSide.Bottom => VelloCompositionDockSide.Bottom,
            DockSide.Fill => VelloCompositionDockSide.Fill,
            _ => VelloCompositionDockSide.Fill,
        },
        HorizontalAlignment = ToNative(child.HorizontalAlignment),
        VerticalAlignment = ToNative(child.VerticalAlignment),
    };

    private static VelloCompositionDockLayoutOptions ToNative(in DockLayoutOptions options) => new()
    {
        Padding = ToNative(options.Padding),
        Spacing = options.Spacing,
        LastChildFill = options.LastChildFill ? 1u : 0u,
    };

    private static LayoutRect FromNative(in VelloCompositionLayoutRect rect) =>
        new(rect.X, rect.Y, rect.Width, rect.Height, rect.PrimaryOffset, rect.PrimaryLength, rect.LineIndex);

    private static VelloCompositionVirtualRowMetric ToNative(in VirtualRowMetric metric) => new()
    {
        NodeId = metric.NodeId,
        Height = metric.Height,
    };

    private static VelloCompositionFrozenKind ToNative(FrozenKind kind) =>
        kind switch
        {
            FrozenKind.Leading => VelloCompositionFrozenKind.Leading,
            FrozenKind.Trailing => VelloCompositionFrozenKind.Trailing,
            _ => VelloCompositionFrozenKind.None,
        };

    private static VelloCompositionVirtualColumnStrip ToNative(in VirtualColumnStrip strip) => new()
    {
        Offset = strip.Offset,
        Width = strip.Width,
        Frozen = ToNative(strip.Frozen),
        Key = strip.Key,
    };

    private static VelloCompositionRowViewportMetrics ToNative(in RowViewportMetrics metrics) => new()
    {
        ScrollOffset = metrics.ScrollOffset,
        ViewportExtent = metrics.ViewportExtent,
        Overscan = metrics.Overscan,
    };

    private static VelloCompositionColumnViewportMetrics ToNative(in ColumnViewportMetrics metrics) => new()
    {
        ScrollOffset = metrics.ScrollOffset,
        ViewportExtent = metrics.ViewportExtent,
        Overscan = metrics.Overscan,
    };

    private static VirtualizerTelemetry FromNative(in VelloCompositionVirtualizerTelemetry telemetry) =>
        new(
            telemetry.RowsTotal,
            telemetry.WindowLength,
            telemetry.Reused,
            telemetry.Adopted,
            telemetry.Allocated,
            telemetry.Recycled,
            telemetry.ActiveBuffers,
            telemetry.FreeBuffers,
            telemetry.Evicted);

    private static ColumnSlice FromNative(in VelloCompositionColumnSlice slice) =>
        new(slice.PrimaryStart, slice.PrimaryCount, slice.FrozenLeading, slice.FrozenTrailing);

    private static RowPlanEntry FromNative(in VelloCompositionRowPlanEntry entry) =>
        new(entry.NodeId, entry.BufferId, entry.Top, entry.Height, entry.Action switch
        {
            VelloCompositionRowAction.Adopt => RowAction.Adopt,
            VelloCompositionRowAction.Allocate => RowAction.Allocate,
            VelloCompositionRowAction.Recycle => RowAction.Recycle,
            _ => RowAction.Reuse,
        });

    private static RowWindow FromNative(in VelloCompositionRowWindow window) =>
        new(window.StartIndex, window.EndIndex, window.TotalHeight);

    public static int SolveStackLayout(
        ReadOnlySpan<StackLayoutChild> children,
        in StackLayoutOptions options,
        in LayoutSize available,
        Span<LayoutRect> results)
    {
        if (children.IsEmpty)
        {
            return 0;
        }

        if (results.Length < children.Length)
        {
            throw new ArgumentException("Result span is too small.", nameof(results));
        }

        int count = children.Length;
        Span<VelloCompositionStackLayoutChild> nativeChildren = count <= LayoutSpanStackThreshold
            ? stackalloc VelloCompositionStackLayoutChild[LayoutSpanStackThreshold]
            : new VelloCompositionStackLayoutChild[count];
        nativeChildren = nativeChildren[..count];

        for (int i = 0; i < count; i++)
        {
            nativeChildren[i] = ToNative(children[i]);
        }

        Span<VelloCompositionLayoutRect> nativeRects = count <= LayoutSpanStackThreshold
            ? stackalloc VelloCompositionLayoutRect[LayoutSpanStackThreshold]
            : new VelloCompositionLayoutRect[count];
        nativeRects = nativeRects[..count];

        nuint produced;
        var nativeOptions = ToNative(options);
        unsafe
        {
            fixed (VelloCompositionStackLayoutChild* childPtr = nativeChildren)
            fixed (VelloCompositionLayoutRect* rectPtr = nativeRects)
            {
                produced = NativeMethods.vello_composition_stack_layout(
                    nativeOptions,
                    childPtr,
                    (nuint)count,
                    available.Width,
                    available.Height,
                    rectPtr,
                    (nuint)count);
            }
        }

        if (produced == 0)
        {
            return 0;
        }

        for (int i = 0; i < (int)produced; i++)
        {
            results[i] = FromNative(nativeRects[i]);
    }

        return (int)produced;
    }

    public sealed class CompositionVirtualizer : SafeHandleZeroOrMinusOneIsInvalid
    {
        public CompositionVirtualizer()
            : base(ownsHandle: true)
        {
            var ptr = NativeMethods.vello_composition_virtualizer_create();
            if (ptr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create composition virtualizer.");
            }

            SetHandle(ptr);
        }

        protected override bool ReleaseHandle()
        {
            NativeMethods.vello_composition_virtualizer_destroy(handle);
            SetHandle(IntPtr.Zero);
            return true;
        }

        private void ThrowIfInvalid()
        {
            if (IsInvalid)
            {
                throw new ObjectDisposedException(nameof(CompositionVirtualizer));
            }
        }

        public void Clear()
        {
            ThrowIfInvalid();
            NativeMethods.vello_composition_virtualizer_clear(handle);
        }

        public void SetRows(ReadOnlySpan<VirtualRowMetric> rows)
        {
            ThrowIfInvalid();
            Span<VelloCompositionVirtualRowMetric> nativeRows = rows.Length <= LayoutSpanStackThreshold
                ? stackalloc VelloCompositionVirtualRowMetric[LayoutSpanStackThreshold]
                : new VelloCompositionVirtualRowMetric[rows.Length];
            nativeRows = nativeRows[..rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                nativeRows[i] = ToNative(rows[i]);
            }

            unsafe
            {
                fixed (VelloCompositionVirtualRowMetric* rowPtr = nativeRows)
                {
                    NativeMethods.vello_composition_virtualizer_set_rows(
                        handle,
                        nativeRows.IsEmpty ? null : rowPtr,
                        (nuint)rows.Length);
                }
            }
        }

        public void SetColumns(ReadOnlySpan<VirtualColumnStrip> columns)
        {
            ThrowIfInvalid();
            Span<VelloCompositionVirtualColumnStrip> nativeColumns = columns.Length <= LayoutSpanStackThreshold
                ? stackalloc VelloCompositionVirtualColumnStrip[LayoutSpanStackThreshold]
                : new VelloCompositionVirtualColumnStrip[columns.Length];
            nativeColumns = nativeColumns[..columns.Length];
            for (int i = 0; i < columns.Length; i++)
            {
                nativeColumns[i] = ToNative(columns[i]);
            }

            unsafe
            {
                fixed (VelloCompositionVirtualColumnStrip* columnPtr = nativeColumns)
                {
                    NativeMethods.vello_composition_virtualizer_set_columns(
                        handle,
                        nativeColumns.IsEmpty ? null : columnPtr,
                        (nuint)columns.Length);
                }
            }
        }

        public void Plan(in RowViewportMetrics rowMetrics, in ColumnViewportMetrics columnMetrics)
        {
            ThrowIfInvalid();
            NativeMethods.vello_composition_virtualizer_plan(
                handle,
                ToNative(rowMetrics),
                ToNative(columnMetrics));
        }

        public int CopyPlan(Span<RowPlanEntry> buffer)
        {
            ThrowIfInvalid();
            nuint required;
            unsafe
            {
                required = NativeMethods.vello_composition_virtualizer_copy_plan(handle, null, 0);
            }

            if (required == 0)
            {
                return 0;
            }

            int copy = Math.Min(buffer.Length, (int)required);
            if (copy == 0)
            {
                return (int)required;
            }

            Span<VelloCompositionRowPlanEntry> temp = copy <= LayoutSpanStackThreshold
                ? stackalloc VelloCompositionRowPlanEntry[LayoutSpanStackThreshold]
                : new VelloCompositionRowPlanEntry[copy];
            temp = temp[..copy];

            unsafe
            {
                fixed (VelloCompositionRowPlanEntry* ptr = temp)
                {
                    NativeMethods.vello_composition_virtualizer_copy_plan(
                        handle,
                        ptr,
                        (nuint)copy);
                }
            }

            for (int i = 0; i < copy; i++)
            {
                buffer[i] = FromNative(temp[i]);
            }

            return (int)required;
        }

        public int CopyRecycle(Span<RowPlanEntry> buffer)
        {
            ThrowIfInvalid();
            nuint required;
            unsafe
            {
                required = NativeMethods.vello_composition_virtualizer_copy_recycle(handle, null, 0);
            }

            if (required == 0)
            {
                return 0;
            }

            int copy = Math.Min(buffer.Length, (int)required);
            if (copy == 0)
            {
                return (int)required;
            }

            Span<VelloCompositionRowPlanEntry> temp = copy <= LayoutSpanStackThreshold
                ? stackalloc VelloCompositionRowPlanEntry[LayoutSpanStackThreshold]
                : new VelloCompositionRowPlanEntry[copy];
            temp = temp[..copy];

            unsafe
            {
                fixed (VelloCompositionRowPlanEntry* ptr = temp)
                {
                    NativeMethods.vello_composition_virtualizer_copy_recycle(
                        handle,
                        ptr,
                        (nuint)copy);
                }
            }

            for (int i = 0; i < copy; i++)
            {
                buffer[i] = FromNative(temp[i]);
            }

            return (int)required;
        }

        public bool TryGetWindow(out RowWindow window)
        {
            ThrowIfInvalid();
            if (!NativeMethods.vello_composition_virtualizer_window(handle, out var native))
            {
                window = default;
                return false;
            }

            window = FromNative(native);
            return true;
        }

        public ColumnSlice GetColumnSlice()
        {
            ThrowIfInvalid();
            return NativeMethods.vello_composition_virtualizer_column_slice(handle, out var slice)
                ? FromNative(slice)
                : default;
        }

        public VirtualizerTelemetry GetTelemetry()
        {
            ThrowIfInvalid();
            return NativeMethods.vello_composition_virtualizer_telemetry(handle, out var telemetry)
                ? FromNative(telemetry)
                : default;
        }
    }
    public static WrapLayoutSolveResult SolveWrapLayout(
        ReadOnlySpan<WrapLayoutChild> children,
        in WrapLayoutOptions options,
        in LayoutSize available,
        Span<LayoutRect> layoutBuffer,
        Span<WrapLayoutLine> lineBuffer)
    {
        if (children.IsEmpty)
        {
            return default;
        }

        int count = children.Length;
        Span<VelloCompositionWrapLayoutChild> nativeChildren = count <= LayoutSpanStackThreshold
            ? stackalloc VelloCompositionWrapLayoutChild[LayoutSpanStackThreshold]
            : new VelloCompositionWrapLayoutChild[count];
        nativeChildren = nativeChildren[..count];
        for (int i = 0; i < count; i++)
        {
            nativeChildren[i] = ToNative(children[i]);
        }

        var nativeOptions = ToNative(options);
        nuint produced;
        nuint lineCount;

        unsafe
        {
            fixed (VelloCompositionWrapLayoutChild* childPtr = nativeChildren)
            {
                produced = NativeMethods.vello_composition_wrap_layout(
                    nativeOptions,
                    childPtr,
                    (nuint)count,
                    available.Width,
                    available.Height,
                    null,
                    0,
                    null,
                    0,
                    &lineCount);
            }
        }

        var rectCount = (int)produced;
        var linesRequired = (int)lineCount;

        var tempRectArray = rectCount > 0
            ? new VelloCompositionLayoutRect[rectCount]
            : Array.Empty<VelloCompositionLayoutRect>();
        var tempLineArray = linesRequired > 0
            ? new VelloCompositionWrapLayoutLine[linesRequired]
            : Array.Empty<VelloCompositionWrapLayoutLine>();

        Span<VelloCompositionLayoutRect> tempRects = tempRectArray;
        Span<VelloCompositionWrapLayoutLine> tempLines = tempLineArray;

        unsafe
        {
            fixed (VelloCompositionWrapLayoutChild* childPtr = nativeChildren)
            fixed (VelloCompositionLayoutRect* rectPtr = tempRects)
            fixed (VelloCompositionWrapLayoutLine* linePtr = tempLines)
            {
                NativeMethods.vello_composition_wrap_layout(
                    nativeOptions,
                    childPtr,
                    (nuint)count,
                    available.Width,
                    available.Height,
                    rectPtr,
                    (nuint)tempRects.Length,
                    linePtr,
                    (nuint)tempLines.Length,
                    &lineCount);
            }
        }

        int layoutCopy = Math.Min(layoutBuffer.Length, rectCount);
        for (int i = 0; i < layoutCopy; i++)
        {
            layoutBuffer[i] = FromNative(tempRects[i]);
        }

        int lineCopy = Math.Min(lineBuffer.Length, linesRequired);
        for (int i = 0; i < lineCopy; i++)
        {
            var native = tempLines[i];
            lineBuffer[i] = new WrapLayoutLine(native.LineIndex, native.Start, native.Count, native.PrimaryOffset, native.PrimaryLength);
        }

        return new WrapLayoutSolveResult(rectCount, linesRequired);
    }

    public static int SolveGridLayout(
        ReadOnlySpan<GridTrack> columns,
        ReadOnlySpan<GridTrack> rows,
        ReadOnlySpan<GridLayoutChild> children,
        in GridLayoutOptions options,
        in LayoutSize available,
        Span<LayoutRect> results)
    {
        if (children.IsEmpty)
        {
            return 0;
        }

        if (results.Length < children.Length)
        {
            throw new ArgumentException("Result span is too small.", nameof(results));
        }

        Span<VelloCompositionGridTrack> nativeColumns = columns.Length <= LayoutSpanStackThreshold
            ? stackalloc VelloCompositionGridTrack[LayoutSpanStackThreshold]
            : new VelloCompositionGridTrack[columns.Length];
        nativeColumns = nativeColumns[..columns.Length];

        for (int i = 0; i < columns.Length; i++)
        {
            nativeColumns[i] = ToNative(columns[i]);
        }

        Span<VelloCompositionGridTrack> nativeRows = rows.Length <= LayoutSpanStackThreshold
            ? stackalloc VelloCompositionGridTrack[LayoutSpanStackThreshold]
            : new VelloCompositionGridTrack[rows.Length];
        nativeRows = nativeRows[..rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            nativeRows[i] = ToNative(rows[i]);
        }

        Span<VelloCompositionGridLayoutChild> nativeChildren = children.Length <= LayoutSpanStackThreshold
            ? stackalloc VelloCompositionGridLayoutChild[LayoutSpanStackThreshold]
            : new VelloCompositionGridLayoutChild[children.Length];
        nativeChildren = nativeChildren[..children.Length];
        for (int i = 0; i < children.Length; i++)
        {
            nativeChildren[i] = ToNative(children[i]);
        }

        Span<VelloCompositionLayoutRect> nativeRects = children.Length <= LayoutSpanStackThreshold
            ? stackalloc VelloCompositionLayoutRect[LayoutSpanStackThreshold]
            : new VelloCompositionLayoutRect[children.Length];
        nativeRects = nativeRects[..children.Length];

        nuint produced;
        var nativeOptions = ToNative(options);
        unsafe
        {
            fixed (VelloCompositionGridTrack* columnsPtr = nativeColumns)
            fixed (VelloCompositionGridTrack* rowsPtr = nativeRows)
            fixed (VelloCompositionGridLayoutChild* childPtr = nativeChildren)
            fixed (VelloCompositionLayoutRect* rectPtr = nativeRects)
            {
                produced = NativeMethods.vello_composition_grid_layout(
                    columnsPtr,
                    (nuint)columns.Length,
                    rowsPtr,
                    (nuint)rows.Length,
                    nativeOptions,
                    childPtr,
                    (nuint)children.Length,
                    available.Width,
                    available.Height,
                    rectPtr,
                    (nuint)children.Length);
            }
        }

        if (produced == 0)
        {
            return 0;
        }

        for (int i = 0; i < (int)produced; i++)
        {
            results[i] = FromNative(nativeRects[i]);
        }

        return (int)produced;
    }

    public static int SolveDockLayout(
        ReadOnlySpan<DockLayoutChild> children,
        in DockLayoutOptions options,
        in LayoutSize available,
        Span<LayoutRect> results)
    {
        if (children.IsEmpty)
        {
            return 0;
        }

        if (results.Length < children.Length)
        {
            throw new ArgumentException("Result span is too small.", nameof(results));
        }

        Span<VelloCompositionDockLayoutChild> nativeChildren = children.Length <= LayoutSpanStackThreshold
            ? stackalloc VelloCompositionDockLayoutChild[LayoutSpanStackThreshold]
            : new VelloCompositionDockLayoutChild[children.Length];
        nativeChildren = nativeChildren[..children.Length];
        for (int i = 0; i < children.Length; i++)
        {
            nativeChildren[i] = ToNative(children[i]);
        }

        Span<VelloCompositionLayoutRect> nativeRects = children.Length <= LayoutSpanStackThreshold
            ? stackalloc VelloCompositionLayoutRect[LayoutSpanStackThreshold]
            : new VelloCompositionLayoutRect[children.Length];
        nativeRects = nativeRects[..children.Length];

        var nativeOptions = ToNative(options);
        nuint produced;
        unsafe
        {
            fixed (VelloCompositionDockLayoutChild* childPtr = nativeChildren)
            fixed (VelloCompositionLayoutRect* rectPtr = nativeRects)
            {
                produced = NativeMethods.vello_composition_dock_layout(
                    nativeOptions,
                    childPtr,
                    (nuint)children.Length,
                    available.Width,
                    available.Height,
                    rectPtr,
                    (nuint)children.Length);
            }
        }

        if (produced == 0)
        {
            return 0;
        }

        for (int i = 0; i < (int)produced; i++)
        {
            results[i] = FromNative(nativeRects[i]);
        }

        return (int)produced;
    }
}

public readonly record struct CompositionColor(float R, float G, float B, float A = 1f)
{
    public CompositionColor Clamp() =>
        new(Math.Clamp(R, 0f, 1f), Math.Clamp(G, 0f, 1f), Math.Clamp(B, 0f, 1f), Math.Clamp(A, 0f, 1f));

    internal VelloCompositionColor ToNative()
    {
        var clamped = Clamp();
        return new VelloCompositionColor
        {
            R = clamped.R,
            G = clamped.G,
            B = clamped.B,
            A = clamped.A,
        };
    }

    internal static CompositionColor FromNative(in VelloCompositionColor native) =>
        new(native.R, native.G, native.B, native.A);
}

public enum CompositionShaderKind : uint
{
    Solid = 0,
}

public readonly record struct CompositionShaderDescriptor(
    CompositionShaderKind Kind,
    CompositionColor Solid);

public static class CompositionShaderRegistry
{
    public static void Register(uint shaderId, CompositionShaderDescriptor descriptor)
    {
        if (shaderId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shaderId), "Shader identifier must be non-zero.");
        }

        var native = new VelloCompositionShaderDescriptor
        {
            Kind = (VelloCompositionShaderKind)descriptor.Kind,
            Solid = descriptor.Solid.ToNative(),
        };

        if (!NativeMethods.vello_composition_shader_register(shaderId, in native))
        {
            throw new InvalidOperationException("Failed to register composition shader.");
        }
    }

    public static void Unregister(uint shaderId)
    {
        if (shaderId == 0)
        {
            return;
        }

        NativeMethods.vello_composition_shader_unregister(shaderId);
    }
}

public readonly record struct CompositionMaterialDescriptor(uint ShaderId, float Opacity = 1f)
{
    internal VelloCompositionMaterialDescriptor ToNative()
    {
        return new VelloCompositionMaterialDescriptor
        {
            Shader = ShaderId,
            Opacity = Math.Clamp(Opacity, 0f, 1f),
        };
    }
}

public static class CompositionMaterialRegistry
{
    public static void Register(uint materialId, CompositionMaterialDescriptor descriptor)
    {
        if (materialId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(materialId), "Material identifier must be non-zero.");
        }

        if (descriptor.ShaderId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "Descriptor must reference a registered shader.");
        }

        var native = descriptor.ToNative();
        if (!NativeMethods.vello_composition_material_register(materialId, in native))
        {
            throw new InvalidOperationException("Failed to register composition material.");
        }
    }

    public static void Unregister(uint materialId)
    {
        if (materialId == 0)
        {
            return;
        }

        NativeMethods.vello_composition_material_unregister(materialId);
    }

    public static bool TryResolveColor(uint materialId, out CompositionColor color)
    {
        if (NativeMethods.vello_composition_material_resolve_color(materialId, out var native))
        {
            color = CompositionColor.FromNative(native);
            return true;
        }

        color = default;
        return false;
    }
}

public readonly record struct DirtyRegion(double MinX, double MaxX, double MinY, double MaxY)
{
    public bool IsEmpty => MinX > MaxX || MinY > MaxY;
}

public sealed class SceneCache : SafeHandle
{
    public SceneCache()
        : base(IntPtr.Zero, ownsHandle: true)
    {
        var nativeHandle = NativeMethods.vello_composition_scene_cache_create();
        SetHandle(nativeHandle);
        if (IsInvalid)
        {
            throw new InvalidOperationException("Failed to create scene cache.");
        }
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        if (!IsInvalid)
        {
            NativeMethods.vello_composition_scene_cache_destroy(handle);
            SetHandle(IntPtr.Zero);
        }

        return true;
    }

    public uint CreateNode(uint? parentId = null)
    {
        ThrowIfInvalid();
        uint parent = parentId ?? uint.MaxValue;
        uint node = NativeMethods.vello_composition_scene_cache_create_node(handle, parent);
        if (node == uint.MaxValue)
        {
            throw new InvalidOperationException("Failed to allocate scene cache node.");
        }

        return node;
    }

    public void DisposeNode(uint nodeId)
    {
        ThrowIfInvalid();
        NativeMethods.vello_composition_scene_cache_dispose_node(handle, nodeId);
    }

    public void MarkDirty(uint nodeId, double x, double y)
    {
        ThrowIfInvalid();
        if (!NativeMethods.vello_composition_scene_cache_mark_dirty(handle, nodeId, x, y))
        {
            throw new InvalidOperationException("Failed to mark scene cache node dirty.");
        }
    }

    public void MarkDirtyBounds(uint nodeId, double minX, double maxX, double minY, double maxY)
    {
        ThrowIfInvalid();
        if (!NativeMethods.vello_composition_scene_cache_mark_dirty_bounds(
                handle,
                nodeId,
                minX,
                maxX,
                minY,
                maxY))
        {
            throw new InvalidOperationException("Failed to mark scene cache node dirty bounds.");
        }
    }

    public bool TakeDirty(uint nodeId, out DirtyRegion region)
    {
        ThrowIfInvalid();
        if (NativeMethods.vello_composition_scene_cache_take_dirty(handle, nodeId, out var native))
        {
            region = new DirtyRegion(native.MinX, native.MaxX, native.MinY, native.MaxY);
            return true;
        }

        region = default;
        return false;
    }

    public void Clear(uint nodeId)
    {
        ThrowIfInvalid();
        NativeMethods.vello_composition_scene_cache_clear(handle, nodeId);
    }

    private void ThrowIfInvalid()
    {
        if (IsInvalid)
        {
            throw new ObjectDisposedException(nameof(SceneCache));
        }
    }
}

[System.Diagnostics.DebuggerDisplay("{Name} (NodeId = {NodeId})")]
public readonly struct RenderLayer
{
    public RenderLayer(string name, uint nodeId)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        NodeId = nodeId;
    }

    public string Name { get; }
    public uint NodeId { get; }
}

public sealed class ScenePartitioner
{
    private readonly SceneCache _cache;
    private readonly Dictionary<string, uint> _layers;
    private readonly object _sync = new();

    public ScenePartitioner(SceneCache cache, uint rootNodeId, IEqualityComparer<string>? comparer = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        if (cache.IsInvalid)
        {
            throw new ObjectDisposedException(nameof(SceneCache));
        }

        RootNodeId = rootNodeId;
        _layers = new Dictionary<string, uint>(comparer ?? StringComparer.Ordinal);
    }

    public SceneCache Cache => _cache;

    public uint RootNodeId { get; }

    public RenderLayer RootLayer => new RenderLayer("root", RootNodeId);

    public RenderLayer GetOrCreateLayer(string name, uint? parentLayerId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Layer name must be provided.", nameof(name));
        }

        lock (_sync)
        {
            if (_layers.TryGetValue(name, out var existing))
            {
                return new RenderLayer(name, existing);
            }

            uint parent = parentLayerId ?? RootNodeId;
            var nodeId = _cache.CreateNode(parent);
            _layers[name] = nodeId;
            return new RenderLayer(name, nodeId);
        }
    }

    public bool TryGetLayer(string name, out RenderLayer layer)
    {
        if (string.IsNullOrEmpty(name))
        {
            layer = default;
            return false;
        }

        lock (_sync)
        {
            if (_layers.TryGetValue(name, out var nodeId))
            {
                layer = new RenderLayer(name, nodeId);
                return true;
            }
        }

        layer = default;
        return false;
    }

    public bool RemoveLayer(string name, bool disposeNode = true)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        uint nodeId;
        lock (_sync)
        {
            if (!_layers.TryGetValue(name, out nodeId))
            {
                return false;
            }

            _layers.Remove(name);
        }

        if (disposeNode)
        {
            _cache.DisposeNode(nodeId);
        }

        return true;
    }

    public IEnumerable<RenderLayer> EnumerateLayers()
    {
        lock (_sync)
        {
            foreach (var pair in _layers)
            {
                yield return new RenderLayer(pair.Key, pair.Value);
            }
        }
    }
}

[Flags]
public enum TimelineSampleFlags : ushort
{
    None = 0,
    Active = 1 << 0,
    Completed = 1 << 1,
    Looped = 1 << 2,
    PingPongReversed = 1 << 3,
    AtRest = 1 << 4,
}

public enum TimelineRepeat
{
    Once = 0,
    Loop = 1,
    PingPong = 2,
}

public enum TimelineEasing
{
    Linear = 0,
    EaseInQuad = 1,
    EaseOutQuad = 2,
    EaseInOutQuad = 3,
    EaseInCubic = 4,
    EaseOutCubic = 5,
    EaseInOutCubic = 6,
    EaseInQuart = 7,
    EaseOutQuart = 8,
    EaseInOutQuart = 9,
    EaseInQuint = 10,
    EaseOutQuint = 11,
    EaseInOutQuint = 12,
    EaseInSine = 13,
    EaseOutSine = 14,
    EaseInOutSine = 15,
    EaseInExpo = 16,
    EaseOutExpo = 17,
    EaseInOutExpo = 18,
    EaseInCirc = 19,
    EaseOutCirc = 20,
    EaseInOutCirc = 21,
}

public enum TimelineDirtyKind
{
    None = 0,
    Point = 1,
    Bounds = 2,
}

public readonly struct TimelineDirtyBinding
{
    public TimelineDirtyBinding(
        TimelineDirtyKind kind,
        double x,
        double y,
        double minX,
        double maxX,
        double minY,
        double maxY)
    {
        Kind = kind;
        X = x;
        Y = y;
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
    }

    public TimelineDirtyKind Kind { get; }
    public double X { get; }
    public double Y { get; }
    public double MinX { get; }
    public double MaxX { get; }
    public double MinY { get; }
    public double MaxY { get; }

    public static TimelineDirtyBinding None => default;

    public static TimelineDirtyBinding Point(double x, double y) =>
        new(TimelineDirtyKind.Point, x, y, 0.0, 0.0, 0.0, 0.0);

    public static TimelineDirtyBinding Bounds(double minX, double maxX, double minY, double maxY) =>
        new(TimelineDirtyKind.Bounds, 0.0, 0.0, minX, maxX, minY, maxY);

    internal VelloCompositionTimelineDirtyBinding ToNative()
    {
        var binding = new VelloCompositionTimelineDirtyBinding
        {
            Kind = (VelloCompositionTimelineDirtyKind)Kind,
            Reserved = 0,
            X = X,
            Y = Y,
            MinX = MinX,
            MaxX = MaxX,
            MinY = MinY,
            MaxY = MaxY,
        };

        if (Kind == TimelineDirtyKind.Bounds)
        {
            var minX = Math.Min(MinX, MaxX);
            var maxX = Math.Max(MinX, MaxX);
            var minY = Math.Min(MinY, MaxY);
            var maxY = Math.Max(MinY, MaxY);
            binding.MinX = minX;
            binding.MaxX = maxX;
            binding.MinY = minY;
            binding.MaxY = maxY;
        }

        return binding;
    }
}

public readonly struct TimelineGroupConfig
{
    public TimelineGroupConfig(float speed = 1.0f, bool autoplay = true)
    {
        Speed = speed;
        Autoplay = autoplay;
    }

    public float Speed { get; }
    public bool Autoplay { get; }

    internal VelloCompositionTimelineGroupConfig ToNative() => new()
    {
        Speed = Speed,
        Autoplay = Autoplay ? 1 : 0,
    };
}

public readonly struct TimelineEasingTrackDescriptor
{
    public TimelineEasingTrackDescriptor(
        uint nodeId,
        ushort channelId,
        float startValue,
        float endValue,
        float duration,
        TimelineEasing easing,
        TimelineRepeat repeat,
        TimelineDirtyBinding dirtyBinding)
    {
        NodeId = nodeId;
        ChannelId = channelId;
        StartValue = startValue;
        EndValue = endValue;
        Duration = duration;
        Easing = easing;
        Repeat = repeat;
        DirtyBinding = dirtyBinding;
    }

    public uint NodeId { get; }
    public ushort ChannelId { get; }
    public float StartValue { get; }
    public float EndValue { get; }
    public float Duration { get; }
    public TimelineEasing Easing { get; }
    public TimelineRepeat Repeat { get; }
    public TimelineDirtyBinding DirtyBinding { get; }

    internal VelloCompositionTimelineEasingTrackDesc ToNative() => new()
    {
        NodeId = NodeId,
        ChannelId = ChannelId,
        Reserved = 0,
        Repeat = (VelloCompositionTimelineRepeat)Repeat,
        Easing = (VelloCompositionTimelineEasing)Easing,
        StartValue = StartValue,
        EndValue = EndValue,
        Duration = Duration,
        DirtyBinding = DirtyBinding.ToNative(),
    };
}

public readonly struct TimelineSpringTrackDescriptor
{
    public TimelineSpringTrackDescriptor(
        uint nodeId,
        ushort channelId,
        float stiffness,
        float damping,
        float mass,
        float startValue,
        float initialVelocity,
        float targetValue,
        float restVelocity,
        float restOffset,
        TimelineDirtyBinding dirtyBinding)
    {
        NodeId = nodeId;
        ChannelId = channelId;
        Stiffness = stiffness;
        Damping = damping;
        Mass = mass;
        StartValue = startValue;
        InitialVelocity = initialVelocity;
        TargetValue = targetValue;
        RestVelocity = restVelocity;
        RestOffset = restOffset;
        DirtyBinding = dirtyBinding;
    }

    public uint NodeId { get; }
    public ushort ChannelId { get; }
    public float Stiffness { get; }
    public float Damping { get; }
    public float Mass { get; }
    public float StartValue { get; }
    public float InitialVelocity { get; }
    public float TargetValue { get; }
    public float RestVelocity { get; }
    public float RestOffset { get; }
    public TimelineDirtyBinding DirtyBinding { get; }

    internal VelloCompositionTimelineSpringTrackDesc ToNative() => new()
    {
        NodeId = NodeId,
        ChannelId = ChannelId,
        Reserved = 0,
        Stiffness = Stiffness,
        Damping = Damping,
        Mass = Mass,
        StartValue = StartValue,
        InitialVelocity = InitialVelocity,
        TargetValue = TargetValue,
        RestVelocity = RestVelocity,
        RestOffset = RestOffset,
        DirtyBinding = DirtyBinding.ToNative(),
    };
}

[StructLayout(LayoutKind.Sequential)]
public struct TimelineSample
{
    public uint TrackId;
    public uint NodeId;
    public ushort ChannelId;
    public TimelineSampleFlags Flags;
    public float Value;
    public float Velocity;
    public float Progress;

    public readonly bool IsCompleted => (Flags & TimelineSampleFlags.Completed) != 0;
    public readonly bool IsActive => (Flags & TimelineSampleFlags.Active) != 0;
}

public sealed class TimelineSystem : SafeHandle
{
    public TimelineSystem()
        : base(IntPtr.Zero, ownsHandle: true)
    {
        var nativeHandle = NativeMethods.vello_composition_timeline_system_create();
        SetHandle(nativeHandle);
        if (IsInvalid)
        {
            throw new InvalidOperationException("Failed to create timeline system.");
        }
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        if (!IsInvalid)
        {
            NativeMethods.vello_composition_timeline_system_destroy(handle);
            SetHandle(IntPtr.Zero);
        }

        return true;
    }

    public uint CreateGroup(TimelineGroupConfig config)
    {
        ThrowIfInvalid();
        var nativeConfig = config.ToNative();
        uint groupId = NativeMethods.vello_composition_timeline_group_create(handle, nativeConfig);
        if (groupId == uint.MaxValue)
        {
            throw new InvalidOperationException("Failed to create timeline group.");
        }

        return groupId;
    }

    public void DestroyGroup(uint groupId)
    {
        ThrowIfInvalid();
        NativeMethods.vello_composition_timeline_group_destroy(handle, groupId);
    }

    public void PlayGroup(uint groupId)
    {
        ThrowIfInvalid();
        NativeMethods.vello_composition_timeline_group_play(handle, groupId);
    }

    public void PauseGroup(uint groupId)
    {
        ThrowIfInvalid();
        NativeMethods.vello_composition_timeline_group_pause(handle, groupId);
    }

    public void SetGroupSpeed(uint groupId, float speed)
    {
        ThrowIfInvalid();
        NativeMethods.vello_composition_timeline_group_set_speed(handle, groupId, speed);
    }

    public uint AddEasingTrack(uint groupId, TimelineEasingTrackDescriptor descriptor)
    {
        ThrowIfInvalid();
        if (descriptor.NodeId == uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "Descriptor must reference a valid scene node.");
        }

        if (descriptor.Duration <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "Duration must be positive.");
        }

        var nativeDescriptor = descriptor.ToNative();
        uint trackId;
        unsafe
        {
            trackId = NativeMethods.vello_composition_timeline_add_easing_track(
                handle,
                groupId,
                (VelloCompositionTimelineEasingTrackDesc*)Unsafe.AsPointer(ref nativeDescriptor));
        }

        if (trackId == uint.MaxValue)
        {
            throw new InvalidOperationException("Failed to add easing track.");
        }

        return trackId;
    }

    public uint AddSpringTrack(uint groupId, TimelineSpringTrackDescriptor descriptor)
    {
        ThrowIfInvalid();
        if (descriptor.NodeId == uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "Descriptor must reference a valid scene node.");
        }

        if (descriptor.Stiffness <= 0f || descriptor.Mass <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "Spring stiffness and mass must be positive.");
        }

        var nativeDescriptor = descriptor.ToNative();
        uint trackId;
        unsafe
        {
            trackId = NativeMethods.vello_composition_timeline_add_spring_track(
                handle,
                groupId,
                (VelloCompositionTimelineSpringTrackDesc*)Unsafe.AsPointer(ref nativeDescriptor));
        }

        if (trackId == uint.MaxValue)
        {
            throw new InvalidOperationException("Failed to add spring track.");
        }

        return trackId;
    }

    public void RemoveTrack(uint trackId)
    {
        ThrowIfInvalid();
        NativeMethods.vello_composition_timeline_track_remove(handle, trackId);
    }

    public void ResetTrack(uint trackId)
    {
        ThrowIfInvalid();
        NativeMethods.vello_composition_timeline_track_reset(handle, trackId);
    }

    public void SetSpringTarget(uint trackId, float targetValue)
    {
        ThrowIfInvalid();
        NativeMethods.vello_composition_timeline_track_set_spring_target(handle, trackId, targetValue);
    }

    public int Tick(TimeSpan delta, SceneCache? cache, Span<TimelineSample> samples)
    {
        ThrowIfInvalid();

        if (cache is { IsInvalid: true })
        {
            throw new ObjectDisposedException(nameof(SceneCache));
        }

        double seconds = delta.TotalSeconds;
        nint cacheHandle = cache?.DangerousGetHandle() ?? IntPtr.Zero;

        nuint result;
        unsafe
        {
            if (samples.IsEmpty)
            {
                result = NativeMethods.vello_composition_timeline_tick(
                    handle,
                    seconds,
                    cacheHandle,
                    null,
                    0);
            }
            else
            {
                fixed (TimelineSample* sampleBase = samples)
                {
                    result = NativeMethods.vello_composition_timeline_tick(
                        handle,
                        seconds,
                        cacheHandle,
                        (VelloCompositionTimelineSample*)sampleBase,
                        (nuint)samples.Length);
                }
            }
        }

        GC.KeepAlive(cache);

        if (result > int.MaxValue)
        {
            throw new InvalidOperationException("Timeline produced more samples than supported.");
        }

        return (int)result;
    }

    private void ThrowIfInvalid()
    {
        if (IsInvalid)
        {
            throw new ObjectDisposedException(nameof(TimelineSystem));
        }
    }
}
