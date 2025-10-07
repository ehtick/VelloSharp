using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VelloSharp.Composition;

internal static partial class NativeMethods
{
    private const string LibraryName = "vello_composition";

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_compute_plot_area")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial bool vello_composition_compute_plot_area(
        double width,
        double height,
        out VelloCompositionPlotArea plotArea);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_measure_label")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial bool vello_composition_measure_label(
        byte* textUtf8,
        nuint textLength,
        float fontSize,
        out VelloCompositionLabelMetrics metrics);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_solve_linear_layout")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial nuint vello_composition_solve_linear_layout(
        VelloCompositionLinearLayoutItem* items,
        nuint itemCount,
        double available,
        double spacing,
        VelloCompositionLinearLayoutSlot* slots,
        nuint slotsLength);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_scene_cache_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint vello_composition_scene_cache_create();

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_scene_cache_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_composition_scene_cache_destroy(nint cache);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_scene_cache_create_node")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial uint vello_composition_scene_cache_create_node(
        nint cache,
        uint parentId);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_scene_cache_dispose_node")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_composition_scene_cache_dispose_node(
        nint cache,
        uint nodeId);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_scene_cache_mark_dirty")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial bool vello_composition_scene_cache_mark_dirty(
        nint cache,
        uint nodeId,
        double x,
        double y);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_scene_cache_mark_dirty_bounds")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial bool vello_composition_scene_cache_mark_dirty_bounds(
        nint cache,
        uint nodeId,
        double minX,
        double maxX,
        double minY,
        double maxY);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_scene_cache_take_dirty")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial bool vello_composition_scene_cache_take_dirty(
        nint cache,
        uint nodeId,
        out VelloCompositionDirtyRegion region);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_scene_cache_clear")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_composition_scene_cache_clear(
        nint cache,
        uint nodeId);
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionPlotArea
{
    public double Left;
    public double Top;
    public double Width;
    public double Height;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionLabelMetrics
{
    public float Width;
    public float Height;
    public float Ascent;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionLinearLayoutItem
{
    public double Min;
    public double Preferred;
    public double Max;
    public double Weight;
    public double MarginLeading;
    public double MarginTrailing;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionLinearLayoutSlot
{
    public double Offset;
    public double Length;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionDirtyRegion
{
    public double MinX;
    public double MaxX;
    public double MinY;
    public double MaxY;
}
