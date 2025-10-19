# Windows Forms Graphics Shim Plan

## Goals & Success Criteria
- Deliver a `VelloSharp.WinForms` shim that mirrors the public surface of key `System.Drawing`/WinForms rendering types while delegating rendering to the existing Vello GPU pipeline.
- Allow WinForms applications to render via Vello without touching `System.Drawing.Graphics`, enabling drop-in migration similar to `VelloSharp.Skia`.
- Ship a sample `WinFormsMotionMarkShim` app that drives the existing MotionMark scene on the GPU and demonstrates animation, resize handling, and DPI awareness.
- Provide automated validation (unit tests plus smoke tests) proving that the shim renders identical scene buffers to the raw Vello renderer for a curated set of samples.

## Out of Scope
- Re-implementing the full WinForms UI framework or non-drawing APIs (event routing, layout, control library).
- CPU-only rendering paths (keep parity with Vello GPU backend; reuse sparse renderer only where already available).
- Linux/OSX System.Drawing compatibility. Target Windows 10+ with D3D12/Vulkan via `wgpu`.

## Reference Points
- `bindings/VelloSharp.Skia.Core`/`Gpu` � API surface mirroring strategy for SkiaSharp.
- `bindings/VelloSharp.Integration/Avalonia/VelloSurfaceView.cs` � swapchain lifecycle and presentation helpers.
- `samples/SkiaShimSmokeTest` and `samples/AvaloniaSkiaSparseMotionMarkShim` � minimal smoke-test and MotionMark scene plumbing to reuse.
- `bindings/VelloSharp.Rendering/VelloScene.cs` and `SparseRenderContext.cs` � scene representation and sparse recording APIs backing the shim.

## Implementation Phases

### Phase 0 � Requirements Capture & Groundwork (1-2 days)
- [x] Inventory `System.Drawing` APIs exercised by typical WinForms paint code and MotionMark port (e.g., `Graphics`, `Pen`, `Brush`, `Path`, `Bitmap`, `ImageAttributes`, text APIs).
- [x] Audit Vello primitives to map supported features (fills, strokes, text, transforms, clipping, image draw) and record gaps.
- [x] Document interoperability expectations: threading model, resource lifetime, DPI/scaling semantics, color space.
- [x] Define success metrics: target FPS on reference hardware, acceptable GC pressure, shader compilation warmup budget.

### Phase 1 � Project Layout & Build Integration (1-2 days)
- [x] Create `bindings/VelloSharp.WinForms.Core/VelloSharp.WinForms.Core.csproj` targeting `net8.0-windows` with references to `VelloSharp`, `VelloSharp.Rendering`, and `VelloSharp.Gpu`.
- [x] Add solution entries in `VelloSharp.sln`, configure `Directory.Build.props` to include new project group, and wire up package metadata (AssemblyInfo, strong name if required).
- [x] Introduce `bindings/VelloSharp.Integration.WinForms` for host-specific glue (interop with HWND, swapchain creation, message pump hooks).
- [x] Set up shared test utilities in `tests/VelloSharp.WinForms.Tests` referencing MotionMark fixture assets.

#### Phase 1 Status (2025-10-04)

- Created `VelloSharp.WinForms.Core` and `VelloSharp.Integration.WinForms` projects targeting `net8.0-windows` with scaffolded device/session types.
- Added solution wiring and initial unit test project (`VelloSharp.WinForms.Tests`) with smoke coverage for `VelloGraphicsDevice` sessions.
- Introduced `VelloGraphicsDevice`, `VelloGraphicsSession`, and option types plus a placeholder `VelloRenderControl` integrating with WinForms paint loop.
- Verified solution builds successfully with new projects included.

### Phase 2 � Rendering Backbone & Device Management (3-4 days)
- [x] Implement `WinFormsGpuContext` handling `wgpu` adapter/device acquisition, queue management, and surface configuration (DX12 primary, Vulkan fallback) with DPI-aware sizing.
- [x] Provide `WinFormsSwapChainSurface` that wraps an HWND using `WgpuSurfaceDescriptorFromWindowsHwnd` and manages resize/reconfigure events.
- [x] Expose renderer tuning (render format, MSAA, color space) via `VelloGraphicsDeviceOptions` and plumb settings into swapchain/device reconfiguration.
- [x] Expand GPU resource pooling beyond swapchain textures (pipeline caches, staging/upload buffers, glyph atlases) and surface utilization metrics (pool hits/misses, memory usage).
- [x] Implement GPU resource lifetime tracking (textures, staging buffers) to avoid per-frame allocation storms; reuse pooling patterns from Avalonia integration if available.
- [x] Add diagnostic hooks (ETW/logging) to trace frame time, queue submission, and surface present.
- [x] Expose the shared `SurfaceHandle` through `VelloRenderControl` render events so WinForms presenters can hand GPU swapchains to shims without additional interop.

#### Phase 2 Status (2025-10-04)

- Introduced `WinFormsGpuContext` shared device manager with reference-counted leasing and adapter/device negotiation.
- Added `WinFormsSwapChainSurface` abstraction to wrap `WgpuSurface` configuration and texture acquisition.
- Added swap-chain presentation with DPI-aware sizing, diagnostics logging, and device-loss fallbacks.
- Introduced GPU diagnostics counters tracking surface configurations, frame presentations, and reset events.
- Updated `VelloRenderControl` to acquire GPU resources alongside the existing CPU path, preparing for swapchain presentation.
- Completed `VelloGraphicsDeviceOptions` extensions for `RenderFormat`, `MsaaSampleCount`, and `ColorSpace`, wiring defaults and swapchain reconfiguration.
- Implemented GPU resource pooling and diagnostics (pipeline cache reuse, staging buffer leasing, glyph atlas counters) beyond swapchain presentation.

### Phase 3 � Graphics API Surface (5-7 days)
#### Phase 3 Status (2025-10-04)

- Added initial `VelloGraphics` helper with rectangle fills/strokes to begin mirroring System.Drawing primitives.
- Namespace scaffolding for `VelloSharp.WinForms` primitives has started to align signatures ahead of command translation work.
- Added initial `VelloBrush`, `VelloSolidBrush`, and `VelloPen` abstractions plus graphics state stack utilities to prep for System.Drawing parity.

- [x] Design namespace `VelloSharp.WinForms` mirroring `System.Drawing` types (`Graphics`, `Bitmap`, `Pen`, `SolidBrush`, `TextureBrush`, `LinearGradientBrush`, `PathGradientBrush`, `Region`, `Matrix`, `GraphicsPath`, `Font`, `StringFormat`) (in progress: skeleton stubs for `Graphics`, `Pen`, and `Brush` drafted under the new namespace).
- [x] For each type:
  - [x] Define public API aligning with `System.Drawing` signatures used in WinForms (constructors, properties, drawing methods, transforms, clip, text).
  - [x] Implement backing logic by recording into `VelloScene`/`PathBuilder` (vector path) and using `TextLayout`/`HarfBuzz` for glyphs.
  - [x] Add translation layers for pens/brushes to Vello gradients, fills, and strokes, reusing `PenikoBrushAdapter` and `BrushNativeAdapter` as needed.
- [x] Implement `VelloGraphics` facade with methods `DrawLine`, `DrawLines`, `DrawPolygon`, `DrawPath`, `FillPath`, `DrawImage`, `DrawString`, etc., mapping to Vello recording commands.
- [x] Support state stack semantics (`Save`, `Restore`, `Transform`, `Clip`) with efficient push/pop using pooled structures.
- [x] Ensure compatibility with GDI coordinate system (top-left origin, device-independent pixels) and implement DPI scaling helpers.
- [x] Provide text rendering via `VelloSharp.Text` fallback pipeline, including font management mirroring `System.Drawing.Font`, `FontFamily`, `TextRenderingHint`.
- [x] Establish resource disposal patterns to match `IDisposable` expectations and guard against double dispose.

### Phase 4 � WinForms Control Hosting (3-4 days)
- [x] Add `VelloRenderControl : System.Windows.Forms.Control` that creates the GPU context, drives frame timing (CompositionTarget-like invalidation), and exposes events for user drawing.
- [x] Implement `PaintSurfaceEventArgs` carrying `VelloGraphics` and timing info, mirroring `SkiaSharp.Views.Desktop.SKControl` semantics for familiarity.
- [x] Handle WM_PAINT, WM_SIZE, and WM_DPICHANGED messages to update swapchain and schedule redraws; ensure background erase suppression to avoid flicker.
- [x] Provide optional composition modes: continuous animation loop vs. invalidation-based redraw (hook into WinForms `Invalidate`/`BeginInvoke`).
- [x] Integrate with `System.Windows.Forms.Application` message loop; include fallback for design-time to render placeholder text without GPU.

#### Phase 4 Status (2025-10-05)

- Delivered `VelloRenderControl` with GPU swapchain management, timing helpers, and render-loop integration while suppressing background flicker.
- Raised `PaintSurface` event with timing metadata and design-time fallback placeholder, aligning semantics with familiar SkiaSharp control patterns.
- Added render loop modes (on-demand vs. continuous) and message handling for `WM_PAINT`/`WM_SIZE`/`WM_DPICHANGED` to keep swapchain and DPI in sync.
### Phase 5 – MotionMark Sample Application (2-3 days)
#### Phase 5 Status (2025-10-05)

- Added `MotionMark.SceneShared` reusable library exposing the MotionMark element generator for Vello-based renderers.
- Introduced `WinFormsMotionMarkShim` sample hosting `VelloRenderControl` with complexity and animation controls to drive the shared scene.
- Wired GPU rendering path via `PathBuilder` + `Scene.StrokePath`, emitting overlay diagnostics (elements, FPS) through the shim.
- Added renderer toggle (GPU vs. CPU sparse path) with CPU fallback rasterisation routed through VelloRenderControl and per-frame EMA FPS reporting via the overlay.
- Created launch profile (launchSettings.json) to streamline running the sample.
- [x] Create `samples/WinFormsMotionMarkShim/WinFormsMotionMarkShim.csproj` referencing the new shim.
- [x] Share MotionMark scene logic via a new cross-targeted project (e.g., `samples/MotionMark.SceneShared`) or move existing code to `samples/Common/MotionMarkScene.cs` to avoid duplication.
- [x] Implement WinForms UI with controls to adjust complexity, toggle animation, choose renderer (GPU vs. sparse CPU), and display FPS metrics.
- [x] Wire `VelloRenderControl` to render MotionMark scene each frame, using a high-resolution timer for animation and a status overlay drawn via the shim.
- [x] Add launch configuration (`Properties/launchSettings.json`) and ensure sample builds via `dotnet run`.

### Phase 6 � Validation, Tooling & Documentation (3-4 days)
- [ ] Author unit tests in `tests/VelloSharp.WinForms.Tests` covering drawing commands -> `VelloScene` translation (e.g., check produced path segments, brush properties).
- [ ] Add integration smoke test similar to `samples/SkiaShimSmokeTest` that renders into an off-screen surface and compares checksum/buffer to known baseline.
- [ ] Automate performance regression harness capturing frame time for MotionMark at target complexity; integrate with CI artifacts.
- [ ] Update documentation:
  - [ ] New section in `README.md` describing WinForms shim usage and sample.
  - [ ] API docs in `docs/` (`winforms-shim-usage.md`) detailing migration steps from `System.Drawing`.
- [ ] Extend packaging scripts (in `packaging/`) to bundle WinForms assemblies into NuGet (split GPU/runtime assets as needed).

## Phase 0 Findings

### API Inventory (WinForms Drawing Surface)

| Category | System.Drawing / WinForms API surface | Usage signals | Shim guidance |
| --- | --- | --- | --- |
| Basic primitives | `Graphics.Clear`, `DrawLine`, `DrawLines`, `DrawRectangle`, `FillRectangle`, `DrawEllipse`, `FillEllipse` | Common in WinForms controls for backgrounds/borders; MotionMark warm-up uses solid fills before path strokes | Map to lightweight helpers that emit cached rectangle/ellipse paths and `Scene.FillPath`/`Scene.StrokePath` invocations to minimize builder churn |
| Path strokes & fills | `GraphicsPath` (with `AddLine`, `AddBezier`, `CloseFigure`), `DrawPath`, `FillPath`, `FillPolygon` | MotionMark scene relies on cubic/quad segments and repeated path reuse for animated strokes | Implement `VelloGraphicsPath` recorder backed by `PathBuilder`; add pooling and conversion utilities to avoid re-tessellating unchanged segments |
| Stroke styling | `Pen` width/join/cap, `Pen.DashPattern`, `AdjustableArrowCap` subsets | MotionMark toggles line join/bevel width; enterprise charts expect dashed outlines | Translate to `StrokeStyle` and document unsupported custom line caps; approximate arrowheads by path expansion in shim |
| Brushes & fills | `SolidBrush`, `LinearGradientBrush`, `PathGradientBrush`, `TextureBrush`, `HatchBrush` subsets | WinForms dashboards rely on gradients; MotionMark only needs solid colors but sample apps expect gradients | Back solid/linear/radial via existing Vello brushes; plan fallbacks for `PathGradient`/`Hatch` (e.g. convert to image brush) and flag unsupported overloads |
| Text layout | `Graphics.DrawString`, `MeasureString`, `TextRenderingHint`, `StringFormat` alignment | Labels, FPS overlay, and accessibility text; MotionMark status overlay will consume alignment APIs | Use `VelloSharp.Text` glyph APIs with shim-level layout cache; map `TextRenderingHint.ClearTypeGridFit` to MSAA + subpixel toggles |
| Images & bitmaps | `Graphics.DrawImage` overload family, `ImageAttributes`, `Bitmap.LockBits`, `Graphics.FromImage` | Existing WinForms apps blit icons/screenshots; MotionMark sample may display overlay icons | Implement BGRA upload path via `Image.FromPixels`; expose render target bitmap surface for `Graphics.FromImage` scenarios |
| Clipping & compositing | `SetClip`, `ResetClip`, `ExcludeClip`, `CompositingMode`, `CompositingQuality`, `SmoothingMode` | UI frameworks clip child controls and apply alpha-compositing | Map to `Scene.PushLayer` with clip path and `LayerBlend`; align `SmoothingMode.AntiAlias` with Vello antialiasing, document unsupported quality toggles |
| Transform & state stack | `Graphics.Save`/`Restore`, `TranslateTransform`, `ScaleTransform`, `RotateTransform`, `MultiplyTransform` | Animation and zoom gestures adjust transforms every frame | Maintain explicit state stack in shim mirroring WinForms semantics; marshal matrices into `Matrix3x2` for Vello commands |
| Resource lifecycle | `IDisposable` semantics for `Graphics`, `Pen`, `Brush`, `Font`, `Region` | Developers expect deterministic disposal and re-usable resource caches | Implement reference-counted wrappers and guard double-dispose to match WinForms error patterns |

- MotionMark path orchestration today issues cubic/quadratic segments with stroke joins via `SKPath` (`samples/AvaloniaSkiaSparseMotionMarkShim/Scenes/MotionMarkScene.cs`), providing a clear baseline for the WinForms shim API.

### Vello Capability Map

| Requirement | Vello support status | Gaps / actions |
| --- | --- | --- |
| Vector geometry recording | `PathBuilder` (`VelloSharp.Core/Geometry/PathBuilder.cs`) stores move/line/quad/cubic verbs; `Scene.FillPath`/`StrokePath` consume spans | Need conversion helpers that translate WinForms path figures and handle empties consistently |
| Stroke styling | `StrokeStyle` (width, miter, caps, dash pattern) and `Scene.StrokePath` already exposed | No built-in custom caps or compound lines; shim must emulate arrowheads and throw on unsupported features |
| Brush types | `SolidColorBrush`, `LinearGradientBrush`, `RadialGradientBrush`, `SweepGradientBrush`, `ImageBrush` available | No direct hatch or path-gradient equivalent; document conversions (e.g. bake to `ImageBrush`) |
| Layering & compositing | `Scene.PushLayer`/`PopLayer` with `LayerBlend` and luminance masks | Need WinForms-friendly abstraction over blend enums and to emulate `CompositingMode.SourceCopy` semantics |
| Images & textures | `Image.FromPixels` and `Scene.DrawImage` with extend/quality controls | Lack of color-correct image attributes (gamma, matrix) � consider CPU pre-processing or shader hook |
| Text & glyphs | `Scene.DrawGlyphRun` with `Font` and `GlyphRunOptions`; HarfBuzz stack already integrated | Need higher-level text layout to mirror `DrawString` measuring, plus bidi/formatting wrappers |
| Clipping | `Scene.PushLayer` accepts clip paths; `PushLuminanceMaskLayer` for opacity masks | Region combination and infinite region semantics must be reproduced at shim layer |
| Render targets | `Renderer.Render` supports RGBA/BGRA buffers; GPU path via `VelloSharp.Gpu` | WinForms swapchain needs dedicated `wgpu` surface management and throttling |

### Interoperability Expectations

- **Threading model**: WinForms painting is single-threaded on the UI thread, while `VelloSharp.Renderer` and `wgpu` queue submissions can run on background threads. The shim will marshal `Paint` events into a render scheduler that owns the GPU device, keeping `Scene` creation on the UI thread but performing queue submit/present on a dedicated thread to avoid blocking message processing.
- **Lifetime & disposal**: Vello objects (`Scene`, `Renderer`, `Image`, brushes) are not thread-safe and throw once disposed. The shim must enforce per-control lifetime, dispose in `OnHandleDestroyed`, and guard against designer reloads.
- **DPI & scaling**: WinForms reports logical units via `DeviceDpi`. The shim should treat WinForms coordinates as DIPs and scale surface size before configuring the `wgpu` surface (rounding to integers) to maintain crisp output on >100% scaling.
- **Color space & alpha**: Vello renders in linear space but exposes `RenderFormat.Bgra8` to match WinForms `PixelFormat.Format32bppPArgb`. Ensure premultiplication flags (`ImageAlphaMode.Premultiplied`) align when interoperating with `Bitmap` and GDI operations.
- **Device ownership**: Multiple controls may share a GPU device; design a `WinFormsGpuContext` singleton with reference counting so disposing the last control tears down the queue cleanly.
- **Error handling & diagnostics**: Surface loss or adapter removal should bubble up as WinForms `Paint` exceptions with actionable messages and optional automatic fallback to the CPU renderer for telemetry builds.

### Success Metrics

- Sustain >=90 FPS (<=11 ms/frame) on a 1600x900 surface at MotionMark complexity 7 on a reference RTX 2060 laptop GPU after warm-up, with 95th percentile frame time <=13 ms.
- First frame latency <=150 ms from control creation (covers shader compilation and swapchain setup) and hide visual until ready to avoid flicker.
- GPU memory usage for MotionMark demo <=250 MB steady-state; no per-frame allocations over 32 KB on the managed heap during animation (verified via ETW or EventPipe sampling).
- Text rendering accuracy within +/-0.5 px compared to GDI `DrawString` for a representative font matrix across DPI 96-192.
- Smoke test renders (off-screen checksum harness) must match baseline buffer within +/-1 LSB per channel in 99.9% of pixels, ensuring determinism for CI.

## Risks & Mitigations
- **API surface breadth** � `System.Drawing` is large; mitigate by prioritising APIs required by MotionMark and common WinForms apps, documenting unsupported members, and adding stubs throwing `NotSupportedException` with tracking issues.
- **Text rendering parity** � Matching GDI text metrics may require additional kerning/shaping work; leverage existing HarfBuzz integration and add measurement tests comparing against GDI outputs.
- **Swapchain compatibility** � Some hardware may lack DX12; provide Vulkan fallback via `wgpu` and detect failure to fall back to software path, logging clear diagnostics.
- **Resource lifetime** � WinForms devs expect implicit resource sharing; implement reference counting/pooling and document usage patterns to avoid leaks.
- **Designer tooling** � Visual Studio designer loads controls at design time; supply dummy renderer and guard GPU initialization behind runtime checks.

## Milestones & Deliverables
- **M1 (Week 1)** � Project scaffolding, GPU context, basic `VelloGraphics` drawing (lines/rectangles), smoke test passing.
- **M2 (Week 2)** � Brushes, paths, text rendering, WinForms control hosting end-to-end; MotionMark scene renders statically.
- **M3 (Week 3)** � Animation, resize/DPI support, sample app polished with UI controls, baseline performance captured.
- **M4 (Week 4)** � Test suite stable in CI, documentation finalized, NuGet/package metadata ready for preview release.

## Open Questions
- Should the shim expose interop hooks for mixing native GDI/Direct2D drawing inside the same control?
- Do we need to support multiple renderer instances per process with shared GPU resources?
- What is the expected story for printing or bitmap export � reuse existing `Renderer.Render` to BGRA buffer or integrate with WinForms printing pipeline?
- How do we align release cadence with `VelloSharp.Skia` packages (joint versioning or independent)?





















