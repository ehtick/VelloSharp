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
