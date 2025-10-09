# VelloSharp.Skia Migration Plan

## Vision And Success Criteria

- **Primary goal:** Replace `Avalonia.Skia`'s dependency on SkiaSharp with a drop-in `VelloSharp.Skia` shim so application source that references SkiaSharp-based Avalonia builds continue to compile/function without code changes.
- **Secondary goal:** Leverage Vello for all raster/vector output, including bitmap upload, text, image filters, and GPU compositions.
- **Stretch goal:** Provide a `GRContext`-compatible surface backed by WGPU/Vello so native Skia GPU interop consumers continue to work while being powered by Vello.

### Success metrics

- All Avalonia unit/integration tests that currently target `Avalonia.Skia` run against `VelloSharp.Skia` without source changes.
- Public `ISkiaSharpApiLeaseFeature` continues to function; third-party code can still request `SKCanvas`/`SKSurface`-like handles mapped to Vello equivalents.
- No regression in text shaping, gradient fills, image drawing, or filters across platforms currently supported by Vello.
- Optional stretch metric: `GRContext` lease returns an `IVelloWgpuContext` implementation usable by OpenGL/Vulkan bridging samples.

## Constraints And Assumptions

- We must maintain the SkiaSharp namespace surface to satisfy existing build-time bindings.
- Vello currently exposes CPU/GPU backends via our FFI; some Skia APIs (e.g., PDF/document output) have no counterpart and will require shimmed behavior or explicit `NotSupportedException` with documented alternatives.
- Text handling must continue to leverage HarfBuzz/ICU logic already present in Avalonia; the shim must map glyph rasterization to Vello font APIs (or extend Vello FFI accordingly).
- Performance parity is expected; we should budget time for Vulkan/Metal-specific fallbacks when WGPU is unavailable.
- Display-list parity (`SKPicture`, `SKDrawable`) requires Vello scene recording support; we may need to extend the FFI to emit/import serialized display lists for deferred renderers.
- Broader codec coverage (JPEG/WebP) is blocked on Vello exposing additional decoders; until then we surface capability checks and retain PNG-only behaviour.

## API Coverage Matrix

> Legend: **Usage** references representative Avalonia types/files. **Vello Equivalent** lists existing types (or proposed additions) in `VelloSharp`. **Action** enumerates required tasks (A = Already provided by Vello, E = Extend FFI, S = Shim-only).
>
> For a living, per-type breakdown of the shimmed APIs (including status and Vello dependencies) see
> `docs/skiasharp-shim-api-coverage.md`.

| Category | SkiaSharp API | Usage in Avalonia | Vello Equivalent / Proposed | Action |
| --- | --- | --- | --- | --- |
| Rendering Surfaces | `SKSurface`, `SKSurfaceProperties`, `SKSurfaceRenderTarget`, `SKCanvas` | `SurfaceRenderTarget`, `FramebufferRenderTarget`, `DrawingContextImpl` | `VelloSurface`, `VelloSurfaceRenderer`, `VelloCanvas` abstraction on top of Vello scene | A/E (wrap Vello render surface; extend for surface props) |
| GPU Context | `GRContext` (via `ISkiaSharpPlatformGraphics`) | `DrawingContextImpl`, control catalog GPU samples | `IVelloWgpuContext` bridging to WGPU `Device` | E (add GRContext-compatible adapter) |
| Bitmaps & Images | `SKBitmap`, `SKImage`, `SKImageInfo`, `SKImageCachingHint`, `SKColorType`, `SKAlphaType`, `SKColorSpace`, `SKData`, `SKCodec` | `WriteableBitmapImpl`, `ImageSavingHelper`, `PlatformRenderInterface` | `VelloBitmap`, `VelloImage`, FFI load/save helpers | E (extend Vello for decode/encode, color space) |
| Drawing Primitives | `SKPaint`, `SKPaintCache`, `SKPaints`, `SKSamplingOptions`, `SKMipmapMode`, `SKFilterMode`, `SKShader`, `SKShaderTileMode`, `SKPath`, `SKPathEffect`, `SKRoundRect`, `SKTextBlob`, `SKTextBlobBuilder` | `DrawingContextImpl`, `GeometryGroupImpl`, `StreamGeometryImpl`, `SKRoundRectCache` | `VelloPaint`, `VelloBrush`, `VelloPathBuilder`, `VelloTextBlob` | E (mirror paint/path config, caching) |
| Recording & Replay | `SKPicture`, `SKPictureRecorder`, `SKDrawable`, `SKNoDrawCanvas` | `ImmediateRendererSceneGraph`, `DeferredRendererSceneGraph`, `RenderTargetBitmapImpl` | `VelloSceneRecorder`, `VelloPicture`, `VelloDrawable` façade over Vello display lists | E/S (add recorder shim; extend Vello for picture serialization & playback) |
| Paths & Geometry | `SKPathHelper`, `SKPathMeasure`, `SKPathOp`, `SKPathFillType`, `SKPathVerb`, `SKGeometry`, `SKRegion`, `SKRegionOperation` | `StreamGeometryImpl`, `SkiaRegionImpl` | `VelloPath`, `VelloRegion` extension layer | E/S (region operations if absent) |
| Matrix & Transform | `SKMatrix`, `SKMatrix44`, `SKMatrix4x4` | `DrawingContextImpl`, `SKPaintHelper` | `VelloMatrix` (math types) | S (shim to System.Numerics + Vello FFI) |
| Colors | `SKColor`, `SKColorF`, `SKColorFilter`, `SKColorSpace` | `PixelFormatHelper`, `DrawingContextImpl` | `VelloColor`, `VelloColorSpace` | E (color filters support) |
| Text | `SKFont`, `SKFontManager`, `SKTypeface`, `SKGlyphTypefaceImpl`, `SKFontStyle*`, `SKFontEdging`, `SKFontHinting`, `SKTextAlign` | `GlyphTypefaceImpl`, `FontManagerImpl`, text layout pipeline | `VelloFont`, `VelloFontCollection`, `VelloTextShaper` (existing) | E (font hinting/edging toggles) |
| Caching Helpers | `SKCacheBase`, `SKTextBlobBuilderCache`, `SKRoundRectCache`, `SKPaintCache` | Caching for paints, round rects, text blobs | Vello-specific caches mirroring semantics | S (managed caches) |
| Documents | `SKDocument`, `SKEncodedImageFormat` | `ImageSavingHelper` (export) | optional `VelloDocument` (if available) otherwise stub | S/E (if Vello lacks document export, provide limited support) |
| Streams | `SKManagedStream` | `GlyphTypefaceImpl`, asset loading | Wrap .NET stream bridging to Vello | S (shim to managed stream) |
| Misc | `SKPixelGeometry`, `SKImageFilter`, `SKBlendMode`, `SKClipOperation`, `SKSamplingOptions`, `SKStrokeCap`, `SKStrokeJoin`, `SKCubicResampler`, `SKPathArcSize`, `SKPathDirection`, `SKPathMeasure`, `SKTextBlobBuilder` | `DrawingContextImpl`, `GeometryGroupImpl`, `Helpers` | Equivalent enumerations/structs in shim -> Vello pipelines | E (introduce mapping enums) |

### API inventory detail

- Primitive structs/enums (e.g., `SKPoint`, `SKRect`, `SKSizeI`) can be shimmed with thin wrappers over `System.Numerics` types and Vello's geometry inputs.
- Resource lifecycle is currently orchestrated through caches; our shim must implement IDisposable semantics mirroring Skia's pattern while delegating actual resources to Vello handles.
- GPU-specific APIs (OpenGL/Vulkan surface creation) must be rethought: we can expose stub handles that forward to WGPU surfaces, with restrictions documented.

## Work Breakdown & Progress Tracking

| Status | Step | Description | Dependencies | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| ✅ | 0 | Capture SkiaSharp API inventory and categorize usage. | none | Inventory list produced (see matrix) and checked into repo. |
| ✅ | 1 | Design `VelloSharp.Skia` namespace scaffold mirroring SkiaSharp types (interfaces, enums, structs). | Step 0 | `VelloSharp.Skia` project contains stub types compiling under namespace `SkiaSharp` with TODO markers. |
| ✅ | 2 | Implement resource primitives (colors, matrices, points) and ensure Avalonia builds against stubbed assembly. | Step 1 | Avalonia compiles when referencing `VelloSharp.Skia` with primitive structs implemented; unit tests compile. |
| ✅ | 3 | Implement drawing surface pipeline (surface/canvas/paint/path) backed by Vello scene/renderer + caching. | Step 2 | `DrawingContextImpl` executes simple draw (rect/fill/path) using shim; sample app renders basic geometry. |
| 🟡 | 4 | Map text APIs (fonts, text blobs, glyph metrics) to Vello font subsystem; extend FFI as needed. | Step 3 | Text rendering parity for baseline Latin fonts validated via render tests. HarfBuzzSharp-compatible shim shares Vello-backed shaper via rust bindings. |
| 🟡 | 5 | Support bitmap/image decoding & encoding (including `SKBitmap`, `SKImage`, `SKData`). | Step 3 | PNG decode/resize implemented via Vello; additional codecs (JPEG/WebP/etc.) tracked under Step 3e3. |
| ✅ | 6 | Implement display list recording/replay (`SKPicture`, `SKPictureRecorder`, `SKDrawable`, `SKNoDrawCanvas`) on top of Vello scene capture. | Steps 3-5 | Recorded pictures round-trip through shim; Avalonia deferred renderer replays via Vello. |
| ⬜ | 7 | Implement shaders/effects/filters (gradients, blur, color filters) via Vello pipelines, extending FFI if absent. | Steps 3-6 | Gradient fill & blur tests pass. |
| ⬜ | 8 | Provide GPU context lease (`ISkiaSharpPlatformGraphicsApiLease`) backed by WGPU; optionally expose GRContext compatibility layer. | Steps 3,5,6 | Control catalog OpenGL/Vulkan samples execute using WGPU-backed context. |
| ⬜ | 9 | Establish document/export story (optional PDF fallback or explicit not-supported w/ docs). | Steps 5-7 | Export APIs behave per spec (either implemented or documented). |
| ⬜ | 10 | Comprehensive regression suite & benchmarking (render tests, text tests, perf). | Prior steps | All Skia render/unit tests pass when run with shim; perf metrics documented. |
| ⬜ | 11 | Release packaging & documentation for `VelloSharp.Skia`. | Steps 1-10 | NuGet package ships with docs/guides; migration published. |

## Acceptance Criteria (Detailed)

1. **API parity:** For every SkiaSharp type listed in the matrix, either provide a functional mapping or a documented exception with alternative guidance.
2. **Rendering parity:** Pixel output for baseline render tests matches SkiaSharp within tolerance (<1% pixel diff).
3. **Performance:** Frame time within ±10% of current Skia backend on benchmark scenes, excluding GPU stretch goal.
4. **Reliability:** No unmanaged leaks (verified via stress test) and safe multi-threaded use consistent with existing Avalonia expectations.
5. **Documentation:** Developer guide describing differences, limitations, and migration steps.

## Risks & Mitigations

- **Vello feature gaps:** Some Skia features (advanced image filters, PDF export) may not exist. Mitigation: prioritize commonly used features, provide partial fallbacks or polyfills.
- **Text rendering parity:** Hinting/edging differences may cause visual regressions. Mitigation: early integration tests with glyph caches, extend Vello FFI to expose hinting toggles.
- **Third-party integrations expecting raw Skia handles:** Provide compatibility layer interfaces or explicit breaking change plan.

## Recent Plan Updates

- Shared rendering helpers (`RenderTargetDescriptor`, `VelloRenderPath`, lightweight bridges) now live in `VelloSharp.Rendering`, and both the Avalonia backend (`VelloView`, `VelloSurfaceView`) and the Skia shim (`SkiaRenderBridge`) reference the new project without behaviour changes.
- Gradient and sweep shader work (Steps 3b/3c) ship with checksum-backed validation in `samples/SkiaShimSmokeTest`, keeping rendering parity measurable.
- Step 3e tracks richer Vello features; current focus is 3e2 to unblock Avalonia's bitmap pipeline so it never falls back to SkiaSharp for decode/stream scenarios.
- Once 3e2 lands, we can stage follow-up texture upload and color space tasks against the extracted rendering layer without duplicating Avalonia plumbing.
- Removed the temporary `SixLabors.ImageSharp` dependency; `SKBitmap` decoding now goes through Vello's native PNG decode/resize helpers. Non-PNG formats remain not implemented and will surface as `null`/`NotSupportedException` so we can add targeted codecs later without regressing behaviour.
- Added a `HarfBuzzSharp` shim project that shapes text through the existing Vello Rust bindings (UTF-16 entrypoint + glyph metrics), giving Avalonia's text pipeline a drop-in replacement for HarfBuzzSharp while we flesh out Step 4.
- Step 4 now aims to route Avalonia's text services through `VelloTextShaper` with golden glyph baselines guarding regressions; Step 3f is complete and picture support is validated end-to-end.
- Step 3f landed: `SKPicture`/`SKPictureRecorder`/`SKDrawable` shims record via the Vello-backed canvas and replay correctly; `samples/SkiaShimSmokeTest` now records to a picture before rendering, protecting the path with checksum validation.
- Step 4a landed: `VelloTextServices.Initialize()` (invoked via `UseVelloSkiaTextServices`) binds Avalonia's `IFontManagerImpl`/`ITextShaperImpl` to the Vello implementations so Skia-shimmed apps render text entirely through Vello glyph/typeface pipelines.
- Staged `tests/VelloTextParityTests` to exercise the HarfBuzz shim with Vello-backed shaping and fallback flows; groundwork for Step 4b's golden metrics now lives alongside the recorded smoke tests.
- Introduced `VelloSharp.Text` with a shared `ParleyFontService` and `VelloTextShaperCore`, consolidating Parley font discovery and fallback shaping logic used by the Avalonia backend, Skia shim, and HarfBuzz wrapper.

## Next Actions

- [x] Step 1: Create `VelloSharp.Skia` project scaffold with namespace placeholders and shared enum definitions. (Existing project already provides initial surface; future work refines contents.)
- [x] Step 1a: Add automated script to track Skia API usage drift (`scripts/report_skia_usage.sh`, `docs/vello-skia-skia-usage.csv`).
- [x] Step 2: Implement basic struct shims (colors, points, rects) and unit tests verifying conversions to Vello types.
- [x] Step 3a: Flesh out `SKCanvas`/`SKSurface` drawing flows (fill, stroke, text) and validate with a minimal Vello render smoke test (`samples/SkiaShimSmokeTest`).
- [x] Step 3b: Add gradient brush support (linear/radial/two-point/sweep) and image drawing to `SKPaint`/`SKCanvas`; verified via extended smoke test.
- [x] Step 3c: Wire smoke test checksum assertion to fail on regressions (`dotnet run --project samples/SkiaShimSmokeTest`).
- [x] Step 3d: Extract shared rendering helpers (descriptor/path) into `VelloSharp.Rendering` for reuse by Avalonia backend & Skia shim.
- [x] Step 3e1: Apply sweep-gradient local matrix support within `SKShader.CreateSweepGradient`.
- [x] Step 3e2: Implement `SKBitmap` decoding/resizing paths (including `SKData`, `SKManagedStream`) and integrate with shim; Vello-backed PNG decode/resize is live and non-PNG formats intentionally throw `NotSupportedException` to make future codec work explicit.
- [ ] Step 3e3: Add JPEG/WebP decoding once Vello exposes the native codecs via FFI; surface capability detection and extend bitmap tests to cover new formats.
- [x] Step 3f: Implement `SKPicture`/`SKPictureRecorder`/`SKDrawable` on top of Vello scene recording so deferred render paths replay through the shim.
- [x] Step 4a: Hook Avalonia text services to `VelloTextShaper`/`VelloGlyphTypeface`, replacing HarfBuzzSharp usage so UI text renders through the shim end-to-end.
- [ ] Step 4b: Build shaping parity tests (golden glyph positions, rendered diff baselines) to validate the HarfBuzz/Vello text pipeline against SkiaSharp.

#### Step 3f – Implementation Outline

- **FFI alignment:** Confirm Vello scene recording API shape (`vello::Scene::to_bytes`, `SceneBuilder::from_bytes` equivalents) and ensure managed P/Invoke bindings cover buffer lifetime and thread safety.
- **Managed recorder layer:** Implement `VelloSceneRecorder` that mirrors `SKPictureRecorder` semantics (`BeginRecording`, `EndRecording`, optional `Record` overloads) while accumulating commands into a Vello scene graph.
- **Drawable bridge:** Provide `VelloPicture`/`VelloDrawable` wrappers that can draw onto an existing `SKCanvas` shim by replaying the captured Vello scene into the destination renderer.
- **No-draw canvas parity:** Add a `SkiaNoDrawCanvasShim` variant that routes state changes into the recorder without producing output, matching Skia's expectation for bounding-box queries.
- **Integration touchpoints:** Update Avalonia deferred rendering (`DeferredRendererSceneGraph`, `ImmediateRendererSceneGraph`) to consume the shimmed picture APIs and validate layout behaviors.
- **Validation:** Extend `samples/SkiaShimSmokeTest` with picture-recorded scenes, plus checksum-based regression tests comparing immediate vs deferred render output. ✅
- **Remaining gaps:** Introduce `SKNoDrawCanvas`-style recorder that tracks paint state changes without draw calls, and design a serialized picture format once Vello exposes scene export APIs.

### Ticket Breakdowns

- **Step 3e3 – JPEG/WebP codec FFI enablement:**
  - Draft `vello#ffi-codec-jpeg-webp` to track native decoder exposure (ownership: runtime/FFI team).
  - Draft `vsh#shim-jpeg-webp` to add capability probing, decode plumbing, and regression tests once the FFI lands.
  - Acceptance: PNG/JPEG/WebP parity tests in `samples/SkiaShimSmokeTest` + bitmap round-trip unit coverage.
- **Step 3f – Recorder shim (`SKPicture`/`SKDrawable`):**
  - Draft `vsh#scene-recorder-shim` for managed shims, including compatibility layers for `SKCanvas` recording APIs.
  - Draft `vello#ffi-scene-recording` asking for serialized display-list capture/import handles needed by the shim.
  - Acceptance: Avalonia deferred renderer replay executing through Vello scene playback with checksum parity.
- **Step 4a – Avalonia text services:**
  - `UseVelloSkiaTextServices` now binds `IFontManagerImpl`/`ITextShaperImpl` to Vello implementations via `VelloTextServices.Initialize()`, keeping shim text rendering fully on the Vello stack.
- **Step 4b – Shaping parity harness:**
  - Added `tests/VelloTextParityTests` with initial Vello HarfBuzz smoke tests (Latin, RTL, emoji, fallback) to seed golden-baseline coverage; next iteration compares against recorded reference metrics once captured.
  - Track remaining work: capture baseline glyph JSON/image artefacts, wire tolerances (<0.5 px position delta), integrate into CI, and expose OpenType feature toggles/variation axes so tab/line-break parity checks can exercise script-specific shaping.

### Coordination Notes

- Sync with the Vello FFI maintainers during the next triage call to confirm delivery windows for codec decoders and scene recording handles (blocking tickets above).
- Share the shim-side requirements doc (once drafted) to ensure FFI signatures cover stream callbacks and managed resource lifetime concerns.

### Golden Glyph Baseline Prep

- Enumerate representative UI text cases (Latin, CJK, combining marks, emoji) for baseline captures.
- Plan snapshot sources: reuse Avalonia glyph-run visual tests and export golden glyph position data using existing HarfBuzzSharp pipeline.
- Define comparison harness goals: per-glyph advance deltas, cluster break validation, and rendered image diff tolerance (<0.5 px average delta).
- Identify tooling updates needed in `samples/SkiaShimSmokeTest` or a new `tests/VelloTextParityTests` project to host these baselines. ✅ (`tests/VelloTextParityTests` now provides shaping smoke tests; expand with recorded goldens.)
- Capture golden glyph data (JSON + PNG) from current Skia baseline and compare against Vello shim within tolerance; add CI job to enforce parity.
