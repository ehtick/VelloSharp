using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VelloSharp;

internal static partial class KurboNativeMethods
{
    private const string LibraryName = "kurbo_ffi";

    [LibraryImport(LibraryName, EntryPoint = "kurbo_last_error_message")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint kurbo_last_error_message();

    [LibraryImport(LibraryName, EntryPoint = "kurbo_affine_identity")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial KurboStatus kurbo_affine_identity(out KurboAffine affine);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_affine_mul")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial KurboStatus kurbo_affine_mul(KurboAffine lhs, KurboAffine rhs, out KurboAffine result);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_affine_invert")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial KurboStatus kurbo_affine_invert(KurboAffine affine, out KurboAffine inverse);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_affine_transform_point")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial KurboStatus kurbo_affine_transform_point(KurboAffine affine, KurboPoint point, out KurboPoint result);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_affine_transform_vec")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial KurboStatus kurbo_affine_transform_vec(KurboAffine affine, KurboVec2 vector, out KurboVec2 result);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_bez_path_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial nint kurbo_bez_path_create();

    [LibraryImport(LibraryName, EntryPoint = "kurbo_bez_path_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial void kurbo_bez_path_destroy(nint path);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_bez_path_clear")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial KurboStatus kurbo_bez_path_clear(nint path);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_bez_path_move_to")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial KurboStatus kurbo_bez_path_move_to(nint path, KurboPoint point);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_bez_path_line_to")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial KurboStatus kurbo_bez_path_line_to(nint path, KurboPoint point);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_bez_path_quad_to")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial KurboStatus kurbo_bez_path_quad_to(nint path, KurboPoint control, KurboPoint point);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_bez_path_cubic_to")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial KurboStatus kurbo_bez_path_cubic_to(nint path, KurboPoint control1, KurboPoint control2, KurboPoint point);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_bez_path_close")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial KurboStatus kurbo_bez_path_close(nint path);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_bez_path_len")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial KurboStatus kurbo_bez_path_len(nint path, out nuint length);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_bez_path_copy_elements")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial KurboStatus kurbo_bez_path_copy_elements(nint path, KurboPathElement* elements, nuint capacity, out nuint length);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_bez_path_from_elements")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial nint kurbo_bez_path_from_elements(KurboPathElement* elements, nuint length);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_bez_path_apply_affine")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial KurboStatus kurbo_bez_path_apply_affine(nint path, KurboAffine affine);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_bez_path_translate")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial KurboStatus kurbo_bez_path_translate(nint path, KurboVec2 offset);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_bez_path_stroke")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial KurboStatus kurbo_bez_path_stroke(nint path, KurboStrokeStyle style, double tolerance, out nint result);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_bez_path_dash")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial KurboStatus kurbo_bez_path_dash(nint path, double dashOffset, double* dashPattern, nuint dashLength, out nint result);

    [LibraryImport(LibraryName, EntryPoint = "kurbo_bez_path_bounds")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial KurboStatus kurbo_bez_path_bounds(nint path, out KurboRect bounds);
}
