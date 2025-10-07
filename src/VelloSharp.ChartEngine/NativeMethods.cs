using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VelloSharp.ChartEngine;

internal static partial class NativeMethods
{
    private const string LibraryName = "vello_chart_engine";

    [LibraryImport(LibraryName, EntryPoint = "vello_chart_engine_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint vello_chart_engine_create(VelloChartEngineOptions options);

    [LibraryImport(LibraryName, EntryPoint = "vello_chart_engine_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_chart_engine_destroy(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_chart_engine_publish_samples")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloChartEngineStatus vello_chart_engine_publish_samples(
        nint handle,
        VelloChartSamplePoint* samples,
        nuint count);

    [LibraryImport(LibraryName, EntryPoint = "vello_chart_engine_render")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloChartEngineStatus vello_chart_engine_render(
        nint handle,
        nint sceneHandle,
        uint width,
        uint height,
        out VelloChartFrameStats stats);

    [LibraryImport(LibraryName, EntryPoint = "vello_chart_engine_last_error_message")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint vello_chart_engine_last_error_message();

    [LibraryImport(LibraryName, EntryPoint = "vello_chart_engine_set_trace_callback")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloChartEngineStatus vello_chart_engine_set_trace_callback(
        delegate* unmanaged[Cdecl]<ChartTraceLevel, long, nint, nint, nint, void> callback);

    [LibraryImport(LibraryName, EntryPoint = "vello_chart_engine_set_palette")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloChartEngineStatus vello_chart_engine_set_palette(
        nint handle,
        VelloChartColor* palette,
        nuint count);

    [LibraryImport(LibraryName, EntryPoint = "vello_chart_engine_set_series_definitions")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloChartEngineStatus vello_chart_engine_set_series_definitions(
        nint handle,
        VelloChartSeriesDefinition* definitions,
        nuint count);

    [LibraryImport(LibraryName, EntryPoint = "vello_chart_engine_set_composition")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloChartEngineStatus vello_chart_engine_set_composition(
        nint handle,
        VelloChartComposition* composition);

    [LibraryImport(LibraryName, EntryPoint = "vello_chart_engine_apply_series_overrides")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloChartEngineStatus vello_chart_engine_apply_series_overrides(
        nint handle,
        VelloChartSeriesOverride* overrides,
        nuint count);

    [LibraryImport(LibraryName, EntryPoint = "vello_chart_engine_last_frame_metadata")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloChartEngineStatus vello_chart_engine_last_frame_metadata(
        nint handle,
        out VelloChartFrameMetadata metadata);
}

internal enum VelloChartEngineStatus
{
    Success = 0,
    NullPointer = 1,
    InvalidArgument = 2,
    Unknown = 255,
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloChartEngineOptions
{
    public double VisibleDurationSeconds;
    public double VerticalPaddingRatio;
    public double StrokeWidth;
    public uint ShowAxes;
    public nuint PaletteLength;
    public nint Palette;
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct VelloChartSamplePoint
{
    public readonly uint SeriesId;
    public readonly double TimestampSeconds;
    public readonly double Value;

    public VelloChartSamplePoint(uint seriesId, double timestampSeconds, double value)
    {
        SeriesId = seriesId;
        TimestampSeconds = timestampSeconds;
        Value = value;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct VelloChartColor
{
    public readonly byte R;
    public readonly byte G;
    public readonly byte B;
    public readonly byte A;

    public VelloChartColor(byte r, byte g, byte b, byte a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloChartFrameStats
{
    public float CpuTimeMs;
    public float GpuTimeMs;
    public float QueueLatencyMs;
    public uint EncodedPaths;
    public long TimestampMs;
}

internal enum ChartTraceLevel
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warn = 3,
    Error = 4,
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloChartSeriesOverride
{
    public uint SeriesId;
    public SeriesOverrideFlags Flags;
    public nint Label;
    public nuint LabelLength;
    public double StrokeWidth;
    public VelloChartColor Color;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloChartAxisTickMetadata
{
    public double Position;
   public nint Label;
    public nuint LabelLength;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloChartCompositionPane
{
    public nint Id;
    public nuint IdLength;
    public double HeightRatio;
    public uint ShareXAxisWithPrimary;
    public nint SeriesIds;
    public nuint SeriesIdCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloChartComposition
{
    public nint Panes;
    public nuint PaneCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloChartPaneMetadata
{
    public nint Id;
    public nuint IdLength;
    public uint ShareXAxisWithPrimary;
    public double PlotLeft;
    public double PlotTop;
    public double PlotWidth;
    public double PlotHeight;
    public double ValueMin;
    public double ValueMax;
    public double DirtyTimeMin;
    public double DirtyTimeMax;
    public double DirtyValueMin;
    public double DirtyValueMax;
    public nint ValueTicks;
    public nuint ValueTickCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloChartSeriesMetadataNative
{
    public uint SeriesId;
    public uint Kind;
    public VelloChartColor Color;
    public nint Label;
    public nuint LabelLength;
    public double StrokeWidth;
    public double FillOpacity;
    public double MarkerSize;
    public double BarWidthSeconds;
    public double Baseline;
    public uint PaneIndex;
    public uint BandLowerSeriesId;
    public uint HeatmapBucketIndex;
    public uint HeatmapBucketCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloChartFrameMetadata
{
    public double RangeStart;
    public double RangeEnd;
    public double ValueMin;
    public double ValueMax;
    public double PlotLeft;
    public double PlotTop;
    public double PlotWidth;
    public double PlotHeight;
    public nint TimeTicks;
    public nuint TimeTickCount;
    public nint ValueTicks;
    public nuint ValueTickCount;
    public nint Series;
    public nuint SeriesCount;
    public nint Panes;
    public nuint PaneCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloChartSeriesDefinition
{
    public uint SeriesId;
    public uint Kind;
    public SeriesDefinitionFlags Flags;
    public uint Reserved;
    public double Baseline;
    public double FillOpacity;
    public double StrokeWidth;
    public double MarkerSize;
    public double BarWidthSeconds;
    public uint BandLowerSeriesId;
    public uint HeatmapBucketIndex;
    public uint HeatmapBucketCount;
}

