using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VelloSharp;

internal static partial class SparseNativeMethods
{
    internal const string LibraryName = "vello_sparse_ffi";

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_last_error_message")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_sparse_last_error_message();

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_render_context_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_sparse_render_context_create(ushort width, ushort height);

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_render_context_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_sparse_render_context_destroy(IntPtr context);

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_render_context_reset")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloSparseStatus vello_sparse_render_context_reset(IntPtr context);

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_render_context_flush")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloSparseStatus vello_sparse_render_context_flush(IntPtr context);

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_render_context_get_size")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloSparseStatus vello_sparse_render_context_get_size(
        IntPtr context,
        out ushort width,
        out ushort height);

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_render_context_set_fill_rule")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloSparseStatus vello_sparse_render_context_set_fill_rule(
        IntPtr context,
        VelloFillRule fillRule);

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_render_context_set_transform")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloSparseStatus vello_sparse_render_context_set_transform(
        IntPtr context,
        VelloAffine transform);

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_render_context_reset_transform")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloSparseStatus vello_sparse_render_context_reset_transform(IntPtr context);

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_render_context_set_paint_transform")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloSparseStatus vello_sparse_render_context_set_paint_transform(
        IntPtr context,
        VelloAffine transform);

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_render_context_reset_paint_transform")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloSparseStatus vello_sparse_render_context_reset_paint_transform(IntPtr context);

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_render_context_set_aliasing_threshold")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloSparseStatus vello_sparse_render_context_set_aliasing_threshold(
        IntPtr context,
        int threshold);

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_render_context_set_solid_paint")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloSparseStatus vello_sparse_render_context_set_solid_paint(
        IntPtr context,
        VelloColor color);

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_render_context_set_stroke")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloSparseStatus vello_sparse_render_context_set_stroke(
        IntPtr context,
        VelloStrokeStyle stroke);

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_render_context_fill_path")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloSparseStatus vello_sparse_render_context_fill_path(
        IntPtr context,
        VelloPathElement* elements,
        nuint elementCount);

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_render_context_stroke_path")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloSparseStatus vello_sparse_render_context_stroke_path(
        IntPtr context,
        VelloPathElement* elements,
        nuint elementCount);

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_render_context_fill_rect")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloSparseStatus vello_sparse_render_context_fill_rect(
        IntPtr context,
        VelloRect rect);

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_render_context_stroke_rect")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloSparseStatus vello_sparse_render_context_stroke_rect(
        IntPtr context,
        VelloRect rect);

    [LibraryImport(LibraryName, EntryPoint = "vello_sparse_render_context_render_to_buffer")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloSparseStatus vello_sparse_render_context_render_to_buffer(
        IntPtr context,
        IntPtr buffer,
        nuint length,
        ushort width,
        ushort height,
        VelloSparseRenderMode mode);
}
