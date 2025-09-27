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

    [LibraryImport(LibraryName, EntryPoint = "vello_image_decode_png")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_image_decode_png(byte* data, nuint length, out IntPtr image);

    [LibraryImport(LibraryName, EntryPoint = "vello_image_encode_png")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_image_encode_png(IntPtr image, byte compression, out IntPtr blob);

    [LibraryImport(LibraryName, EntryPoint = "vello_image_get_info")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_image_get_info(IntPtr image, out VelloImageInfoNative info);

    [LibraryImport(LibraryName, EntryPoint = "vello_image_map_pixels")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_image_map_pixels(IntPtr image, out IntPtr pixels, out nuint length);

    [LibraryImport(LibraryName, EntryPoint = "vello_image_unmap_pixels")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_image_unmap_pixels(IntPtr image);

    [LibraryImport(LibraryName, EntryPoint = "vello_image_resize")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_image_resize(IntPtr image, uint width, uint height, VelloImageQualityMode mode, out IntPtr resizedImage);

    [LibraryImport(LibraryName, EntryPoint = "vello_scene_draw_image")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_scene_draw_image(
        IntPtr scene,
        VelloImageBrushParams brush,
        VelloAffine transform);

    [LibraryImport(LibraryName, EntryPoint = "vello_blob_get_data")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_blob_get_data(IntPtr blob, out VelloBlobDataNative data);

    [LibraryImport(LibraryName, EntryPoint = "vello_blob_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_blob_destroy(IntPtr blob);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_font_create(
        IntPtr data,
        nuint length,
        uint index);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_font_destroy(IntPtr font);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_get_glyph_metrics")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_font_get_glyph_metrics(
        IntPtr font,
        ushort glyphId,
        float fontSize,
        out VelloGlyphMetricsNative metrics);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_get_glyph_outline")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_font_get_glyph_outline(
        IntPtr font,
        ushort glyphId,
        float fontSize,
        float tolerance,
        out IntPtr outlineHandle);

    [LibraryImport(LibraryName, EntryPoint = "vello_glyph_outline_get_data")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_glyph_outline_get_data(IntPtr outlineHandle, out VelloGlyphOutlineData data);

    [LibraryImport(LibraryName, EntryPoint = "vello_glyph_outline_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_glyph_outline_destroy(IntPtr outlineHandle);

    [LibraryImport(LibraryName, EntryPoint = "vello_text_shape_utf16")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_text_shape_utf16(
        IntPtr font,
        ushort* text,
        nuint length,
        float fontSize,
        int isRtl,
        out VelloShapedRunNative run,
        out IntPtr handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_text_shape_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_text_shape_destroy(IntPtr handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_svg_load_from_memory")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_svg_load_from_memory(byte* data, nuint length, float scale);

    [LibraryImport(LibraryName, EntryPoint = "vello_svg_load_from_file", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_svg_load_from_file(string path, float scale);

    [LibraryImport(LibraryName, EntryPoint = "vello_svg_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_svg_destroy(IntPtr svg);

    [LibraryImport(LibraryName, EntryPoint = "vello_svg_get_size")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_svg_get_size(IntPtr svg, out VelloPoint size);

    [LibraryImport(LibraryName, EntryPoint = "vello_svg_render")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_svg_render(IntPtr svg, IntPtr scene, VelloAffine* transform);

    [LibraryImport(LibraryName, EntryPoint = "vello_velato_composition_load_from_memory")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_velato_composition_load_from_memory(byte* data, nuint length);

    [LibraryImport(LibraryName, EntryPoint = "vello_velato_composition_load_from_file", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_velato_composition_load_from_file(string path);

    [LibraryImport(LibraryName, EntryPoint = "vello_velato_composition_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_velato_composition_destroy(IntPtr composition);

    [LibraryImport(LibraryName, EntryPoint = "vello_velato_composition_get_info")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_velato_composition_get_info(
        IntPtr composition,
        out VelloVelatoCompositionInfo info);

    [LibraryImport(LibraryName, EntryPoint = "vello_velato_renderer_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_velato_renderer_create();

    [LibraryImport(LibraryName, EntryPoint = "vello_velato_renderer_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_velato_renderer_destroy(IntPtr renderer);

    [LibraryImport(LibraryName, EntryPoint = "vello_velato_renderer_render")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_velato_renderer_render(
        IntPtr renderer,
        IntPtr composition,
        IntPtr scene,
        double frame,
        double alpha,
        VelloAffine* transform);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_instance_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_wgpu_instance_create(WgpuInstanceDescriptorNative* descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_instance_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_instance_destroy(IntPtr instance);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_instance_request_adapter")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_wgpu_instance_request_adapter(
        IntPtr instance,
        WgpuRequestAdapterOptionsNative* options);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_adapter_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_adapter_destroy(IntPtr adapter);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_adapter_get_info")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_wgpu_adapter_get_info(IntPtr adapter, out WgpuAdapterInfoNative info);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_adapter_get_features")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_wgpu_adapter_get_features(IntPtr adapter, out ulong features);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_adapter_request_device")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_wgpu_adapter_request_device(
        IntPtr adapter,
        WgpuDeviceDescriptorNative* descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_device_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_device_destroy(IntPtr device);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_device_get_features")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_wgpu_device_get_features(IntPtr device, out ulong features);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_device_get_queue")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_wgpu_device_get_queue(IntPtr device);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_device_create_pipeline_cache")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_wgpu_device_create_pipeline_cache(
        IntPtr device,
        WgpuPipelineCacheDescriptorNative* descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_pipeline_cache_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_pipeline_cache_destroy(IntPtr cache);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_pipeline_cache_get_data")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_wgpu_pipeline_cache_get_data(
        IntPtr cache,
        out IntPtr data,
        out nuint length);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_pipeline_cache_free_data")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_pipeline_cache_free_data(IntPtr data, nuint length);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_queue_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_queue_destroy(IntPtr queue);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_surface_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_wgpu_surface_create(IntPtr instance, VelloSurfaceDescriptor descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_surface_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_surface_destroy(IntPtr surface);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_surface_get_preferred_format")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_wgpu_surface_get_preferred_format(
        IntPtr surface,
        IntPtr adapter,
        WgpuTextureFormatNative* outFormat);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_surface_configure")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_wgpu_surface_configure(
        IntPtr surface,
        IntPtr device,
        WgpuSurfaceConfigurationNative* configuration);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_surface_acquire_next_texture")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_wgpu_surface_acquire_next_texture(IntPtr surface);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_surface_texture_create_view")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_wgpu_surface_texture_create_view(
        IntPtr texture,
        WgpuTextureViewDescriptorNative* descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_texture_view_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_texture_view_destroy(IntPtr view);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_surface_texture_present")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_surface_texture_present(IntPtr texture);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_surface_texture_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_surface_texture_destroy(IntPtr texture);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_renderer_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_wgpu_renderer_create(IntPtr device, VelloRendererOptions options);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_renderer_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_renderer_destroy(IntPtr renderer);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_renderer_render")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_wgpu_renderer_render(
        IntPtr renderer,
        IntPtr scene,
        IntPtr textureView,
        VelloRenderParams parameters);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_renderer_render_surface")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_wgpu_renderer_render_surface(
        IntPtr renderer,
        IntPtr scene,
        IntPtr textureView,
        VelloRenderParams parameters,
        WgpuTextureFormatNative surfaceFormat);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_renderer_profiler_set_enabled")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_wgpu_renderer_profiler_set_enabled(
        IntPtr renderer,
        [MarshalAs(UnmanagedType.I1)] bool enabled);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_renderer_profiler_get_results")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_wgpu_renderer_profiler_get_results(
        IntPtr renderer,
        out VelloGpuProfilerResults results);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_renderer_profiler_results_free")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_renderer_profiler_results_free(IntPtr handle);

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
