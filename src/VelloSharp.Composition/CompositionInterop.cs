using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace VelloSharp.Composition;

public static class CompositionInterop
{
    private const int StackAllocThreshold = 256;
    private const int LinearLayoutStackThreshold = 8;

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
