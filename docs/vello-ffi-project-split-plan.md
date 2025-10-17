# Vello FFI Split Plan

## Objectives
- Split the current monolithic bindings so each upstream Rust crate with a public runtime surface (e.g., `vello`, `vello_encoding`, `vello_shaders`, `wgpu`, `wgpu_profiler`, `raw_window_handle`, `vello_svg`, `velato`, `parley`, `fontique`, `skrifa`, `swash`, `harfrust`, `read_fonts`, `png`, `once_cell`) has a dedicated FFI crate (`vello_ffi`, `vello_encoding_ffi`, `vello_shaders_ffi`, `wgpu_ffi`, `wgpu_profiler_ffi`, etc.) when that crate exposes callable APIs that must be reached from managed code.
- Introduce a shared `common_ffi` crate for reusable helpers without breaking the one-crate ↔ one-FFI mapping, while leaving purely internal helper crates (e.g., `pollster`, `futures_intrusive`) embedded where appropriate.
- Document which dependencies remain internal versus those that warrant first-class FFI crates so the split mirrors the upstream crate topology faithfully and avoids needless proliferation.
- Mirror the same structure on the managed side with focused `.csproj` projects that package only the matching native library.
- Preserve current functionality, packaging conventions, and sample coverage throughout the transition.

## Constraints & Assumptions
- Existing published NuGet packages must remain consumable; any breaking changes require coordinated versioning.
- New crates should reuse shared utilities (error marshalling, type wrappers) rather than duplicating code.
- CI builds already cross-compile VelloSharp; the plan assumes these workflows can be extended instead of recreated.

## Milestones & Tasks

1. [ ] **Milestone 1 — Baseline Audit**
   - [ ] Task 1.1 — Catalogue exported symbols in `ffi/vello_ffi/src/lib.rs` and annotate which upstream crate they originate from (`extern/vello`, `extern/vello_encoding`, `extern/vello_shaders`, `extern/wgpu`, `extern/parley`, `extern/fontique`, etc.).
   - [ ] Task 1.2 — Record shared utility code (error handling, type conversions, memory helpers) that needs to move into a `common_ffi` crate.
   - [ ] Task 1.3 — Inventory managed consumers by scanning the `bindings/` projects to understand which Rust crates they touch today and which assemblies they currently ship in.
   - [ ] Task 1.4 — Decide for each dependency whether a standalone FFI crate is required (public API exposed) or if it remains an internal dependency of another FFI crate.
   - [ ] Task 1.5 — Audit existing dedicated FFI crates in `ffi/` (`peniko_ffi`, `kurbo_ffi`, `vello_sparse_ffi`, `vello_webgpu_ffi`, `winit_ffi`, etc.) to determine reuse, renaming, or consolidation paths.

2. [ ] **Milestone 2 — Target Architecture Design**
   - [ ] Task 2.1 — Finalize the mapping table (subject to Task 1.4) so each upstream crate with externally consumed APIs gets a dedicated FFI crate (e.g., `vello_ffi`, `vello_encoding_ffi`, `vello_shaders_ffi`, `wgpu_ffi`, `wgpu_profiler_ffi`, `raw_window_handle_ffi`, `vello_svg_ffi`, `velato_ffi`, `parley_ffi`, `fontique_ffi`, `skrifa_ffi`, `swash_ffi`, `harfrust_ffi`, `read_fonts_ffi`, `png_ffi`, `once_cell_ffi`), plus `common_ffi`, while documenting crates intentionally left internal.
   - [ ] Task 2.2 — Define the responsibilities and dependency direction for each FFI crate, ensuring they mirror the upstream crate graph without introducing new cycles.
   - [ ] Task 2.3 — Draft ABI contracts for every FFI crate (function list, ownership rules, feature flags) and validate them with the maintainer team.
   - [ ] Task 2.4 — Capture migration risks (e.g., cross-crate initialization ordering, duplicated static state) and mitigation strategies before implementation.
   - [ ] Task 2.5 — Specify cross-crate handle and resource ownership rules so shared types can flow through `common_ffi` without requiring FFI-to-FFI calls.

3. [ ] **Milestone 3 — Rust Workspace Restructure**
   - [ ] Task 3.1 — Scaffold new crate directories under `ffi/` for each upstream crate identified in Task 2.1 that still lacks a dedicated FFI package (`vello_encoding_ffi`, `vello_shaders_ffi`, `wgpu_ffi`, `wgpu_profiler_ffi`, `raw_window_handle_ffi`, `vello_svg_ffi`, `velato_ffi`, `parley_ffi`, `fontique_ffi`, `skrifa_ffi`, `swash_ffi`, `harfrust_ffi`, `read_fonts_ffi`, `png_ffi`, `once_cell_ffi`, `common_ffi`, etc.).
   - [ ] Task 3.2 — Slim the existing `vello_ffi` crate so it wraps only the `extern/vello` crate, moving unrelated exports into the new FFI crates.
   - [ ] Task 3.3 — Port encoding- and shader-specific exports from `vello_ffi` into `vello_encoding_ffi` and `vello_shaders_ffi`, reusing shared types through `common_ffi`.
   - [ ] Task 3.4 — Introduce `wgpu_ffi` for device/surface management currently exposed via `vello_ffi`, aligning platform-specific modules (e.g., `windows_shared_texture`) with the new crate.
   - [ ] Task 3.5 — Relocate peniko/kurbo-specific bindings into their existing dedicated crates, ensuring dependencies now reference `common_ffi` where needed.
   - [ ] Task 3.6 — Move text/font shaping exports into the new dedicated crates (`parley_ffi`, `fontique_ffi`, `skrifa_ffi`, `swash_ffi`, `harfrust_ffi`, `read_fonts_ffi`) while centralizing shared caches in `common_ffi`.
   - [ ] Task 3.7 — Update `Cargo.toml` at the workspace root to include the new members, adjust features, and ensure each crate builds as `cdylib`/`staticlib` with consistent artifact naming.
   - [ ] Task 3.8 — Update developer scripts under `scripts/` and automation helpers to reference the new crate names and build targets.
   - [ ] Task 3.9 — Align existing `*_ffi` crates with the new naming and ownership scheme instead of duplicating functionality (e.g., decide if `vello_sparse_ffi` or `vello_webgpu_ffi` need refactoring).

4. [ ] **Milestone 4 — Shared Infrastructure & Utilities**
   - [ ] Task 4.1 — Implement `common_ffi` with error reporting, allocation helpers, and shared FFI-safe structs; replace duplicate code in dependent crates.
   - [ ] Task 4.2 — Refactor macros and tracing helpers (e.g., `trace_path!`, error propagation) into `common_ffi` so they are reused without duplication.
   - [ ] Task 4.3 — Update build scripts and tooling in `scripts/` to support building each crate independently and collectively (including version stamping and symbol verification).
   - [ ] Task 4.4 — Add documentation comments explaining crate boundaries to guard against regressions into a monolithic design.

5. [ ] **Milestone 5 — Integration Test Suite**
   - [ ] Task 5.1 — Audit existing integration tests under `tests/`, `integration/`, and `samples/` to map coverage against the new FFI crate layout.
   - [ ] Task 5.2 — Update existing integration tests to reference the renamed crates, initialization flows, and shared helpers introduced during the split.
   - [ ] Task 5.3 — Create new integration tests where coverage is missing (e.g., `vello_encoding_ffi`, `wgpu_ffi`, `parley_ffi`), ensuring cross-crate scenarios are exercised.
   - [ ] Task 5.4 — Integrate the expanded integration test matrix into CI builds across supported platforms, with clear pass/fail reporting.

6. [ ] **Milestone 6 — Native Validation**
   - [ ] Task 6.1 — Build example binaries or harnesses that exercise cross-crate flows (scene creation → render → surface present) using only the split crates.
   - [ ] Task 6.2 — Update existing benchmarks in `ffi/benchmarks` to consume the new APIs and confirm performance parity with the monolithic crate.
   - [ ] Task 6.3 — Extend CI pipelines to build and execute the native validation binaries/benchmarks across supported targets (Windows/macOS/Linux).
   - [ ] Task 6.4 — Produce pilot artifacts and validate symbol exports with `nm` / `dumpbin` to ensure ABI stability.

7. [ ] **Milestone 7 — Managed Binding Refactor**
   - [ ] Task 7.1 — Design the new managed project layout so each FFI crate has a matching `.csproj` (e.g., `VelloSharp.Ffi.Vello`, `VelloSharp.Ffi.VelloEncoding`, `VelloSharp.Ffi.VelloShaders`, `VelloSharp.Ffi.Wgpu`, `VelloSharp.Ffi.WgpuProfiler`, `VelloSharp.Ffi.Pollster`, `VelloSharp.Ffi.FuturesIntrusive`, `VelloSharp.Ffi.RawWindowHandle`, `VelloSharp.Ffi.VelloSvg`, `VelloSharp.Ffi.Velato`, `VelloSharp.Ffi.Parley`, `VelloSharp.Ffi.Fontique`, `VelloSharp.Ffi.Skrifa`, `VelloSharp.Ffi.Swash`, `VelloSharp.Ffi.Harfrust`, `VelloSharp.Ffi.ReadFonts`, `VelloSharp.Ffi.Png`, `VelloSharp.Ffi.OnceCell`, plus `VelloSharp.Ffi.Common`).
   - [ ] Task 7.2 — Create corresponding `.csproj` files with consistent metadata, native asset packaging, and target frameworks aligned with the existing solution.
   - [ ] Task 7.3 — Split P/Invoke declarations so each project exposes only its crate’s API surface, moving shared types into the common assembly to avoid duplication.
   - [ ] Task 7.4 — Update `VelloSharp.sln`, `Directory.Build.props`, and `Directory.Packages.props` to reference the new projects and keep analyzers consistent.
   - [ ] Task 7.5 — Adjust higher-level managed libraries (e.g., `VelloSharp.Core`, `VelloSharp.Gpu`, samples) to depend on the new assemblies and remove stale references.
   - [ ] Task 7.6 — Ensure NuGet packaging (including native assets layout under `runtimes/`) is correct for each new project and that transitive dependencies resolve.

8. [ ] **Milestone 8 — Packaging Projects**
   - [ ] Task 8.1 — Audit existing packaging projects and author new ones where gaps exist, covering both native artifacts and managed wrappers.
   - [ ] Task 8.2 — Create or update `.csproj`/MSBuild packaging configurations so every FFI crate and managed assembly has a corresponding pack definition.
   - [ ] Task 8.3 — Update packaging scripts in `packaging/` and CI workflows to emit the expanded set of NuGet packages with aligned versioning.
   - [ ] Task 8.4 — Validate generated packages locally (metadata, native asset layout, dependency graph) before enabling CI publication.

9. [ ] **Milestone 9 — End-to-End Validation**
   - [ ] Task 9.1 — Run sample applications under `samples/` and `integration/` to verify they load the new native libraries without runtime errors.
   - [ ] Task 9.2 — Perform smoke tests on all supported platforms (Windows, macOS, Linux, possibly mobile/WebGPU) to confirm parity with the previous layout.
   - [ ] Task 9.3 — Update automated packaging/release scripts to publish both native crates and managed packages under the new names.
   - [ ] Task 9.4 — Gather performance and memory benchmarks comparing old vs. new layout; document any regressions and resolutions.
   - [ ] Task 9.5 — Adjust documentation site build scripts (DocFX or equivalent) to include content from the new crates and managed projects.

10. [ ] **Milestone 10 — Documentation & Rollout**
   - [ ] Task 10.1 — Update README, `docs/ffi-api-coverage.md`, documentation website content, and relevant integration guides to reflect the new crate/project structure.
   - [ ] Task 10.2 — Publish a migration guide for external consumers explaining renamed binaries, new assembly references, and sample updates.
   - [ ] Task 10.3 — Record follow-up technical debt items (e.g., future feature splits, optional bindings) and schedule them into the backlog.
   - [ ] Task 10.4 — Tag the repository with the release version that includes the split and announce availability to stakeholders.
   - [ ] Task 10.5 — Update `STATUS.md`, release notes, and any roadmap documents to reference the new milestones and artifact layout.

## Deliverables
- Signed-off architecture document outlining the new crate and managed project boundaries.
- Updated Rust workspace and managed solution with the split applied and verified by CI.
- Migration documentation and release notes accompanying the first release that ships the split artifacts.

## Exit Criteria
- All milestones marked complete with corresponding PRs merged.
- CI green across supported platforms with the new structure.
- Sample apps and integration scenarios running exclusively against the split FFI bindings.
- Documentation repository updated to describe the new layout and migration path.
