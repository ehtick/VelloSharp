using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VelloSharp.TreeDataGrid;

internal static partial class NativeMethods
{
    private const string LibraryName = "vello_tree_datagrid";

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_last_error_message")]
    private static partial nint vello_tdg_last_error_message_ptr();

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_shader_register")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool vello_tdg_shader_register(
        uint handle,
        VelloTdgShaderDescriptor descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_shader_unregister")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_tdg_shader_unregister(uint handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_material_register")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool vello_tdg_material_register(
        uint handle,
        VelloTdgMaterialDescriptor descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_material_unregister")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_tdg_material_unregister(uint handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_render_hook_register")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool vello_tdg_render_hook_register(
        uint handle,
        VelloTdgRenderHookDescriptor descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_render_hook_unregister")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_tdg_render_hook_unregister(uint handle);

    internal static string? GetLastError()
    {
        var ptr = vello_tdg_last_error_message_ptr();
        return ptr == nint.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_model_create")]
    internal static partial nint vello_tdg_model_create();

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_model_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_tdg_model_destroy(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_model_clear")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_tdg_model_clear(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_model_attach_roots")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial bool vello_tdg_model_attach_roots(
        nint handle,
        VelloTdgNodeDescriptor* descriptors,
        nuint descriptorCount);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_model_attach_children")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial bool vello_tdg_model_attach_children(
        nint handle,
        uint parentId,
        VelloTdgNodeDescriptor* descriptors,
        nuint descriptorCount);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_model_set_expanded")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial bool vello_tdg_model_set_expanded(
        nint handle,
        uint nodeId,
        uint expanded);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_model_set_selected")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial bool vello_tdg_model_set_selected(
        nint handle,
        uint nodeId,
        VelloTdgSelectionMode mode);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_model_select_range")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial bool vello_tdg_model_select_range(
        nint handle,
        uint anchorId,
        uint focusId);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_model_diff_count")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nuint vello_tdg_model_diff_count(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_model_copy_diffs")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial nuint vello_tdg_model_copy_diffs(
        nint handle,
        VelloTdgModelDiff* diffs,
        nuint length);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_model_selection_diff_count")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nuint vello_tdg_model_selection_diff_count(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_model_copy_selection_diffs")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial nuint vello_tdg_model_copy_selection_diffs(
        nint handle,
        VelloTdgSelectionDiff* diffs,
        nuint length);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_model_dequeue_materialization")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial bool vello_tdg_model_dequeue_materialization(
        nint handle,
        uint* nodeId);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_model_node_metadata")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial bool vello_tdg_model_node_metadata(
        nint handle,
        uint nodeId,
        VelloTdgNodeMetadata* metadata);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_virtualizer_create")]
    internal static partial nint vello_tdg_virtualizer_create();

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_virtualizer_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_tdg_virtualizer_destroy(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_virtualizer_clear")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_tdg_virtualizer_clear(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_virtualizer_set_rows")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial bool vello_tdg_virtualizer_set_rows(
        nint handle,
        VelloTdgRowMetric* rows,
        nuint length);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_virtualizer_set_columns")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial bool vello_tdg_virtualizer_set_columns(
        nint handle,
        VelloTdgColumnMetric* columns,
        nuint length);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_virtualizer_plan")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial bool vello_tdg_virtualizer_plan(
        nint handle,
        VelloTdgViewportMetrics metrics,
        VelloTdgColumnSlice* slice);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_virtualizer_copy_plan")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial nuint vello_tdg_virtualizer_copy_plan(
        nint handle,
        VelloTdgRowPlanEntry* plan,
        nuint length);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_virtualizer_copy_recycle")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial nuint vello_tdg_virtualizer_copy_recycle(
        nint handle,
        VelloTdgRowPlanEntry* plan,
        nuint length);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_virtualizer_window")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial bool vello_tdg_virtualizer_window(
        nint handle,
        VelloTdgRowWindow* window);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_virtualizer_telemetry")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial bool vello_tdg_virtualizer_telemetry(
        nint handle,
        VelloTdgVirtualizerTelemetry* telemetry);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_template_program_create")]
    internal static unsafe partial nint vello_tdg_template_program_create(
        VelloTdgTemplateInstruction* instructions,
        nuint instructionCount);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_template_program_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_tdg_template_program_destroy(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_template_program_encode_pane")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial bool vello_tdg_template_program_encode_pane(
        nint program,
        nint cache,
        uint nodeId,
        VelloTdgTemplatePaneKind paneKind,
        VelloTdgColumnPlan* columns,
        nuint columnCount,
        VelloTdgTemplateBinding* bindings,
        nuint bindingCount);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_renderer_create")]
    internal static partial nint vello_tdg_renderer_create(VelloTdgRendererOptions options);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_renderer_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_tdg_renderer_destroy(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_renderer_begin_frame")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial bool vello_tdg_renderer_begin_frame(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_renderer_record_gpu_summary")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial bool vello_tdg_renderer_record_gpu_summary(
        nint handle,
        VelloTdgGpuTimestampSummary summary);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_renderer_end_frame")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial bool vello_tdg_renderer_end_frame(
        nint handle,
        float gpuTimeMs,
        float queueTimeMs,
        VelloTdgFrameStats* stats);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_scene_encode_row")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial bool vello_tdg_scene_encode_row(
        nint cache,
        uint nodeId,
        VelloTdgRowVisual* visual,
        VelloTdgColumnPlan* columns,
        nuint columnCount);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_scene_encode_group_header")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial bool vello_tdg_scene_encode_group_header(
        nint cache,
        uint nodeId,
        VelloTdgGroupHeaderVisual* visual,
        VelloTdgColumnPlan* columns,
        nuint columnCount);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_scene_encode_summary")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial bool vello_tdg_scene_encode_summary(
        nint cache,
        uint nodeId,
        VelloTdgSummaryVisual* visual,
        VelloTdgColumnPlan* columns,
        nuint columnCount);

    [LibraryImport(LibraryName, EntryPoint = "vello_tdg_scene_encode_chrome")]
    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial bool vello_tdg_scene_encode_chrome(
        nint cache,
        uint nodeId,
        VelloTdgRowChromeVisual* visual,
        VelloTdgColumnPlan* columns,
        nuint columnCount);

    internal enum VelloTdgShaderKind : uint
    {
        Solid = 0,
    }

    internal enum VelloTdgRenderHookKind : uint
    {
        FillRounded = 0,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgShaderDescriptor
    {
        public VelloTdgShaderKind Kind;
        public VelloTdgColor Solid;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgMaterialDescriptor
    {
        public uint Shader;
        public float Opacity;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgRenderHookDescriptor
    {
        public VelloTdgRenderHookKind Kind;
        public uint Material;
        public double Inset;
        public double Radius;
    }

    internal enum VelloTdgRowKind : uint
    {
        Data = 0,
        GroupHeader = 1,
        Summary = 2,
    }

    internal enum VelloTdgModelDiffKind : uint
    {
        Inserted = 0,
        Removed = 1,
        Expanded = 2,
        Collapsed = 3,
    }

    internal enum VelloTdgSelectionMode : uint
    {
        Replace = 0,
        Add = 1,
        Toggle = 2,
        Range = 3,
    }

    internal enum VelloTdgTemplateOpCode : uint
    {
        OpenNode = 0,
        CloseNode = 1,
        SetProperty = 2,
        BindProperty = 3,
    }

    internal enum VelloTdgTemplateNodeKind : uint
    {
        Templates = 0,
        RowTemplate = 1,
        GroupHeaderTemplate = 2,
        SummaryTemplate = 3,
        ChromeTemplate = 4,
        PaneTemplate = 5,
        CellTemplate = 6,
        Stack = 7,
        Text = 8,
        Rectangle = 9,
        Image = 10,
        ContentPresenter = 11,
        AccessText = 12,
        TextBox = 13,
        Unknown = 14,
    }

    internal enum VelloTdgTemplateValueKind : uint
    {
        String = 0,
        Number = 1,
        Boolean = 2,
        Binding = 3,
        Color = 4,
        Unknown = 5,
    }

    internal enum VelloTdgTemplatePaneKind : uint
    {
        Primary = 0,
        Leading = 1,
        Trailing = 2,
    }

    internal enum VelloTdgFrozenKind : uint
    {
        None = 0,
        Leading = 1,
        Trailing = 2,
    }

    internal enum VelloTdgRowAction : uint
    {
        Reuse = 0,
        Adopt = 1,
        Allocate = 2,
        Recycle = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgNodeDescriptor
    {
        public ulong Key;
        public VelloTdgRowKind RowKind;
        public float Height;
        public uint HasChildren;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgModelDiff
    {
        public uint NodeId;
        public uint ParentId;
        public uint Index;
        public uint Depth;
        public VelloTdgRowKind RowKind;
        public VelloTdgModelDiffKind Kind;
        public float Height;
        public uint HasChildren;
        public uint IsExpanded;
        public ulong Key;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgSelectionDiff
    {
        public uint NodeId;
        public uint IsSelected;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgNodeMetadata
    {
        public ulong Key;
        public uint Depth;
        public float Height;
        public VelloTdgRowKind RowKind;
        public uint IsExpanded;
        public uint IsSelected;
        public uint HasChildren;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgRowMetric
    {
        public uint NodeId;
        public float Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgColumnMetric
    {
        public double Offset;
        public double Width;
        public VelloTdgFrozenKind Frozen;
        public uint Key;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgViewportMetrics
    {
        public double RowScrollOffset;
        public double RowViewportHeight;
        public double RowOverscan;
        public double ColumnScrollOffset;
        public double ColumnViewportWidth;
        public double ColumnOverscan;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgColumnSlice
    {
        public uint PrimaryStart;
        public uint PrimaryCount;
        public uint FrozenLeading;
        public uint FrozenTrailing;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgRowPlanEntry
    {
        public uint NodeId;
        public uint BufferId;
        public double Top;
        public float Height;
        public VelloTdgRowAction Action;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgRowWindow
    {
        public uint StartIndex;
        public uint EndIndex;
        public double TotalHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgVirtualizerTelemetry
    {
        public uint RowsTotal;
        public uint WindowLen;
        public uint Reused;
        public uint Adopted;
        public uint Allocated;
        public uint Recycled;
        public uint ActiveBuffers;
        public uint FreeBuffers;
        public uint Evicted;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgRendererOptions
    {
        public float TargetFps;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgGpuTimestampSummary
    {
        public float GpuTimeMs;
        public float QueueTimeMs;
        public uint SampleCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgFrameStats
    {
        public ulong FrameIndex;
        public float CpuTimeMs;
        public float GpuTimeMs;
        public float QueueTimeMs;
        public float FrameIntervalMs;
        public uint GpuSampleCount;
        public long TimestampMs;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgTemplateInstruction
    {
        public VelloTdgTemplateOpCode OpCode;
        public VelloTdgTemplateNodeKind NodeKind;
        public VelloTdgTemplateValueKind ValueKind;
        public IntPtr Property;
        public IntPtr Value;
        public double NumberValue;
        public int BooleanValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgTemplateBinding
    {
        public IntPtr Path;
        public VelloTdgTemplateValueKind Kind;
        public double NumberValue;
        public int BooleanValue;
        public IntPtr StringValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgColor
    {
        public float R;
        public float G;
        public float B;
        public float A;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgRowVisual
    {
        public double Width;
        public double Height;
        public uint Depth;
        public double Indent;
        public VelloTdgColor Background;
        public VelloTdgColor HoverBackground;
        public VelloTdgColor SelectionFill;
        public VelloTdgColor Outline;
        public float OutlineWidth;
        public VelloTdgColor Stripe;
        public float StripeWidth;
        public uint IsSelected;
        public uint IsHovered;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgGroupHeaderVisual
    {
        public double Width;
        public double Height;
        public uint Depth;
        public double Indent;
        public VelloTdgColor Background;
        public VelloTdgColor Accent;
        public VelloTdgColor Outline;
        public float OutlineWidth;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgSummaryVisual
    {
        public double Width;
        public double Height;
        public VelloTdgColor Highlight;
        public VelloTdgColor Background;
        public VelloTdgColor Outline;
        public float OutlineWidth;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgRowChromeVisual
    {
        public double Width;
        public double Height;
        public VelloTdgColor GridColor;
        public float GridWidth;
        public uint FrozenLeading;
        public uint FrozenTrailing;
        public VelloTdgColor FrozenFill;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloTdgColumnPlan
    {
        public double Offset;
        public double Width;
        public VelloTdgFrozenKind Frozen;
        public uint Key;
    }
    }
