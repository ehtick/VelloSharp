# FFI API Coverage

This document compares the public C ABI exported by each native FFI crate with the .NET bindings in `VelloSharp`. It highlights coverage gaps that need attention to achieve feature parity with the upstream Rust libraries. All crates now live under the `ffi/` directory (for example `ffi/vello_ffi`).

## Summary

- `vello_ffi`: 100% of exported functions have .NET bindings.
- `kurbo_ffi`: 6 native functions are not bound in .NET (`kurbo_bez_path_append`, `kurbo_bez_path_from_elements`, `kurbo_rect_intersect`, `kurbo_rect_is_empty`, `kurbo_rect_union`, `kurbo_vec2_length`).
- `peniko_ffi`: 100% of exported functions have .NET bindings.
- `winit_ffi`: 100% of exported functions have .NET bindings.
- `accesskit_ffi`: 100% of exported functions have .NET bindings (JSON-powered interop surface).

> **Note:** The upstream Rust crates (`vello`, `kurbo`, `peniko`, `winit`) expose a much richer API surface than what is currently bridged via FFI. The tables below focus only on functions exported from the `*_ffi` crates and indicate whether a managed binding is present.

### Vello (renderer/runtime) (`vello_ffi`)

Rust crate: `vello` (subset exposed here).

| Function | Exposed via FFI | Exposed via .NET | Notes |
| --- | --- | --- | --- |
| `vello_font_create` | Yes | Yes |  |
| `vello_font_destroy` | Yes | Yes |  |
| `vello_image_create` | Yes | Yes |  |
| `vello_image_destroy` | Yes | Yes |  |
| `vello_last_error_message` | Yes | Yes |  |
| `vello_render_context_create` | Yes | Yes |  |
| `vello_render_context_destroy` | Yes | Yes |  |
| `vello_render_surface_create` | Yes | Yes |  |
| `vello_render_surface_destroy` | Yes | Yes |  |
| `vello_render_surface_resize` | Yes | Yes |  |
| `vello_renderer_create` | Yes | Yes |  |
| `vello_renderer_create_with_options` | Yes | Yes |  |
| `vello_renderer_destroy` | Yes | Yes |  |
| `vello_renderer_render` | Yes | Yes |  |
| `vello_renderer_resize` | Yes | Yes |  |
| `vello_scene_create` | Yes | Yes |  |
| `vello_scene_destroy` | Yes | Yes |  |
| `vello_scene_draw_blurred_rounded_rect` | Yes | Yes |  |
| `vello_scene_draw_glyph_run` | Yes | Yes |  |
| `vello_scene_draw_image` | Yes | Yes |  |
| `vello_scene_fill_path` | Yes | Yes |  |
| `vello_scene_fill_path_brush` | Yes | Yes |  |
| `vello_scene_pop_layer` | Yes | Yes |  |
| `vello_scene_push_layer` | Yes | Yes |  |
| `vello_scene_push_luminance_mask_layer` | Yes | Yes |  |
| `vello_scene_reset` | Yes | Yes |  |
| `vello_scene_stroke_path` | Yes | Yes |  |
| `vello_scene_stroke_path_brush` | Yes | Yes |  |
| `vello_surface_renderer_create` | Yes | Yes |  |
| `vello_surface_renderer_destroy` | Yes | Yes |  |
| `vello_surface_renderer_render` | Yes | Yes |  |
| `vello_svg_destroy` | Yes | Yes |  |
| `vello_svg_get_size` | Yes | Yes |  |
| `vello_svg_load_from_file` | Yes | Yes |  |
| `vello_svg_load_from_memory` | Yes | Yes |  |
| `vello_svg_render` | Yes | Yes |  |
| `vello_velato_composition_destroy` | Yes | Yes |  |
| `vello_velato_composition_get_info` | Yes | Yes |  |
| `vello_velato_composition_load_from_file` | Yes | Yes |  |
| `vello_velato_composition_load_from_memory` | Yes | Yes |  |
| `vello_velato_renderer_create` | Yes | Yes |  |
| `vello_velato_renderer_destroy` | Yes | Yes |  |
| `vello_velato_renderer_render` | Yes | Yes |  |
| `vello_wgpu_adapter_destroy` | Yes | Yes |  |
| `vello_wgpu_adapter_request_device` | Yes | Yes |  |
| `vello_wgpu_device_destroy` | Yes | Yes |  |
| `vello_wgpu_device_get_queue` | Yes | Yes |  |
| `vello_wgpu_instance_create` | Yes | Yes |  |
| `vello_wgpu_instance_destroy` | Yes | Yes |  |
| `vello_wgpu_instance_request_adapter` | Yes | Yes |  |
| `vello_wgpu_queue_destroy` | Yes | Yes |  |
| `vello_wgpu_renderer_create` | Yes | Yes |  |
| `vello_wgpu_renderer_destroy` | Yes | Yes |  |
| `vello_wgpu_renderer_render` | Yes | Yes |  |
| `vello_wgpu_surface_acquire_next_texture` | Yes | Yes |  |
| `vello_wgpu_surface_configure` | Yes | Yes |  |
| `vello_wgpu_surface_create` | Yes | Yes |  |
| `vello_wgpu_surface_destroy` | Yes | Yes |  |
| `vello_wgpu_surface_get_preferred_format` | Yes | Yes |  |
| `vello_wgpu_surface_texture_create_view` | Yes | Yes |  |
| `vello_wgpu_surface_texture_destroy` | Yes | Yes |  |
| `vello_wgpu_surface_texture_present` | Yes | Yes |  |
| `vello_wgpu_texture_view_destroy` | Yes | Yes |  |

### Kurbo (geometry) (`kurbo_ffi`)

Rust crate: `kurbo` (subset exposed here).

| Function | Exposed via FFI | Exposed via .NET | Notes |
| --- | --- | --- | --- |
| `kurbo_affine_identity` | Yes | Yes |  |
| `kurbo_affine_invert` | Yes | Yes |  |
| `kurbo_affine_mul` | Yes | Yes |  |
| `kurbo_affine_transform_point` | Yes | Yes |  |
| `kurbo_affine_transform_vec` | Yes | Yes |  |
| `kurbo_bez_path_append` | Yes | No | Missing P/Invoke binding |
| `kurbo_bez_path_apply_affine` | Yes | Yes |  |
| `kurbo_bez_path_bounds` | Yes | Yes |  |
| `kurbo_bez_path_clear` | Yes | Yes |  |
| `kurbo_bez_path_close` | Yes | Yes |  |
| `kurbo_bez_path_copy_elements` | Yes | Yes |  |
| `kurbo_bez_path_create` | Yes | Yes |  |
| `kurbo_bez_path_cubic_to` | Yes | Yes |  |
| `kurbo_bez_path_destroy` | Yes | Yes |  |
| `kurbo_bez_path_from_elements` | Yes | No | Missing P/Invoke binding |
| `kurbo_bez_path_len` | Yes | Yes |  |
| `kurbo_bez_path_line_to` | Yes | Yes |  |
| `kurbo_bez_path_move_to` | Yes | Yes |  |
| `kurbo_bez_path_quad_to` | Yes | Yes |  |
| `kurbo_bez_path_translate` | Yes | Yes |  |
| `kurbo_last_error_message` | Yes | Yes |  |
| `kurbo_rect_intersect` | Yes | No | Missing P/Invoke binding |
| `kurbo_rect_is_empty` | Yes | No | Missing P/Invoke binding |
| `kurbo_rect_union` | Yes | No | Missing P/Invoke binding |
| `kurbo_vec2_length` | Yes | No | Missing P/Invoke binding |

### Peniko (brushes) (`peniko_ffi`)

Rust crate: `peniko` (subset exposed here).

| Function | Exposed via FFI | Exposed via .NET | Notes |
| --- | --- | --- | --- |
| `peniko_brush_clone` | Yes | Yes |  |
| `peniko_brush_create_linear` | Yes | Yes |  |
| `peniko_brush_create_radial` | Yes | Yes |  |
| `peniko_brush_create_solid` | Yes | Yes |  |
| `peniko_brush_create_sweep` | Yes | Yes |  |
| `peniko_brush_destroy` | Yes | Yes |  |
| `peniko_brush_get_gradient_kind` | Yes | Yes |  |
| `peniko_brush_get_kind` | Yes | Yes |  |
| `peniko_brush_get_linear_gradient` | Yes | Yes |  |
| `peniko_brush_get_radial_gradient` | Yes | Yes |  |
| `peniko_brush_get_solid_color` | Yes | Yes |  |
| `peniko_brush_get_sweep_gradient` | Yes | Yes |  |
| `peniko_brush_multiply_alpha` | Yes | Yes |  |
| `peniko_brush_with_alpha` | Yes | Yes |  |
| `peniko_last_error_message` | Yes | Yes |  |

### AccessKit (accessibility schema) (`accesskit_ffi`)

Rust crate: `accesskit::common` (serialized schema surface).

| Function | Exposed via FFI | Exposed via .NET | Notes |
| --- | --- | --- | --- |
| `accesskit_last_error_message` | Yes | Yes |  |
| `accesskit_string_free` | Yes | Yes | Managed helper frees returned strings |
| `accesskit_tree_update_from_json` | Yes | Yes | Accepts UTF-8 JSON payload |
| `accesskit_tree_update_clone` | Yes | Yes |  |
| `accesskit_tree_update_to_json` | Yes | Yes |  |
| `accesskit_tree_update_destroy` | Yes | Yes |  |
| `accesskit_action_request_from_json` | Yes | Yes |  |
| `accesskit_action_request_to_json` | Yes | Yes |  |
| `accesskit_action_request_destroy` | Yes | Yes |  |

### Winit (windowing) (`winit_ffi`)

Rust crate: `winit` (subset exposed here).

| Function | Exposed via FFI | Exposed via .NET | Notes |
| --- | --- | --- | --- |
| `winit_context_exit` | Yes | Yes |  |
| `winit_context_get_window` | Yes | Yes |  |
| `winit_context_is_exiting` | Yes | Yes |  |
| `winit_context_set_control_flow` | Yes | Yes |  |
| `winit_event_loop_run` | Yes | Yes |  |
| `winit_last_error_message` | Yes | Yes |  |
| `winit_window_get_vello_handle` | Yes | Yes |  |
| `winit_window_id` | Yes | Yes |  |
| `winit_window_pre_present_notify` | Yes | Yes |  |
| `winit_window_request_redraw` | Yes | Yes |  |
| `winit_window_scale_factor` | Yes | Yes |  |
| `winit_window_set_title` | Yes | Yes |  |
| `winit_window_surface_size` | Yes | Yes |  |

## Native Rust API vs. FFI/.NET Mapping

### Vello (renderer/runtime)

| FFI Function | Rust Hint | .NET P/Invoke | Managed usage (type ▸ member ▸ location) |
| --- | --- | --- | --- |
| `vello_font_create` | std::ptr::null_mut | `NativeMethods.vello_font_create` | `VelloSharp.Font` ▸ `public static Font Load(ReadOnlySpan<byte> fontData, uint index = 0)` ▸ VelloSharp/VelloTypes.cs:692 |
| `vello_font_destroy` | Box::from_raw | `NativeMethods.vello_font_destroy` | `VelloSharp.Font` ▸ `public void Dispose()` ▸ VelloSharp/VelloTypes.cs:712<br>`VelloSharp.Font` ▸ `public void Dispose()` ▸ VelloSharp/VelloTypes.cs:722 |
| `vello_image_create` | std::ptr::null_mut | `NativeMethods.vello_image_create` | `VelloSharp.Image` ▸ `private Image(IntPtr handle)` ▸ VelloSharp/VelloTypes.cs:623 |
| `vello_image_destroy` | Box::from_raw | `NativeMethods.vello_image_destroy` | `VelloSharp.Image` ▸ `public void Dispose()` ▸ VelloSharp/VelloTypes.cs:649<br>`VelloSharp.Image` ▸ `public void Dispose()` ▸ VelloSharp/VelloTypes.cs:659 |
| `vello_last_error_message` | std::ptr::null | `NativeMethods.vello_last_error_message` | (no managed wrapper) |
| `vello_render_context_create` | RenderContext::new | `NativeMethods.vello_render_context_create` | `VelloSharp.VelloSurfaceContext` ▸ `public VelloSurfaceContext()` ▸ VelloSharp/VelloSurface.cs:11 |
| `vello_render_context_destroy` | Box::from_raw | `NativeMethods.vello_render_context_destroy` | `VelloSharp.VelloSurfaceContext` ▸ `public void Dispose()` ▸ VelloSharp/VelloSurface.cs:34<br>`VelloSharp.VelloSurfaceContext` ▸ `public void Dispose()` ▸ VelloSharp/VelloSurface.cs:44 |
| `vello_render_surface_create` | std::ptr::null_mut | `NativeMethods.vello_render_surface_create` | `VelloSharp.VelloSurface` ▸ `public VelloSurface(VelloSurfaceContext? context, in SurfaceDescriptor descriptor)` ▸ VelloSharp/VelloSurface.cs:58 |
| `vello_render_surface_destroy` | Box::from_raw | `NativeMethods.vello_render_surface_destroy` | `VelloSharp.VelloSurface` ▸ `public void Dispose()` ▸ VelloSharp/VelloSurface.cs:87<br>`VelloSharp.VelloSurface` ▸ `public void Dispose()` ▸ VelloSharp/VelloSurface.cs:97 |
| `vello_render_surface_resize` | VelloStatus::NullPointer | `NativeMethods.vello_render_surface_resize` | `VelloSharp.VelloSurface` ▸ `public void Resize(uint width, uint height)` ▸ VelloSharp/VelloSurface.cs:79 |
| `vello_renderer_create` | RendererContext::new | `NativeMethods.vello_renderer_create` | `VelloSharp.Renderer` ▸ `public Renderer(uint width, uint height, RendererOptions? options = null)` ▸ VelloSharp/VelloRenderer.cs:14 |
| `vello_renderer_create_with_options` | RendererContext::new_with_options | `NativeMethods.vello_renderer_create_with_options` | `VelloSharp.Renderer` ▸ `public Renderer(uint width, uint height, RendererOptions? options = null)` ▸ VelloSharp/VelloRenderer.cs:13 |
| `vello_renderer_destroy` | Box::from_raw | `NativeMethods.vello_renderer_destroy` | `VelloSharp.Renderer` ▸ `public void Dispose()` ▸ VelloSharp/VelloRenderer.cs:85<br>`VelloSharp.Renderer` ▸ `public void Dispose()` ▸ VelloSharp/VelloRenderer.cs:95 |
| `vello_renderer_render` | VelloStatus::NullPointer | `NativeMethods.vello_renderer_render` | `VelloSharp.Renderer` ▸ `public void Render(Scene scene, RenderParams renderParams, Span<byte> destination, int strideBytes)` ▸ VelloSharp/VelloRenderer.cs:68 |
| `vello_renderer_resize` | VelloStatus::NullPointer | `NativeMethods.vello_renderer_resize` | `VelloSharp.Renderer` ▸ `public void Resize(uint width, uint height)` ▸ VelloSharp/VelloRenderer.cs:24 |
| `vello_scene_create` | Box::into_raw | `NativeMethods.vello_scene_create` | `VelloSharp.Scene` ▸ `public Scene()` ▸ VelloSharp/VelloScene.cs:13 |
| `vello_scene_destroy` | Box::from_raw | `NativeMethods.vello_scene_destroy` | `VelloSharp.Scene` ▸ `public void Dispose()` ▸ VelloSharp/VelloScene.cs:153<br>`VelloSharp.Scene` ▸ `public void DrawGlyphRun(Font font, ReadOnlySpan<Glyph> glyphs, GlyphRunOptions options)` ▸ VelloSharp/VelloScene.cs:351 |
| `vello_scene_draw_blurred_rounded_rect` | VelloStatus::NullPointer | `NativeMethods.vello_scene_draw_blurred_rounded_rect` | `VelloSharp.Scene` ▸ `public void DrawBlurredRoundedRect(Vector2 origin, Vector2 size, Matrix3x2 transform, RgbaColor color, double radius, double stdDev)` ▸ VelloSharp/VelloScene.cs:238 |
| `vello_scene_draw_glyph_run` | VelloStatus::NullPointer | `NativeMethods.vello_scene_draw_glyph_run` | `VelloSharp.Scene` ▸ `public void DrawGlyphRun(Font font, ReadOnlySpan<Glyph> glyphs, GlyphRunOptions options)` ▸ VelloSharp/VelloScene.cs:327 |
| `vello_scene_draw_image` | VelloStatus::NullPointer | `NativeMethods.vello_scene_draw_image` | `VelloSharp.Scene` ▸ `public void DrawImage(ImageBrush brush, Matrix3x2 transform)` ▸ VelloSharp/VelloScene.cs:256 |
| `vello_scene_fill_path` | VelloStatus::NullPointer | `NativeMethods.vello_scene_fill_path` | (no managed wrapper) |
| `vello_scene_fill_path_brush` | VelloStatus::NullPointer | `NativeMethods.vello_scene_fill_path_brush` | `VelloSharp.Scene` ▸ `public void DrawGlyphRun(Font font, ReadOnlySpan<Glyph> glyphs, GlyphRunOptions options)` ▸ VelloSharp/VelloScene.cs:381 |
| `vello_scene_pop_layer` | (see implementation) | `NativeMethods.vello_scene_pop_layer` | `VelloSharp.Scene` ▸ `public void PopLayer()` ▸ VelloSharp/VelloScene.cs:219 |
| `vello_scene_push_layer` | VelloStatus::NullPointer | `NativeMethods.vello_scene_push_layer` | `VelloSharp.Scene` ▸ `public void PushLayer(PathBuilder clip, LayerBlend blend, Matrix3x2 transform, float alpha = 1f)` ▸ VelloSharp/VelloScene.cs:184 |
| `vello_scene_push_luminance_mask_layer` | VelloStatus::NullPointer | `NativeMethods.vello_scene_push_luminance_mask_layer` | `VelloSharp.Scene` ▸ `public void PushLuminanceMaskLayer(PathBuilder clip, Matrix3x2 transform, float alpha = 1f)` ▸ VelloSharp/VelloScene.cs:205 |
| `vello_scene_reset` | (see implementation) | `NativeMethods.vello_scene_reset` | `VelloSharp.Scene` ▸ `public void Reset()` ▸ VelloSharp/VelloScene.cs:23 |
| `vello_scene_stroke_path` | VelloStatus::NullPointer | `NativeMethods.vello_scene_stroke_path` | (no managed wrapper) |
| `vello_scene_stroke_path_brush` | VelloStatus::NullPointer | `NativeMethods.vello_scene_stroke_path_brush` | `VelloSharp.Scene` ▸ `public void DrawGlyphRun(Font font, ReadOnlySpan<Glyph> glyphs, GlyphRunOptions options)` ▸ VelloSharp/VelloScene.cs:426<br>`VelloSharp.Scene` ▸ `public void DrawGlyphRun(Font font, ReadOnlySpan<Glyph> glyphs, GlyphRunOptions options)` ▸ VelloSharp/VelloScene.cs:441 |
| `vello_surface_renderer_create` | std::ptr::null_mut | `NativeMethods.vello_surface_renderer_create` | `VelloSharp.VelloSurfaceRenderer` ▸ `public VelloSurfaceRenderer(VelloSurface surface, RendererOptions? options = null)` ▸ VelloSharp/VelloSurface.cs:114 |
| `vello_surface_renderer_destroy` | Box::from_raw | `NativeMethods.vello_surface_renderer_destroy` | `VelloSharp.VelloSurfaceRenderer` ▸ `public void Dispose()` ▸ VelloSharp/VelloSurface.cs:159<br>`VelloSharp.VelloSurfaceRenderer` ▸ `public void Dispose()` ▸ VelloSharp/VelloSurface.cs:169 |
| `vello_surface_renderer_render` | VelloStatus::NullPointer | `NativeMethods.vello_surface_renderer_render` | `VelloSharp.VelloSurfaceRenderer` ▸ `public void Render(VelloSurface surface, Scene scene, RenderParams renderParams)` ▸ VelloSharp/VelloSurface.cs:147 |
| `vello_svg_destroy` | Box::from_raw | `NativeMethods.vello_svg_destroy` | `VelloSharp.VelloSvg` ▸ `public void Dispose()` ▸ VelloSharp/VelloSvg.cs:114<br>`VelloSharp.VelloSvg` ▸ `public void Dispose()` ▸ VelloSharp/VelloSvg.cs:124 |
| `vello_svg_get_size` | VelloStatus::NullPointer | `NativeMethods.vello_svg_get_size` | `VelloSharp.VelloSvg` ▸ `public static VelloSvg LoadFromUtf8(ReadOnlySpan<byte> utf8, float scale = 1f)` ▸ VelloSharp/VelloSvg.cs:66 |
| `vello_svg_load_from_file` | std::ptr::null_mut | `NativeMethods.vello_svg_load_from_file` | `VelloSharp.VelloSvg` ▸ `public static VelloSvg LoadFromFile(string path, float scale = 1f)` ▸ VelloSharp/VelloSvg.cs:24 |
| `vello_svg_load_from_memory` | std::ptr::null_mut | `NativeMethods.vello_svg_load_from_memory` | `VelloSharp.VelloSvg` ▸ `public static VelloSvg LoadFromUtf8(ReadOnlySpan<byte> utf8, float scale = 1f)` ▸ VelloSharp/VelloSvg.cs:50 |
| `vello_svg_render` | VelloStatus::NullPointer | `NativeMethods.vello_svg_render` | `VelloSharp.VelloSvg` ▸ `public void Render(Scene scene, Matrix3x2? transform = null)` ▸ VelloSharp/VelloSvg.cs:91<br>`VelloSharp.VelloSvg` ▸ `public void Render(Scene scene, Matrix3x2? transform = null)` ▸ VelloSharp/VelloSvg.cs:96 |
| `vello_velato_composition_destroy` | Box::from_raw | `NativeMethods.vello_velato_composition_destroy` | `VelloSharp.VelatoComposition` ▸ `public void Dispose()` ▸ VelloSharp/Velato.cs:113<br>`VelloSharp.VelatoComposition` ▸ `public void Dispose()` ▸ VelloSharp/Velato.cs:123 |
| `vello_velato_composition_get_info` | VelloStatus::NullPointer | `NativeMethods.vello_velato_composition_get_info` | `VelloSharp.VelatoComposition` ▸ `public static VelatoComposition LoadFromUtf8(ReadOnlySpan<byte> utf8)` ▸ VelloSharp/Velato.cs:86 |
| `vello_velato_composition_load_from_file` | std::ptr::null_mut | `NativeMethods.vello_velato_composition_load_from_file` | `VelloSharp.VelatoComposition` ▸ `public static VelatoComposition LoadFromFile(string path)` ▸ VelloSharp/Velato.cs:44 |
| `vello_velato_composition_load_from_memory` | std::ptr::null_mut | `NativeMethods.vello_velato_composition_load_from_memory` | `VelloSharp.VelatoComposition` ▸ `public static VelatoComposition LoadFromUtf8(ReadOnlySpan<byte> utf8)` ▸ VelloSharp/Velato.cs:70 |
| `vello_velato_renderer_create` | VelatoRenderer::new | `NativeMethods.vello_velato_renderer_create` | `VelloSharp.VelatoRenderer` ▸ `public VelatoRenderer()` ▸ VelloSharp/Velato.cs:134 |
| `vello_velato_renderer_destroy` | Box::from_raw | `NativeMethods.vello_velato_renderer_destroy` | `VelloSharp.VelatoRenderer` ▸ `public void Dispose()` ▸ VelloSharp/Velato.cs:196<br>`VelloSharp.VelatoRenderer` ▸ `public void Dispose()` ▸ VelloSharp/Velato.cs:206 |
| `vello_velato_renderer_render` | VelloStatus::NullPointer | `NativeMethods.vello_velato_renderer_render` | `VelloSharp.VelatoRenderer` ▸ `public void Append(Scene scene, VelatoComposition composition, double frame, Matrix3x2? transform = null, double alpha = 1.0)` ▸ VelloSharp/Velato.cs:152<br>`VelloSharp.VelatoRenderer` ▸ `public void Append(Scene scene, VelatoComposition composition, double frame, Matrix3x2? transform = null, double alpha = 1.0)` ▸ VelloSharp/Velato.cs:163 |
| `vello_wgpu_adapter_destroy` | Box::from_raw | `NativeMethods.vello_wgpu_adapter_destroy` | `VelloSharp.WgpuAdapter` ▸ `public void Dispose()` ▸ VelloSharp/Wgpu.cs:335<br>`VelloSharp.WgpuAdapter` ▸ `public void Dispose()` ▸ VelloSharp/Wgpu.cs:347 |
| `vello_wgpu_adapter_request_device` | std::ptr::null_mut | `NativeMethods.vello_wgpu_adapter_request_device` | `VelloSharp.WgpuAdapter` ▸ `public WgpuDevice RequestDevice(WgpuDeviceDescriptor? descriptor = null)` ▸ VelloSharp/Wgpu.cs:302<br>`VelloSharp.WgpuAdapter` ▸ `public WgpuDevice RequestDevice(WgpuDeviceDescriptor? descriptor = null)` ▸ VelloSharp/Wgpu.cs:314 |
| `vello_wgpu_device_destroy` | Box::from_raw | `NativeMethods.vello_wgpu_device_destroy` | `VelloSharp.WgpuDevice` ▸ `public void Dispose()` ▸ VelloSharp/Wgpu.cs:400<br>`VelloSharp.WgpuDevice` ▸ `public void Dispose()` ▸ VelloSharp/Wgpu.cs:412 |
| `vello_wgpu_device_get_queue` | std::ptr::null_mut | `NativeMethods.vello_wgpu_device_get_queue` | `VelloSharp.WgpuDevice` ▸ `public WgpuQueue GetQueue()` ▸ VelloSharp/Wgpu.cs:382 |
| `vello_wgpu_instance_create` | std::ptr::null_mut | `NativeMethods.vello_wgpu_instance_create` | `VelloSharp.WgpuInstance` ▸ `public WgpuInstance(WgpuInstanceOptions? options = null)` ▸ VelloSharp/Wgpu.cs:169<br>`VelloSharp.WgpuInstance` ▸ `public WgpuInstance(WgpuInstanceOptions? options = null)` ▸ VelloSharp/Wgpu.cs:173 |
| `vello_wgpu_instance_destroy` | Box::from_raw | `NativeMethods.vello_wgpu_instance_destroy` | `VelloSharp.WgpuInstance` ▸ `public void Dispose()` ▸ VelloSharp/Wgpu.cs:235<br>`VelloSharp.WgpuInstance` ▸ `public void Dispose()` ▸ VelloSharp/Wgpu.cs:247 |
| `vello_wgpu_instance_request_adapter` | std::ptr::null_mut | `NativeMethods.vello_wgpu_instance_request_adapter` | `VelloSharp.WgpuInstance` ▸ `public WgpuAdapter RequestAdapter(WgpuRequestAdapterOptions? options = null)` ▸ VelloSharp/Wgpu.cs:210<br>`VelloSharp.WgpuInstance` ▸ `public WgpuAdapter RequestAdapter(WgpuRequestAdapterOptions? options = null)` ▸ VelloSharp/Wgpu.cs:214 |
| `vello_wgpu_queue_destroy` | Box::from_raw | `NativeMethods.vello_wgpu_queue_destroy` | `VelloSharp.WgpuQueue` ▸ `public void Dispose()` ▸ VelloSharp/Wgpu.cs:453<br>`VelloSharp.WgpuQueue` ▸ `public void Dispose()` ▸ VelloSharp/Wgpu.cs:465 |
| `vello_wgpu_renderer_create` | std::ptr::null_mut | `NativeMethods.vello_wgpu_renderer_create` | `VelloSharp.WgpuRenderer` ▸ `public WgpuRenderer(WgpuDevice device, RendererOptions? options = null)` ▸ VelloSharp/Wgpu.cs:782 |
| `vello_wgpu_renderer_destroy` | Box::from_raw | `NativeMethods.vello_wgpu_renderer_destroy` | `VelloSharp.WgpuRenderer` ▸ `public void Dispose()` ▸ VelloSharp/Wgpu.cs:839<br>`VelloSharp.WgpuRenderer` ▸ `public void Dispose()` ▸ VelloSharp/Wgpu.cs:851 |
| `vello_wgpu_renderer_render` | VelloStatus::NullPointer | `NativeMethods.vello_wgpu_renderer_render` | `VelloSharp.WgpuRenderer` ▸ `public void Render(Scene scene, WgpuTextureView textureView, RenderParams parameters)` ▸ VelloSharp/Wgpu.cs:813 |
| `vello_wgpu_surface_acquire_next_texture` | std::ptr::null_mut | `NativeMethods.vello_wgpu_surface_acquire_next_texture` | `VelloSharp.WgpuSurface` ▸ `public WgpuSurfaceTexture AcquireNextTexture()` ▸ VelloSharp/Wgpu.cs:571 |
| `vello_wgpu_surface_configure` | VelloStatus::NullPointer | `NativeMethods.vello_wgpu_surface_configure` | `VelloSharp.WgpuSurface` ▸ `public void Configure(WgpuDevice device, WgpuSurfaceConfiguration configuration)` ▸ VelloSharp/Wgpu.cs:545<br>`VelloSharp.WgpuSurface` ▸ `public void Configure(WgpuDevice device, WgpuSurfaceConfiguration configuration)` ▸ VelloSharp/Wgpu.cs:562 |
| `vello_wgpu_surface_create` | std::ptr::null_mut | `NativeMethods.vello_wgpu_surface_create` | `VelloSharp.WgpuSurface` ▸ `public static WgpuSurface Create(WgpuInstance instance, SurfaceDescriptor descriptor)` ▸ VelloSharp/Wgpu.cs:492 |
| `vello_wgpu_surface_destroy` | Box::from_raw | `NativeMethods.vello_wgpu_surface_destroy` | `VelloSharp.WgpuSurface` ▸ `public void Dispose()` ▸ VelloSharp/Wgpu.cs:589<br>`VelloSharp.WgpuSurface` ▸ `public void Dispose()` ▸ VelloSharp/Wgpu.cs:601 |
| `vello_wgpu_surface_get_preferred_format` | VelloStatus::NullPointer | `NativeMethods.vello_wgpu_surface_get_preferred_format` | `VelloSharp.WgpuSurface` ▸ `public WgpuTextureFormat GetPreferredFormat(WgpuAdapter adapter)` ▸ VelloSharp/Wgpu.cs:517 |
| `vello_wgpu_surface_texture_create_view` | std::ptr::null_mut | `NativeMethods.vello_wgpu_surface_texture_create_view` | `VelloSharp.WgpuSurfaceTexture` ▸ `public WgpuTextureView CreateView(WgpuTextureViewDescriptor? descriptor = null)` ▸ VelloSharp/Wgpu.cs:654<br>`VelloSharp.WgpuSurfaceTexture` ▸ `public WgpuTextureView CreateView(WgpuTextureViewDescriptor? descriptor = null)` ▸ VelloSharp/Wgpu.cs:666 |
| `vello_wgpu_surface_texture_destroy` | Box::from_raw | `NativeMethods.vello_wgpu_surface_texture_destroy` | `VelloSharp.WgpuSurfaceTexture` ▸ `public void Dispose()` ▸ VelloSharp/Wgpu.cs:695<br>`VelloSharp.WgpuSurfaceTexture` ▸ `public void Dispose()` ▸ VelloSharp/Wgpu.cs:707 |
| `vello_wgpu_surface_texture_present` | Box::from_raw | `NativeMethods.vello_wgpu_surface_texture_present` | `VelloSharp.WgpuSurfaceTexture` ▸ `public void Present()` ▸ VelloSharp/Wgpu.cs:681 |
| `vello_wgpu_texture_view_destroy` | Box::from_raw | `NativeMethods.vello_wgpu_texture_view_destroy` | `VelloSharp.WgpuTextureView` ▸ `public void Dispose()` ▸ VelloSharp/Wgpu.cs:748<br>`VelloSharp.WgpuTextureView` ▸ `public void Dispose()` ▸ VelloSharp/Wgpu.cs:760 |

### Kurbo (geometry)

| FFI Function | Rust Hint | .NET P/Invoke | Managed usage (type ▸ member ▸ location) |
| --- | --- | --- | --- |
| `kurbo_affine_identity` | KurboStatus::NullPointer | `KurboNativeMethods.kurbo_affine_identity` | `VelloSharp.KurboAffine` ▸ `public static KurboAffine FromMatrix3x2(in Matrix3x2 matrix) =>` ▸ VelloSharp/KurboPath.cs:182 |
| `kurbo_affine_invert` | KurboStatus::NullPointer | `KurboNativeMethods.kurbo_affine_invert` | `VelloSharp.KurboAffine` ▸ `public KurboAffine Invert()` ▸ VelloSharp/KurboPath.cs:195 |
| `kurbo_affine_mul` | KurboStatus::NullPointer | `KurboNativeMethods.kurbo_affine_mul` | `VelloSharp.KurboAffine` ▸ `public KurboAffine Multiply(KurboAffine other)` ▸ VelloSharp/KurboPath.cs:189 |
| `kurbo_affine_transform_point` | KurboStatus::NullPointer | `KurboNativeMethods.kurbo_affine_transform_point` | `VelloSharp.KurboAffine` ▸ `public KurboPoint TransformPoint(KurboPoint point)` ▸ VelloSharp/KurboPath.cs:201 |
| `kurbo_affine_transform_vec` | KurboStatus::NullPointer | `KurboNativeMethods.kurbo_affine_transform_vec` | `VelloSharp.KurboAffine` ▸ `public KurboVec2 TransformVector(KurboVec2 vector)` ▸ VelloSharp/KurboPath.cs:207 |
| `kurbo_bez_path_append` | KurboStatus::NullPointer | (missing) | (no managed wrapper) |
| `kurbo_bez_path_apply_affine` | KurboStatus::NullPointer | `KurboNativeMethods.kurbo_bez_path_apply_affine` | `VelloSharp.KurboPath` ▸ `public void ApplyAffine(in KurboAffine affine)` ▸ VelloSharp/KurboPath.cs:95 |
| `kurbo_bez_path_bounds` | KurboStatus::NullPointer | `KurboNativeMethods.kurbo_bez_path_bounds` | `VelloSharp.KurboPath` ▸ `public KurboRect GetBounds()` ▸ VelloSharp/KurboPath.cs:106 |
| `kurbo_bez_path_clear` | KurboStatus::NullPointer | `KurboNativeMethods.kurbo_bez_path_clear` | `VelloSharp.KurboPath` ▸ `public void Clear()` ▸ VelloSharp/KurboPath.cs:54 |
| `kurbo_bez_path_close` | KurboStatus::NullPointer | `KurboNativeMethods.kurbo_bez_path_close` | `VelloSharp.KurboPath` ▸ `public void Close()` ▸ VelloSharp/KurboPath.cs:90 |
| `kurbo_bez_path_copy_elements` | KurboStatus::NullPointer | `KurboNativeMethods.kurbo_bez_path_copy_elements` | `VelloSharp.KurboPath` ▸ `public KurboPathElement[] GetElements()` ▸ VelloSharp/KurboPath.cs:133<br>`VelloSharp.KurboPath` ▸ `public KurboPathElement[] GetElements()` ▸ VelloSharp/KurboPath.cs:138 |
| `kurbo_bez_path_create` | Box::into_raw | `KurboNativeMethods.kurbo_bez_path_create` | `VelloSharp.KurboPath` ▸ `public KurboPath()` ▸ VelloSharp/KurboPath.cs:37 |
| `kurbo_bez_path_cubic_to` | KurboStatus::NullPointer | `KurboNativeMethods.kurbo_bez_path_cubic_to` | `VelloSharp.KurboPath` ▸ `public void CubicTo(double c1x, double c1y, double c2x, double c2y, double x, double y)` ▸ VelloSharp/KurboPath.cs:80<br>`VelloSharp.KurboPath` ▸ `public void CubicTo(double c1x, double c1y, double c2x, double c2y, double x, double y)` ▸ VelloSharp/KurboPath.cs:85 |
| `kurbo_bez_path_destroy` | Box::from_raw | `KurboNativeMethods.kurbo_bez_path_destroy` | `VelloSharp.KurboPathHandle` ▸ `protected override bool ReleaseHandle()` ▸ VelloSharp/KurboPath.cs:26 |
| `kurbo_bez_path_from_elements` | ptr::null_mut | (missing) | (no managed wrapper) |
| `kurbo_bez_path_len` | KurboStatus::NullPointer | `KurboNativeMethods.kurbo_bez_path_len` | `VelloSharp.KurboPath` ▸ `public KurboRect GetBounds()` ▸ VelloSharp/KurboPath.cs:114<br>`VelloSharp.KurboPath` ▸ `public KurboPathElement[] GetElements()` ▸ VelloSharp/KurboPath.cs:121 |
| `kurbo_bez_path_line_to` | KurboStatus::NullPointer | `KurboNativeMethods.kurbo_bez_path_line_to` | `VelloSharp.KurboPath` ▸ `public void LineTo(double x, double y)` ▸ VelloSharp/KurboPath.cs:64 |
| `kurbo_bez_path_move_to` | KurboStatus::NullPointer | `KurboNativeMethods.kurbo_bez_path_move_to` | `VelloSharp.KurboPath` ▸ `public void MoveTo(double x, double y)` ▸ VelloSharp/KurboPath.cs:59 |
| `kurbo_bez_path_quad_to` | KurboStatus::NullPointer | `KurboNativeMethods.kurbo_bez_path_quad_to` | `VelloSharp.KurboPath` ▸ `public void QuadraticTo(double cx, double cy, double x, double y)` ▸ VelloSharp/KurboPath.cs:70<br>`VelloSharp.KurboPath` ▸ `public void QuadraticTo(double cx, double cy, double x, double y)` ▸ VelloSharp/KurboPath.cs:74 |
| `kurbo_bez_path_translate` | KurboStatus::NullPointer | `KurboNativeMethods.kurbo_bez_path_translate` | `VelloSharp.KurboPath` ▸ `public void Translate(double dx, double dy)` ▸ VelloSharp/KurboPath.cs:101 |
| `kurbo_last_error_message` | ptr::null | `KurboNativeMethods.kurbo_last_error_message` | (no managed wrapper) |
| `kurbo_rect_intersect` | KurboStatus::NullPointer | (missing) | (no managed wrapper) |
| `kurbo_rect_is_empty` | KurboStatus::NullPointer | (missing) | (no managed wrapper) |
| `kurbo_rect_union` | KurboStatus::NullPointer | (missing) | (no managed wrapper) |
| `kurbo_vec2_length` | KurboStatus::NullPointer | (missing) | (no managed wrapper) |

### Peniko (brushes)

| FFI Function | Rust Hint | .NET P/Invoke | Managed usage (type ▸ member ▸ location) |
| --- | --- | --- | --- |
| `peniko_brush_clone` | ptr::null_mut | `PenikoNativeMethods.peniko_brush_clone` | `VelloSharp.PenikoBrush` ▸ `public PenikoBrush Clone()` ▸ VelloSharp/PenikoBrush.cs:64 |
| `peniko_brush_create_linear` | ptr::null_mut | `PenikoNativeMethods.peniko_brush_create_linear` | `VelloSharp.PenikoBrush` ▸ `public static PenikoBrush CreateLinear(PenikoLinearGradient gradient, PenikoExtend extend, ReadOnlySpan<PenikoColorStop> stops)` ▸ VelloSharp/PenikoBrush.cs:32 |
| `peniko_brush_create_radial` | ptr::null_mut | `PenikoNativeMethods.peniko_brush_create_radial` | `VelloSharp.PenikoBrush` ▸ `public static PenikoBrush CreateRadial(PenikoRadialGradient gradient, PenikoExtend extend, ReadOnlySpan<PenikoColorStop> stops)` ▸ VelloSharp/PenikoBrush.cs:44 |
| `peniko_brush_create_solid` | Brush::Solid | `PenikoNativeMethods.peniko_brush_create_solid` | `VelloSharp.PenikoBrush` ▸ `public static PenikoBrush CreateSolid(VelloColor color)` ▸ VelloSharp/PenikoBrush.cs:22 |
| `peniko_brush_create_sweep` | ptr::null_mut | `PenikoNativeMethods.peniko_brush_create_sweep` | `VelloSharp.PenikoBrush` ▸ `public static PenikoBrush CreateSweep(PenikoSweepGradient gradient, PenikoExtend extend, ReadOnlySpan<PenikoColorStop> stops)` ▸ VelloSharp/PenikoBrush.cs:56 |
| `peniko_brush_destroy` | Box::from_raw | `PenikoNativeMethods.peniko_brush_destroy` | `VelloSharp.PenikoBrush` ▸ `public void Dispose()` ▸ VelloSharp/PenikoBrush.cs:130 |
| `peniko_brush_get_gradient_kind` | PenikoStatus::NullPointer | `PenikoNativeMethods.peniko_brush_get_gradient_kind` | `VelloSharp.PenikoBrush` ▸ `public PenikoGradientKind? GetGradientKind()` ▸ VelloSharp/PenikoBrush.cs:91 |
| `peniko_brush_get_kind` | PenikoStatus::NullPointer | `PenikoNativeMethods.peniko_brush_get_kind` | `VelloSharp.PenikoBrush` ▸ `public PenikoBrush Clone()` ▸ VelloSharp/PenikoBrush.cs:72<br>`VelloSharp.PenikoBrush` ▸ `public PenikoGradientKind? GetGradientKind()` ▸ VelloSharp/PenikoBrush.cs:85 |
| `peniko_brush_get_linear_gradient` | PenikoStatus::NullPointer | `PenikoNativeMethods.peniko_brush_get_linear_gradient` | `VelloSharp.PenikoBrush` ▸ `private static unsafe PenikoStatus QueryLinearGradient(nint brush, ref GradientUnion gradient, ref ExtendHolder extend, nint stops, nuint capacity, out nuint length) =>` ▸ VelloSharp/PenikoBrush.cs:188 |
| `peniko_brush_get_radial_gradient` | PenikoStatus::NullPointer | `PenikoNativeMethods.peniko_brush_get_radial_gradient` | `VelloSharp.PenikoBrush` ▸ `private static unsafe PenikoStatus QueryRadialGradient(nint brush, ref GradientUnion gradient, ref ExtendHolder extend, nint stops, nuint capacity, out nuint length) =>` ▸ VelloSharp/PenikoBrush.cs:191 |
| `peniko_brush_get_solid_color` | PenikoStatus::NullPointer | `PenikoNativeMethods.peniko_brush_get_solid_color` | `VelloSharp.PenikoBrush` ▸ `public VelloColor GetSolidColor()` ▸ VelloSharp/PenikoBrush.cs:79 |
| `peniko_brush_get_sweep_gradient` | PenikoStatus::NullPointer | `PenikoNativeMethods.peniko_brush_get_sweep_gradient` | `VelloSharp.PenikoBrush` ▸ `private static unsafe PenikoStatus QuerySweepGradient(nint brush, ref GradientUnion gradient, ref ExtendHolder extend, nint stops, nuint capacity, out nuint length) =>` ▸ VelloSharp/PenikoBrush.cs:194 |
| `peniko_brush_multiply_alpha` | PenikoStatus::NullPointer | `PenikoNativeMethods.peniko_brush_multiply_alpha` | `VelloSharp.PenikoBrush` ▸ `public void MultiplyAlpha(float alpha)` ▸ VelloSharp/PenikoBrush.cs:123 |
| `peniko_brush_with_alpha` | PenikoStatus::NullPointer | `PenikoNativeMethods.peniko_brush_with_alpha` | `VelloSharp.PenikoBrush` ▸ `public void WithAlpha(float alpha)` ▸ VelloSharp/PenikoBrush.cs:118 |
| `peniko_last_error_message` | ptr::null | `PenikoNativeMethods.peniko_last_error_message` | (no managed wrapper) |

### Winit (windowing)

| FFI Function | Rust Hint | .NET P/Invoke | Managed usage (type ▸ member ▸ location) |
| --- | --- | --- | --- |
| `winit_context_exit` | WinitStatus::Success | `WinitNativeMethods.winit_context_exit` | `VelloSharp.WinitEventLoopContext` ▸ `public void Exit()` ▸ VelloSharp/WinitEventLoop.cs:78 |
| `winit_context_get_window` | WinitStatus::NullPointer | `WinitNativeMethods.winit_context_get_window` | `VelloSharp.WinitEventLoopContext` ▸ `public WinitWindow? GetWindow()` ▸ VelloSharp/WinitEventLoop.cs:92 |
| `winit_context_is_exiting` | WinitStatus::NullPointer | `WinitNativeMethods.winit_context_is_exiting` | `VelloSharp.WinitEventLoopContext` ▸ `public void Exit()` ▸ VelloSharp/WinitEventLoop.cs:85 |
| `winit_context_set_control_flow` | WinitControlFlow::Poll | `WinitNativeMethods.winit_context_set_control_flow` | `VelloSharp.WinitEventLoopContext` ▸ `public void SetControlFlow(WinitControlFlow flow, TimeSpan? wait = null)` ▸ VelloSharp/WinitEventLoop.cs:73 |
| `winit_event_loop_run` | WinitStatus::InvalidArgument | `WinitNativeMethods.winit_event_loop_run` | `VelloSharp.WinitEventLoop` ▸ `public WinitStatus Run(WinitRunConfiguration configuration, IWinitEventHandler handler)` ▸ VelloSharp/WinitEventLoop.cs:31 |
| `winit_last_error_message` | ptr::null | `WinitNativeMethods.winit_last_error_message` | (no managed wrapper) |
| `winit_window_get_vello_handle` | WinitStatus::NullPointer | `WinitNativeMethods.winit_window_get_vello_handle` | `VelloSharp.WinitWindow` ▸ `public VelloWindowHandle GetVelloWindowHandle()` ▸ VelloSharp/WinitEventLoop.cs:148 |
| `winit_window_id` | WinitStatus::NullPointer | `WinitNativeMethods.winit_window_id` | `VelloSharp.WinitWindow` ▸ `public (uint Width, uint Height) GetSurfaceSize()` ▸ VelloSharp/WinitEventLoop.cs:135 |
| `winit_window_pre_present_notify` | WinitStatus::Success | `WinitNativeMethods.winit_window_pre_present_notify` | `VelloSharp.WinitWindow` ▸ `public void PrePresentNotify()` ▸ VelloSharp/WinitEventLoop.cs:113 |
| `winit_window_request_redraw` | WinitStatus::Success | `WinitNativeMethods.winit_window_request_redraw` | `VelloSharp.WinitWindow` ▸ `public void RequestRedraw()` ▸ VelloSharp/WinitEventLoop.cs:108 |
| `winit_window_scale_factor` | WinitStatus::NullPointer | `WinitNativeMethods.winit_window_scale_factor` | `VelloSharp.WinitWindow` ▸ `public (uint Width, uint Height) GetSurfaceSize()` ▸ VelloSharp/WinitEventLoop.cs:126 |
| `winit_window_set_title` | WinitStatus::NullPointer | `WinitNativeMethods.winit_window_set_title` | `VelloSharp.WinitWindow` ▸ `public void SetTitle(string title)` ▸ VelloSharp/WinitEventLoop.cs:143 |
| `winit_window_surface_size` | WinitStatus::NullPointer | `WinitNativeMethods.winit_window_surface_size` | `VelloSharp.WinitWindow` ▸ `public (uint Width, uint Height) GetSurfaceSize()` ▸ VelloSharp/WinitEventLoop.cs:118 |
