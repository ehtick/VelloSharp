# Vello Text & Imaging Stack Integration Plan

## Context
- The Avalonia Vello backend has shed its Skia/ImageSharp fallbacks; geometry, text shaping, and bitmap IO now flow through the Vello/Parley native stack.
- Remaining work focuses on polishing the FFI surface, broadening feature coverage, and validating the end-to-end pipeline across platforms.
- Goal: maintain a managed pipeline that loads fonts, shapes glyphs, builds outlines, and rasterizes images entirely through the Vello/Parley ecosystem.

## Milestones Overview
1. **Workspace Preparation** – vendor Rust crates, align Cargo workspace, document prerequisites.
2. **FFI Surface Design** – define C ABI contracts for fonts, shaping, glyph geometry, and bitmap IO.
3. **Rust FFI Implementation** – implement/export bindings in the native crate, add validation tests.
4. **.NET Bindings & Abstractions** – author P/Invoke wrappers and managed layers that replace Skia/ImageSharp usage.
5. **Renderer Integration** – swap Avalonia surfaces over to the new bindings, remove Skia/ImageSharp fallbacks.
6. **Validation & Cleanup** – cross-platform verification, dependency pruning, docs, follow-up issues.

## Detailed Milestones & Tasks

### 1. Workspace Preparation *(In Progress)*
- [x] Audit existing native workspace layout and build scripts.
- [x] Add submodules for `peniko`, `skrifa`, `swash`, `fontique`, `harfrust`, `parley` under `extern/`.
- [x] Update root `Cargo.toml` to treat these crates as workspace members/patches.
- [x] Ensure packaging/smoke scripts build the expanded workspace locally (`cargo check -p vello_ffi`).
  - Adjusted vendored `skrifa` COLR helpers to account for `NameId`/`u16` API divergence so the workspace compiles cleanly.

#### Crate source inventory *(Completed)*
| Crate | Repository | Latest HEAD (captured) | Notes |
| --- | --- | --- | --- |
| `peniko` | https://github.com/linebender/peniko | `8cbfc12c0415ca5023a060183769f533b84bb516` | Public; provides brush/image utilities used by Vello. |
| `parley` | https://github.com/linebender/parley | `f20a1fa65c29eac521a0fe4e124d5ea40b792e43` | Public; text layout/shaping orchestrator. Contains `fontique` crate within workspace. |
| `skrifa` | https://github.com/googlefonts/fontations | `8b6497da9ae0e94e27fd488c01d7955f8f1b2b9f` | Resides in the `fontations` monorepo (path `skrifa/`). |
| `swash` | https://github.com/dfrg/swash | `5d885ff4ec8d8b56eceeae29a4f595c7320630b9` | Public repo under `dfrg`. |
| `fontique` | https://github.com/linebender/parley | `f20a1fa65c29eac521a0fe4e124d5ea40b792e43` | Workspace member inside `parley` repository. |
| `harf-rs` | https://github.com/manuel-rhdt/harfbuzz_rs | `e77d94a9855850ec27b7ac34fa883a74e317cb25` | Public bindings for HarfBuzz; confirm crate path (`harfbuzz_rs/`). |

### 2. FFI Surface Design
- [x] Catalogue .NET functionality currently backed by Skia/ImageSharp (glyph metrics, shaping, image load/save).
- [x] For each feature, map to target Rust crate APIs and outline the exported structs/enums.
- [x] Define error propagation strategy (status codes, last-error strings) consistent with existing bindings.
- [x] Document expected lifetime management and threading requirements.

#### Avalonia dependencies replaced *(Completed)*
- [x] Fonts & shaping now resolved via `VelloGlyphTypeface`/`VelloTextShaper`, eliminating the prior `Avalonia.Skia` bindings in `VelloPlatform`.
- [x] `VelloPlatformRenderInterface` constructs all geometries, glyph runs, and bitmap operations through the native Vello abstractions without Skia fallbacks.
- [x] Font streaming flows through the Vello font manager helpers, providing typeface data directly from the FFI layer.
- [x] Bitmap load/save/resize paths delegate to the new Peniko-backed native exports, removing the ImageSharp dependency for the Vello backend.

#### FFI/API mapping *(Draft)*
| Avalonia touchpoint | Replacement surface | Rust provider | Notes |
| --- | --- | --- | --- |
| `VelloGlyphRunImpl` bounding boxes (`GlyphTypeface.TryGetGlyphMetrics`) | `vello_font_get_glyph_metrics` → `VelloGlyphMetrics` struct | `skrifa::metrics::GlyphMetrics` | Returns advance, bearings, and bounds at resolved font size. |
| Text shaping (`ITextShaperImpl.ShapeText`) | `vello_text_shape_utf16` (implemented) + future feature flags | `harfrust`, `parley` | Current export shapes UTF-16 runs; expand for feature/variation tables and BiDi support. |
| Glyph outline extraction (`BuildGlyphRunGeometry`) | `vello_font_get_glyph_outline` returning path commands | `swash`, `peniko` | Supplies path segments compatible with `VelloPathData` for hit-testing and underline shapes. |
| Geometry primitives (`CreateRectangleGeometry`, `CreateEllipseGeometry`, etc.) | `Vello*GeometryImpl` managed wrappers → `VelloPathData` | Managed (GeometryUtilities) + `peniko` | Replace Skia interface usage with existing managed builders backed by `VelloPathData`. |
| Font discovery/streaming (`VelloFontManager`) | `vello_font_create`, `vello_font_release`, planned `vello_font_enumerate` | `fontique`, `skrifa` | Presently uses Avalonia’s stream loader; future work to enumerate via fontique. |
| Bitmap surfaces (`VelloBitmapImpl`) | `vello_image_*` encode/decode/resize (planned) | `peniko::ImageData`, `swash::scale::Context` | Replace SixLabors ImageSharp IO/resizing once the native routines land. |
| Render surface creation (`CreateRenderTargetBitmap`, swapchains) | `VelloSwapchainRenderTarget` + `VelloGraphicsDevice` | `wgpu`, `vello` | Maintains GPU-backed surfaces without Skia. |

#### Status, lifetime, and error model *(Draft)*
- All new exports return `VelloStatus`, reusing the existing enum in `vello_ffi`. Success paths clear the thread-local last-error slot; failure paths populate it with human-readable diagnostics.
- Any handle that transfers ownership across the FFI boundary follows a `create -> use -> destroy` pattern (`*_create`, `*_decode`, or `*_get_*` paired with `*_destroy`). Destroy calls tolerate `NULL` and treat double-free as `InvalidArgument` while leaving last-error untouched.
- Glyph outlines: `vello_font_get_glyph_outline(font, glyph_id, point_size, tolerance, out_outline)` allocates an immutable outline handle backed by an internal `Vec<VelloPathCommand>`. Callers inspect the path via `vello_glyph_outline_get_commands(handle, out_span)` and must release resources with `vello_glyph_outline_destroy`. Handles are `Send + Sync` because the backing buffer is reference-counted; mutation is prohibited after creation.
- Bitmap surfaces: `vello_image_decode_{png,jpeg}` construct a `VelloImageHandle` (wrapping `peniko::ImageData`). Pixel access uses explicit map/unmap (`vello_image_map_pixels`/`vello_image_unmap_pixels`) to honor Rust's borrow rules; callers must unmap before destroy. Encode paths (`vello_image_encode_png`) write into a `VelloBlobHandle`, sharing the same lifetime semantics as existing blob exports.
- Error cases follow a consistent mapping: `NullPointer` for missing arguments, `InvalidArgument` for dimension/format mismatch, `Unsupported` for formats the underlying crate cannot produce, and `RenderError` for IO/codec failures. Native panics are trapped and surfaced as `RenderError` with the panic message stored in `vello_last_error_message`.
- Threading: glyph outline and bitmap handles are immutable once returned and may be used on any thread. Decode/encode operations themselves are not thread safe with respect to the same handle and require external synchronization if multiple threads call into the same resource simultaneously.

#### Initial FFI wiring *(In Progress)*
- Added workspace dependencies on `parley`, `fontique`, `swash`, and `skrifa` in `ffi/vello_ffi/Cargo.toml`, enabling compilation against the vendored sources with explicit feature control.
- Introduced `VelloTextStackProbe` and the exported `vello_text_stack_probe` entry point (`ffi/vello_ffi/src/lib.rs:205-236`) to validate basic crate integration from the managed side.
- Patched the vendored `parley` sources to define an internal `Font` handle and applied Rust 2024 `impl Trait` capture annotations so the crate builds cleanly in this workspace.
- Verified `cargo check -p vello_ffi` succeeds against the expanded workspace.
- Added production FFI for glyph metrics (`vello_font_get_glyph_metrics`) and hooked it through `VelloFontManager.TryGetGlyphMetrics`, allowing `VelloGlyphRunImpl` to favour Vello metrics over the Skia fallback during glyph-run construction.
- Added a first-cut shaping surface (`vello_text_shape_utf16`/`vello_text_shape_destroy`) powered by `harfbuzz_rs`, plus the managed `VelloTextShaper` implementation that now supplies `ITextShaperImpl` without going through the Skia fallback.
- Shipped glyph outline extraction (`vello_font_get_glyph_outline`, `vello_glyph_outline_get_data`, `vello_glyph_outline_destroy`) and consumed it from `VelloFontManager` to power native glyph-run geometry.
- Introduced PNG decode and pixel mapping exports (`vello_image_decode_png`, `vello_image_get_info`, `vello_image_map_pixels`) as the first step toward a Peniko-backed bitmap pipeline.

### 3. Rust FFI Implementation
- [x] Implement font loading/metrics exports (using Fontique/Skrifa).
- [x] Implement shaping exports (Parley/HarfRust/Swash) returning glyph runs & cluster data.
- [x] Implement glyph outline extraction (Peniko/Swash) for BuildGlyphRunGeometry.
- [>] Expose bitmap decode/encode APIs via Peniko or supporting crates if ImageSharp removal is required initially. *(PNG decode/map now live; JPEG/encode variants remain follow-up.)*
- [ ] Add unit tests (Rust) covering happy path and error cases for each export.

### 4. .NET Bindings & Abstractions
- [x] Add P/Invoke declarations in `VelloSharp.NativeMethods` for the glyph metrics/outline and bitmap decode/map surface.
- [x] Implement managed wrappers (`VelloFontManager` outline access, `VelloGlyphRunImpl` bounds) encapsulating native handles.
- [x] Replace ImageSharp-backed bitmap implementation with the new native pipeline. *(Load/save/resize funnel through the Vello FFI; encode uses the new blob surface.)*
- [ ] Provide fallbacks or feature flags if native functionality is temporarily unavailable.

### 5. Renderer Integration
- [x] Replace Skia glyph run creation in `VelloPlatformRenderInterface` with Vello-native glyph runs and geometry building.
- [x] Update drawing context to use native bitmap and font abstractions exclusively.
- [x] Remove or gate existing Skia/ImageSharp dependencies in project files and DI setup for the Vello backend (other samples still carry their own dependencies).
- [x] Align default antialiasing with available shader permutations (fallback to Area until MSAA shaders are packaged).
- [ ] Update docs/readme to reflect the new dependency stack.

#### Fallback usage audit *(In Progress)*
- [x] `CreateRectangleGeometry`/`CreateEllipseGeometry`/`CreateLineGeometry` return `Vello*GeometryImpl` instances instead of delegating to Skia.
- [x] Implement `CreateStreamGeometry` using `VelloStreamGeometryImpl` with native `VelloPathData` builders.
- [x] Provide `VelloGeometryGroupImpl` and `VelloCombinedGeometryImpl` pathways so grouping/boolean ops no longer call into the fallback.
- [x] Replace bitmap load/save/resize methods once the Peniko-backed image exports are in place.
- [x] Supply a Vello-backed region implementation so `CreateRegion` no longer defers to Skia.
- [x] Remove the fallback render interface instance after all methods are wired to native implementations (keeping it only as optional diagnostic fallback).

### 6. Validation & Cleanup
- [ ] Run cross-platform builds and samples (`dotnet build`, `dotnet run` on macOS, Windows, Linux).
- [ ] Validate text rendering (BiDi, combining marks, font fallback) using the new pipeline.
- [ ] Verify bitmap IO paths (decode/encode) and ensure parity with previous behaviour.
- [ ] Remove obsolete code paths, cleanup packages, update LICENSE/NOTICE if necessary.
- [ ] File follow-up tasks for advanced features (color fonts, glyph caching, GPU surfaces).

## Immediate Next Steps
- [x] Kick off Milestone 1 by auditing the native workspace layout and documenting initial observations.
- [x] Collect repository URLs and preferred revisions for each Rust crate.
- [x] Resolve access for private/404 repositories (`skrifa`, `fontique`, `harf-rs`) or identify alternative mirrors/tarballs.
- [x] Decide submodule layout for multi-crate repositories (`fontations` for `skrifa`, `parley` for `fontique`) and document paths.

### Submodule Layout Proposal *(Completed)*
- `extern/peniko` → git submodule to `https://github.com/linebender/peniko` @ `8cbfc12c0415ca5023a060183769f533b84bb516`.
- `extern/parley` → git submodule to `https://github.com/linebender/parley` @ `f20a1fa65c29eac521a0fe4e124d5ea40b792e43` (provides `parley` + `fontique`).
- `extern/fontations` → git submodule to `https://github.com/googlefonts/fontations` @ `8b6497da9ae0e94e27fd488c01d7955f8f1b2b9f`; crates of interest: `skrifa` (and potentially shared utilities).
- `extern/swash` → git submodule to `https://github.com/dfrg/swash` @ `5d885ff4ec8d8b56eceeae29a4f595c7320630b9`.
- `extern/harfbuzz_rs` → git submodule to `https://github.com/manuel-rhdt/harfbuzz_rs` @ `e77d94a9855850ec27b7ac34fa883a74e317cb25`.

Each submodule would be referenced in the root workspace via `[patch."https://github.com/..." ]` entries or explicit `path` overrides inside `Cargo.toml`. Crate-specific build scripts may require updating `Cargo.lock` or enabling feature flags to match Vello’s expectations.
