# ADR-0005: Shared Composition Ownership Across Charts and TreeDataGrid

## Status
Accepted – aligned with TreeDataGrid Phase 0 completion.

## Context
- The chart engine currently owns layout constraint solving, rich text shaping, and scene-diff orchestration within `ffi/chart-engine` and `src/VelloSharp.ChartEngine`.
- Upcoming TreeDataGrid and editor controls require the same primitives to guarantee deterministic frame pacing, non-uniform virtualization, and consistent typography.
- Duplication of these components would multiply maintenance overhead, introduce divergent optimisation paths, and complicate GPU resource pooling.
- Catalogued components earmarked for reuse include:
  - Layout solver (`layout::grid`, axis/panel arrangement) with constraint graph and dirty-region propagation.
  - Typography pipeline (font resolver, glyph atlas management, shaping caches).
  - Scene diff/patch system (command buffer pooling, instance recycling, material registries).
  - Diagnostics surfaces (frame stats, scheduler instrumentation) consumed by render scheduling.

## Decision
- Extract shared composition primitives into a dedicated Rust crate `ffi/composition` exposing:
  - Constraint solver and layout graph abstractions.
  - Text shaping façade over Vello text stack with glyph atlas pooling.
  - Scene diff engine with reusable material and geometry caches.
  - Instrumentation hooks emitting shared diagnostics payloads.
- Publish managed bindings under `src/VelloSharp.Composition` with Span-friendly APIs for charts, TreeDataGrid, and future controls.
- Charts, TreeDataGrid, and advanced editors take hard dependencies on the shared crate; feature-specific logic remains in their owning modules.
- Governance for shared composition is owned by the Rendering Architecture working group, with explicit API review gates before changes land.

## Consequences
- **Benefits**
  - Guarantees rendering consistency and predictable performance budgets across controls.
  - Enables shared benchmarking and regression suites (layout, text, diff) to cover both charts and TreeDataGrid.
  - Simplifies resource pooling, reducing GPU memory churn under concurrent chart/TDG usage.
- **Trade-offs**
  - Requires refactoring timeline coordination between chart and TreeDataGrid milestones.
  - Introduces versioning considerations; shared crate releases must avoid breaking dependent controls.
- **Mitigations**
  - Incremental extraction: mirror APIs behind feature flags until both consumers adopt the shared crate.
  - Document compatibility contracts and semantic version expectations for the shared composition layer.

## Implementation Notes
- Initial extraction targets layout solver and text shaping; scene diff moves once virtualization APIs stabilise.
- Shared diagnostics payloads piggyback on existing `FrameStats` structures with control-specific extensions via tagged unions.
- Native interop surface exposes opaque handles for layout graphs and shaped text blocks to keep .NET bindings allocation-free.
