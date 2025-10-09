# VelloSharp Real-Time TreeDataGrid Implementation Plan

## Vision and Scope
- Deliver a high-performance, real-time `TreeDataGrid` (TDG) that renders entirely through the Vello scene API, eliminating UI-framework layout bottlenecks.
- Unify composition primitives (layout, rich text, vector assets) across charting and data-grid experiences via reusable Rust crates and managed bindings.
- Support enterprise-grade datasets with hierarchical grouping, non-uniform row heights, dynamic columns, and micro-interactions under sub-8 ms frame budgets.

## Architectural Pillars
- **Scene-Native Rendering**: Author cells, rows, and chrome with Vello primitives; maintain zero-copy GPU command buffers with diff-friendly scene updates.
- **Composable Layout Core**: Extract chart engine layout, typography, and composition layers into shared crates (`ffi/composition`, `src/VelloSharp.Composition`) for reuse, expanding pluggable stack/wrap/grid/dock primitives and virtualization surfaces shared with charting.
- **Hybrid Virtualization**: Combine UI virtualization (row/column windowing, non-linear indices) with data virtualization (paged adapters, async fetch) to sustain large hierarchies.
- **Input + Interaction Fabric**: Centralise pointer, keyboard, and accessibility input routing in Rust-managed state synchronized with .NET hosts via FFI event channels, anchored by a reusable `InputControl` base and pluggable host adapters consumable by charting and TDG-derived controls.
- **Shared Control Toolkit**: Build ItemsControl/ListBox/TreeView experiences, templated control bases (`TemplatedControl`, `Panel`, `UserControl`), standard controls (`Button`, `CheckBox`, `RadioButton`, `DropDown`, `TabControl`), and geometry/shape primitives (`Border`, `Decorator`, `Path`, `Rectangle`, `Ellipse`, `GeometryPresenter`) plus text primitives (`TextBlock`, `AccessText`, `TextBox`) on top of TDG virtualization/composition so dashboards and charting surfaces reuse the same control stack while mirroring `Avalonia.Controls` naming and semantics.
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
- [x] Extend composition layout library with reusable stack, wrap, grid, and dock components plus virtualization hooks ready for charting and TDG control reuse.
- [x] Define shared templated control lifecycle (mount, template apply, visual tree virtualization) and expose core abstractions (`TemplatedControl`, `Panel`, `UserControl`, `Border`, `Decorator`, `Shape`) through the composition contract for downstream controls, preserving `Avalonia.Controls` behavioural parity for templates and shapes.
- [x] Port managed adapters to `src/VelloSharp.Composition`, providing Span-friendly APIs for layout requests and rich text runs.
- [x] Implement scene graph cache with dirty-region tracking for hierarchical nodes (point and bounds invalidation).
- [x] Author golden tests (Rust + .NET) comparing pre/post extraction metrics for chart rendering to ensure regression-free refactor.

#### Phase 1 Progress Snapshot
- Seeded `ffi/composition` crate with exported plot-area computation and text layout primitives (label metrics + glyph layout) referenced by the chart engine.
- Introduced `src/VelloSharp.Composition` managed interop with UTF-8 safe helpers for plot metrics and label measurement, enabling downstream TDG prototypes to consume shared composition services.
- Extended the shared scene cache with dirty-bounds propagation and SafeHandle-managed FFI (`SceneCache.MarkDirtyBounds`), now backed by Rust + .NET golden metrics for layout, text shaping, and dirty-region aggregation (TreeSceneGraph + linear layout smoke tests).
- Bootstrapped `src/VelloSharp.TreeDataGrid` with composition-ready column layout (`TreeNodeLayoutEngine`) and scene cache helpers (`TreeSceneGraph`) to prove reuse inside managed prototypes.
- Added console walkthrough sample (`samples/VelloSharp.TreeDataGrid.CompositionSample`) showcasing column layout solving and dirty-region aggregation for TreeDataGrid composition prototyping.
- Completed stack/wrap/grid/dock solvers and virtualization planning in `ffi/composition/src/panels.rs` + `ffi/composition/src/virtualization.rs`, with managed layout primitives under `src/VelloSharp.Composition/LayoutPrimitives.cs`.
- Landed templated control/shape primitives (`src/VelloSharp.Composition/Controls/*`) with virtualization capture, exercised by the TDG composition sample and charting tests.
- Shipped shared input pipeline (`InputControl`, `ICompositionInputSource`/`Sink`) with Avalonia + WPF/WinUI adapters ready for TDG hosts; pointer capture/focus coverage lives in `tests/VelloSharp.Charting.Tests/Composition/InputControlTests.cs`.
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
  - [x] Integrate Vello GPU timestamp summaries and publish metrics into shared diagnostics channel (TreeRenderLoop now forwards stats through `FrameDiagnosticsCollector`/`VelloSharp.ChartDiagnostics`).

- **Integration scaffolding**
  - [x] Publish CLI composition sample exercising model → virtualization → scene + render loop handshake.
  - [x] Author combined integration test harness stitching data model diffs through virtualization into scene graph updates.
  - [x] Document FFI invariants, threading model, and buffer ownership ahead of Phase 3 host bindings.

#### Phase 2 Progress Snapshot
- Integrated freeze-pane column diffing into the virtualization scheduler (`TreeVirtualizationPlan.PaneDiff`) with managed helpers for per-pane spans and metrics.
- Extended `TreeColumnStripCache` to expose leading/primary/trailing snapshots and diff union helpers so host chrome/scene updates react to freeze-band transitions.
- Hardened samples and stress/integration tests around pane-aware virtualization telemetry and buffer reuse, ensuring plan outputs stay in sync with managed orchestration.
- `TreeRenderLoop` now publishes frame stats through `FrameDiagnosticsCollector`, aligning TDG telemetry with charting dashboards and enabling shared perf gating.

### Phase 3 – Declarative Templates and Cell Customization (3 weeks)
- [x] Define XAML schema `Vello.Tdg.*` mirroring Avalonia primitives (TextBlock, Path, StackPanel) but targeting composition descriptors.
- [x] Implement XAML-to-scene compilation pipeline (parse -> expression tree -> FFI) with caching and invalidation strategy.
- [x] Provide C# fluent builders for scenarios without XAML; ensure type-safe binding to row/column contexts.
- [x] Introduce per-column render hooks for custom Vello drawing, including shared shader/material registries.
- [x] Promote shared text primitives (`TextBlock`, `AccessText`, basic `TextBox`) within the template schema and composition bindings to guarantee parity with charting controls.
- [x] Deliver reusable control atoms (`Button`, `CheckBox`, `RadioButton`, `DropDown`, `TabControl`, `Border`, `Decorator`, `Path`, `Rectangle`, `Ellipse`, `GeometryPresenter`) and base classes (`TemplatedControl`, `Panel`, `UserControl`) built on the shared template pipeline with TDG and chart samples validating reuse, mirroring `Avalonia.Controls.*` naming/behaviour for downstream parity.

#### Phase 3 Kickoff Snapshot
- Drafted the `Vello.Tdg.*` schema framing row, group-header, summary, and chrome templates around the freeze-aware pane slices surfaced by `TreeColumnPaneSnapshot`.
- Aligned pane-aware template resolution with the virtualization scheduler so leading/primary/trailing content can be evaluated independently while sharing cached column metrics.
- Captured helper requirements for batching scene nodes per pane, enabling template rendering to reuse the `TreeColumnStripCache` snapshots without re-computing span metrics.
- Built the managed template compiler (`TreeTemplateCompiler`) that parses XAML into an intermediate expression tree, emits scene instructions, and hydrates FFI-friendly batches with per-template caching and explicit generation-based invalidation.
- Linked template runtime to a native backend so compiled instructions hydrate pane batches through FFI without requiring XAML.
- Added fluent C# builders (`TreeTemplateBuilder`) that generate the same instruction stream as XAML, using generics to enforce row/column binding correctness and enabling template definitions to be composed programmatically.

#### Vello.Tdg Template Schema (Draft)
| Element | Purpose | Key Attributes / Notes |
| --- | --- | --- |
| `Vello.Tdg.Templates` | Root resource that attaches to a `TreeDataGridHost` and fans out to row/group/summary/chrome pipelines. | `{RowTemplate}`, `{GroupHeaderTemplate}`, `{SummaryTemplate}`, `{ChromeTemplate}` |
| `Vello.Tdg.RowTemplate` | Declarative description of a row’s content tree. | `PaneTemplates` (collection), `DefaultCell`, `AlternateCell`, `Bindings` (scoped to row data context) |
| `Vello.Tdg.PaneTemplate` | Associates template fragments with a freeze band (`Leading`, `Primary`, `Trailing`). | `Pane` (enum), `Cells` (collection of `Vello.Tdg.CellTemplate`) |
| `Vello.Tdg.CellTemplate` | Describes composition nodes bound to a column definition. | `ColumnKey`, `Content` (scene descriptor subtree), optional `FallbackPane` routing |
| `Vello.Tdg.SceneNode` primitives | Thin wrappers mapping XAML-like elements (`Stack`, `Text`, `Path`, `Image`) to composition descriptors. | Schema mirrors Avalonia naming while emitting `SceneFragment` descriptors consumed by the shared renderer |

Schema application:
- Pane evaluation resolves `TreeColumnPaneSnapshot` slices into `Vello.Tdg.PaneTemplate` instances; each template receives the column span/margin metadata plus contextual bindings (row value, selection state, stripe offsets).
- `Vello.Tdg.TemplateScope` surfaces attached properties for `RowIndex`, `Depth`, and `PaneKind`, allowing declarative triggers without bespoke code-behind.

### Phase 3.5 – High-Performance Animation System (2–3 weeks)
- **Shared composition animation core**
  - [x] Extend `ffi/composition` with a low-allocation animation timeline engine (easing curves, spring/damping models, grouped timelines) that can drive Vello scene updates without reallocating command buffers. (`TDG-ANIM-001`)
  - [x] Expose managed bindings under `src/VelloSharp.Composition` for timeline creation, property tracks, and FFI-backed tick scheduling; keep APIs Span-friendly and avoid delegate boxing. (`TDG-ANIM-002`)
  - [x] Publish Rust + .NET benchmarks validating ≤0.5 ms CPU overhead per frame for 10k animated properties and golden tests comparing interpolated outputs against analytical expectations.
    - `chart_benchmarks -- timeline` (Rust) and `VelloSharp.Composition.Benchmarks` (.NET) now export timeline metrics captured in `docs/metrics/performance-baselines.md`.

- **TreeDataGrid animation integration**
  - [x] Replace ad-hoc column resize/stripe transitions with the shared animation engine so virtualization plans reuse timeline state per buffer and avoid scene re-encodes.
    - [x] Add row expand/collapse micro-interactions (height easing, selection glow, caret rotation) that run within the 8 ms frame budget and respect virtualization recycling.
    - [x] Surface animation configuration through managed APIs/XAML (duration presets, easing curves, reduced-motion toggles) and document interoperability expectations in `docs/specs/tdg-interop.md`.
    - [x] Author integration tests and profiling traces ensuring animation-driven dirty regions stay bounded and do not trigger full scene re-encodes.

- **Chart engine alignment**
  - [x] Adopt the shared animation bindings inside chart cursor/annotation transitions to validate cross-control reuse and synchronize with the charts roadmap.
  - [x] Capture a joint motion guideline addendum (`docs/diagrams/tdg-flows/tdg-motion-study.puml`) covering synchronized chart/TDG animations for dashboard scenarios.

#### Phase 3.5 Progress Snapshot (Week 1)
- Tree column layout transitions now run entirely on the shared timeline via `TreeColumnLayoutAnimator`, which maps offsets/widths to spring tracks, deduplicates track lifetimes, and snaps samples at rest to prevent drift before reusing buffers (`TreeVirtualizationScheduler` consumes the animator on every plan tick to keep pane diffs incremental).
- Virtualization polling activates the animator between plans so windowed panes observe continuous motion while keeping dirty regions bounded; regression coverage asserts eased transitions settle and continue during polling (`TreeDataGridPhase2Tests.ColumnAnimator_SoftensTransition` and `.Virtualizer_Plan_PollsColumnAnimator` guard behaviour).
- Introduced `TreeRowInteractionAnimator` to drive expand/collapse micro-interactions (height easing, selection glow, caret rotation) surfaced via `TreeVirtualizationPlan.RowAnimations`; `TreeVirtualizationScheduler.NotifyRowExpansion` wires expansion events into the timeline with coverage in `TreeDataGridPhase2Tests.RowAnimator_*` and harness updates consuming the animated state.
- Added `TreeRowAnimationProfile`/`TreeAnimationTimeline` helpers so hosts can call `TreeVirtualizationScheduler.ConfigureRowAnimations(...)` to tune easing, durations, and reduced-motion behaviour; the configuration is documented in `docs/specs/tdg-interop.md` and exercised in new reduced-motion tests.
- The composition sample exercises the new pipeline end-to-end, wiring the animator into the scheduling loop and emitting diagnostics so teams can validate timelines alongside scene generation (`samples/VelloSharp.TreeDataGrid.CompositionSample`).
- Charting now exposes `ChartAnimationProfile`/`ChartAnimationController` hooks through engine options, allowing dashboards to align TDG column motion with series stroke-width emphasis while honouring reduced-motion toggles; shared binding tests (`TimelineSystemInteropTests`) continue to validate the underlying timeline runtime.
- Added chart engine coverage in `tests/VelloSharp.Charting.Tests/Engine/ChartEngineAnimationTests.cs`, confirming cursor, annotation, and reduced-motion scenarios project shared timeline state into `ChartFrameMetadata` overlays.
- Captured a dashboard sync scenario in `docs/diagrams/tdg-flows/tdg-motion-study.puml`, highlighting shared timeline groups driving simultaneous TDG row glow and chart crosshair emphasis under reduced-motion flags.

#### Ticket Backlog & Sequencing
1. `TDG-ANIM-001` (Owner: Composition WG) – Implement the low-allocation timeline runtime in `ffi/composition` with easing/spring primitives. _Predecessor: none._
2. `TDG-ANIM-002` (Owner: Managed Bindings) – Surface animation builders and tick scheduling APIs in `VelloSharp.Composition`, including telemetry hooks. _Depends on TDG-ANIM-001._
3. `TDG-ANIM-003` (Owner: TreeDataGrid) – Integrate timelines into column resizing, buffer reuse, and expand/collapse flows using the shared runtime. _Depends on TDG-ANIM-002._
4. `CHT-ANIM-001` (Owner: Charts Engine) – Adopt shared animation bindings for cursor/annotation transitions and coordinate cross-control motion guidelines. _Depends on TDG-ANIM-002; informs TDG/Charts joint motion study._

#### XAML Compilation Pipeline
- Parsing uses an XML-aware reader that normalizes the `http://schemas.vello.dev/tdg` namespace into a `TreeTemplateSyntaxTree`, capturing attributes, text nodes, and nested elements without allocating reflection metadata.
- Expression conversion maps schema elements to `TreeTemplateExpression` nodes, inferring binding vs. literal values (string, number, boolean, color) and preserving pane metadata for downstream rendering.
- Instruction emission produces a compact array of `TreeTemplateInstruction` opcodes (`OpenNode`, `SetProperty`, `BindProperty`, `CloseNode`) suitable for native ingestion.
- `TreeTemplateCompiler` hashes template content and tracks a caller-provided generation number; cached programs are reused when both hash and generation match, while `Invalidate` forcefully drops affected keys.
- `TreeTemplateRuntime` defers realization to a backend abstraction; the default managed backend stores realized programs, while the native backend streams instruction spans into `vello_tree_datagrid` template entry points so scene nodes remain zero-copy.

#### Fluent Builder Surface
- `TreeTemplateBuilder.Row<TRow, TColumn>` constructs row templates via chained builders (`TreeRowTemplateBuilder`, `TreePaneTemplateBuilder`, `TreeCellTemplateBuilder`) that mirror the XAML schema.
- Binding helpers accept `Expression<Func<TRow, ...>>` / `Expression<Func<TColumn, ...>>`, emitting canonical `Row.*` and `Column.*` binding paths to keep data-context usage type-safe.
- Builder nodes (`Stack`, `Text`, `Rectangle`, `ContentPresenter`) feed directly into the expression/ instruction pipeline, ensuring parity with XAML while enabling template composition from code or configuration.
- Caching reuses the same generation-aware pipeline: fluent definitions compile to `TreeTemplateExpression`, which hashes structurally before emitting instructions, so code-built templates participate in the same diff heuristics.

#### Runtime Integration
- Added `TreeTemplateNativeBackend` to compile instruction streams into native template programs and encode freeze-aware pane batches inside `SceneGraph`.
- `TreeTemplateRuntime` now tracks backend ownership, supports repeated execution with new bindings/pane batches, and disposes native programs safely.
- Native FFI (`vello_tdg_template_program_*`) caches template atoms, parses lightweight pane metadata, and seeds placeholder scenes so managed virtualization can validate pane hydration without XAML.
#### Pane Snapshot Mapping
- `TreeColumnStripSnapshot` now feeds template resolution by producing three `TreeColumnPaneSnapshot` values (leading/primary/trailing). The managed helper (`TreeTemplatePaneBatcher`) batches these snapshots into reusable scene descriptors so template runs can stream directly into the `TreeSceneGraph`.
- Each pane batch contains the ordered `TreeColumnSpan` slice, `TreeColumnMetric` slice, and a lazily created `SceneBatchId`. Freeze bands therefore render deterministically even when scheduler diffing only invalidates a subset of panes.
- Pane batches expose `Span<TreeColumnSpan>` for hot-path renderers while preserving allocations via pooled buffers tied to scheduler telemetry.

#### Upcoming Work
- Add golden tests validating that XAML-compiled templates emit the same instruction stream and `TreeTemplateNativeBackend` output as fluent builder definitions.
- Expand managed tests to cover pane-aware template swapping and the diagnostics pipeline introduced for buffer adoption/allocation heuristics.

### Phase 4 – Interaction, Editing, and Accessibility (3–4 weeks)
- [ ] Implement pointer routing (hit testing, hover, drag, context actions) unified with chart engine input router and surfaced through the shared `InputControl` base for reuse across derived controls.
- [ ] Add keyboard navigation (arrows, page, home/end, search) and command routing for editing workflows via the shared `InputControl` adapter pipeline.
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
- [ ] Produce derived controls (`VelloSharp.ItemsControl`, `.ListBox`, `.TreeView`, `.Button`, `.CheckBox`, `.RadioButton`, `.DropDown`, `.TabControl`, `.Border`, `.Decorator`, `.Path`, `.Rectangle`, `.Ellipse`) backed by TreeDataGrid virtualization/composition and shared InputControl/layout/text/templated/shape primitives for charting dashboards and editor reuse.
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
- [ ] **Shared Control Toolkit**: Track stack/wrap/grid/dock layout primitives, text controls, geometry/shape atoms, and the shared `InputControl` base as reusable packages consumed by charts, TDG, and derived ItemsControl/ListBox/TreeView surfaces.
- [ ] **Animation System** (`TDG-ANIM-001`..`003`, `CHT-ANIM-001`): Kick-off held with Composition, TDG, and Charts owners (animation guild sync, 2025-10-08); shared backlog established to deliver the timeline engine, validate reduced-motion toggles, and guard regressions with CI perf thresholds.

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
