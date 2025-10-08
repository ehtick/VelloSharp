# VelloSharp Real-Time Charts Library Implementation Plan

## Vision and Guiding Principles
- Deliver a high-performance, low-latency charting engine that leverages Vello's GPU-accelerated 2D rendering via VelloSharp.
- Provide a platform-agnostic API surface that bridges major .NET UI frameworks (WPF, WinUI, MAUI, Avalonia, Uno, WinForms) with a shared rendering and interaction core.
- Target demanding financial, economic, and enterprise analytics workloads (trading desks, risk platforms, operations dashboards) with real-time data throughput, high visual fidelity, and extensibility.
- Emphasize composability, deterministic updates, memory safety, and predictable frame pacing under bursty data conditions.

## Implementation Conventions
- [x] Rust production code (engine, data pipeline, diagnostics adapters) resides under `ffi/` with crates per module (e.g., `ffi/chart-engine`, `ffi/chart-data`, `ffi/chart-diagnostics`); experimental prototypes stay in `ffi/experimental`.
- [x] C# production code (bindings, platform hosts, tooling) lives under `src/` following the solution layout (`src/VelloSharp.*`, `src/Samples`, `src/Benchmarks`).
- [x] Avalonia/Vello integration for the chart engine ships from `src/VelloSharp.Charting.Avalonia`, exposing reusable controls and wiring to the shared engine APIs.
- [x] The live validation sample (`samples/VelloSharp.Charting.AvaloniaSample`) must be updated alongside every charting feature and exercise real-time external data feeds for user testing.
- [ ] Checkboxes in this plan only flip to done when corresponding production code, tests, and docs are merged into the repository at the specified paths.
- [ ] Each phase produces shippable artifacts intended for real-world deployments; interim experiments must graduate into production directories before marking deliverables complete.

## Technical Architecture Overview
- **Core Engine**: Rust-powered Vello scene graph orchestrated through VelloSharp bindings, exposing deterministic command buffers for drawing primitives, text, and GPU-backed gradients.
- **Data and State Model**: Lock-free, frame-coherent data pipeline that separates ingestion, transformation, and rendering-ready datasets using time-indexed data structures and ring buffers.
- **Layout and Styling System**: Declarative chart specification composed of scales, axes, panels, series, annotations, and interaction layers. Supports theming and adaptive styling per DPI and color scheme.
- **Shared Composition Stack**: Reusable layout, text shaping, and scene-diff crates (Rust + .NET) powering both charting surfaces and higher-level controls (TreeDataGrid, editors) with deterministic frame budgeting.
- **Interop Layer**: Platform adapters that host a Vello surface, translate UI framework input events into engine commands, and manage swapchain surfaces or texture interop where needed.
- **Extensibility**: Plugin model for new chart types, data feeds, analytics overlays, and export providers without forking the rendering core.

## Phase 0 – Discovery and Architecture Foundations (2–3 weeks)
**Objectives**
- [x] Validate VelloSharp capabilities for dynamic scene updates, path instancing, text rendering, and GPU resource reuse, with prototypes committed under `ffi/experimental`.
- [x] Establish performance success metrics: frame budget targets (<8 ms), data throughput (100k points/sec), interaction latency (<30 ms), and store results in `docs/metrics/performance-baselines.md`.
- [x] Produce architectural decision records (ADRs) covering rendering ownership, multi-threading model, memory strategy, serialization formats, located in `docs/adrs/`.
- [x] Draft API requirements for platform-agnostic usage and interop constraints per framework, recorded in `docs/specs/platform-interop.md`.

**Deliverables**
- [x] Proof-of-concept Vello scene demonstrating batched line rendering with real-time updates (`ffi/experimental/poc_line_scene`, host sample pending).
- [x] Benchmark harness comparing CPU and GPU load under synthetic streaming data (`ffi/benchmarks`, JSON baseline stored in `docs/metrics/performance-baselines.md`).
- [x] ADRs for concurrency, memory pools, dependency injection, and plugin architecture (`docs/adrs/ADR-0001-rendering-ownership.md` – `docs/adrs/ADR-0004-extensibility-and-di.md`).
- [x] Initial UML component diagram and interop contract definitions (`docs/specs/platform-interop.md`, `docs/specs/interop-contracts.md`; export stored under `docs/diagrams/chart-engine-integration.svg`).

### Phase 0 Progress Snapshot (Week 3)
- Prototype line scene generator committed at `ffi/experimental/poc_line_scene` validates deterministic dynamic geometry and text rendering.
- Benchmark harness (`ffi/benchmarks`) now exercises the production `vello_chart_engine` crate; baseline results captured in `docs/metrics/performance-baselines.md`.
- API requirements, interop specifications, and ADR drafts authored under `docs/specs` and `docs/adrs`; awaiting council ratification.

#### Artifact Locations
- Prototype crate: `ffi/experimental/poc_line_scene`
- Benchmark harness: `ffi/benchmarks`
- Metrics dossier: `docs/metrics/performance-baselines.md`
- ADRs: `docs/adrs/ADR-0001-rendering-ownership.md` – `docs/adrs/ADR-0004-extensibility-and-di.md`
- Interop specs: `docs/specs/platform-interop.md`, `docs/specs/interop-contracts.md`

#### Performance Success Metrics
- Frame rendering budget: hard cap at 8 ms on 120 FPS displays, soft budget of 5 ms, measured at 95th percentile over 10-minute trading burst runs.
- Data ingestion throughput: sustain 120k points/sec aggregated over three concurrent time-series streams without frame drops, with burst tolerance to 200k points/sec for 30 seconds.
- Interaction latency: cursor-driven crosshair and pan/zoom feedback under 25 ms on primary display, under 35 ms on secondary 4K display with HDR enabled.
- Memory utilization: GPU memory budget under 1.2 GB per surface at 4K with 10 live chart panes; CPU heap allocations capped at 150 MB steady-state with fallbacks for 32-bit processes.
- Deterministic frame pacing: jitter less than 1.5 ms standard deviation across consecutive frames during synthetic burst workload benchmarks.

#### Platform-Agnostic API Requirements (Draft)
- Core chart engine exposes `IVelloChartSurface` abstraction with lifecycle hooks (`AttachSurface`, `DetachSurface`, `Resize`, `RenderFrame`).
- Rendering surface accepts dependency-injected services (`ITimeProvider`, `ITelemetrySink`, `IInputRouter`) to avoid static global state.
- UI framework adapters implement thin `ChartHost` controls that forward composition and input to the shared engine while honoring framework threading models (e.g., WPF Dispatcher, WinUI CoreDispatcher, Avalonia Dispatcher).
- Data ingestion interfaces standardized around `IStreamingSeries<TPoint>` with push and pull semantics, supporting batching and delta updates.
- Configuration model uses immutable descriptors (`ChartDescriptor`, `SeriesDescriptor`, `ThemeDescriptor`) with builder pattern for ergonomic C# usage and serialization to JSON/YAML for cross-platform tooling.
- Interop contract mandates headless rendering capability for server/CI scenarios via off-screen surfaces (e.g., WGPU headless, D3D texture targets).

#### ADR Portfolio Kickoff
- ADR-0001 Rendering Ownership: requirements drafted; ADR to be authored under `docs/adrs` before code merges.
- ADR-0002 Threading Model: awaiting Architecture Council guidance; documentation to follow.
- ADR-0003 Serialization Strategy: research ongoing; final decision to be captured in ADR.

### Phase 0 Remaining Actions
- Publish UML diagram assets (`docs/diagrams/chart-engine-components.puml/png`) post-gating review.
- Secure Architecture Council approval for ADRs 0001–0004 and update status to Accepted.

## Phase 1 – Core Rendering and Data Pipeline (4–6 weeks)
**Objectives**
- [x] Implement the core rendering engine in Rust (Vello) with C# facade: scene graph management, double-buffered command submission, GPU resource pooling (`ffi/chart-engine`, `src/VelloSharp.ChartEngine`).
- [x] Build data ingestion pipeline: adapters for time-series feeds, buffering strategy, and transformation hooks (normalization, decimation, aggregation) (`ffi/chart-data`, `src/VelloSharp.ChartData`).
- [x] Define stable C# API surface for engine initialization, surface binding, frame submission, and lifecycle management (`src/VelloSharp.ChartEngine`).
- [x] Integrate deterministic scheduler for frame updates (VSync aware, manual tick override) and instrumentation hooks (shared scheduling module under `src/VelloSharp.ChartRuntime`).

**Deliverables**
- [x] `VelloSharp.ChartEngine` core library (Rust + C#) with basic drawing primitives (line segments, rectangles, gradient fills, glyph batches).
- [x] Thread-safe data bus with configurable ring buffer, snapshot API, and eventing for data updates (`ffi/chart-data`, `src/VelloSharp.ChartData`).
- [x] Engine diagnostics module: frame timing logs, GPU/CPU utilization estimators, configurable tracing (`src/VelloSharp.ChartDiagnostics` and supporting Rust adapters).
- [x] Documentation for initialization patterns and extension points (`docs/guides/engine-initialization.md`).
- [x] Real-time Avalonia validation sample (`samples/VelloSharp.Charting.AvaloniaSample`) leveraging `src/VelloSharp.Charting.Avalonia` to exercise streaming data sources.

### Phase 1 Progress Snapshot (Week 3)
#### Render Core Stream
- `ffi/chart-engine` crate now encapsulates scene construction from streaming samples, draws adaptive time/value axes, and emits layered fills/lines suitable for GPU presentation.
- C# facade (`src/VelloSharp.ChartEngine`) exposes `ChartEngine`, `ChartEngineOptions` (including the new `ShowAxes` toggle), and now ships with the finalized scheduler/diagnostics wiring plus palette and series override APIs.
- Avalonia control (`ChartView`) now wraps `VelloSurfaceView`, automatically attaching GPU swapchains and reusing pooled surfaces while preserving the software fallback path when hardware acceleration is unavailable.

#### Data Pipeline Stream
- Rust `DataBus` (`ffi/chart-data`) provides bounded, non-blocking ingestion with `SeriesSample` batches; mirror managed implementation (`src/VelloSharp.ChartData/ChartDataBus.cs`) uses pooled buffers for span-friendly writes.
- Benchmark harness feeds synthetic data via `DataBus`, validating throughput and retention policies.

#### Diagnostics & Scheduler Stream
- Diagnostics scaffolded in Rust (`ffi/chart-diagnostics`) and managed land (`src/VelloSharp.ChartDiagnostics`) with histogram logging and recent frame queue.
- Rust `tracing` events now flow through the FFI bridge into `.NET` `EventSource` with structured property payloads for downstream subscribers.
- Deterministic scheduler in `src/VelloSharp.ChartRuntime/RenderScheduler.cs` now drives the engine render loop, including manual tick overrides and instrumentation callbacks.

#### Avalonia Validation Sample
- `src/VelloSharp.Charting.Avalonia` introduces `ChartView` backed by `VelloSurfaceView`, wiring scheduler callbacks and span-based ingestion directly into the GPU render pipeline.
- Live sample (`samples/VelloSharp.Charting.AvaloniaSample`) streams BTC/USDT trades from Binance WebSocket feeds, updating the chart engine in real time and surfacing latency/connection diagnostics.
- Runtime scripts updated (`copy-runtimes*`, `remove-runtimes*`) to propagate charting runtimes alongside existing samples.

### Governance and Artifacts
- ADRs 0001–0004 submitted for Architecture Council review; Phase 1 merges blocked pending approval.
- UML component diagram and `.plantuml` export targeted for publication alongside ADR ratification.

## Phase 2 - Layout, Coordinate Systems, and Styling (4 weeks)
**Objectives**
- [x] Implement scale abstractions (linear, log, time, ordinal), coordinate transforms, and unit conversion pipelines.
 - [x] Develop layout engine for axes, grids, panels, legends, labels, and viewports with responsive sizing and DPI awareness.
 - [x] Introduce styling subsystem: theming tokens, palette management, typography profiles, and animation presets.
- [x] Add text shaping pipeline leveraging Vello text stack or Harfbuzz integration via VelloSharp for multi-language support.
- [x] Extract layout, typography, and composition primitives into shared `ffi/composition` crate with managed adapters for reuse in TreeDataGrid and future controls.
- [x] Publish integration blueprint aligning chart composition APIs with TreeDataGrid requirements (`docs/specs/shared-composition-contract.md`).

**Deliverables**
 - [x] Layout manager module with constraint-based sizing and overlap avoidance.
 - [x] Axes rendering components with tick generation (fixed, dynamic, algorithmic) and smart labeling.
 - [x] Styling API (fluent builder or declarative JSON/YAML schema) with serialization/deserialization support.
- [x] Gallery of reference layouts demonstrating dark/light themes and adaptive resizing.
- [x] Shared composition libraries (`ffi/composition`, `src/VelloSharp.Composition`) with initial regression tests covering label layout, linear layout, and scene cache dirty tracking; automate perf parity benchmarking in a follow-up milestone.
- [x] Guidance doc covering composable primitives for charts, TreeDataGrid, and editors (`docs/guides/composition-reuse.md`).

### Phase 2 Progress Snapshot (Week 1)
- Introduced `src/VelloSharp.Charting` library hosting reusable scale abstractions (linear, logarithmic, time, ordinal) with normalized projection/unprojection APIs.
- Added coordinate transformation utilities (`Coordinates/CoordinateTransformer.cs`) and unit conversion helpers, enabling consistent mapping between data space and render targets.
- Created validation test suite (`tests/VelloSharp.Charting.Tests`) covering numerical, temporal, and categorical projections plus round-trip conversions.
- Built DPI-aware layout engine (`Layout/ChartLayoutEngine.cs`) that arranges axes/panels and computes plot regions from viewport + device pixel ratio inputs.
- Bootstrapped shared composition crate (`ffi/composition`) and managed adapter (`src/VelloSharp.Composition`) to host plot-area heuristics, text layout services, and reusable scene cache APIs (including dirty-bounds propagation) consumed by charting and TreeDataGrid roadmaps; formalised the reuse boundaries in `docs/specs/shared-composition-contract.md`.
- Added tick generators for linear, time, and ordinal scales (`Ticks/*Generator.cs`) to produce normalized positions and labels for axis rendering.
- Established axis composition pipeline linking layout outputs, tick generation, and styling tokens for forthcoming renderers (`Axis/*`, `Ticks/AxisTickGeneratorRegistry.cs`).
- Introduced theme-aware axis styling and typography tokens (`Styling/*`) with runtime configuration for tick generator selection (`Axis/AxisDefinition*.cs`).
- Implemented axis/legend renderers (`Rendering/AxisRenderer.cs`, `Legend/LegendRenderer.cs`) that consume `AxisRenderSurface`, emit drawable visuals, and honor light/dark palette variants.
- Documented layout gallery presets in  `docs/guides/layout-gallery.md` for dark/light adaptive sizing.
- Unified overlay text measurement with `CompositionInterop.MeasureLabel` and added shared golden metric coverage (Rust + .NET) validating layout, typography, and scene cache outputs across charts/TDG.
- Hardened automated native packaging for test runs (cargo build + copy of `vello_composition` / `vello_chart_engine`) to keep `dotnet test` green and ready for CI performance gating.

## Phase 3 – Core Chart Primitives and Composition (5–7 weeks)
**Objectives**
- [x] Implement reusable primitives: line, area, bar/column, scatter, polyline band, and heatmap series wired through the FFI (`ffi/chart-engine/src/lib.rs`, `src/VelloSharp.ChartEngine/ChartSeriesDefinition.cs`).
- [ ] Deliver custom geometry/overlay primitives (e.g., VWAP clouds, anchored patterns) once requirements land.
- [x] Design the chart composition API for multiple panes, synchronized axes, stacked/overlaid series, and annotation layers (`src/VelloSharp.ChartEngine/ChartComposition.cs`).
- [x] Enable incremental ingestion and rolling windows for real-time feeds via `ChartEngine.PumpData` and `SeriesState` pruning (`src/VelloSharp.ChartEngine/ChartEngine.cs`, `ffi/chart-engine/src/lib.rs`).
- [x] Add backfill reconciliation plus dirty-rect/instancing optimisations to minimise redraw cost (`ffi/chart-engine/src/lib.rs`, regression tests under `tests/VelloSharp.Charting.Tests/Engine`).
- [x] Expose composable render layers, material registries, and scene partitioning hooks consumable by TreeDataGrid and forthcoming editor controls.
- [ ] Integrate data-driven styling (value-based coloring, gradient fills, threshold markers).

**Deliverables**
- [x] `ChartSeries` hierarchy with GPU-friendly geometry buffers and attribute bindings.
- [x] Annotation framework (lines, zones, callouts, Fibonacci tools) with snapping and persistence.
- [x] Live price tick scenario wired to Binance WebSocket feed (`samples/VelloSharp.Charting.AvaloniaSample/MainWindow.axaml.cs`); add remaining volume/heatmap showcases.
- [x] Volume histogram scenario highlighting stacked panes with dynamic volume overlays and value-driven styling.
- [x] Rolling heatmap scenario showcasing density visualisation with adaptive bucket annotations.
- [x] Automated rendering tests capturing pixel diffs across configurations.
- [ ] Public render-hook API specimens and documentation demonstrating TreeDataGrid cell visual reuse.
### Phase 3 Progress Snapshot (Week 1)
- Introduced `ChartSeriesDefinition` hierarchy with line, area, bar, scatter, band, and heatmap primitives wired through the native engine.
- Expanded the Avalonia sample to stream latency, spike markers, and scaled delta bars alongside price data.
- Refreshed legend rendering so markers adapt to series kind visibility (line/area/scatter/bar).
- Added the composition builder under `src/VelloSharp.ChartEngine/ChartComposition.cs` to model multi-pane layouts and annotation layers.
- Implemented pane-aware annotations covering horizontal/vertical guides, shaded zones, and callouts with snap modes.
- Added a headless rendering regression harness (`tests/VelloSharp.Charting.Tests/Rendering/ChartRenderingRegressionTests.cs`) that produces pixel baselines for overlays and multi-pane compositions.
- Added engine-level coverage for multi-pane metadata, band series, heatmap buckets, and backfill/dirty-region behaviour (`tests/VelloSharp.Charting.Tests/Engine/ChartEngineSeriesTests.cs`).

## Phase 3.5 – High-Performance Animation System (2–3 weeks)
- **Shared composition animation runtime**
  - [x] Partner with the TDG initiative to extend `ffi/composition` with a reusable animation timeline (easing curves, spring/damping, grouped timelines) that drives Vello scene updates without reallocating command buffers.
  - [x] Surface managed bindings in `src/VelloSharp.Composition` for animation builders, property tracks, and tick scheduling; ensure the APIs integrate with chart update loops without unnecessary allocations.
  - [x] Add Rust + .NET microbenchmarks proving ≤0.5 ms CPU overhead per frame for 10k animated properties and golden tests validating interpolation accuracy.

- **Chart engine adoption**
- [x] Replace bespoke easing logic for cursor trails, crosshair fades, zoom transitions, and indicator overlays with the shared animation runtime; capture before/after performance metrics.
- [x] Introduce motion presets for streaming data (fade/slide-in, rolling window shifts) with reduced-motion toggles and deterministic timelines for recording/playback scenarios.
  - [x] Introduce `ChartAnimationController` to drive series stroke-width emphasis and per-series overrides through the shared timeline runtime, wiring reduced-motion flags and scheduler ticks into chart updates.
  - [x] Expose animation descriptors through `ChartComposition`/`ChartEngineOptions` so host applications can configure durations, easing curves, and synchronize with TreeDataGrid micro-interactions.
    - `ChartEngineOptions.Animations` and `ChartComposition.UseAnimations(...)` ship `ChartAnimationProfile`/`ChartStreamingAnimationPreset` descriptors, wiring reduced-motion and deterministic timeline toggles into the shared controller.

### Phase 3.5 Progress Snapshot (Week 1)
- Timeline-driven series emphasis now runs through `ChartAnimationController`, pooling grouped tracks around the shared runtime and scheduling ticks alongside the render pipeline to avoid redundant command buffers (`src/VelloSharp.ChartEngine/ChartAnimationController.cs`).
- Cursor trails, crosshair fades, and annotation emphasis now ride the shared timeline via `ChartCursorUpdate`/`AnimateAnnotation`, projecting overlay snapshots through `ChartFrameMetadata` and validated with new `ChartAnimationControllerTests` coverage (`tests/VelloSharp.Charting.Tests/ChartAnimationControllerTests.cs`).
- Streaming motion presets now drive fade, slide-in, and rolling-window emphasis through `ChartAnimationController.AnimateStreaming`, exposing per-series values via `ChartFrameMetadata.StreamingOverlays` and covered by new streaming animation tests.
- `ChartAnimationProfile` descriptors propagate from engine options and the composition builder, letting hosts customise easing, durations, and reduced-motion toggles in sync with TreeDataGrid micro-interactions (`src/VelloSharp.ChartEngine/ChartEngineOptions.cs`, `src/VelloSharp.ChartEngine/ChartComposition.cs`).
- Cross-platform coverage remains green via `TimelineSystemInteropTests`, and the shared microbenchmarks logged in `docs/metrics/performance-baselines.md` confirm CPU cost stays below the 0.5 ms budget for 10k tracks.
- Avalonia samples now consume the animation profile to highlight streaming emphasis, illustrating the path for cursor and crosshair migrations to the shared runtime (`samples/VelloSharp.Charting.AvaloniaSample`). 

- **Diagnostics and tooling**
  - [ ] Emit animation telemetry (active timelines, dropped frames, CPU/GPU cost) via `VelloSharp.ChartDiagnostics` and extend the Avalonia sample with an animation inspector overlay.
  - [ ] Update motion guidelines (`docs/diagrams/chart-engine-integration.svg`) to illustrate shared animation flows across chart and TDG surfaces.

#### Ticket Backlog & Sequencing
1. `TDG-ANIM-001` (Owner: Composition WG) – Deliver the shared timeline runtime in `ffi/composition`. _Dependency for all downstream animation work._
2. `TDG-ANIM-002` (Owner: Managed Bindings) – Project managed animation builders/FFI bindings into `VelloSharp.Composition`, including diagnostics hooks. _Depends on TDG-ANIM-001._
3. `TDG-ANIM-003` (Owner: TreeDataGrid) – Apply the shared runtime across TDG column/row animations to validate virtualization behaviour. _Depends on TDG-ANIM-002; provides baseline for dashboard parity._
4. `CHT-ANIM-001` (Owner: Charts Engine) – Adopt the shared runtime for chart cursor, annotation, and streaming transitions; feed results into the motion guideline addendum. _Depends on TDG-ANIM-002; reviews outcomes with TDG team._

## Phase 4 – Advanced Financial and Enterprise Chart Types (6–8 weeks)
**Objectives**
- [ ] Implement high-value trading and analytics charts: candlestick/ohlc, renko, kagi, point-and-figure, market profile, depth of market (DOM) ladders, treemaps, bubble charts, waterfall, and KPI scorecards.
- [ ] Add multi-series correlation visuals (scatter matrix, pair trading overlays) and volatility indicators (Bollinger bands, MACD).
- [ ] Provide analytics pipeline for pre/post-processing indicators with GPU-friendly caching.
- [ ] Support large dataset virtualization (millions of points) with level-of-detail (LOD) strategies and data windowing.

**Deliverables**
- [ ] Suite of advanced chart controls with configuration schemas and interactive samples.
- [ ] Indicator library with plug-in registration, dependency graph resolution, and caching strategy.
- [ ] Performance benchmarks on representative workloads (tick data, economic time-series, corporate dashboards).
- [ ] UX guidelines for common trading layouts and executive dashboards.

## Phase 5 – Interaction, Tooling, and User Experience (4–6 weeks)
**Objectives**
- [ ] Implement interaction layer: panning, zooming (time and value axes), brushing, selection, crosshair, hover tooltips, and keyboard shortcuts.
- [ ] Add input abstraction bridging mouse, touch, pen, and accessibility APIs across supported frameworks.
- [ ] Integrate recording/playback for session review and deterministic testing.
- [ ] Build export pipeline: image snapshots, vector export (SVG/PDF), data extraction APIs.

**Deliverables**
- [ ] Interaction controller with configurable gestures and performance-tuned hit testing.
- [ ] Accessibility bridge for screen readers, high-contrast mode, and reduced motion.
- [ ] Export services (sync and async) with quality presets.
- [ ] Demo applications showcasing multi-touch, device synchronization, and enterprise report exports.

## Phase 6 – Platform Integrations and Host Framework Adapters (parallel tracks, 6–8 weeks total)
**Objectives**
- [ ] Produce thin adapters for WPF, WinUI, MAUI, WinForms, Avalonia, Uno, and GTK (if needed) that host the Vello surface, translate input, and surface dependency properties/bindings.
- [ ] Ensure consistent layout and DPI handling across platforms, including software fallback for environments lacking GPU features.
- [ ] Provide integration samples for key frameworks (desktop, web assembly via Uno/Avalonia, mobile via MAUI).
- [ ] Align with packaging and distribution strategy (NuGet multi-targeting, native asset bundling, AOT considerations).

**Deliverables**
- [ ] Framework-specific packages (`VelloSharp.Charts.Wpf`, `.Avalonia`, `.Maui`, etc.) wrapping the shared engine.
- [ ] Cross-platform sample gallery with consistent feature set and evaluation harness.
- [ ] Integration guides covering lifecycle management, threading considerations, and dependency injection setup.
- [ ] Build tooling updates for CI/CD pipelines, including artifact signing and symbol publishing.

## Phase 7 – Performance Hardening, QA, and Release (4 weeks)
**Objectives**
- [ ] Conduct deep performance profiling (GPU captures, memory leak detection, cache behavior) under high load and multi-monitor setups.
- [ ] Implement automated regression suites: unit tests, property-based tests for scaling algorithms, golden image comparisons, stress scenarios.
- [ ] Run usability studies with target user personas (traders, analysts) and iterate on ergonomics.
- [ ] Finalize documentation, API references, migration guides, and support playbooks.

**Deliverables**
- [ ] Performance report with optimization backlog and prioritized fixes.
- [ ] Comprehensive test suite integrated into CI with performance gates.
- [ ] Documentation site updates: API reference, tutorials, integration cookbooks, troubleshooting.
- [ ] Release candidate builds with signed NuGet packages and versioning scheme.

## Cross-Cutting Concerns and Enablers
- [ ] **Telemetry and Observability**: Unified logging, metrics, and optional telemetry export for host applications; include privacy-respecting toggles.
- [ ] **Security and Compliance**: Review for sandboxed deployment, code signing, and dependency licensing for enterprise adoption.
- [ ] **Localization and Internationalization**: Ensure text rendering, number/date formatting, and RTL layouts are supported from the core.
- [ ] **Plugin Marketplace Readiness**: Define extension manifest format, sandboxing rules, and validation pipeline for third-party add-ons.
- [ ] **Community and Support**: Establish samples, templates, and starter kits; plan for GitHub Discussions, issue triage, and commercial support offerings.
- [ ] **Animation System** (`TDG-ANIM-001`..`003`, `CHT-ANIM-001`): Kick-off held with Composition, TDG, and Charts owners (animation guild sync, 2025-10-08) to coordinate shared timelines, honour reduced-motion preferences, and gate regressions with animation-focused perf tests.
- [ ] **Shared Control Composition**: Coordinate with TreeDataGrid and forthcoming editor controls to keep composition/text stacks aligned, share benchmarks, and publish joint regression suites.

## Milestones and Governance
- [ ] **M0**: Architecture sign-off, metrics defined.
- [ ] **M1**: Core engine and data pipeline complete with baseline benchmarks.
- [ ] **M2**: Layout/styling system feature complete and integrated demos.
- [ ] **M3**: Advanced chart suite validated with trading/economic scenarios.
- [ ] **M4**: Cross-platform adapters shipped with sample gallery.
- [ ] **M5**: Performance hardening finished, release candidate published.
- Governance: fortnightly architecture reviews, performance budget audits, and cross-team integration syncs; maintain ADR log and public roadmap.
