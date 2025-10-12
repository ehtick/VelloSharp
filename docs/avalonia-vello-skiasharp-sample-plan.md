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

## Dependencies & Assets
- Fonts: reuse Roboto, Inconsolata, Noto Emoji from existing samples; add variable fonts (Roboto Flex) to exercise `SKFont` variation APIs.
- Images and sprites: add PNG, JPEG, WebP, GIF assets under `Assets/images`. Include gradient LUT textures for shader demos.
- Vector data: include SVG path snippets or JSON-defined path commands for geometry explorer.
- Shaders: ship WGSL/SkSL scripts under `Assets/shaders` for `SKRuntimeEffect` demos; include sample uniform sets.
- Packages: reference `AvaloniaVelloCommon`, `MiniMvvm`, `Avalonia.Diagnostics`, and ensure `VelloSharp.Skia.*` bindings are consumable.
- Testing: rely on xUnit harness already in repo; add sample-enabled smoke tests in `tests/SamplesSmokeTests`.

## Phase 0 – Project Skeleton & Shared Infrastructure
**Objectives**
- Stand up the application shell, navigation, and shared services so feature pages can plug in modularly.

**Deliverables**
- [ ] Create `samples/AvaloniaVelloSkiaSharpSample/AvaloniaVelloSkiaSharpSample.csproj` targeting `net9.0-windows` + cross-platform net9.0, referencing `AvaloniaVelloCommon`, `VelloSharp.Skia.Core`, `VelloSharp.Skia.Windows`, `MiniMvvm`, and optionally `CommunityToolkit.Diagnostics`.
- [ ] Add project to `VelloSharp.sln`, update `Directory.Build.props` with shared compiler settings, and ensure packaging maps fonts/images into output.
- [ ] Scaffold `App.axaml`, `App.axaml.cs`, `Program.cs`, `ViewLocator.cs`, and `AppIcon` (ICO + PNG). Mirror resource dictionaries from controls sample for consistent theming.
- [ ] Implement `MainWindow.axaml` with a header showing sample title, backend selector (GPU/CPU), and diagnostics toggle; `TabControl` uses `TabStripPlacement="Left"` and binds to `ObservableCollection<SamplePageViewModel>`.
- [ ] Define `Navigation/ISamplePage` (`Title`, `Description`, `Icon`, `ContentFactory`, `DocumentationLinks`) and base view model class with activation hooks.
- [ ] Build `Rendering/SkiaLeaseSurface.cs`: inherits `Control`, uses `IVelloApiLeaseFeature` to obtain `Scene`/`Renderer`, executes delegates returning `SkiaLeaseRenderContext` (contains `SKSurface`, `SKCanvas`, current backend info).
- [ ] Provide `Services/SkiaResourceService` loading `SKData`, decoding images, and caching `SKImage`/`SKBitmap`/`SKTypeface`. Manage `SKManagedStream` lifecycle.
- [ ] Provide `Services/SkiaSceneRecorder` wrapping `SKPictureRecorder`, enabling capture/replay plus export to `.skp` (if needed) and JSON command log.
- [ ] Establish diagnostics infrastructure: `Diagnostics/SkiaCaptureRecorder` storing screenshots (via `SKSurface.Snapshot`) and metadata (paints used, GPU info).
- [ ] Wire headless smoke test harness to instantiate `MainWindow`, iterate tabs, and trigger rendering once to guard regressions.

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

**Implementation Steps**
- [ ] Create `SurfaceDashboardViewModel` exposing backend selection, surface size, color type, sample count, and `SKSurface.Create` scenarios (GPU vs CPU).
- [ ] Show live stats (frame time, command count, backend adapter) pulled from lease diagnostics.
- [ ] Render grid of primitives using `SKCanvas` draw calls (rect, oval, arc, text, image) with toggles for `SaveLayer` usage, `ClipRect`, `ClipPath`, and `ResetMatrix`.
- [ ] Visualise matrix stack: push transforms on canvas and display stack list in UI.
- [ ] Implement `ExportSnapshotAsync` capturing `SKSurface.Snapshot`, saving as PNG (via `SKData`).
- [ ] Validate `SKSurface.WrapBackendRenderTarget` path with fallback stub (if available).

**Coverage Targets**
- `SKSurface`, `SKCanvas`, state stack (`Save`, `Restore`), `ClipRect`, `ClipPath`, `Clear`, `DrawPaint`, `Flush`, `SKMatrix`.

### 2. Canvas & Paint Studio
**Purpose** – Explore `SKPaint` styles, blend modes, stroke caps/joins, dash effects, and anti-alias toggles.

**Implementation Steps**
- [ ] Build `CanvasPaintStudioViewModel` with `SKPaint` configuration (style, stroke width, cap/join, blend mode, antialias, filter quality).
- [ ] Provide palette of `SKColor` choices and gradient brush creation.
- [ ] Render sample strokes and fills, overlay real-time metadata (computed stroke path, dash pattern) using Vello annotate pass.
- [ ] Showcase `DrawPoints`, `DrawLine`, `DrawArc`, `DrawPath`, `DrawRoundRect`, `DrawVertices`.
- [ ] Include blend mode explorer comparing results vs reference thumbnails; highlight unsupported modes with warning.
- [ ] Expose `SKPaint.PathEffect` (dash, corner, discrete) plus `SKMaskFilter` (blur) once supported.

**Coverage Targets**
- `SKPaint` properties, `SKBlendMode`, `SKStrokeCap`, `SKStrokeJoin`, `SKPaint.PathEffect`, `SKMaskFilter`, `DrawVertices`, `SKPointMode`.

### 3. Geometry Explorer
**Purpose** – Manipulate `SKPath` objects, boolean operations, tessellation, and region computations.

**Implementation Steps**
- [ ] Provide path builder UI (move, line, quad, cubic, conic, arc). Visualise control points and tangents.
- [ ] Implement boolean operations using `SKPath.Op` (union, intersect, difference, XOR, reverse difference) with overlays.
- [ ] Demonstrate `SKPath.Simplify`, `SKPath.Offset`, `SKPath.Transform`, `Contains`.
- [ ] Integrate `SKRegion` by deriving regions from paths, highlight pixel coverage.
- [ ] Show `SKPathMeasure` (distance, tangent) results along path; animate marker moving along path.
- [ ] Export path to JSON/`SKData` along with screenshot.

**Coverage Targets**
- `SKPath`, `SKPathBuilder`, `SKPath.Op`, `SKPathMeasure`, `SKRegion`, `SKRoundRect`, matrix transforms.

### 4. Shader & Color Lab
**Purpose** – Cover gradient, image, perlin noise, composed shaders, and color filter chains.

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

**Implementation Steps**
- [ ] Load assets via `SKCodec`. Display frame info, color type, orientation, ICC profile metadata.
- [ ] Demonstrate decoding into `SKBitmap`, `SKImage`, `SKPixmap`. Provide per-channel histogram computed via shader.
- [ ] Showcase `SKBitmap.Resize`, `SKImage.Resize`, `SKImage.Subset`, `SKBitmap.ExtractAlpha`.
- [ ] Implement encode panel: export selected image as PNG/JPEG/WebP using `SKImage.Encode`.
- [ ] Provide pixel inspector: click to read pixel values with `SKColor`. Show `SKPixmap.ReadPixels` usage.
- [ ] Compare CPU vs GPU decoding path (if FFI supports).

**Coverage Targets**
- `SKCodec`, `SKBitmap`, `SKImage`, `SKImageInfo`, `SKPixmap`, `SKData`, encode/decode helpers, `SKColorType`, `SKAlphaType`.

### 6. Typography Playground
**Purpose** – Render text with different fonts, hinting, subpixel rendering, shaping (via HarfBuzz), and fallback flows.

**Implementation Steps**
- [ ] Provide font picker sourced from `SKFontManager.Default`, ability to load custom typeface via `SKTypeface.FromStream`.
- [ ] Show `SKFont` controls: size, edging, hinting, subpixel, embolden, skew.
- [ ] Render sample paragraphs using `SKCanvas.DrawText`, `DrawTextOnPath`, `SKTextBlob`.
- [ ] Integrate HarfBuzz-shaped runs (from previously built service) into Skia draw pipeline to prove compatibility.
- [ ] Visualise glyph bounds (using `SKFont.GetGlyphWidths`, `GetGlyphBounds`, `GetPath`).
- [ ] Implement fallback demonstration with multi-script text, showing fallback typefaces applied per run.

**Coverage Targets**
- `SKFont`, `SKTypeface`, `SKFontManager`, `SKTextBlobBuilder`, `SKShaper` (if available), text metrics APIs, glyph path extraction.

### 7. Recording & Replay Studio
**Purpose** – Capture `SKCanvas` commands into `SKPicture`, replay with transforms, and export logs.

**Implementation Steps**
- [ ] Build `RecordingStudioViewModel` wrapping `SKPictureRecorder`. Provide UI to record from other pages or scripted sequences.
- [ ] Replay recorded picture into different surfaces using `SKCanvas.DrawPicture`, applying `SKMatrix` transforms.
- [ ] Export `.skp` (or JSON format) plus Vello scene command log via `SkiaSceneRecorder`.
- [ ] Show `SKDrawable` integration: create custom drawable that draws onto recorder.
- [ ] Demonstrate `SKPictureRecorder.BeginRecording(SKRect)` variations, `EndRecording`, `Save`.
- [ ] Provide playback timeline with camera overlay illustrating bounding boxes.

**Coverage Targets**
- `SKPictureRecorder`, `SKPicture`, `SKDrawable`, `SKCanvas.DrawPicture`, `SKNWayCanvas` (if implemented), serialization helpers.

### 8. Runtime Effect Forge
**Purpose** – Compile and run runtime effects (SkSL or WGSL bridging), uniform updates, and error reporting.

**Implementation Steps**
- [ ] Provide code editor for SkSL snippet; compile via `SKRuntimeEffect.Create`. Display diagnostics (errors, warnings).
- [ ] Offer uniform/property inspector: scalars, vectors, matrices, textures; allow real-time adjustments.
- [ ] Render effect on quad or text; animate uniforms over time using dispatcher timer.
- [ ] Support child effects by composing multiple runtime shaders (if shim supports).
- [ ] Capture GPU/CPU fallback info, display evaluation time.
- [ ] Export compiled bytecode or fallback message to diagnostics log.

**Coverage Targets**
- `SKRuntimeEffect`, `SKRuntimeEffectUniform`, `SKRuntimeShaderBuilder`, uniform binding, sampling textures, error handling.

### 9. IO & Diagnostics Workbench
**Purpose** – Demonstrate stream APIs, data serialization, and debugging helpers.

**Implementation Steps**
- [ ] Load assets through `SKFileStream`, `SKManagedStream`, `SKMemoryStream`, showing disposal semantics and `IsAtEnd`.
- [ ] Create raw `SKData` from arrays, strings, file snapshots; highlight reference counting.
- [ ] Inspect `SKColorSpace`, `SKICCProfile`, `SKEncodedInfo` metadata for decoders.
- [ ] Provide command log viewer showing `SkiaSceneRecorder` output alongside Vello command stats (draw call counts).
- [ ] Display GPU capabilities (msaa, features) via `SkiaBackendService`.
- [ ] Add toggles to stress test lease (resize, DPI change, suspended rendering) and log results.

**Coverage Targets**
- `SKData`, stream types, `SKColorSpace`, `SKEncodedInfo`, diagnostic utilities, backend capability queries.

### 10. Advanced Utilities (optional expansion)
**Purpose** – Cover remaining niche APIs (e.g., `SKVertices`, `SKPathEffect`s not elsewhere, `SKRegion` operations).

**Implementation Steps**
- [ ] Build interactive mesh editor to manipulate `SKVertices` (triangles) and render with texture and colors.
- [ ] Showcase `SKPictureRecorder` culling, `SKOverdrawCanvas` (if implemented), GPU-specific features.
- [ ] Integrate `SKPathEffect.Create2DLine`, `Create1DPath`, `CreateCorner` to visualise effects beyond dash.
- [ ] Provide region boolean operations vs path operations comparison.

**Coverage Targets**
- Remaining APIs flagged Partial/Stub in coverage doc; update as implementations land.

## Validation & Documentation
- [ ] Add headless smoke test to ensure each tab renders without exceptions on CPU backend; optionally extend to GPU where CI permits.
- [ ] Update `README.md` sample matrix to include AvaloniaVelloSkiaSharpSample with screenshots and quick start instructions.
- [ ] Extend `docs/skiasharp-shim-api-coverage.md` with “Sample coverage” column linking to the relevant page(s).
- [ ] Capture GIF/MP4 walkthroughs per page and store under `docs/media/skiasharp-sample`.
- [ ] Document known gaps (e.g., unsupported blend modes) and reference planned fixes in `docs/skiasharp-shim-completion-plan.md`.
- [ ] Ensure DEBUG runs assert `ShimNotImplemented.Throw` is never hit during sample interactions; add telemetry logging for quick triage.

## Milestone Exit Criteria
- All tasks above checked off, automated smoke tests green across supported OSes, and manual verification shows consistent output between CPU/GPU backends.
- `docs/skiasharp-shim-api-coverage.md` reports 100% coverage with sample references.
- Artifacts repository contains capture bundles (renders, command logs, encoded exports) for at least five scenarios (gradient, image IO, geometry ops, text, runtime effect).
- No regressions flagged by Avalonia integration tests or existing Skia parity suites after sample changes.
