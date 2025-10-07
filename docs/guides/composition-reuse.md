# Composition Reuse Guide

## Audience
- Charting, TreeDataGrid, and editor teams adopting the shared composition stack.
- Host integration engineers wiring managed controls to native renderers.

## Prerequisites
- `ffi/composition` and `src/VelloSharp.Composition` must be built (run `cargo build -p vello_composition` or `dotnet build bindings/VelloSharp`).
- Consumers reference `VelloSharp.Composition` and the relevant native runtime packages.

## Core Concepts
- **Layout**: Use `TreeNodeLayoutEngine` / `CompositionInterop.SolveLinearLayout` for pane, column, and chrome sizing. Inputs and outputs are device-independent pixels.
- **Typography**: Measure text using `CompositionInterop.MeasureLabel`; draw via existing text renderers (HarfBuzz/Vello) to maintain glyph parity.
- **Scene Diffing**: Manage dirty regions through `SceneCache` or higher-level helpers (e.g., `TreeSceneGraph`) to avoid redundant scene rebuilds.
- **Diagnostics**: Record metrics in `FrameStats` / `InputLatencyStats` and forward them to telemetry sinks for CI enforcement.

## Integration Checklist
1. **Reference the Managed Bridge**
   - Add `<ProjectReference Include="..\VelloSharp.Composition\VelloSharp.Composition.csproj" />`.
   - Ensure native artefacts are copied into the output folder (see `tests/VelloSharp.Charting.Tests/VelloSharp.Charting.Tests.csproj`).
2. **Adopt Shared Layout APIs**
   - Map control-specific definitions (columns, panes, chrome) to `LinearLayoutChild`.
   - Persist or cache solved slots (`LinearLayoutResult`) for diff-friendly updates.
3. **Unify Text Measurement**
   - Replace bespoke glyph measurement with `CompositionInterop.MeasureLabel`.
   - Keep existing renderers for glyph drawing to respect styling and localisation.
4. **Track Dirty Regions**
   - Use `SceneCache.MarkDirty` for point updates and `MarkDirtyBounds` for ranges.
   - Consume `TakeDirty` during render scheduling to minimise scene submissions.
5. **Telemetry + Benchmarks**
   - Emit frame timing metrics through shared diagnostics collectors.
   - Update `docs/metrics/performance-baselines.md` whenever a new workload is added.

## Recommended Project Structure
- `YourControl.Composition` (optional): thin wrappers converting domain concepts into shared primitives (see `TreeSceneGraph`, `TreeNodeLayoutEngine`).
- `YourControl.Rendering`: consumes wrappers and produces Vello scenes, deferring layout/text/dirty decisions to shared code.
- Tests: add golden metric coverage comparing slot positions, label metrics, and dirty bounds to documented baselines.

## Regression Strategy
- **Rust**: keep unit tests in `ffi/composition` for each export (layout, text, scene cache).
- **.NET**: maintain golden coverage in `tests/VelloSharp.Charting.Tests` (or sibling test projects) using known inputs/outputs.
- **Benchmarks**: extend CI with representative workloads; gate merges on meeting target thresholds (<8 ms CPU+GPU for charts/TDG).

## When to Extend the Contract
- New optional parameters (e.g., baseline alignment modes) → additive exports with version notes.
- Behavioural changes → update `docs/specs/shared-composition-contract.md` and bump semantic versions for `ffi/composition` + bindings.
- Breaking changes → require ADR updates and synchronization between chart and TDG release trains.
