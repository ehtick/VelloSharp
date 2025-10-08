# VelloSharp Real-Time TreeDataGrid Implementation Plan

## Vision and Scope
- Deliver a high-performance, real-time `TreeDataGrid` (TDG) that renders entirely through the Vello scene API, eliminating UI-framework layout bottlenecks.
- Unify composition primitives (layout, rich text, vector assets) across charting and data-grid experiences via reusable Rust crates and managed bindings.
- Support enterprise-grade datasets with hierarchical grouping, non-uniform row heights, dynamic columns, and micro-interactions under sub-8 ms frame budgets.

## Architectural Pillars
- **Scene-Native Rendering**: Author cells, rows, and chrome with Vello primitives; maintain zero-copy GPU command buffers with diff-friendly scene updates.
- **Composable Layout Core**: Extract chart engine layout, typography, and composition layers into shared crates (`ffi/composition`, `src/VelloSharp.Composition`) for reuse.
- **Hybrid Virtualization**: Combine UI virtualization (row/column windowing, non-linear indices) with data virtualization (paged adapters, async fetch) to sustain large hierarchies.
- **Input + Interaction Fabric**: Centralise pointer, keyboard, and accessibility input routing in Rust-managed state synchronized with .NET hosts via FFI event channels.
- **Declarative XAML Surface**: Provide Vello-aligned XAML and C# object graph that maps to scene descriptors, enabling designers to define columns, templates, and custom visuals.

## Deliverables by Phase

### Phase 0 – Research, Alignment, and Refactoring Blueprint (2 weeks)
- [x] Catalogue chart-engine layout, typography, and scene-diff components suitable for extraction; create ADRs for shared composition ownership.
- [x] Profile current chart engine frame timings with representative grid workloads; capture baselines in `docs/metrics/performance-baselines.md`.
- [x] Define interoperability contracts for TDG host surfaces, input routing, and diagnostics (`docs/specs/tdg-interop.md`).
- [x] Produce UX motion study (scrolling, expand/collapse, editing) validating 120 Hz targets; store artifacts under `docs/diagrams/tdg-flows/`.

#### Phase 0 Progress Snapshot
- Shared composition catalog captured in ADR-0005 with extraction plan for layout, typography, and scene diff primitives.
- TDG hybrid virtualization benchmark appended to `docs/metrics/performance-baselines.md`, confirming <8 ms frames and 18 ms input latency targets.
- Interop contract authored (`docs/specs/tdg-interop.md`) aligning surface hosting, input routing, data adapters, and telemetry with existing chart contracts.
- Motion study assets added under `docs/diagrams/tdg-flows/` demonstrating scroll, expand, and edit flows holding 120 Hz budgets.

### Phase 1 – Shared Composition and Layout Foundations (3–4 weeks)
- [x] Extract layout solver, constraint system, and text shaper from chart engine into `ffi/composition` crate with safe FFI surface.
- [x] Port managed adapters to `src/VelloSharp.Composition`, providing Span-friendly APIs for layout requests and rich text runs.
- [x] Implement scene graph cache with dirty-region tracking for hierarchical nodes (point and bounds invalidation).
- [x] Author golden tests (Rust + .NET) comparing pre/post extraction metrics for chart rendering to ensure regression-free refactor.

#### Phase 1 Progress Snapshot
- Seeded `ffi/composition` crate with exported plot-area computation and text layout primitives (label metrics + glyph layout) referenced by the chart engine.
- Introduced `src/VelloSharp.Composition` managed interop with UTF-8 safe helpers for plot metrics and label measurement, enabling downstream TDG prototypes to consume shared composition services.
- Extended the shared scene cache with dirty-bounds propagation and SafeHandle-managed FFI (`SceneCache.MarkDirtyBounds`), now backed by Rust + .NET golden metrics for layout, text shaping, and dirty-region aggregation (TreeSceneGraph + linear layout smoke tests).
- Bootstrapped `src/VelloSharp.TreeDataGrid` with composition-ready column layout (`TreeNodeLayoutEngine`) and scene cache helpers (`TreeSceneGraph`) to prove reuse inside managed prototypes.
- Added console walkthrough sample (`samples/VelloSharp.TreeDataGrid.CompositionSample`) showcasing column layout solving and dirty-region aggregation for TreeDataGrid composition prototyping.
- Published `docs/specs/shared-composition-contract.md` detailing API responsibilities, versioning, and performance expectations shared between charts and TDG.

### Phase 2 – TreeDataGrid Core Rendering Engine (4–5 weeks)
- **Core data model**
  - [x] Implement hierarchical data model in Rust supporting lazy children materialization, selection state, and expansion diffs.
  - [x] Surface managed `TreeDataModel` wrapper + FFI coverage for attach/expand/select/materialize flows.
  - [x] Build large-tree stress harness validating diff batching, selection range breadth, and queue backpressure.

- **Scene generation & caching**
  - [x] Build Vello scene generators for rows, group headers, summaries, and chrome (grid lines, backgrounds, frozen panes).
  - [x] Wire `TreeSceneGraph` to shared scene cache with dirty-region helpers for row + chrome nodes.
  - [x] Connect virtualization plan output to scene graph reuse (buffer retargeting + dirty rect updates) via integration harness.

- **Hybrid virtualization**
  - [x] Integrate hybrid virtualization scheduler (windowed rows, column slicer, viewport-aware buffer reuse).
  - [x] Expose managed `TreeVirtualizationScheduler` with plan/recycle APIs and unit coverage.
  - [x] Add telemetry hooks surfacing reuse/adopt/allocate/recycle counts and pool sizes to managed callers.
  - [x] Tune buffer eviction heuristics for sustained >50k row scroll scenarios without churn spikes.

- **Column sizing & layout**
  - [x] Implement adaptive column sizing modes (auto, star, pixel) with constraint propagation and animation-friendly transitions.
  - [x] Ship `TreeColumnLayoutAnimator` damping pipeline with tests + sample usage.
  - [x] Validate column strip outputs flowing into virtualization + scene encoding in harness.
  - [x] Complete production wiring for freeze-aligned strip diffing across panes.

- **Render loop & diagnostics**
  - [x] Expose high-frequency renderer loop returning frame stats + vended instrumentation slots for GPU/queue timings.
  - [x] Integrate Vello GPU timestamp summaries and publish metrics into shared diagnostics channel.

- **Integration scaffolding**
  - [x] Publish CLI composition sample exercising model → virtualization → scene + render loop handshake.
  - [x] Author combined integration test harness stitching data model diffs through virtualization into scene graph updates.
  - [x] Document FFI invariants, threading model, and buffer ownership ahead of Phase 3 host bindings.

#### Phase 2 Progress Snapshot
- Integrated freeze-pane column diffing into the virtualization scheduler (`TreeVirtualizationPlan.PaneDiff`) with managed helpers for per-pane spans and metrics.
- Extended `TreeColumnStripCache` to expose leading/primary/trailing snapshots and diff union helpers so host chrome/scene updates react to freeze-band transitions.
- Hardened samples and stress/integration tests around pane-aware virtualization telemetry and buffer reuse, ensuring plan outputs stay in sync with managed orchestration.

  ### Phase 3 – Declarative Templates and Cell Customization (3 weeks)
  - [ ] Define XAML schema `Vello.Tdg.*` mirroring Avalonia primitives (TextBlock, Path, StackPanel) but targeting composition descriptors.
  - [ ] Implement XAML-to-scene compilation pipeline (parse -> expression tree -> FFI) with caching and invalidation strategy.
- [ ] Provide C# fluent builders for scenarios without XAML; ensure type-safe binding to row/column contexts.
- [ ] Introduce per-column render hooks for custom Vello drawing, including shared shader/material registries.

### Phase 4 – Interaction, Editing, and Accessibility (3–4 weeks)
- [ ] Implement pointer routing (hit testing, hover, drag, context actions) unified with chart engine input router.
- [ ] Add keyboard navigation (arrows, page, home/end, search) and command routing for editing workflows.
- [ ] Integrate inline editors (text, combo, numeric) leveraging shared rich text layout; provide IME and clipboard support.
- [ ] Deliver accessibility tree projections (UIA/AX) with live updates and focus tracking.
- [ ] Build deterministic interaction recorder for regression playback and diagnostics export.

### Phase 5 – Data Virtualization and Connectors (3 weeks)
- [ ] Ship data providers supporting paging, background loading, and eviction policies with configurable concurrency.
- [ ] Implement predictive prefetch (scroll velocity-based) and background summarization tasks.
- [ ] Provide adapters for common data sources (IAsyncEnumerable, Reactive streams, gRPC) and sample integrations.
- [ ] Add telemetry hooks for refresh rates, cache hit ratios, and data latency.

### Phase 6 – Host Integrations and Tooling (parallel, 4 weeks)
- [ ] Deliver Avalonia host control (`VelloSharp.TreeDataGrid.Avalonia`) with swapchain management and compositor integration.
- [ ] Add WinUI/WPF/MAUI hosts leveraging shared surface contracts; document DPI and input nuances.
- [ ] Update sample gallery with TDG scenarios (financial blotter, hierarchical analytics, log viewer) demonstrating custom templates.
- [ ] Extend benchmarking harness with TDG workloads, capturing CPU/GPU metrics and memory snapshots.

### Phase 7 – Hardening, QA, and Release (3 weeks)
- [ ] Run large-scale stress suites (1M row hierarchies, 10k columns) measuring frame budget adherence.
- [ ] Validate multi-monitor, high-DPI, and RTL locales; ensure text shaping parity with chart engine.
- [ ] Complete documentation set: developer guide, XAML cookbook, perf tuning manual, extensibility samples.
- [ ] Publish NuGet packages, host deliverables, and migration guide for preview consumers.

## Cross-Cutting Enablers
- [ ] **Performance Budgeting**: Enforce <8 ms frame budget via automated CI gate; integrate into shared telemetry dashboards.
- [ ] **Observability**: Embed tracing spans and frame diagnostics consistent with chart engine (`FrameStats`, `InputLatencyStats`).
- [ ] **Testing Strategy**: Property-based virtualization tests, golden image snapshots, and XAML template diff validation.
- [ ] **Security & Compliance**: Ensure sandbox-safe data provider extensions, signed native binaries, and vetted dependencies.
- [ ] **Developer Ergonomics**: Publish schematic diagrams (`docs/diagrams/tdg-architecture.puml`) and provide CLI scaffolding for new columns/templates.

## Dependencies and Risks
- Alignment with chart engine refactor schedule; cross-team code ownership must be settled before Phase 1.
- GPU memory pressure under simultaneous chart + TDG views; requires shared resource pooling.
- Declarative template compilation must stay deterministic; caching strategy needs eviction policy to avoid stutters.
- Accessibility and IME handling across hosts may require per-platform shims; plan for early prototyping.

## Success Metrics
- Maintain ≥120 FPS under 50k visible row nodes with active updates.
- Keep input-to-visual response under 30 ms for selection/edit operations.
- Achieve ≥90% reuse of shared composition/text infrastructure across charts and TDG.
- Hit <2% layout thrash per frame with adaptive column sizing scenarios.
