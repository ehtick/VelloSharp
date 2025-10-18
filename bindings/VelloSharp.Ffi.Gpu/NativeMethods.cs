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

    [LibraryImport(LibraryName, EntryPoint = "vello_path_contains_point")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_path_contains_point(
        VelloPathElement* elements,
        nuint elementCount,
        VelloFillRule fill,
        VelloPoint point,
        [MarshalAs(UnmanagedType.I1)] out bool contains);

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

    [LibraryImport(LibraryName, EntryPoint = "vello_image_decode_ico")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_image_decode_ico(byte* data, nuint length, out IntPtr image);

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
        VelloTextShapeOptionsNative* options,
        out VelloShapedRunNative run,
        out IntPtr handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_text_segment_utf16")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_text_segment_utf16(
        ushort* text,
        nuint length,
        out IntPtr handle,
        out VelloScriptSegmentArrayNative array);

    [LibraryImport(LibraryName, EntryPoint = "vello_text_segments_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_text_segments_destroy(IntPtr handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_get_variation_axes")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_font_get_variation_axes(
        IntPtr font,
        out IntPtr handle,
        out VelloVariationAxisArrayNative array);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_variation_axes_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_font_variation_axes_destroy(IntPtr handle);

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

        [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_device_create_shared_texture")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_wgpu_device_create_shared_texture(
        IntPtr device,
        VelloSharedTextureDesc* desc,
        out IntPtr handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_shared_texture_acquire_mutex")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_shared_texture_acquire_mutex(IntPtr handle, ulong key, uint timeoutMilliseconds);

    [LibraryImport(LibraryName, EntryPoint = "vello_shared_texture_release_mutex")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_shared_texture_release_mutex(IntPtr handle, ulong key);

    [LibraryImport(LibraryName, EntryPoint = "vello_shared_texture_flush")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_shared_texture_flush(IntPtr handle);
    [LibraryImport(LibraryName, EntryPoint = "vello_shared_texture_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_shared_texture_destroy(IntPtr handle);
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

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_device_create_shader_module")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_wgpu_device_create_shader_module(
        IntPtr device,
        WgpuShaderModuleDescriptorNative* descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_shader_module_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_shader_module_destroy(IntPtr module);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_device_create_buffer")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_wgpu_device_create_buffer(
        IntPtr device,
        WgpuBufferDescriptorNative* descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_buffer_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_buffer_destroy(IntPtr buffer);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_buffer_get_size")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial ulong vello_wgpu_buffer_get_size(IntPtr buffer);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_device_create_sampler")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_wgpu_device_create_sampler(
        IntPtr device,
        WgpuSamplerDescriptorNative* descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_sampler_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_sampler_destroy(IntPtr sampler);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_device_create_texture")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_wgpu_device_create_texture(
        IntPtr device,
        WgpuTextureDescriptorNative* descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_texture_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_texture_destroy(IntPtr texture);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_texture_create_view")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_wgpu_texture_create_view(
        IntPtr texture,
        WgpuTextureViewDescriptorNative* descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_device_create_bind_group_layout")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_wgpu_device_create_bind_group_layout(
        IntPtr device,
        WgpuBindGroupLayoutDescriptorNative* descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_bind_group_layout_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_bind_group_layout_destroy(IntPtr layout);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_device_create_bind_group")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_wgpu_device_create_bind_group(
        IntPtr device,
        WgpuBindGroupDescriptorNative* descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_bind_group_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_bind_group_destroy(IntPtr bindGroup);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_device_create_pipeline_layout")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_wgpu_device_create_pipeline_layout(
        IntPtr device,
        WgpuPipelineLayoutDescriptorNative* descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_pipeline_layout_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_pipeline_layout_destroy(IntPtr layout);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_device_create_render_pipeline")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_wgpu_device_create_render_pipeline(
        IntPtr device,
        WgpuRenderPipelineDescriptorNative* descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_device_create_command_encoder")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_wgpu_device_create_command_encoder(
        IntPtr device,
        VelloWgpuCommandEncoderDescriptorNative* descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_command_encoder_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_command_encoder_destroy(IntPtr encoder);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_command_encoder_begin_render_pass")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_wgpu_command_encoder_begin_render_pass(
        IntPtr encoder,
        VelloWgpuRenderPassDescriptorNative* descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_render_pass_set_viewport")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_render_pass_set_viewport(
        IntPtr pass,
        float x,
        float y,
        float width,
        float height,
        float minDepth,
        float maxDepth);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_render_pass_set_pipeline")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_render_pass_set_pipeline(IntPtr pass, IntPtr pipeline);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_render_pass_set_scissor_rect")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_render_pass_set_scissor_rect(
        IntPtr pass,
        uint x,
        uint y,
        uint width,
        uint height);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_render_pass_set_bind_group")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial void vello_wgpu_render_pass_set_bind_group(
        IntPtr pass,
        uint index,
        IntPtr bindGroup,
        uint* dynamicOffsets,
        nuint dynamicOffsetCount);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_render_pass_set_vertex_buffer")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_render_pass_set_vertex_buffer(
        IntPtr pass,
        uint slot,
        IntPtr buffer,
        ulong offset,
        ulong size);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_render_pass_set_index_buffer")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_render_pass_set_index_buffer(
        IntPtr pass,
        IntPtr buffer,
        uint format,
        ulong offset,
        ulong size);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_render_pass_draw")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_render_pass_draw(
        IntPtr pass,
        uint vertexCount,
        uint instanceCount,
        uint firstVertex,
        uint firstInstance);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_render_pass_draw_indexed")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_render_pass_draw_indexed(
        IntPtr pass,
        uint indexCount,
        uint instanceCount,
        uint firstIndex,
        int baseVertex,
        uint firstInstance);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_render_pass_end")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_render_pass_end(IntPtr pass);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_command_encoder_finish")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial IntPtr vello_wgpu_command_encoder_finish(
        IntPtr encoder,
        VelloWgpuCommandBufferDescriptorNative* descriptor);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_command_buffer_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_command_buffer_destroy(IntPtr buffer);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_queue_submit")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial ulong vello_wgpu_queue_submit(
        IntPtr queue,
        IntPtr* buffers,
        nuint bufferCount);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_queue_write_buffer")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_wgpu_queue_write_buffer(
        IntPtr queue,
        IntPtr buffer,
        ulong offset,
        VelloBytesNative data);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_queue_write_texture")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_wgpu_queue_write_texture(
        IntPtr queue,
        VelloWgpuImageCopyTextureNative* destination,
        VelloBytesNative data,
        VelloWgpuTextureDataLayoutNative dataLayout,
        VelloWgpuExtent3dNative size);

    [LibraryImport(LibraryName, EntryPoint = "vello_wgpu_render_pipeline_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_wgpu_render_pipeline_destroy(IntPtr pipeline);

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

    [LibraryImport(LibraryName, EntryPoint = "vello_string_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_string_destroy(IntPtr ptr);

    [LibraryImport(LibraryName, EntryPoint = "vello_parley_font_handle_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_parley_font_handle_destroy(IntPtr handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_parley_string_array_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_parley_string_array_destroy(IntPtr handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_parley_get_default_family")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr vello_parley_get_default_family();

    [LibraryImport(LibraryName, EntryPoint = "vello_parley_get_family_names")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_parley_get_family_names(
        out IntPtr handle,
        out VelloStringArrayNative array);

    [LibraryImport(LibraryName, EntryPoint = "vello_parley_match_character", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_parley_match_character(
        uint codepoint,
        float weight,
        float stretch,
        int style,
        string? familyName,
        string? locale,
        out IntPtr handle,
        out VelloParleyFontInfoNative info);

    [LibraryImport(LibraryName, EntryPoint = "vello_parley_load_typeface", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_parley_load_typeface(
        string familyName,
        float weight,
        float stretch,
        int style,
        out IntPtr handle,
        out VelloParleyFontInfoNative info);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_count_faces")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_font_count_faces(
        IntPtr data,
        nuint length,
        out uint count);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_get_glyph_index")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_font_get_glyph_index(
        IntPtr font,
        uint codepoint,
        out ushort glyph);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_get_glyph_count")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_font_get_glyph_count(
        IntPtr font,
        out uint count);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_get_table_tags")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_font_get_table_tags(
        IntPtr font,
        out IntPtr handle,
        out VelloFontTableTagArrayNative array);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_table_tags_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_font_table_tags_destroy(IntPtr handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_reference_table")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_font_reference_table(
        IntPtr font,
        uint tag,
        out IntPtr handle,
        out VelloFontTableDataNative table);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_table_data_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void vello_font_table_data_destroy(IntPtr handle);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_get_ot_metric")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_font_get_ot_metric(
        IntPtr font,
        uint metricsTag,
        int xScale,
        int yScale,
        VelloVariationAxisValueNative* variationAxes,
        nuint variationAxisCount,
        out int position);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_get_ot_variation")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_font_get_ot_variation(
        IntPtr font,
        uint metricsTag,
        VelloVariationAxisValueNative* variationAxes,
        nuint variationAxisCount,
        out float delta);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_get_ot_variation_x")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_font_get_ot_variation_x(
        IntPtr font,
        uint metricsTag,
        int xScale,
        VelloVariationAxisValueNative* variationAxes,
        nuint variationAxisCount,
        out int delta);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_get_ot_variation_y")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloStatus vello_font_get_ot_variation_y(
        IntPtr font,
        uint metricsTag,
        int yScale,
        VelloVariationAxisValueNative* variationAxes,
        nuint variationAxisCount,
        out int delta);

    [LibraryImport(LibraryName, EntryPoint = "vello_font_get_metrics")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloStatus vello_font_get_metrics(
        IntPtr font,
        float fontSize,
        out VelloFontMetricsNative metrics);
}
