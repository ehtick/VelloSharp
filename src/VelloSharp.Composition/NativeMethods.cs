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

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_stack_layout")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial nuint vello_composition_stack_layout(
        in VelloCompositionStackLayoutOptions options,
        VelloCompositionStackLayoutChild* children,
        nuint childCount,
        double availableWidth,
        double availableHeight,
        VelloCompositionLayoutRect* rects,
        nuint rectLen);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_wrap_layout")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial nuint vello_composition_wrap_layout(
        in VelloCompositionWrapLayoutOptions options,
        VelloCompositionWrapLayoutChild* children,
        nuint childCount,
        double availableWidth,
        double availableHeight,
        VelloCompositionLayoutRect* rects,
        nuint rectLen,
        VelloCompositionWrapLayoutLine* lines,
        nuint lineLen,
        nuint* outLineCount);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_grid_layout")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial nuint vello_composition_grid_layout(
        VelloCompositionGridTrack* columns,
        nuint columnsLen,
        VelloCompositionGridTrack* rows,
        nuint rowsLen,
        in VelloCompositionGridLayoutOptions options,
        VelloCompositionGridLayoutChild* children,
        nuint childCount,
        double availableWidth,
        double availableHeight,
        VelloCompositionLayoutRect* rects,
        nuint rectLen);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_dock_layout")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial nuint vello_composition_dock_layout(
        in VelloCompositionDockLayoutOptions options,
        VelloCompositionDockLayoutChild* children,
        nuint childCount,
        double availableWidth,
        double availableHeight,
        VelloCompositionLayoutRect* rects,
        nuint rectLen);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_shader_register")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial bool vello_composition_shader_register(
        uint handle,
        in VelloCompositionShaderDescriptor descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_shader_unregister")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_composition_shader_unregister(uint handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_material_register")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial bool vello_composition_material_register(
        uint handle,
        in VelloCompositionMaterialDescriptor descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_material_unregister")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_composition_material_unregister(uint handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_material_resolve_color")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial bool vello_composition_material_resolve_color(
        uint handle,
        out VelloCompositionColor color);

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

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_timeline_system_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint vello_composition_timeline_system_create();

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_timeline_system_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_composition_timeline_system_destroy(nint system);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_timeline_group_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial uint vello_composition_timeline_group_create(
        nint system,
        VelloCompositionTimelineGroupConfig config);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_timeline_group_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_composition_timeline_group_destroy(
        nint system,
        uint groupId);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_timeline_group_play")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_composition_timeline_group_play(
        nint system,
        uint groupId);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_timeline_group_pause")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_composition_timeline_group_pause(
        nint system,
        uint groupId);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_timeline_group_set_speed")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_composition_timeline_group_set_speed(
        nint system,
        uint groupId,
        float speed);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_timeline_add_easing_track")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial uint vello_composition_timeline_add_easing_track(
        nint system,
        uint groupId,
        VelloCompositionTimelineEasingTrackDesc* descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_timeline_add_spring_track")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial uint vello_composition_timeline_add_spring_track(
        nint system,
        uint groupId,
        VelloCompositionTimelineSpringTrackDesc* descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_timeline_track_remove")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_composition_timeline_track_remove(
        nint system,
        uint trackId);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_timeline_track_reset")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_composition_timeline_track_reset(
        nint system,
        uint trackId);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_timeline_track_set_spring_target")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_composition_timeline_track_set_spring_target(
        nint system,
        uint trackId,
        float targetValue);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_timeline_tick")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial nuint vello_composition_timeline_tick(
        nint system,
        double deltaSeconds,
        nint cache,
        VelloCompositionTimelineSample* samples,
        nuint samplesLength);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_virtualizer_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint vello_composition_virtualizer_create();

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_virtualizer_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_composition_virtualizer_destroy(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_virtualizer_clear")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_composition_virtualizer_clear(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_virtualizer_set_rows")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial void vello_composition_virtualizer_set_rows(
        nint handle,
        VelloCompositionVirtualRowMetric* rows,
        nuint rowsLen);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_virtualizer_set_columns")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial void vello_composition_virtualizer_set_columns(
        nint handle,
        VelloCompositionVirtualColumnStrip* columns,
        nuint columnsLen);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_virtualizer_plan")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_composition_virtualizer_plan(
        nint handle,
        VelloCompositionRowViewportMetrics rowMetrics,
        VelloCompositionColumnViewportMetrics columnMetrics);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_virtualizer_copy_plan")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial nuint vello_composition_virtualizer_copy_plan(
        nint handle,
        VelloCompositionRowPlanEntry* entries,
        nuint entryLen);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_virtualizer_copy_recycle")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial nuint vello_composition_virtualizer_copy_recycle(
        nint handle,
        VelloCompositionRowPlanEntry* entries,
        nuint entryLen);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_virtualizer_window")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial bool vello_composition_virtualizer_window(
        nint handle,
        out VelloCompositionRowWindow window);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_virtualizer_column_slice")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial bool vello_composition_virtualizer_column_slice(
        nint handle,
        out VelloCompositionColumnSlice slice);

    [LibraryImport(LibraryName, EntryPoint = "vello_composition_virtualizer_telemetry")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial bool vello_composition_virtualizer_telemetry(
        nint handle,
        out VelloCompositionVirtualizerTelemetry telemetry);
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
internal struct VelloCompositionScalarConstraint
{
    public double Min;
    public double Preferred;
    public double Max;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionLayoutConstraints
{
    public VelloCompositionScalarConstraint Width;
    public VelloCompositionScalarConstraint Height;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionLayoutThickness
{
    public double Left;
    public double Top;
    public double Right;
    public double Bottom;
}

internal enum VelloCompositionLayoutOrientation : uint
{
    Horizontal = 0,
    Vertical = 1,
}

internal enum VelloCompositionLayoutAlignment : uint
{
    Start = 0,
    Center = 1,
    End = 2,
    Stretch = 3,
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionLayoutRect
{
    public double X;
    public double Y;
    public double Width;
    public double Height;
    public double PrimaryOffset;
    public double PrimaryLength;
    public uint LineIndex;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionStackLayoutChild
{
    public VelloCompositionLayoutConstraints Constraints;
    public double Weight;
    public VelloCompositionLayoutThickness Margin;
    public VelloCompositionLayoutAlignment CrossAlignment;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionStackLayoutOptions
{
    public VelloCompositionLayoutOrientation Orientation;
    public double Spacing;
    public VelloCompositionLayoutThickness Padding;
    public VelloCompositionLayoutAlignment CrossAlignment;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionWrapLayoutChild
{
    public VelloCompositionLayoutConstraints Constraints;
    public VelloCompositionLayoutThickness Margin;
    public uint LineBreak;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionWrapLayoutOptions
{
    public VelloCompositionLayoutOrientation Orientation;
    public double ItemSpacing;
    public double LineSpacing;
    public VelloCompositionLayoutThickness Padding;
    public VelloCompositionLayoutAlignment LineAlignment;
    public VelloCompositionLayoutAlignment CrossAlignment;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionWrapLayoutLine
{
    public uint LineIndex;
    public uint Start;
    public uint Count;
    public double PrimaryOffset;
    public double PrimaryLength;
}

internal enum VelloCompositionGridTrackKind : uint
{
    Fixed = 0,
    Auto = 1,
    Star = 2,
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionGridTrack
{
    public VelloCompositionGridTrackKind Kind;
    public double Value;
    public double Min;
    public double Max;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionGridLayoutChild
{
    public VelloCompositionLayoutConstraints Constraints;
    public ushort Column;
    public ushort ColumnSpan;
    public ushort Row;
    public ushort RowSpan;
    public VelloCompositionLayoutThickness Margin;
    public VelloCompositionLayoutAlignment HorizontalAlignment;
    public VelloCompositionLayoutAlignment VerticalAlignment;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionGridLayoutOptions
{
    public VelloCompositionLayoutThickness Padding;
    public double ColumnSpacing;
    public double RowSpacing;
}

internal enum VelloCompositionDockSide : uint
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3,
    Fill = 4,
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionDockLayoutChild
{
    public VelloCompositionLayoutConstraints Constraints;
    public VelloCompositionLayoutThickness Margin;
    public VelloCompositionDockSide Side;
    public VelloCompositionLayoutAlignment HorizontalAlignment;
    public VelloCompositionLayoutAlignment VerticalAlignment;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionDockLayoutOptions
{
    public VelloCompositionLayoutThickness Padding;
    public double Spacing;
    public uint LastChildFill;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionDirtyRegion
{
    public double MinX;
    public double MaxX;
    public double MinY;
    public double MaxY;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionColor
{
    public float R;
    public float G;
    public float B;
    public float A;
}

internal enum VelloCompositionShaderKind : uint
{
    Solid = 0,
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionShaderDescriptor
{
    public VelloCompositionShaderKind Kind;
    public VelloCompositionColor Solid;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionMaterialDescriptor
{
    public uint Shader;
    public float Opacity;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionTimelineGroupConfig
{
    public float Speed;
    public int Autoplay;
}

internal enum VelloCompositionTimelineDirtyKind : uint
{
    None = 0,
    Point = 1,
    Bounds = 2,
}

internal enum VelloCompositionTimelineRepeat : uint
{
    Once = 0,
    Loop = 1,
    PingPong = 2,
}

internal enum VelloCompositionTimelineEasing : uint
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

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionTimelineDirtyBinding
{
    public VelloCompositionTimelineDirtyKind Kind;
    public uint Reserved;
    public double X;
    public double Y;
    public double MinX;
    public double MaxX;
    public double MinY;
    public double MaxY;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionTimelineEasingTrackDesc
{
    public uint NodeId;
    public ushort ChannelId;
    public ushort Reserved;
    public VelloCompositionTimelineRepeat Repeat;
    public VelloCompositionTimelineEasing Easing;
    public float StartValue;
    public float EndValue;
    public float Duration;
    public VelloCompositionTimelineDirtyBinding DirtyBinding;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionTimelineSpringTrackDesc
{
    public uint NodeId;
    public ushort ChannelId;
    public ushort Reserved;
    public float Stiffness;
    public float Damping;
    public float Mass;
    public float StartValue;
    public float InitialVelocity;
    public float TargetValue;
    public float RestVelocity;
    public float RestOffset;
    public VelloCompositionTimelineDirtyBinding DirtyBinding;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionTimelineSample
{
   public uint TrackId;
   public uint NodeId;
   public ushort ChannelId;
   public ushort Flags;
   public float Value;
   public float Velocity;
   public float Progress;
}

internal enum VelloCompositionFrozenKind : uint
{
    None = 0,
    Leading = 1,
    Trailing = 2,
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionVirtualRowMetric
{
    public uint NodeId;
    public double Height;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionVirtualColumnStrip
{
    public double Offset;
    public double Width;
    public VelloCompositionFrozenKind Frozen;
    public uint Key;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionRowViewportMetrics
{
    public double ScrollOffset;
    public double ViewportExtent;
    public double Overscan;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionColumnViewportMetrics
{
    public double ScrollOffset;
    public double ViewportExtent;
    public double Overscan;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionColumnSlice
{
    public uint PrimaryStart;
    public uint PrimaryCount;
    public uint FrozenLeading;
    public uint FrozenTrailing;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionVirtualizerTelemetry
{
    public uint RowsTotal;
    public uint WindowLength;
    public uint Reused;
    public uint Adopted;
    public uint Allocated;
    public uint Recycled;
    public uint ActiveBuffers;
    public uint FreeBuffers;
    public uint Evicted;
}

internal enum VelloCompositionRowAction : uint
{
    Reuse = 0,
    Adopt = 1,
    Allocate = 2,
    Recycle = 3,
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionRowPlanEntry
{
    public uint NodeId;
    public uint BufferId;
    public double Top;
    public float Height;
    public VelloCompositionRowAction Action;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloCompositionRowWindow
{
    public uint StartIndex;
    public uint EndIndex;
    public double TotalHeight;
}
