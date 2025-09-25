using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VelloSharp;

internal static partial class NativeMethods
{
    internal const string LibraryName = "vello_ffi";

    [LibraryImport(LibraryName, EntryPoint = "vello_renderer_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_renderer_create(uint width, uint height);

    [LibraryImport(LibraryName, EntryPoint = "vello_renderer_create_with_options")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_renderer_create_with_options(
        uint width,
        uint height,
        VelloRendererOptions options);

    [LibraryImport(LibraryName, EntryPoint = "vello_renderer_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_renderer_destroy(IntPtr renderer);

    [LibraryImport(LibraryName, EntryPoint = "vello_renderer_resize")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_renderer_resize(IntPtr renderer, uint width, uint height);

    [LibraryImport(LibraryName, EntryPoint = "vello_renderer_render")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_renderer_render(
        IntPtr renderer,
        IntPtr scene,
        VelloRenderParams parameters,
        IntPtr buffer,
        nuint stride,
        nuint bufferSize);

    [LibraryImport(LibraryName, EntryPoint = "vello_scene_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_scene_create();

    [LibraryImport(LibraryName, EntryPoint = "vello_scene_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_scene_destroy(IntPtr scene);

    [LibraryImport(LibraryName, EntryPoint = "vello_scene_reset")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_scene_reset(IntPtr scene);

    [LibraryImport(LibraryName, EntryPoint = "vello_scene_fill_path")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_scene_fill_path(
        IntPtr scene,
        VelloFillRule fill,
        VelloAffine transform,
        VelloColor color,
        VelloPathElement* elements,
        nuint elementCount);

    [LibraryImport(LibraryName, EntryPoint = "vello_scene_stroke_path")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_scene_stroke_path(
        IntPtr scene,
        VelloStrokeStyle style,
        VelloAffine transform,
        VelloColor color,
        VelloPathElement* elements,
        nuint elementCount);

    [LibraryImport(LibraryName, EntryPoint = "vello_scene_fill_path_brush")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_scene_fill_path_brush(
        IntPtr scene,
        VelloFillRule fill,
        VelloAffine transform,
        VelloBrush brush,
        VelloAffine* brushTransform,
        VelloPathElement* elements,
        nuint elementCount);

    [LibraryImport(LibraryName, EntryPoint = "vello_scene_stroke_path_brush")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_scene_stroke_path_brush(
        IntPtr scene,
        VelloStrokeStyle style,
        VelloAffine transform,
        VelloBrush brush,
        VelloAffine* brushTransform,
        VelloPathElement* elements,
        nuint elementCount);

    [LibraryImport(LibraryName, EntryPoint = "vello_scene_push_layer")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_scene_push_layer(
        IntPtr scene,
        VelloLayerParams layer);

    [LibraryImport(LibraryName, EntryPoint = "vello_scene_push_luminance_mask_layer")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_scene_push_luminance_mask_layer(
        IntPtr scene,
        float alpha,
        VelloAffine transform,
        VelloPathElement* elements,
        nuint elementCount);

    [LibraryImport(LibraryName, EntryPoint = "vello_scene_pop_layer")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_scene_pop_layer(IntPtr scene);

    [LibraryImport(LibraryName, EntryPoint = "vello_scene_draw_blurred_rounded_rect")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_scene_draw_blurred_rounded_rect(
        IntPtr scene,
        VelloAffine transform,
        VelloRect rect,
        VelloColor color,
        double radius,
        double stdDev);

    [LibraryImport(LibraryName, EntryPoint = "vello_image_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_image_create(
        VelloRenderFormat format,
        VelloImageAlphaMode alpha,
        uint width,
        uint height,
        IntPtr pixels,
        nuint stride);

    [LibraryImport(LibraryName, EntryPoint = "vello_image_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_image_destroy(IntPtr image);

    [LibraryImport(LibraryName, EntryPoint = "vello_scene_draw_image")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_scene_draw_image(
        IntPtr scene,
        VelloImageBrushParams brush,
        VelloAffine transform);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_font_create(
        IntPtr data,
        nuint length,
        uint index);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_font_destroy(IntPtr font);

    [LibraryImport(LibraryName, EntryPoint = "vello_scene_draw_glyph_run")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_scene_draw_glyph_run(
        IntPtr scene,
        IntPtr font,
        VelloGlyph* glyphs,
        nuint glyphCount,
        VelloGlyphRunOptions options);

    [LibraryImport(LibraryName, EntryPoint = "vello_render_context_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_render_context_create();

    [LibraryImport(LibraryName, EntryPoint = "vello_render_context_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_render_context_destroy(IntPtr context);

    [LibraryImport(LibraryName, EntryPoint = "vello_render_surface_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_render_surface_create(
        IntPtr context,
        VelloSurfaceDescriptor descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_render_surface_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_render_surface_destroy(IntPtr surface);

    [LibraryImport(LibraryName, EntryPoint = "vello_render_surface_resize")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_render_surface_resize(
        IntPtr surface,
        uint width,
        uint height);

    [LibraryImport(LibraryName, EntryPoint = "vello_surface_renderer_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_surface_renderer_create(
        IntPtr surface,
        VelloRendererOptions options);

    [LibraryImport(LibraryName, EntryPoint = "vello_surface_renderer_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_surface_renderer_destroy(IntPtr renderer);

    [LibraryImport(LibraryName, EntryPoint = "vello_surface_renderer_render")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_surface_renderer_render(
        IntPtr renderer,
        IntPtr surface,
        IntPtr scene,
        VelloRenderParams parameters);

    [LibraryImport(LibraryName, EntryPoint = "vello_last_error_message")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_last_error_message();
}
