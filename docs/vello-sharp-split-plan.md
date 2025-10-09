# VelloSharp Skia Refactor Master Plan

## Objectives
- Decouple shared geometry, text, and paint primitives from the `VelloSharp` FFI layer so they can be reused without native bindings.
- Consolidate duplicated Skia wrapper code into a reusable core that both GPU and CPU backends consume.
- Introduce a dedicated sparse-FFI pipeline for the CPU backend so `VelloSharp.Skia.Cpu` no longer references `VelloSharp`.
- Keep packaging and build flows compatible with existing native asset production, while enabling a future split between GPU and sparse FFI crates.

## Current State (Phase 1 Findings)

### Assemblies & Dependencies
| Assembly | Role | Direct References | Notes |
| --- | --- | --- | --- |
| `bindings/VelloSharp/VelloSharp.csproj` | Mixed managed surface plus GPU-focused FFI glue | Builds native crates (`vello_ffi`, `peniko_ffi`, `kurbo_ffi`, `winit_ffi`, `accesskit_ffi`) through MSBuild; exposes geometry, brushes, scene graph, renderer types | Managed types such as `Brush`, `PathBuilder`, `Font`, and layer/blend definitions are intertwined with P/Invoke structs and enums. |
| `bindings/VelloSharp.Text/VelloSharp.Text.csproj` | Text shaper integration layer | `VelloSharp` | Consumes `Font`, `Glyph`, and `PathBuilder` from the mixed assembly. |
| `bindings/VelloSharp.Skia.Gpu/VelloSharp.Skia.Gpu.csproj` | Skia GPU renderer bindings | `VelloSharp`, `VelloSharp.Text` | Contains both generic Skia wrapper types (`SKSurface`, `SKImage`, `SKCanvas`, etc.) and GPU-only backend types (`GpuSkiaBackend`). |
| `bindings/VelloSharp.Skia.Cpu/VelloSharp.Skia.Cpu.csproj` | Experimental sparse renderer backend | `VelloSharp`, `VelloSharp.Text` | Mirrors almost every file from the GPU package; `CpuSkiaBackend` invokes `SparseRenderContext` from `VelloSharp`. |
| `bindings/VelloSharp.HarfBuzzSharp/VelloSharp.HarfBuzzSharp.csproj` | HarfBuzz helper layer | Native HarfBuzz bindings | Serves fonts/text; will need to target the extracted text primitives. |
| `bindings/VelloSharp.Integration/VelloSharp.Integration.csproj` | Cross-platform integration helpers | `VelloSharp`, `VelloSharp.Gpu`, `VelloSharp.Rendering`, `VelloSharp.Avalonia.Vello`, `src/VelloSharp.Composition` | Hosts Avalonia controls, render-path registries, and shared hosting services. Now published as its own NuGet so downstream apps pick up the utilities without referencing the entire solution. |

### Shared Managed Types Encapsulated in `VelloSharp`
- **Geometry & Paths**: `PathBuilder`, `KurboPath`, matrix helpers, layer stack helpers live beside P/Invoke types in `VelloSharp/PathBuilder.cs` and `VelloSharp/VelloTypes.cs`.
- **Painting Primitives**: `Brush`, `SolidColorBrush`, gradient brushes, `StrokeStyle`, `LayerBlend`, `RgbaColor` reside in `VelloSharp/VelloTypes.cs` and depend on native enums for serialization.
- **Images & Glyph Runs**: `ImageBrush`, `Glyph`, `GlyphRunOptions`, and `Font` appear in `VelloSharp/VelloTypes.cs`, tying glyph data to native structs.
- **Sparse Rendering**: `SparseRenderContext` in `VelloSharp/SparseRenderContext.cs` consumes `vello_sparse_ffi` through `SparseNativeMethods`, yet is published from the same assembly as GPU FFI helpers.

### Skia Backend Duplication Snapshot
- GPU and CPU projects each define identical wrappers: `SKBitmap.cs`, `SKCanvas.cs`, `SKColor.cs`, `SKFont.cs`, `SKFontManager.cs`, `SKGeometry.cs`, `SKImage.cs`, `SKImageInfo.cs`, `SKPaint.cs`, `SKPath.cs`, `SKPicture.cs`, `SKPictureRecorder.cs`, `SKPixmap.cs`, `SKShader.cs`, `SKSurface.cs`, `SKTextBlob.cs`, and `SKTypeface.cs`.
- Common support folders (`Fonts/Roboto-Regular.ttf`, `IO`, `Primitives`, `Recording`, `PaintBrush.cs`, `SkiaBackend.cs`) are duplicated byte-for-byte between GPU and CPU directories.
- Backend-specific files are limited to `GpuSkiaBackend.cs` and `CpuSkiaBackend.cs`, indicating a clean seam for a shared Skia abstraction layer.

### Native Binding Inventory
- `ffi/vello_ffi`: GPU renderer entry point used by `VelloSharp` today.
- `ffi/vello_sparse_ffi`: Sparse rasterizer library already vendored but only consumed indirectly via `SparseNativeMethods`.
- `ffi/peniko_ffi`, `ffi/kurbo_ffi`, `ffi/accesskit_ffi`, `ffi/winit_ffi`: Ancillary crates invoked through the current MSBuild pipeline.
- Native interop definitions like `SparseNativeMethods`, `PenikoNativeMethods`, and `KurboNativeMethods` are co-located with managed abstractions, complicating reuse.

## Target Architecture
- **Managed Core (`bindings/VelloSharp.Core`)**
  - Hosts purely managed primitives: colors, brushes, gradients, stroke styles, path data, glyph run structs, and utility extensions.
  - No direct P/Invoke dependency; serialization helpers move to adapter layers.
- **GPU FFI Layer (`bindings/VelloSharp.Ffi.Gpu`)**
  - Wraps `vello_ffi`, `peniko_ffi`, `kurbo_ffi`, `winit_ffi`, exposing only the interop surface needed by GPU rendering.
  - Depends on `VelloSharp.Core` for managed types and provides conversion helpers to native structs.
- **Sparse FFI Layer (`bindings/VelloSharp.Ffi.Sparse`)**
  - Wraps `vello_sparse_ffi` APIs, rehouses `SparseRenderContext`, and depends on `VelloSharp.Core`.
  - Delivers sparse rendering features without pulling in GPU code.
- **Text Integration (`bindings/VelloSharp.Text`)**
  - Updated to consume `VelloSharp.Core` primitives (fonts, glyphs) and HarfBuzz utilities.
- **Skia Shared Layer (`bindings/VelloSharp.Skia.Core`)**
  - Consolidates all duplicated Skia wrappers, paint abstractions, recording infrastructure, and backend contracts (`ISkiaSurfaceBackend`, `SkiaBackendService`).
  - Depends on `VelloSharp.Core` and `VelloSharp.Text` only.
- **GPU Backend (`bindings/VelloSharp.Skia.Gpu`)**
  - Now lightweight: references `VelloSharp.Skia.Core`, `VelloSharp.Ffi.Gpu`, and `VelloSharp.Text`; implements the GPU-specific backend factory.
- **CPU Backend (`bindings/VelloSharp.Skia.Cpu`)**
  - References `VelloSharp.Skia.Core`, `VelloSharp.Ffi.Sparse`, and `VelloSharp.Text`; no project reference to `VelloSharp`.
- **Build & Packaging**
  - MSBuild native build targets migrate into the respective FFI projects, preserving crate builds while avoiding leakage into managed-only assemblies.

## Migration Phases
### Phase 1 – Discovery & Architecture Outline ✅
- [x] Audited Skia GPU/CPU projects to enumerate duplicated wrappers and confirm backend-specific seams (`SkiaBackend.cs`, `GpuSkiaBackend.cs`, `CpuSkiaBackend.cs`).
- [x] Cataloged managed primitives inside `VelloSharp` that mix business logic and interop concerns (`VelloSharp/VelloTypes.cs`, `PathBuilder.cs`, `SparseRenderContext.cs`).
- [x] Confirmed existing project references tying both Skia backends and text stack directly to `VelloSharp`.
- [x] Identified existing native crates (`vello_ffi`, `vello_sparse_ffi`, `peniko_ffi`, `kurbo_ffi`, `winit_ffi`, `accesskit_ffi`) and their MSBuild integration points.

### Phase 2 – Managed Core Extraction
- [x] Spin up `VelloSharp.Core` project; migrate geometry (`PathBuilder`, `KurboPath`), painting primitives (`Brush`, `StrokeStyle`, `LayerBlend`), glyph structs, and helper extensions into namespaces independent of native enums.
- [x] Replace inline conversions with portable representations (e.g., internal `CoreColor`, `CoreGradientStop`); layer GPU-specific conversion logic in FFI projects.
- [x] Update `VelloSharp.Text` and HarfBuzz integration to depend on the new core project; ensure no lingering `VelloSharp` references remain in shared codepaths.

### Phase 3 – Skia Shared Layer Introduction
- [x] Create `VelloSharp.Skia.Core` project and move all duplicated Skia wrapper files plus shared folders (`IO`, `Primitives`, `Recording`, `PaintBrush.cs`, `SkiaBackend.cs`).
- [x] Refactor namespaces/usings to reference shared primitives exposed from `VelloSharp.Core` while keeping interop adapters inside `VelloSharp`.
- [x] Adjust GPU/CPU project structures to consume the new core layer while retaining their backend-specific sources.

### Phase 4 – FFI Segmentation & CPU Backend Wiring
- [x] Establish `VelloSharp.Ffi.Gpu` and `VelloSharp.Ffi.Sparse` with scoped P/Invoke surfaces and wire `VelloSharp` against them (follow-up: expose interop types publicly and align downstream aliases).
- [x] Refactor GPU backend to rely on the new GPU FFI layer for renderer construction, ensuring conversion helpers map `VelloSharp.Core` types to native structs.

#### GPU Backend FFI Refactor Checklist
- [x] Trace every `VelloSharp` type currently consumed by `bindings/VelloSharp.Skia.Gpu/GpuSkiaBackend.cs` (scene, renderer, canvas commands) and map them to their `VelloSharp.Core` equivalents or shared abstractions.
- [x] Expose typed handle wrappers in `VelloSharp.Ffi.Gpu` (renderer, scene, layer stack) so GPU code requests native resources solely through the new FFI project.
- [x] Implement converter helpers that translate `VelloSharp.Core` primitives (colors, brushes, stroke styles, layer blends, paths) into the native struct layout consumed by P/Invoke calls (currently hosted with the GPU backend).
- [x] Refactor `GpuSurfaceBackend` and `GpuCanvasBackend` to construct renderers/scenes through the FFI wrappers and use the conversion helpers instead of direct struct initialization.
- [x] Clean up dependencies by removing `using VelloSharp;` from the GPU backend, updating project references, and ensuring disposal/lifetime is owned by the FFI-provided safe handles.
- [x] Backfill coverage: add a GPU smoke test that renders gradients, strokes, and glyph runs through the new FFI path (converter unit tests now live in `tests/VelloSharp.Skia.Gpu.Tests`).

- [x] Rebuild CPU backend on top of `VelloSharp.Ffi.Sparse`, exposing sparse operations without any `VelloSharp` project reference.
- [x] Validate solution and packaging updates (NuGet metadata, runtime asset flow) to reflect the split assemblies.

### Phase 5 – Validation, Cleanup, and Tooling
- [ ] Update solution files, Directory.Build.props entries, and CI/test scripts to include new projects.
- [ ] Add or refresh tests covering both GPU and CPU backends using shared code (unit tests for geometry conversions, rendering smoke tests when feasible).
- [ ] Refresh documentation (`README.md`, existing plan docs) to describe the new architecture and migration guidance for consumers.
- [ ] Remove obsolete files/references from legacy `VelloSharp` once dependencies have fully migrated.

## Risks & Open Questions
- **Interop Enum Mapping**: Splitting managed enums from native counterparts requires careful conversion logic to avoid behavioral regressions.
- **Packaging Layout**: NuGet packages currently expect runtime assets under `VelloSharp`; packaging scripts must be reviewed to ensure the new split assembles artifacts correctly.
- **API Compatibility**: Existing consumers referencing `VelloSharp` types may need migration guidance; determine whether type-forwarders or partial classes are necessary.
- **Testing Coverage**: Sparse CPU backend lacks automated validation today; additional tests or sample updates may be required to guard against regressions during refactor.
- **Namespace Strategy**: Decide whether to keep `VelloSharp` namespaces for backwards compatibility or introduce new neutral namespaces for the core project.

## Appendix A – Candidate Type Relocation Map
| Area | Current Path | Proposed Destination |
| --- | --- | --- |
| Geometry | `bindings/VelloSharp/PathBuilder.cs` | `bindings/VelloSharp.Core/Geometry/PathBuilder.cs` |
| Painting | `bindings/VelloSharp/VelloTypes.cs` (`Brush`, `GradientStop`, `StrokeStyle`) | `bindings/VelloSharp.Core/Painting/*` |
| Text | `bindings/VelloSharp/VelloTypes.cs` (`Font`, `Glyph`, `GlyphRunOptions`) | `bindings/VelloSharp.Core/Text/*` with adapters in `VelloSharp.Text` |
| Sparse Rendering | `bindings/VelloSharp/SparseRenderContext.cs` | `bindings/VelloSharp.Ffi.Sparse/SparseRenderContext.cs` |
| Skia Wrappers | `bindings/VelloSharp.Skia.Gpu/SKSurface.cs` (and peers) | `bindings/VelloSharp.Skia.Core/Skia/*` |
| GPU Backend | `bindings/VelloSharp.Skia.Gpu/GpuSkiaBackend.cs` | Remains in `VelloSharp.Skia.Gpu`, now referencing `VelloSharp.Ffi.Gpu` |
| CPU Backend | `bindings/VelloSharp.Skia.Cpu/CpuSkiaBackend.cs` | Remains in `VelloSharp.Skia.Cpu`, referencing `VelloSharp.Ffi.Sparse` |
