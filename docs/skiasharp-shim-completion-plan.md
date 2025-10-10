# VelloSharp Skia Shim Completion Plan

## Vision and Goals
- Deliver feature parity with Avalonia's SkiaSharp dependencies using native Vello execution paths across GPU and CPU backends.
- Replace all partial or managed-only behaviour with first-class Vello primitives, exposing consistent results across platforms and colour spaces.
- Maintain confidence through automated validation, benchmarks, and living gallery scenes in `samples/SkiaShimGallery`.

## Implementation Conventions
- [ ] All new native functionality lands in `ffi/vello_ffi` (or `ffi/vello_sparse_ffi` for CPU) with unit and integration coverage under `cargo test`.
- [ ] Corresponding P/Invoke glue lives in `bindings/VelloSharp.Native` and is surfaced via thin wrappers in `bindings/VelloSharp.*`.
- [ ] Every completed feature updates `docs/skiasharp-shim-api-coverage.md`, adds regression tests under `tests/VelloSharp.Skia.Tests`, and refreshes `samples/SkiaShimGallery`.
- [ ] Benchmarks that exercise the new surface area are captured in `docs/metrics/skia-shim-baselines.md`.

## Phase 0 – Program Foundations (1 sprint)
**Objectives**
- Lock in validation scaffolding that keeps the Skia shim and Vello FFI additions testable.
- Baseline current behaviour in the gallery to detect regressions as deeper native work lands.

**Deliverables**
- [ ] Introduce a conformance test harness (`tests/VelloSharp.Skia.Tests/SkiaParitySuite.cs`) that renders Skia reference images and Vello outputs, diffing via compute-assisted SSIM; wire into CI and `build-pack.yml`.
- [ ] Extend `samples/SkiaShimGallery` with baseline scenes for blend modes, gradients, image codecs, text hinting, and geometry stress cases to be toggled per feature.
- [ ] Document baseline metrics and outstanding gaps in `docs/skiasharp-shim-api-coverage.md` and replicate as Jira/GitHub issue list.

## Phase 1 – Painting, Blend Modes, and Shader Parity (2–3 sprints)
**Objectives**
- Remove `SKPaint`/`SKCanvas` partials by mapping every Skia blend mode and shader composition path to Vello.
- Validate shader graph behaviour through gallery scenes and automated tests.

**Deliverables**
- [ ] Implement full Skia blend mode coverage in `SKPaint` and `SKCanvas`. Extend the Vello renderer with GPU pipelines for `Multiply`, `Screen`, `Overlay`, `Darken`, `Lighten`, `ColorDodge`, `ColorBurn`, `HardLight`, `SoftLight`, `Difference`, `Exclusion`, `Hue`, `Saturation`, `Color`, and `Luminosity` by adding WGSL fragments in `ffi/vello_ffi/src/shaders/blend/` and new dispatch glue in `ffi/vello_ffi/src/renderer/passes.rs`. Surface new enums through `bindings/VelloSharp.Native/NativeMethods.Blend.cs` and update `SKPaint.cs`. Add parity tests that render all modes and diff results, then expand `samples/SkiaShimGallery` with an interactive blend mode explorer scene.
- [ ] Add colour filter and runtime shader composition support equivalent to Skia's `SKShader` compose APIs. Model shader DAGs inside Vello by extending the scene node encoding (`ffi/vello_ffi/src/scene/shader_graph.rs`) and introducing a `vello_shader_graph_create_compose` FFI. Update `SKShader.cs` to translate nested Skia shaders into graph nodes, handling local matrix stacks. Produce gallery samples showing composed gradients and image filters.
- [ ] Implement displacement, turbulence, and noise shaders required by Avalonia by adding compute-powered procedural shader nodes (WGSL compute shaders under `ffi/vello_ffi/src/shaders/procedural/`). Add managed factory methods in `SKShader` to map Skia parameters, and expand gallery scenes with animated procedural shaders.
- [ ] Finalise picture shader support so `SKPictureShader` no longer falls back to snapshots. Add scene serialization to `ffi/vello_ffi/src/scene/encoding.rs`, expose `vello_scene_serialize`/`_deserialize`, and implement lazy picture textures in `SKPicture.cs`. Add a gallery scene rendering picture shaders at multiple scales.

## Phase 2 – Raster Resources, Sampling, and Codec Coverage (2–3 sprints)
**Objectives**
- Provide a complete decode/encode pipeline that matches Skia's supported formats and colour types.
- Ensure sampling and colour management paths are hardware-accelerated.

**Deliverables**
- [ ] Expand `SkiaImageDecoder` to support JPEG, WebP, and GIF (static) using new FFI entry points (`vello_image_decode_jpeg`, `_decode_webp`, `_decode_gif`) implemented with the `image` crate and GPU-assisted YUV conversion kernels in `ffi/vello_ffi/src/image/decoders`. Update `SKCodec.cs` to negotiate frame info, and add gallery scenes showing codec fidelity alongside reference thumbnails.
- [ ] Provide image encode APIs for PNG, JPEG, and WebP by adding `vello_image_encode_*` functions that read render targets directly (zero-copy where possible via `wgpu` texture to buffer copies). Expose them through `SKImage.cs` and add regression tests emitting encode outputs compared to Skia. Update the gallery with a save/export tab that previews encoded artefacts.
- [ ] Broaden `SKImageInfo`/`SKImageInfoExtensions` to handle `Gray8`, `Rgb565`, `Argb4444`, `Bgra8888`, `F16`, and `F32` colour types by adding conversion kernels in `ffi/vello_ffi/src/image/convert.rs` (compute shaders where GPU acceleration is required). Add benchmarks for conversion throughput and refresh gallery scenes that display colour ramp validation targets.
- [ ] Enhance `SKSamplingOptions` to express cubic filters (`Mitchell`, `CatmullRom`) and anisotropic sampling by mapping to Vello sampler descriptors (`ffi/vello_ffi/src/sampling.rs`). Update `SKImage` draw paths to accept the richer enum and show high-quality up/down-sampling in the gallery with side-by-side comparisons.
- [ ] Migrate `SkiaSharp.IO` helpers from managed-only to native-backed spans: expose streaming decode APIs (`vello_image_decode_stream`) that can work with `SKManagedStream`. Provide tests reading from large files and extend the gallery with a streaming decode demo.

## Phase 3 – Geometry and Path Operations (1–2 sprints)
**Objectives**
- Close the gaps in rounded rectangle fidelity and region algebra so complex vector content matches Skia output.

**Deliverables**
- [ ] Implement analytic arc joins for `SKRoundRect` by extending the path builder in `ffi/vello_ffi/src/geometry/arc.rs` to emit exact elliptical arcs and adaptive tessellation. Update `SKGeometry.cs` to call the new builder and validate against Skia snapshots. Refresh gallery geometry scenes to compare rounded rectangle corners under extreme radii.
- [ ] Add region boolean operations (`Union`, `Intersect`, `Difference`, `Xor`) by wiring Kurbo-based clipping in `ffi/kurbo_ffi` or introducing a GPU sweep-line compute kernel (`ffi/vello_ffi/src/geometry/boolean.rs`). Surface APIs through `SKPath.Op` and `SKRegion` analogues, add unit tests covering degenerate cases, and update the gallery with region overlay demonstrations.
- [ ] Provide path measurement helpers (tangents, contour length) if required by downstream controls by exposing `vello_path_measure` FFI. Integrate into `SKPath` extension methods and add gallery diagnostics overlays showing arc length markers.

## Phase 4 – Text and Typography Fidelity (2 sprints)
**Objectives**
- Match Skia's glyph positioning, hinting, and font discovery to remove partial implementations in `SKFont`, `SKFontManager`, and `SKTextBlob`.

**Deliverables**
- [ ] Wire font hinting and edging options into the Vello text pipeline by extending `ffi/vello_ffi/src/text/shaping.rs` to support grayscale and LCD subpixel filters (WGSL compute for per-channel offset). Update `SKFont.cs` to translate `SKFontHinting`/`SKFontEdging`, add golden image tests for glyph rendering, and enhance gallery text scenes with toggles for hinting styles.
- [ ] Complete `SKFontManager` by integrating platform font discovery (DirectWrite on Windows, CoreText on macOS/iOS, Fontconfig on Linux) through a native abstraction layer in `ffi/vello_ffi/src/text/font_manager`. Provide caching and async font loading hooks, add managed fallbacks, and expand the gallery to include a font browser demonstrating system-wide fonts.
- [ ] Finalise `SKTextBlob` intercept calculations by adding glyph intercept measurement utilities in `ffi/vello_ffi/src/text/metrics.rs`, exposing `vello_text_measure_intercepts`. Update `SKTextBlob.cs` to compute bounding runs, add regression tests with complex scripts, and create a gallery scene visualising selection regions.
- [ ] Ensure glyph fallback and shaping for complex scripts (Arabic, Indic) by validating HarfBuzz integration or Vello shaping enhancements. Capture script-specific test cases and update the gallery with multilingual text samples.

## Phase 5 – Recording, CPU Parity, and Final Integration (2 sprints)
**Objectives**
- Remove remaining partials (`SKPicture`, CPU sparse renderer, sampling toggles) and ensure consistent behaviour across backends.

**Deliverables**
- [ ] Implement scene recording serialization for `SKPicture`. Add binary and incremental encoders in `ffi/vello_ffi/src/scene/serialization.rs`, expose `vello_picture_record_start/finish/replay`, and update `SKPictureRecorder.cs` to stream commands directly. Provide regression tests that round-trip complex scenes and add a gallery tool to export/import recorded pictures.
- [ ] Close CPU sparse renderer gaps by porting GPU tiling optimisations to `ffi/vello_sparse_ffi/src/renderer.rs`, including compute-driven tile culling and gradient evaluation. Update `CpuScene`/`CpuRenderer` wrappers, run performance benchmarks, and enhance the gallery with a backend toggle comparing GPU vs CPU output.
- [ ] Add deterministic snapshotting for cross-thread replay via `SkiaBackendService`, guaranteeing identical results regardless of backend. Introduce smoke tests covering multi-threaded replay and show the behaviour in the gallery through a stress toggle.
- [ ] Final documentation sweep: update `docs/skiasharp-shim-api-coverage.md` statuses to "Complete", record ADRs for major shader/codec decisions under `docs/adrs`, and produce migration notes in `docs/releases/next.md`. Capture final gallery screenshots illustrating each completed feature.

## Risks and Open Questions
- Advanced blend and colour filter modes may require new WGSL features; validate against current `wgpu` limits and plan fallbacks.
- Font discovery on Linux distributions is highly variable; allocate time for packaging shared libraries and testing within sandboxed environments.
- Adding full codec support increases binary size; explore optional feature flags or dynamic loading strategies.

## Success Indicators
- 100% of entries in `docs/skiasharp-shim-api-coverage.md` show **Complete** status with linked tests.
- `samples/SkiaShimGallery` exercises every Skia surface Avalonia relies on, with automated screenshots captured in CI.
- Performance budgets for GPU and CPU paths remain within 5% of current baselines despite the richer feature set.
