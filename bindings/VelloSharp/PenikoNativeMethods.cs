using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VelloSharp;

internal static partial class PenikoNativeMethods
{
    private const string LibraryName = "peniko_ffi";

    [LibraryImport(LibraryName, EntryPoint = "peniko_last_error_message")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint peniko_last_error_message();

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_create_solid")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint peniko_brush_create_solid(VelloColor color);

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_create_linear")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial nint peniko_brush_create_linear(PenikoLinearGradient gradient, PenikoExtend extend, PenikoColorStop* stops, nuint count);

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_create_radial")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial nint peniko_brush_create_radial(PenikoRadialGradient gradient, PenikoExtend extend, PenikoColorStop* stops, nuint count);

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_create_sweep")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial nint peniko_brush_create_sweep(PenikoSweepGradient gradient, PenikoExtend extend, PenikoColorStop* stops, nuint count);

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_clone")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint peniko_brush_clone(nint brush);

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void peniko_brush_destroy(nint brush);

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_get_kind")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial PenikoStatus peniko_brush_get_kind(nint brush, out PenikoBrushKind kind);

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_get_solid_color")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial PenikoStatus peniko_brush_get_solid_color(nint brush, out VelloColor color);

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_get_gradient_kind")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial PenikoStatus peniko_brush_get_gradient_kind(nint brush, out PenikoGradientKind kind);

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_get_linear_gradient")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial PenikoStatus peniko_brush_get_linear_gradient(nint brush, out PenikoLinearGradient gradient, out PenikoExtend extend, PenikoColorStop* stops, nuint capacity, out nuint length);

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_get_radial_gradient")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial PenikoStatus peniko_brush_get_radial_gradient(nint brush, out PenikoRadialGradient gradient, out PenikoExtend extend, PenikoColorStop* stops, nuint capacity, out nuint length);

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_get_sweep_gradient")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial PenikoStatus peniko_brush_get_sweep_gradient(nint brush, out PenikoSweepGradient gradient, out PenikoExtend extend, PenikoColorStop* stops, nuint capacity, out nuint length);

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_with_alpha")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial PenikoStatus peniko_brush_with_alpha(nint brush, float alpha);

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_multiply_alpha")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial PenikoStatus peniko_brush_multiply_alpha(nint brush, float alpha);
}
