# Limited Vello Feature Implementation Plan

This document tracks the outstanding feature gaps in the current “limited Vello” drawing context and outlines the concrete work required to close them using the existing Vello FFI surface or by extending it where necessary. File/line references point to the places where each limitation is enforced today.

## Offscreen Layers And Render Targets

- **Current gap**: `CreateLayer` throws (`bindings/VelloSharp.Avalonia.Vello/Rendering/VelloDrawingContextImpl.cs:361`), `CreateOffscreenRenderTarget` throws (`.../VelloPlatformRenderInterfaceContext.cs:97`), and `CreateRenderTargetBitmap` throws (`.../VelloPlatformRenderInterface.cs:113`), preventing Avalonia’s layered composition from working.
- **Implementation steps**
  1. Introduce a `VelloDrawingContextLayerImpl` that records into its own `Scene` and exposes the recorded content as an `Image`/`Brush` back to the parent context.
  2. For GPU-backed layers, allocate a headless `VelloSurface` via `SurfaceHandle.Headless`, tie it to a `VelloSurfaceRenderer`, and expose the result as a texture-backed `VelloBitmapImpl`.
  3. For CPU fallbacks, wire `Renderer.Render` to populate a managed pixel buffer that backs `VelloBitmapImpl`.
  4. Implement `CreateOffscreenRenderTarget` by reusing the layer infrastructure with explicit lifetime management, and have `CreateRenderTargetBitmap` wrap the CPU path.
  5. Track layer stack disposal so layer scenes are reset and pooled to avoid excessive allocations.
- **Vello FFI usage**: `Scene`, `Renderer.Render`, `VelloSurface`, `VelloSurfaceRenderer`, `RenderParams`.
- **FFI extensions (if needed)**: expose a `vello_surface_renderer_map` API for efficient read-back when only a GPU surface is available.
- **Validation**: exercise Avalonia `DrawingContext` layer tests and run `ControlCatalog` scenarios that rely on `DrawingPresenter` or `VisualBrush` with layers.

## Region Drawing Coverage

- **Current gap**: `DrawRegion` is unimplemented (`VelloDrawingContextImpl.cs:260`) even though the backend advertises `SupportsRegions = true`, so hit-testing focus rectangles and other region-based visuals disappear.
- **Implementation steps**
  1. Extend `VelloRegionImpl` with a helper that emits a `PathBuilder` covering its rectangles (respecting overlap removal when Avalonia later depends on it).
  2. In `DrawRegion`, reuse that geometry to call `_scene.FillPath` for the brush and `_scene.StrokePath` for the pen when present.
  3. Add batching so large region rectangles are grouped before dispatch to avoid oversized FFI calls.
- **Vello FFI usage**: `Scene.FillPath`, `Scene.StrokePath`, existing `PathBuilder`.
- **Validation**: enable Avalonia’s region-based adorners (focus rect, selection marquee) inside `ControlCatalog` and add a regression test that draws a complex region.

## Opacity Mask Support

- **Current gap**: `DrawBitmap` with opacity mask throws (`VelloDrawingContextImpl.cs:190`), and `PushOpacityMask` currently falls back to a uniform alpha except for solid brushes (`...:498`), so gradient/image masks have no effect.
- **Implementation steps**
  1. Implement `DrawBitmap` mask handling by pushing a luminance mask layer (`Scene.PushLuminanceMaskLayer`), rendering the mask brush into that layer via the existing brush conversion helpers, then drawing the bitmap while the mask is active.
  2. Extend `PushOpacityMask` to stitch the same mask layer flow for all brush types; bake the mask using a rectangle path when possible and fall back to a temporary `Scene` for complex brushes (e.g., `VisualBrush` content).
  3. For bitmap masks, materialize a `VelloSharp.Image` from the mask brush and render it into the mask layer with `Scene.DrawImage`.
  4. Ensure bounds clipping matches Avalonia’s expectations and that layer stack balancing covers nested masks.
- **Vello FFI usage**: `Scene.PushLuminanceMaskLayer`, `Scene.FillPath`, `Scene.DrawImage`, `Scene.PopLayer`.
- **FFI extensions (if needed)**: add a helper to upload single-channel mask images without duplicating channels on the managed side.
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

- **Current gap**: `DrawRectangle` ignores its `BoxShadows` parameter (`VelloDrawingContextImpl.cs:237`), so drop shadows rendered by controls do not appear.
- **Implementation steps**
  1. Iterate the provided `BoxShadow` entries and map them to `Scene.DrawBlurredRoundedRect`, translating blur radii and offsets into Vello’s expected parameters.
  2. Respect spread, corner radii, and clipping semantics by inflating the rectangle before rendering the blur.
  3. Provide a cache for precomputed shadow geometries per radius to reduce allocation churn.
- **Vello FFI usage**: `Scene.DrawBlurredRoundedRect`.
- **Validation**: compare rendered shadows for `Border`, `Popup`, and `ToolTip` controls against the Skia backend.

## Render Options Handling

- **Current gap**: `PushRenderOptions`/`PopRenderOptions` are no-ops (`VelloDrawingContextImpl.cs:544`), so edge mode, bitmap interpolation, and text rendering hints are ignored.
- **Implementation steps**
  1. Honor `RenderOptions.EdgeMode` by toggling `RenderParams.Antialiasing` and, when necessary, switching to `AntialiasingMode.None`.
  2. Pass `RenderOptions.BitmapInterpolationMode` through to `ImageBrush` creation so bitmap scaling respects user settings.
  3. Preserve `RenderOptions.TextRenderingMode` by hooking it into the glyph run `Hint` flag and any future subpixel positioning controls.
- **Vello FFI usage**: `RenderParams`, `Scene.DrawImage`, `Scene.DrawGlyphRun`.
- **Validation**: run Avalonia render option samples (e.g., pixel snapping demos) and confirm property changes propagate through nested drawing contexts.

---

Closing these gaps will move the renderer from the current “limited Vello” prototype to feature parity with Avalonia’s expectations, enabling a full drop-in alternative to Skia. Each item above should be tracked individually, with progress cross-referenced in `docs/VelloAvaloniaIntegrationPlan.md`.
