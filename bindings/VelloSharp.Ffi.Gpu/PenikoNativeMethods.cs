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

    [LibraryImport(LibraryName, EntryPoint = "peniko_image_data_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint peniko_image_data_create(PenikoImageFormat format, PenikoImageAlphaType alpha, uint width, uint height, IntPtr pixels, nuint stride);

    [LibraryImport(LibraryName, EntryPoint = "peniko_image_data_create_from_vello")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint peniko_image_data_create_from_vello(IntPtr image);

    [LibraryImport(LibraryName, EntryPoint = "peniko_image_data_clone")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint peniko_image_data_clone(IntPtr image);

    [LibraryImport(LibraryName, EntryPoint = "peniko_image_data_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void peniko_image_data_destroy(IntPtr image);

    [LibraryImport(LibraryName, EntryPoint = "peniko_image_data_get_info")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial PenikoStatus peniko_image_data_get_info(IntPtr image, out PenikoImageInfo info);

    [LibraryImport(LibraryName, EntryPoint = "peniko_image_data_copy_pixels")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial PenikoStatus peniko_image_data_copy_pixels(IntPtr image, IntPtr destination, nuint destinationSize);

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

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_create_image")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint peniko_brush_create_image(PenikoImageBrushParams parameters);

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

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_get_image")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial PenikoStatus peniko_brush_get_image(nint brush, out PenikoImageBrushParams parameters, out nint image);

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_serialize")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial PenikoStatus peniko_brush_serialize(nint brush, out PenikoSerializedBrush serialized, PenikoColorStop* stops, nuint capacity, out nuint length);

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_with_alpha")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial PenikoStatus peniko_brush_with_alpha(nint brush, float alpha);

    [LibraryImport(LibraryName, EntryPoint = "peniko_brush_multiply_alpha")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial PenikoStatus peniko_brush_multiply_alpha(nint brush, float alpha);

    [LibraryImport(LibraryName, EntryPoint = "peniko_color_space_transfer_fn_srgb")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial PenikoStatus peniko_color_space_transfer_fn_srgb(out PenikoColorSpaceTransferFn transferFn);

    [LibraryImport(LibraryName, EntryPoint = "peniko_color_space_transfer_fn_linear_srgb")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial PenikoStatus peniko_color_space_transfer_fn_linear_srgb(out PenikoColorSpaceTransferFn transferFn);

    [LibraryImport(LibraryName, EntryPoint = "peniko_color_space_transfer_fn_display_p3")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial PenikoStatus peniko_color_space_transfer_fn_display_p3(out PenikoColorSpaceTransferFn transferFn);

    [LibraryImport(LibraryName, EntryPoint = "peniko_color_space_xyz_srgb")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial PenikoStatus peniko_color_space_xyz_srgb(out PenikoColorSpaceXyz xyz);

    [LibraryImport(LibraryName, EntryPoint = "peniko_color_space_xyz_linear_srgb")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial PenikoStatus peniko_color_space_xyz_linear_srgb(out PenikoColorSpaceXyz xyz);

    [LibraryImport(LibraryName, EntryPoint = "peniko_color_space_xyz_display_p3")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial PenikoStatus peniko_color_space_xyz_display_p3(out PenikoColorSpaceXyz xyz);
}
