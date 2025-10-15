# Avalonia Vello SkiaSharp Sample Plan

## Vision and Goals
- Deliver `samples/AvaloniaVelloSkiaSharpSample`, a gallery that exercises every public surface exposed by `VelloSharp.Skia.*`, proving the shim can replace SkiaSharp in Avalonia and partner apps.
- Pair each SkiaSharp concept with an interactive Vello lease-driven scene, demonstrating real rendering of vector, bitmap, text, shader, and runtime effects in both CPU and GPU backends.
- Match or exceed the API coverage listed in `docs/skiasharp-shim-api-coverage.md`; every type or major member must appear in at least one page, targeting 100% coverage.
- Ship prescriptive documentation so contributors can extend the sample, validate behaviours, or diagnose gaps without reverse engineering.

## Architecture & Conventions
- Base the hosted application on `AvaloniaVelloControlsSample` shell: single `MainWindow` with left-aligned `TabControl` navigation, consistent styling, and optional theme toggle.
- Each tab hosts a `UserControl` backed by its own view model; view models manage Skia/Vello state (surfaces, canvases, paints, etc.), while views own UI and Vello lease orchestration.
- Reuse `AvaloniaVelloCommon` for shared styles, asset loading, the `IVelloApiLeaseFeature` pattern, font caches, and dispatch timers.
- Centralise Skia-to-Vello conversions inside `samples/AvaloniaVelloSkiaSharpSample/Rendering`: e.g., `SkiaLeaseSurface` handles `OnRender`, requests the lease, and invokes supplied scene builder delegates for each page.
- Implement service layer (`Services/SkiaResourceService`, `Services/SkiaSceneRecorder`) for loading assets, recording pictures, serialising `SKData`, and exporting diagnostics.
- Ensure every sample can switch between CPU and GPU backends at runtime (toggle in header) by binding to `SkiaBackendService` factories.
- Persist sample captures (renders, command dumps, encoded images) under `artifacts/samples/skiasharp`, using timestamped filenames.

## Current Status
- Dual-targeting `net9.0` and `net9.0-windows10.0.19041.0` is live in `samples/AvaloniaVelloSkiaSharpSample/AvaloniaVelloSkiaSharpSample.csproj`, enabling cross-platform smoke tests while keeping Windows-specific features available.
- The navigation contract (`Navigation/ISamplePage`, `ViewModels/Pages/SamplePageViewModel`) drives tab discovery, activation hooks, and lease renderers. `MainWindow.axaml` exposes backend and theme toggles backed by `MainWindowViewModel`.
- Backend switching flows through `Services/SkiaBackendService` and `Rendering/SkiaLeaseSurface`, so CPU/GPU transitions invalidate the renderer and refresh the scene on demand.
- Shared services are wired: `SkiaResourceService` hydrates fonts/images/shaders for consumers, while `SkiaSceneRecorder` powers the recording studio and smoke captures.
- Assets now ship under `Assets/fonts`, `Assets/images`, `Assets/vectors`, and `Assets/shaders`; sample pages load them via the resource service instead of embedding constants.
- `tests/AvaloniaVelloSkiaSharpSample.Tests` exercises every page under the default backend without tripping CA1416; platform checks in `Program.cs` and cross-target analyzer settings keep builds warning-clean.

## Dependencies & Assets
- [x] Fonts: reuse Roboto, Inconsolata, Noto Emoji, and Roboto Flex (`Assets/fonts`) to exercise `SKFont` variation APIs.
- [x] Images and sprites: ship PNG, JPEG, WebP, and GIF samples (`Assets/images/*`) plus gradient LUT textures for shader demos.
- [x] Vector data: store JSON/SVG snippets (`Assets/vectors`) for the geometry explorer and matrix tooling.
- [x] Shaders: include WGSL/SkSL scripts (`Assets/shaders`) for runtime effect presets and diagnostics.
- [x] Packages: reference `AvaloniaVelloCommon`, `MiniMvvm`, `Avalonia.Diagnostics`, and `VelloSharp.Skia.*` bindings in the sample project.
- [x] Testing: rely on the xUnit harness and smoke coverage in `tests/AvaloniaVelloSkiaSharpSample.Tests` to validate headless rendering.

## Phase 0 – Project Skeleton & Shared Infrastructure
**Objectives**
- Stand up the application shell, navigation, and shared services so feature pages can plug in modularly.

**Deliverables**
- [x] Create `samples/AvaloniaVelloSkiaSharpSample/AvaloniaVelloSkiaSharpSample.csproj` targeting `net9.0-windows` + cross-platform net9.0, referencing `AvaloniaVelloCommon`, `VelloSharp.Skia.Core`, `VelloSharp.Skia.Windows`, `MiniMvvm`, and optionally `CommunityToolkit.Diagnostics`.
- [x] Add project to `VelloSharp.sln`, update `Directory.Build.props` with shared compiler settings, and ensure packaging maps fonts/images into output.
- [x] Scaffold `App.axaml`, `App.axaml.cs`, `Program.cs`, `ViewLocator.cs`, and `AppIcon` (ICO + PNG). Mirror resource dictionaries from controls sample for consistent theming.
- [x] Implement `MainWindow.axaml` with a header showing sample title, backend selector (GPU/CPU), and diagnostics/theme toggles; `TabControl` uses `TabStripPlacement="Left"` and binds to `ObservableCollection<SamplePageViewModel>`.
- [x] Define `Navigation/ISamplePage` (`Title`, `Description`, `Icon`, `ContentFactory`, `DocumentationLinks`) and base view model class with activation hooks.
- [x] Build `Rendering/SkiaLeaseSurface.cs`: inherits `Control`, uses `IVelloApiLeaseFeature` to obtain `Scene`/`Renderer`, executes delegates returning `SkiaLeaseRenderContext` (contains `SKSurface`, `SKCanvas`, current backend info).
- [x] Provide `Services/SkiaResourceService` loading `SKData`, decoding images, and caching `SKImage`/`SKTypeface`. Manage `SKManagedStream` lifecycle.
- [x] Provide `Services/SkiaSceneRecorder` wrapping `SKPictureRecorder`, enabling capture/replay plus export to `.skp` (if needed) and JSON command log.
- [x] Establish diagnostics infrastructure: `Diagnostics/SkiaCaptureRecorder` storing screenshots (via `SKSurface.Snapshot`) and metadata (paints used, GPU info).
- [x] Wire headless smoke test harness to instantiate `MainWindow`, iterate tabs, and trigger rendering once to guard regressions.

## SkiaSharp API Coverage Map
| API block | Representative types/members | Sample page(s) | Coverage notes |
| --- | --- | --- | --- |
| Surfaces & Canvas | `SKSurface`, `SKCanvas`, `SKMatrix`, `SKPaint`, draw primitives, layers, clip, save/restore | Surface Dashboard, Canvas Studio | Demonstrate scene lease, CPU/GPU toggle, layer stack visualiser. |
| Paths & Geometry | `SKPath`, `SKPathBuilder`, `SKPathEffect`, `SKRegion`, `SKMatrix`, `SKPoint`, `SKRect` helpers | Geometry Explorer | Visualise path ops, dash effects, boolean operations, offsets. |
| Brushes & Shaders | `SKShader`, gradient factories, `SKImageFilter`, `SKColorFilter`, `SKPaint.Style`, blend modes | Shader & Color Lab | Compose gradient/image shaders, animate blend modes, show filter graphs. |
| Images & Bitmaps | `SKBitmap`, `SKImage`, `SKPixmap`, `SKImageInfo`, `SKCodec`, `SKData`, encode/decode helpers | Image Workshop | Load PNG/JPEG/WebP/GIF (as available), show resizing, encoding exports. |
| Text & Fonts | `SKFont`, `SKTypeface`, `SKTextBlob`, `SKFontManager`, hinting, subpixel, glyph bounds | Typography Playground | Render paragraphs, variable fonts, alignments, metrics overlays. |
| Recording & Pictures | `SKPictureRecorder`, `SKPicture`, `SKDrawable`, `SKCanvas.DrawPicture`, serialization | Recording & Replay | Record animated sequences, replay with transforms, export `.skp`. |
| Runtime Effects & GPU | `SKRuntimeEffect`, `SKRuntimeShaderBuilder`, uniform inputs, shader compilation errors | Runtime Effect Forge | Run SkSL-like snippets, animate uniforms, fallback on CPU. |
| Filters & Masks | `SKColorFilter`, `SKImageFilter`, `SKMaskFilter`, blur/shadow, table filters | Filter Lab | Chain filters, compare output vs reference, highlight missing implementations. |
| IO & Streams | `SKManagedStream`, `SKStreamAsset`, `SKFileStream`, `SKMemoryStream`, `SKData` | IO & Diagnostics | Show streaming decode, manual data creation, disposal semantics. |
| Utility & Regions | `SKRegion`, `SKRoundRect`, `SKVertices`, `SKPictureRecorder` advanced features | Advanced Utilities | Cover rounding, vertex meshes, region fills, picture culling. |

## Sample Page Breakdown

### 1. Surface Dashboard
**Purpose** – Introduce lease flow, surface lifetimes, backend toggles, and global canvas options.

**Completed**
- [x] `SurfaceDashboardViewModel` surfaces backend descriptors, frame dimensions, and smoothed frame timing sourced from `SkiaBackendService`.
- [x] Live scene renders primitives with toggles for `SaveLayer`, `ClipRect`, `ClipPath`, and matrix reset while updating the matrix stack summary displayed in the UI.
- [x] Snapshot capture flows through the shared `CaptureSnapshotCommand`/`SkiaCaptureRecorder`.

**Outstanding**
- [ ] Report surface color type/sample count alongside dimensions (requires backend telemetry hook).
- [ ] Validate `SKSurface.WrapBackendRenderTarget` when the shim exposes the necessary entry points.

**Coverage Targets**
- `SKSurface`, `SKCanvas`, state stack (`Save`, `Restore`), `ClipRect`, `ClipPath`, `Clear`, `DrawPaint`, `Flush`, `SKMatrix`.

### 2. Canvas & Paint Studio
**Purpose** – Explore `SKPaint` styles, blend modes, stroke caps/joins, dash effects, and anti-alias toggles.

**Completed**
- [x] Delivered `CanvasPaintStudioViewModel` with configurable `SKPaint` style, stroke width, cap/join, antialias, and blend mode selections plus a gradient-aware palette.
- [x] Rendered ribbon + stroke demos that respond to the paint settings and expose summary text in the UI, with optional guides to visualise stroke geometry.

**Outstanding**
- [ ] Extend the demo with `DrawPoints`, `DrawVertices`, and dash/arc samples.
- [ ] Layer in blend mode comparisons against reference thumbnails and highlight unsupported modes.
- [ ] Surface `SKPaint.PathEffect`/`SKMaskFilter` exploration once the shim exposes the requisite features.

**Coverage Targets**
- `SKPaint` properties, `SKBlendMode`, `SKStrokeCap`, `SKStrokeJoin`, `SKPaint.PathEffect`, `SKMaskFilter`, `DrawVertices`, `SKPointMode`.

### 3. Geometry Explorer
**Purpose** – Manipulate `SKPath` objects, boolean operations, tessellation, and region computations.

**Completed**
- [x] Added configurable primary/secondary outlines with rotation/scale/offset sliders, bounds overlays, and control-point visualisation.
- [x] Displayed geometry summaries (command counts, bounds) and leveraged `SkiaResourceService` assets for secondary visuals.
- [x] Implemented boolean combine explorer using `SKPath.Op` with live overlays and summary metrics.

**Outstanding**
- [ ] Add an interactive path builder with segment-level editing and `SKRegion` overlays.
- [ ] Demonstrate `SKPath.Measure`, offset/simplify helpers, and JSON export of constructed paths.

**Coverage Targets**
- `SKPath`, `SKPathBuilder`, `SKPath.Op`, `SKPathMeasure`, `SKRegion`, `SKRoundRect`, matrix transforms.

### 4. Shader & Color Lab
**Purpose** – Cover gradient, image, perlin noise, composed shaders, and color filter chains.

**Status**
- [ ] Dedicated Shader & Color Lab page has not been implemented yet; the following backlog remains for a future milestone.

**Implementation Steps**
- [ ] Implement `ShaderLabViewModel` with gradient editor (linear, radial, sweep, two-point conical) feeding `SKShader` factories (`CreateLinearGradient`, etc.).
- [ ] Add `ImageShader` creation using loaded textures; provide tile mode controls.
- [ ] Compose shaders (with optional fallback to single pass) to demonstrate `SKShader.CreateCompose`.
- [ ] Introduce color filters: matrix filters, gamma correction, table filters; visualise filter graph (nodes) and effect on sample image.
- [ ] Hook `SKImageFilter` blur, drop shadow, color filter; run both CPU/GPU to compare results.
- [ ] Capture before/after bitmaps and export as PNG.

**Coverage Targets**
- `SKShader` family, `SKShaderTileMode`, `SKColorFilter`, `SKColorFilters`, `SKImageFilter`, `SKMaskFilter`, `SKPaint.ImageFilter`.

### 5. Image Workshop
**Purpose** – Exercise decode, encode, resizing, pixel access, and mipmap generation.

**Completed**
- [x] Procedural image modes (gradient, checkerboard, plasma) render via the shim, with zoom, animation, histogram overlay, and metadata summary updated live.
- [x] Asset pipeline driven through `SKCodec`, exposing metadata capture, half-resolution scaling, and guarded PNG export fallbacks.

**Outstanding**
- [ ] Provide pixel inspector tooling and CPU vs GPU decoding comparisons when FFI paths land.

**Coverage Targets**
- `SKCodec`, `SKBitmap`, `SKImage`, `SKImageInfo`, `SKPixmap`, `SKData`, encode/decode helpers, `SKColorType`, `SKAlphaType`.

### 6. Typography Playground
**Purpose** – Render text with different fonts, hinting, subpixel rendering, shaping (via HarfBuzz), and fallback flows.

**Completed**
- [x] Implemented preset text samples with size, gradient fill, subpixel, and metrics toggles; draws baseline rectangles and glyph bounds for quick inspection.

**Outstanding**
- [ ] Wire up `SKFontManager`-driven font picker and custom typeface loading.
- [ ] Add shaping pipeline (HarfBuzz) with `SKTextBlob` rendering and draw-on-path examples.
- [ ] Demonstrate fallback handling across multi-script paragraphs and expose edging/hinting controls.

**Coverage Targets**
- `SKFont`, `SKTypeface`, `SKFontManager`, `SKTextBlobBuilder`, `SKShaper` (if available), text metrics APIs, glyph path extraction.

### 7. Recording & Replay Studio
**Purpose** – Capture `SKCanvas` commands into `SKPicture`, replay with transforms, and export logs.

**Completed**
- [x] `RecordingStudioViewModel` wraps `SkiaSceneRecorder`, captures scripted scenes into `SKPicture`, cycles recordings, and renders animated playback with status updates.

**Outstanding**
- [ ] Replay frames into alternate surfaces (`DrawPicture` with transforms) and expose `.skp`/JSON exports via `SkiaSceneRecorder`.
- [ ] Showcase custom `SKDrawable` integration and multiple `BeginRecording` variants with overlays/timelines.

**Coverage Targets**
- `SKPictureRecorder`, `SKPicture`, `SKDrawable`, `SKCanvas.DrawPicture`, `SKNWayCanvas` (if implemented), serialization helpers.

### 8. Runtime Effect Forge
**Purpose** – Compile and run runtime effects (SkSL or WGSL bridging), uniform updates, and error reporting.

**Completed**
- [x] Preset-based SkSL compilation via `SKRuntimeEffect.Create` with intensity/speed toggles, animation, and compilation status messaging.
- [x] Inline shader editor with compile/reset commands, real-time diagnostics, and status reporting.

**Outstanding**
- [ ] Expand uniform inspector (matrices, textures) and capture GPU/CPU fallback timing.
- [ ] Support child effect composition and export compiled bytecode/diagnostics logs.

**Coverage Targets**
- `SKRuntimeEffect`, `SKRuntimeEffectUniform`, `SKRuntimeShaderBuilder`, uniform binding, sampling textures, error handling.

### 9. IO & Diagnostics Workbench
**Purpose** – Demonstrate stream APIs, data serialization, and debugging helpers.

**Completed**
- [x] Refreshable diagnostics load assets through `SkiaResourceService`, summarising `SKImage`, `SKData`, and streamed shader previews with optional color-space overlays.
- [x] UI surfaces backend descriptor details and ties into the capture pipeline.

**Outstanding**
- [ ] Expand stream coverage to `SKFileStream`/`SKManagedStream`/`SKMemoryStream` API edges (seek, IsAtEnd) and log reference counts.
- [ ] Add command log viewer + backend capability telemetry, and stress-test toggles for lease scenarios.

**Coverage Targets**
- `SKData`, stream types, `SKColorSpace`, `SKEncodedInfo`, diagnostic utilities, backend capability queries.

### 10. Advanced Utilities (optional expansion)
**Purpose** – Cover remaining niche APIs (e.g., `SKVertices`, `SKPathEffect`s not elsewhere, `SKRegion` operations).

**Completed**
- [x] Implemented matrix/transform playground combining composite paths, bounds overlays, and bitmap resampling to stress transform utilities.

**Outstanding**
- [ ] Prototype mesh/vertices editor, overdraw diagnostics, and advanced path/region effects as optional expansions.

**Coverage Targets**
- Remaining APIs flagged Partial/Stub in coverage doc; update as implementations land.

## Validation & Documentation
- [x] Add headless smoke test to ensure each tab renders without exceptions on the default backend (`tests/AvaloniaVelloSkiaSharpSample.Tests/SampleSmokeTests.cs`).
- [x] Resolve CA1416 analyzer noise by guarding platform-specific windowing in `Program.cs` and relying on the dual-TFM build to keep smoke tests warning-clean.
- [x] Update `README.md` sample matrix to include AvaloniaVelloSkiaSharpSample with screenshots and quick start instructions (copy pending).
- [x] Extend `docs/skiasharp-shim-api-coverage.md` with “Sample coverage” column linking to the relevant page(s).
- [ ] Capture GIF/MP4 walkthroughs per page and store under `docs/media/skiasharp-sample`.
- [ ] Document known gaps (e.g., unsupported blend modes) and reference planned fixes in `docs/skiasharp-shim-completion-plan.md`.
- [ ] Ensure DEBUG runs assert `ShimNotImplemented.Throw` is never hit during sample interactions; add telemetry logging for quick triage.

## Milestone Exit Criteria
- All tasks above checked off, automated smoke tests green across supported OSes, and manual verification shows consistent output between CPU/GPU backends.
- `docs/skiasharp-shim-api-coverage.md` reports 100% coverage with sample references.
- Artifacts repository contains capture bundles (renders, command logs, encoded exports) for at least five scenarios (gradient, image IO, geometry ops, text, runtime effect).
- No regressions flagged by Avalonia integration tests or existing Skia parity suites after sample changes.
