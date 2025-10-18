# Limited Vello Feature Implementation Plan

This document tracks the outstanding feature gaps in the current “limited Vello” drawing context and outlines the concrete work required to close them using the existing Vello FFI surface or by extending it where necessary. File/line references point to the places where each limitation is enforced today.

## Offscreen Layers And Render Targets

- **Status**: Implemented in `bindings/VelloSharp.Avalonia.Vello/Rendering/VelloOffscreenRenderTarget.cs` with integration into the drawing context and platform render interface.
- **Current gap**: Resolved – `CreateLayer`, `CreateOffscreenRenderTarget`, and `CreateRenderTargetBitmap` now return `VelloOffscreenRenderTarget`, enabling Avalonia’s layer plumbing.
- **Recent work**: Added GPU-backed composition via `WgpuSurfaceRenderContext` callbacks and introduced pooled CPU layer resources to avoid repeated renderer/bitmap allocation.
- **Implementation steps**
  1. Introduce a `VelloDrawingContextLayerImpl` that records into its own `Scene` and exposes the recorded content as an `Image`/`Brush` back to the parent context.
  2. For GPU-backed layers, allocate a headless `VelloSurface` via `SurfaceHandle.Headless`, tie it to a `VelloSurfaceRenderer`, and expose the result as a texture-backed `VelloBitmapImpl`.
  3. For CPU fallbacks, wire `Renderer.Render` to populate a managed pixel buffer that backs `VelloBitmapImpl`.
  4. Implement `CreateOffscreenRenderTarget` by reusing the layer infrastructure with explicit lifetime management, and have `CreateRenderTargetBitmap` wrap the CPU path.
  5. Track layer stack disposal so layer scenes are reset and pooled to avoid excessive allocations.
- **Vello FFI usage**: `Scene`, `Renderer.Render`, `VelloSurface`, `VelloSurfaceRenderer`, `RenderParams`.
- **Notes**: The core Vello runtime already exposes full layer support via `vello_scene_push_layer`, `vello_scene_push_luminance_mask_layer`, and `vello_scene_pop_layer` (surfaced in `VelloScene.PushLayer/PushLuminanceMaskLayer/PopLayer`). The work here is entirely on the Avalonia side to allocate and compose those layers.
- **FFI extensions (if needed)**: expose a `vello_surface_renderer_map` API for efficient read-back when only a GPU surface is available.
- **Validation**: exercise Avalonia `DrawingContext` layer tests and run `ControlCatalog` scenarios that rely on `DrawingPresenter` or `VisualBrush` with layers.

## Region Drawing Coverage

- **Status**: Implemented – `VelloRegionImpl` can now emit a `PathBuilder`, and `VelloDrawingContextImpl.DrawRegion` fills/strokes regions using the existing scene helpers.
- **Validation**: Run ControlCatalog adorners (focus rectangles, selection marquee) and add a regression covering complex, multi-rect regions to ensure rendering matches Skia.

## Opacity Mask Support

- **Status**: Implemented – `PushOpacityMask` now builds luminance mask layers for any brush type and `DrawBitmap` with an opacity mask executes inside that pipeline, reverting to a no-op only when bounds/brush creation fails.
- **Vello FFI usage**: `Scene.PushLuminanceMaskLayer`, `Scene.FillPath`, `Scene.PushLayer`, `Scene.DrawImage`, `Scene.PopLayer`.
- **Validation**: run `ControlCatalog` pages using `OpacityMask`, compare outputs against Skia via screenshot diffing, and add unit coverage for nested mask/layer interactions.

## Non-Vello Geometry Fallback

- **Current gap**: `DrawGeometry` rejects any `IGeometryImpl` that is not a `VelloGeometryImplBase` (`VelloDrawingContextImpl.cs:217`), breaking third-party controls that supply custom geometry implementations.
- **Implementation steps**
  1. Add a conversion path that detects non-Vello geometries and translates them into a `PathBuilder` using Avalonia’s `IGeometryImpl.TryGetPathGeometryData`/`StreamGeometryContextImpl`.
  2. Cache converted `PathBuilder` instances when geometries expose stable hash codes to avoid repeated tessellation.
  3. Update `BuildPath` to accept a more general geometry descriptor so both native Vello and converted geometries share the same rendering path.
- **Vello FFI usage**: existing `Scene.FillPath`/`Scene.StrokePath`.
- **Validation**: render geometries produced by `StreamGeometry`, `RoundedRectGeometry`, and custom `IGeometryImpl` implementations in sample apps and compare with Skia output.

## Non-Vello Glyph Run Fallback

- **Current gap**: `DrawGlyphRun` demands `VelloGlyphRunImpl` and throws otherwise (`VelloDrawingContextImpl.cs:292`), preventing text rendering from custom text layout engines or fallback glyph pipelines.
- **Implementation steps**
  1. Create an adapter that extracts glyph indices, advances, and offsets from arbitrary `IGlyphRunImpl` via Avalonia’s text formatting contracts.
  2. Use `VelloFontManager.GetFont` with the glyph run’s `GlyphTypeface` to create a `Font` and populate a temporary `Glyph[]` span for the FFI call.
  3. Respect simulation flags (bold/italic) and per-glyph transforms when available.
  4. Gate the optimized `VelloGlyphRunImpl` path, but fall back to the adapter automatically when encountering other implementations.
- **Vello FFI usage**: `Scene.DrawGlyphRun`.
- **Validation**: run text-heavy `ControlCatalog` pages with custom text formatting (including emoji and complex scripts) toggling between native and fallback glyph paths.

## Cross-Backend Bitmap Interop

- **Current gap**: `DrawBitmap` only accepts `VelloBitmapImpl` and throws for other `IBitmapImpl` sources (`VelloDrawingContextImpl.cs:170`), so bitmaps produced by Avalonia APIs (e.g., `RenderTargetBitmap`) cannot be reused.
- **Implementation steps**
  1. Introduce a lightweight conversion utility that can copy pixels from any `IBitmapImpl` exposing `Lock`/`GetPixelSpan` into a new `VelloBitmapImpl`.
  2. Cache converted bitmaps when the source implementation exposes a change stamp, so repeated draws avoid redundant copies.
  3. Update `DrawBitmap` to call this converter when the concrete type is unknown instead of throwing.
- **Vello FFI usage**: `Image`, `ImageBrush` (no new native surface required).
- **Validation**: draw Avalonia `RenderTargetBitmap` instances inside `ControlCatalog`, and run a stress test that blits many small converted bitmaps to monitor GC pressure.

## Box Shadow Rendering

- **Status**: Implemented – `DrawRectangle` now renders both drop and inset shadows using layered `DrawBlurredRoundedRect` calls with proper clipping and spreads (`bindings/VelloSharp.Avalonia.Vello/Rendering/VelloDrawingContextImpl.cs`).
- **Validation**: Compare controls that rely on `BoxShadow` (e.g., `Border`, `Popup`, `ToolTip`) against the Skia backend, especially cases mixing spread, blur, and inset shadows.

## Render Options Handling

- **Status**: Implemented – render options now propagate for bitmap quality/blending and text hinting (`bindings/VelloSharp.Avalonia.Vello/Rendering/VelloDrawingContextImpl.cs`).
- **Validation**: Exercise samples that toggle `RenderOptions` (e.g., image zoom with varying interpolation modes, text rendering demos) and compare to the Skia backend to confirm parity.

---

Closing these gaps will move the renderer from the current “limited Vello” prototype to feature parity with Avalonia’s expectations, enabling a full drop-in alternative to Skia. Each item above should be tracked individually, with progress cross-referenced in `docs/VelloAvaloniaIntegrationPlan.md`.
