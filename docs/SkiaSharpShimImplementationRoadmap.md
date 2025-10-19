# SkiaSharp & HarfBuzz Shim Implementation Roadmap (2025-03-18)

## Summary

- The Skia shim now exposes the full public surface of SkiaSharp, but a number of high-value APIs still throw via `ShimNotImplemented`. The largest gaps concern surface creation with pixel/texture backings, path analysis, shader/filter composition, and PDF/document output.
- Our strategy is to surface existing native capabilities through new FFI bindings or implement high-performance managed fallbacks where no native hook exists—without modifying upstream crates such as `vello`, `peniko`, or `kurbo`.
- The HarfBuzz shim is broadly functional; remaining parity work is centred on vertical metrics/origin fallbacks and richer serialization diagnostics that can be provided via additional HarfBuzz FFI or managed helpers.
- The plan below maps every outstanding Skia API to the Vello/Rust counterpart we can leverage (or must build), focusing on GPU-friendly implementations to keep parity and performance.

## Gap Analysis & Proposed Backing APIs

### 1. Surfaces, RenderTargets & Resource Management

**Goal**: make the shim honour every `SKSurface.Create` overload (CPU pixel buffers, GPU backend textures/render targets, properties) while managing lifetime and reuse of Vello render resources—solely by binding to existing Vello APIs or layering managed logic on top.

- **Surface matrix**
  - `SKSurface.Create(SKSurfaceProperties)` → bind to the existing `vello_surface_renderer_create` and configure swapchain hints via managed translation.
  - `Create(SKImageInfo, pixels, rowBytes, props)` → reuse current CPU render path by exposing `Renderer::render` buffers through FFI and wrapping them with a managed `VelloCpuSurface`.
  - `Create(GRBackendTexture/RenderTarget, origin, colorType, colorSpace, props)` → surface the existing wgpu texture interop helpers through a thin FFI layer so we can wrap user-supplied textures without extra copies.

- **FFI layer (no upstream changes)**
  - Add a shim-owned FFI crate that re-exports Vello’s public structs (`SurfaceConfig`, `RenderParams`) and creation helpers; only binding to what already exists.
  - Provide bindings for surface present/flush routines already available in `vello_surface_renderer`.
  - Expose reference-counted handles and callback plumbing from the shim crate without editing `vello`.

- **Managed shim work**
  - Implement `VelloSurfaceHandle` as a safe managed wrapper around the exposed native handles.
  - Extend `SkiaSharpShim.SKSurfaceFactory` to dispatch by overload, perform validation, and translate `SKSurfaceProperties` to the existing config structs.
  - Implement `SKSurface.Dispose` / `SKSurface.Canvas` pooling purely in managed code so no upstream changes are required.
  - Flow `SKImageInfo.ColorSpace`, `ColorType`, `AlphaType` via managed conversion, defaulting to sRGB when colour space is absent.
  - Mirror the same surface-handle plumbing in the WinUI/UWP and WinForms/WPF presenters so all windowed hosts can benefit from the GPU-backed path.

- **Rendering pipeline**
  - GPU path: wrap swapchain textures by invoking the exposed interop FFI and render via `VelloSurfaceRenderer::render_scene`, using managed callbacks for presentation.
  - CPU path: call the existing CPU render entry point through FFI, expose the framebuffer via `SKImage.PeekPixels`, and manage lifetimes in managed code.
  - Ensure `SKSurface.Flush` simply forwards to the existing flush/present bindings and synchronises managed staging buffers.

- **Resource management**
  - Maintain renderer pools, staging buffers, and canvas caches entirely in managed code, keyed by configuration hashes.
  - Track surface lifetime with safe handles; ensure `Dispose` releases GPU resources through the existing destroy routines exposed via FFI.
  - Add managed caching for `SKSurface.GetCanvas` to avoid redundant canvas allocations.

- **Testing & validation**
  - Unit tests for each overload verifying size, color space, sample count, and pixel buffer reuse (`docs/skiasharp-shim-api-coverage.md` updated).
  - GPU integration test inside `samples/RenderDemo` exercising swapchain-backed surface rendering and present.
  - CPU regression test measuring round-trip pixel writes via `SKBitmap.ReadPixels`.

- **Performance guardrails**
  - Zero-copy for GPU textures (no staging unless explicitly requested).
  - Reuse command encoders per surface to avoid per-frame allocations; benchmark before/after in the parity suite.
  - Emit tracing spans (ETW/EventSource + Rust tracing) around `SurfaceRenderer::render` to detect regressions.

### 2. Canvas Clipping, Region & Path Operations

**Goal**: reach functional parity for `SKCanvas` clip/region APIs and path analysis helpers (`SKPath.Op`, `SKPathMeasure`, `SKPaint.GetFillPath`) by binding to existing geometry utilities or providing fast managed fallbacks—without altering `kurbo` itself.

- **Rust / FFI work (wrapper only)**
  - Create a shim-owned FFI layer that invokes existing `kurbo` boolean operations and stroking helpers; export them as `vello_path_boolean_op`, `vello_path_stroke_to_fill` without touching the crate sources.
  - Surface `kurbo::ParamCurveArclen`/`BezPath` measurement routines through FFI handles to satisfy `SKPathMeasure`.
  - Provide an FFI helper that assembles regions (rectangle lists) by reusing existing `kurbo` region utilities or falling back to managed rectangle tessellation when data exceeds FFI coverage.

- **Managed shim work**
  - Implement `SkiaSharpShim.PathOps` to call the new FFI wrappers when available and fall back to managed algorithms (based on `System.Numerics.Vector` tessellation) when certain operations are not exposed.
  - Update `SKPath.Op`, `SKPaint.GetFillPath`, and `SKPathMeasure` to use `PathOps`, handling winding/fill conversions purely in managed code.
  - Expand `SKCanvasClipper` so all clip operations (`Union`, `Xor`, `ReverseDifference`, etc.) reuse cached intermediate paths and only invoke FFI when necessary.
  - Rework `SKRegion` bridges to translate to/from rectangle spans and draw them via managed tessellation if the FFI helper is not applicable.

- **Scene / rendering integration**
  - Continue to feed precomputed clip masks into the existing scene builder—no renderer changes required—by encoding the paths managed-side.
  - Keep `DrawRegion` implemented by generating fill paths managed-side, ensuring we only rely on existing scene APIs.

- **Caching & threading**
  - Cache boolean op results keyed by `(pathA_id, pathB_id, op)` using managed weak references.
  - Pool FFI-backed path-measure handles and guard access with `ReaderWriterLockSlim` for thread safety.

- **Testing & validation**
  - Add parity tests for every `SKPathOp` value, covering both FFI-backed and managed fallback paths.
  - Extend clip regression scenes (rect/path/region combinations) and include automated snapshot tests to catch behavioural differences.
  - Add `SKPathMeasure` unit tests verifying length, position, and tangent across multi-contour paths.

- **Performance guardrails**
  - Prefer FFI-backed operations for large/complex paths; measure fallback performance and document thresholds where managed algorithms take over.
  - Add tracing hooks around FFI and managed path ops so hotspots are easy to diagnose without modifying native crates.

### 3. Text Measurement & Font Metrics

**Goal**: lift text measurement APIs to full Skia parity, respecting `SKPaint` parameters (hinting, edging, linear metrics), complex scripts, kerning, and vertical metrics by reusing the existing HarfBuzz/Parley capabilities via FFI and managed caching—no upstream engine changes required.

- **FFI bindings (wrapping existing APIs)**
  - Bind to the current Parley/Vello glyph-run shaping entry point and expose handles keyed by `(font_id, paint_flags, text_hash)` without altering the crates.
  - Surface existing HarfBuzz metrics/measurement functions (`hb_shape`, `hb_buffer_get_glyph_positions`, `hb_font_get_extents`) through dedicated shim FFI exports.
  - Provide FFI to query vertical metrics/origins already available in HarfBuzz (`hb_font_get_vertical_origin`, etc.).

- **Managed shim work**
  - Introduce `SkiaSharpShim.FontMeasurement` to orchestrate shaping calls, manage glyph-run caches, and compute derived metrics in managed code.
  - Update `SKFont.MeasureText`, `BreakText`, `GetGlyphWidths`, and `SKPaint.MeasureText` to reuse cached HarfBuzz results and apply Skia-specific paint adjustments managed-side (text align, scale, skew, fake styles).
  - Implement `SKFont.GetMetrics`/`GetVerticalMetrics` by reading the exposed HarfBuzz metrics and translating to Skia conventions.
  - Harmonize `SKTextBlobBuilder` so it shares glyph-run handles and abides by existing HarfBuzz caches—no engine edits.

- **Layout & caching integration**
  - Maintain an LRU cache of glyph runs with weak references tied to font objects; expose invalidation hooks when fonts or paint parameters change.
  - Thread `SKFontManager` variation selection through existing HarfBuzz APIs (language, script, variation axis) without modifying native libraries.
  - Guard cache reads/writes with `ReaderWriterLockSlim` to remain thread-safe on the managed side.

- **Testing & validation**
  - Add comparison tests against native Skia for Latin, CJK, and complex scripts covering `MeasureText`, `BreakText`, `GetGlyphWidths`.
  - Validate vertical metrics across upright and vertical fonts; include regression for fallback fonts without explicit vertical origin.
  - Expand `samples/RenderDemo` typography gallery to render measurement overlays for manual inspection.
  - Update documentation and coverage tables to record the new measurement capabilities.

- **Performance guardrails**
  - Avoid per-call allocations by pooling glyph buffers and reusing run handles exposed via FFI.
  - Ensure shaping stays amortized via cache hits; add tracing to log cache misses and shaping durations strictly in managed code.
  - Benchmark `MeasureText` hot paths under multi-threaded load to ensure lock contention stays minimal.

### 4. Shaders, Blenders & Color Filters

**Goal**: deliver the Skia shader/color-filter/blender matrix while relying only on existing Vello shader constructs (gradients, solid colours, images) exposed through FFI, with managed CPU fallbacks for features not presently available.

- **FFI bindings**
  - Surface the existing shader constructors (`SolidColor`, `LinearGradient`, `RadialGradient`, `ImageShader`, etc.) through shim-owned FFI exports; no renderer modifications required.
  - Expose current color filter/blend hooks (porter-duff, color matrix) if already present; otherwise route these requests to managed fallbacks.
  - Provide reference-counted handle management and serialization helpers entirely within the shim FFI layer.

- **Managed fallbacks**
  - Implement procedural effects not available natively (Perlin noise, turbulence) by generating CPU textures in managed code (using `System.Numerics.Vector` for speed) and uploading them as `ImageShader` instances.
  - Compose shaders/color filters/blenders in managed code by constructing intermediate images or by reusing simple blend math before passing brushes to Vello.
  - Respect `SKShader.WithColorFilter` by stacking managed transformations before invoking the FFI shader creation.

- **Pipeline integration**
  - Build a managed `SkiaSharpShim.Shaders` registry that deduplicates shader/filter/blender combinations and decides when to use native FFI versus CPU fallback.
  - Update `SKPaint` translation to attach either native shader handles or managed textures derived from fallback processing.
  - Ensure serialization (`ToShader`, `Flatten`) records enough metadata to recreate the shader stack, regardless of the underlying execution path.

- **Testing & validation**
  - Create golden tests comparing Skia reference renders for gradients, composed shaders, perlin noise, arithmetic blending, and color matrices—covering both native FFI usage and managed fallbacks.
  - Extend `samples/RenderDemo` with a shader gallery that highlights native vs. managed paths to verify visual parity.
  - Add unit tests for shader caching to guarantee reuse and correct disposal of native handles.

- **Performance guardrails**
  - Cache managed-generated textures (e.g., procedural noise) keyed by parameters to avoid regeneration.
  - Measure the cost of fallback paths and document thresholds where native support is required; expose telemetry counters for managed shader execution time.

### 5. Image Filters & Mask/Path Effects

**Goal**: unlock the complete set of Skia image filters, mask filters, and path effects while relying on existing native capabilities (where present) and high-performance managed implementations otherwise.

- **FFI bindings**
  - Surface the current Vello filter primitives (blur, drop shadow, blend, color matrix) via a shim-owned FFI layer without modifying the renderer.
  - Provide wrappers over existing `kurbo` stroking/dash helpers for path effects in the same manner as section 2.
  - Add optional bindings to external crates (e.g., `image` for convolution) through a new shim crate that we own, leaving upstream crates untouched.

- **Managed filter pipeline**
  - Implement missing filters (displacement map, matrix convolution variants, table filters) on the managed side using `System.Numerics.Vector`-accelerated code paths, producing intermediate bitmaps that can be fed back into Vello as images.
  - Compose filter trees in managed code, deciding per-node whether to call the native FFI helper or execute the SIMD fallback.
  - Build mask filters by generating alpha masks managed-side and applying them through existing scene layering APIs—no renderer edits.

- **Path effects**
  - Reuse the path-operation FFI wrappers (dash, trim, stroke expansion) and supply managed implementations when FFI isn’t available.
  - Guarantee `SKPathEffect.GetFillPath`/`ApplyToPath` semantics in managed code, only delegating to FFI for performance-critical operations.

- **Integration**
  - Extend the managed `SkiaSharpShim.ImageFilters` registry to translate `SKImageFilter`, `SKMaskFilter`, and `SKPathEffect` trees into a mix of FFI handles and managed execution steps, caching results by structural hash.
  - Schedule filter execution entirely in managed code, invoking native draws only after intermediate bitmaps are prepared; existing scene encoder APIs remain unchanged.

- **Testing & validation**
  - Build parity image tests for each filter/path effect, comparing pixmaps against Skia outputs within epsilon tolerances across both native and managed paths.
  - Add `samples/RenderDemo` filter playground covering chained filters, mask filters, dashed/trimmed strokes, and animated offsets.
  - Unit tests exercising serialization/deserialization of filter graphs and verifying reference-count hygiene.
  - Update coverage documentation with filter status and fallback notes.

- **Performance guardrails**
  - Cache intermediate bitmaps and filter plans keyed by parameter hash to reduce recomputation.
  - Profile CPU fallbacks to ensure they meet frame-budget requirements; document scenarios where native acceleration is still required.
  - Expose telemetry counters for time spent in native vs. managed filter execution.

### 6. Colour Management & Spaces

**Goal**: support the full Skia color-space pipeline (ICC profiles, transfer functions, encoded color types) by exposing existing colour-management hooks where available and filling gaps with SIMD-accelerated managed converters—keeping upstream crates untouched.

- **FFI bindings**
  - Wrap the existing named colour spaces and transfer descriptors already exposed by Vello/wgpu (sRGB, linear sRGB, DisplayP3) through shim-owned FFI exports.
  - Provide bindings to wgpu surface configuration so we can query swapchain formats/colour spaces without modifying wgpu.
  - Offer optional bindings to `iccp`/`palette` via a shim crate we control, purely to parse ICC data and produce matrices/LUTs (no upstream edits).

- **Managed colour-management layer**
  - Implement ICC parsing fallback in managed code for scenarios where native parsing is unavailable; cache parsed results by profile hash.
  - Perform colour transforms on CPU using `System.Numerics.Vector` when GPU assistance isn’t exposed—covering transfer functions, gamut conversion, and tone mapping.
  - Maintain a managed colour-space registry that maps `SKColorSpace` instances to either native handles (when FFI is available) or managed conversion routines.

- **Integration**
  - Update `SKSurface`/`SKImage` creation paths to carry colour-space metadata and decide at draw time whether conversions are handled by native FFI or managed routines.
  - Provide `SKColorSpaceXyz` and `SKColorSpaceTransferFn` implementations that read from the colour-space registry rather than altering native crates.
  - Wire `SKPaint.ColorFilter` and shader pipelines to inject managed colour conversions ahead of shader evaluation when native support is missing.

- **GPU / HDR considerations**
  - Detect device/HDR capabilities via the existing wgpu surface APIs exposed through FFI, choosing the appropriate working colour space without changing renderer code.
  - Implement PQ/HLG tone mapping in managed code, using GPU shaders only if existing hooks are already available.
  - Coordinate with the surface management layer to select compatible framebuffer formats based on queried swapchain capabilities.

- **Testing & validation**
  - Create ICC round-trip tests comparing against Skia for sRGB, DisplayP3, AdobeRGB, Rec2020, Gray profiles using both native and managed paths.
  - Add render tests for colour transforms (gradients, images) across SDR/HDR swapchains, verifying delta-E thresholds.
  - Validate transfer function utilities (`SKColorSpace.GetGammaCloseToSrgb`, `IsSrgb`) against Skia reference behaviour.
  - Update documentation with per-feature notes on whether native or managed paths are used.

- **Performance guardrails**
  - Cache matrices/LUTs per colour-space pair; reuse CPU buffers and avoid repeated allocations.
  - Short-circuit conversions when colour spaces match to prevent unnecessary work.
  - Expose diagnostics for when managed fallbacks are used so we can monitor performance impact.

### 7. Image I/O & Sampling

**Goal**: bring image creation/encoding/decoding and sampling behaviour to Skia parity using existing native hooks exposed via FFI, with managed implementations covering gaps (format conversion, advanced sampling).

- **FFI bindings**
  - Expose current Vello image constructors (pixel-backed textures, swapchain images) through the shim FFI.
  - Provide bindings to a shim-owned Rust helper crate that leverages the `image` crate for decode/encode (PNG, JPEG, WebP, AVIF, GIF) without modifying upstream code.
  - Wrap existing sampler/filter enums for nearest/linear sampling; for advanced options absent natively, fall back to managed resampling.

- **Managed sampling pipeline**
  - Implement mipmap generation, anisotropic filtering, and cubic resampling in managed code using SIMD helpers; upload generated mip levels via the exposed FFI.
  - Respect `SKSamplingOptions` by selecting native samplers when available and otherwise applying managed resampling before draw submission.
  - Handle YUV image APIs by performing CPU colour conversion (SIMD-accelerated) into RGBA buffers prior to creating textures.

- **Managed shim work**
  - Update `SKImage.FromPixels`, `FromEncoded`, and `Decode` to call the new FFI decode helpers and to populate colour space/alpha metadata.
  - Implement `SKImage.Encode`/`SKBitmap.Encode` by piping through the helper crate or managed encoders depending on format.
  - Extend `SKCanvas.DrawImage`/`DrawImageRect` to translate `SKPaint` into combined shader/filter/blender descriptors, using managed pre-processing when shader/filter support isn’t native.

- **Caching & resource management**
  - Cache decoded images, generated mip levels, and resampled textures keyed by content hash to minimise recomputation.
  - Pool sampler descriptors and staging buffers in managed code; reuse them across frames.

- **Testing & validation**
  - Add encode/decode parity tests across PNG/JPEG/WebP/AVIF, verifying byte-for-byte or tolerance-based matches with Skia.
  - Test sampling modes via image-based comparisons (zoomed textures, rotated images) to ensure filtering matches Skia output.
  - Validate draw-with-paint scenarios combining shaders, filters, and path effects on images.
  - Update documentation on supported formats, noting which paths are managed fallbacks.

- **Performance guardrails**
  - Benchmark upload + draw pipelines for large images; ensure managed mipmap generation stays within frame budgets.
  - Utilize asynchronous decode tasks and staging-buffer pooling to hide latency.
  - Avoid redundant colour conversions by caching converted buffers and tracking colour-space metadata.

### 8. Document & PDF Export

**Goal**: enable PDF document creation and vector export parity (`SKDocument.CreatePdf` family) by leveraging a shim-owned PDF pipeline that consumes existing scene data—without altering Vello’s internals.

- **PDF backend (shim-owned)**
  - Build a dedicated Rust helper crate (separate from Vello) that depends on `printpdf`/`pdf-writer` and consumes Vello scene snapshots via public APIs. The helper crate is ours to modify while leaving upstream crates untouched.
  - Support incremental page streaming by emitting PDF objects as soon as pages close; expose callbacks for managed streams.
  - Handle colour spaces and ICC profiles by reusing the colour-management registry from section 6 (managed side) and embedding profiles through the helper crate.
  - Map transparency, blend modes, and image filters to available PDF constructs; fall back to rasterized inserts (generated managed-side) when necessary.

- **FFI bindings**
  - Expose document lifecycle functions (`document_create`, `begin_page`, `draw_scene_snapshot`, `end_page`, `close`) from the helper crate via FFI.
  - Allow registering managed callbacks for stream output and progress/error reporting.

- **Managed shim work**
  - Implement `SKDocument.CreatePdf` overloads by wrapping the helper FFI handles.
  - Translate `BeginPage` parameters into captured scene snapshots using existing APIs; no renderer modification needed.
  - Feed text, image, and shader data into the PDF helper, leveraging managed fallbacks for features not directly supported.
  - Handle metadata (`SKDocumentPdfMetadata`), bookmarks, and structure tree requirements in managed code before forwarding to the helper.

- **Fonts & text**
  - Reuse HarfBuzz font data already accessible via FFI to embed subsets; when not available natively, derive glyph outlines managed-side and pass them to the helper crate.
  - Cache embedded font subsets per document to keep file sizes small.

- **Testing & validation**
  - Generate PDF outputs for canonical test scenes and compare both structure and rasterized appearance against Skia-produced PDFs.
  - Validate metadata, bookmarks, and streaming callbacks via unit tests.
  - Add an integration sample in `samples/RenderDemo` that exports multi-page PDFs with filters, gradients, and text.
  - Update documentation on supported PDF features and note any managed fallbacks.

- **Performance guardrails**
  - Stream pages to avoid high memory usage; enforce backpressure when callbacks lag.
  - Cache reusable objects (patterns, gradients) in managed code and pass references to the helper crate.
  - Provide telemetry for page render time and output size to monitor regressions.

### 9. HarfBuzz Enhancements

**Goal**: achieve full HarfBuzzSharp parity by covering vertical metrics/origin fallbacks, richer buffer serialization diagnostics, and Unicode normalization helpers using additional FFI wrappers around the existing HarfBuzz API.

- **Rust / HarfBuzz bridge**
  - Create new shim-owned FFI bindings that call existing HarfBuzz functions (`hb_font_get_vertical_origin`, `hb_font_get_extents`, etc.) without modifying HarfBuzz itself.
  - Expose canonical compose/decompose utilities (`hb_unicode_compose/decompose`) and cluster diagnostics via the same approach.
  - Provide buffer serialization hooks returning JSON/trace-friendly snapshots by invoking HarfBuzz’s serialization helpers.
  - Surface buffer flag APIs (`BOT`, `EOT`, `PRESERVE_DEFAULT_IGNORABLES`) and script/language tagging by wrapping the existing HarfBuzz entry points.

- **FFI additions**
  - `vello_hb_font_get_vertical_origin`, `*_get_vertical_metrics`, `*_get_glyph_extents`.
  - `vello_hb_unicode_compose/decompose`, `vello_hb_unicode_normalize` for managed normalization paths.
  - `vello_hb_buffer_serialize` with options mirroring HarfBuzz (`Glyphs`, `Codepoints`, `JSON`) and callback-driven streaming.
  - Error reporting struct capturing shaping fallback reasons (missing glyphs, feature conflicts) for diagnostics.

- **Managed shim work**
  - Update `HarfBuzzSharp.Font` to surface vertical origin/metrics APIs (`GetGlyphVerticalOrigin`, `GetVerticalExtents`) and integrate fallbacks when data absent.
  - Implement normalization helpers (`Unicode.Compose`, `Unicode.Decompose`) and expose them via `HarfBuzzSharp.UnicodeFunctions`.
  - Enhance `Buffer.Serialize` to map new serialization options and propagate diagnostics; provide convenience wrappers for tooling.
  - Ensure `Buffer.SetFlags`, `SetClusterLevel`, and script/language setters map to new FFI for complete flag coverage.
  - Integrate shaping diagnostics into `SkiaSharpShim.FontMeasurement` to aid text measurement debugging.

- **Testing & validation**
  - Add parity tests comparing vertical layout metrics against upstream HarfBuzz for fonts with/without vertical tables (CJK, emoji).
  - Validate canonical composition/decomposition across normalization forms (NFC, NFD, NFKC) with round-trip assertions.
  - Extend buffer serialization tests to cover glyph/JSON outputs and verify tooling compatibility.
  - Include regression coverage for buffer flags and cluster levels (grapheme vs. character) across scripts.

- **Performance guardrails**
  - Cache vertical metric computations per glyph to avoid repeated font table lookups; invalidate on font change.
  - Keep serialization optional/off by default to avoid perf hits; guard with feature flags.
  - Add tracing for shaping diagnostics to monitor frequency of fallbacks without overwhelming logs.

## Action Plan

1. [ ] **Surface Creation & Resource Pools** – Add shim FFI bindings to existing surface APIs, implement managed pooling/flush paths, and validate CPU/GPU creation scenarios with parity tests.  
2. [ ] **Canvas Clip & Path Ops** – Ship wrapper FFI for existing `kurbo` operations plus managed fallbacks, update clip stack handling, and benchmark complex path workloads.  
3. [ ] **Text Measurement Parity** – Wire HarfBuzz/Parley via FFI handles, build the managed measurement cache, surface vertical metrics, and confirm complex-script parity.  
4. [ ] **Shader / Color Pipeline Bridging** – Expose existing shaders through FFI, add managed fallbacks for missing effects, integrate with `SKPaint`, and expand shader gallery coverage.  
5. [ ] **Image Filters & Path Effects** – Combine native filter/path-effect wrappers with SIMD managed implementations, update registry/caching, and record regression images.  
6. [ ] **Colour Space Pipeline** – Implement colour-space registry leveraging existing hooks, add managed ICC/LUT support, and run colour-accuracy/HDR validation.  
7. [ ] **Image I/O & Sampling** – Provide decode/encode FFI wrappers, managed resampling/mipmap fallbacks, and integrate DrawImage paint handling with caching.  
8. [ ] **Document / PDF Export** – Deliver the helper crate + FFI, managed `SKDocument` bridge, font embedding, and PDF parity comparisons.  
9. [ ] **HarfBuzz Enhancements** – Wrap additional HarfBuzz APIs via FFI, update managed buffers/fonts, and add normalization/serialization regression tests.  
10. [ ] **Procedural Texture Fallbacks** – Implement managed Perlin/turbulence generators and caching so shader features without native support remain available.  
11. [ ] **DrawImage Paint Integration** – Ensure image draws honour shaders/filters/path effects using the bridging layers, with high-DPI and sampling validation scenes.  
12. [ ] **Validation & Tooling** – Expand automated parity harness (filters, shaders, text, PDF), add telemetry hooks, update coverage docs, and track native vs. managed usage metrics.

Each milestone should update `docs/skiasharp-shim-api-coverage.md` / `docs/hardbuzzsharp-shim-api-coverage.md`, land automated tests, and capture performance metrics to ensure the Vello-backed implementations meet parity and throughput targets.
