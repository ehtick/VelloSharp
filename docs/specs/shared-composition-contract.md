# Shared Composition Contract

## Purpose
- Unify the composition primitives (layout, text, scene diff, diagnostics) consumed by the real-time charting engine and the forthcoming TreeDataGrid (TDG).
- Guarantee deterministic frame budgeting, typography parity, and dirty-region management across controls.
- Provide a documented API surface so new consumers (editors, dashboards) can adopt the shared stack without re-discovery.

## In-Scope Consumers
- `vello_chart_engine` (charting) and `src/VelloSharp.ChartEngine` (managed bindings).
- TreeDataGrid native core under `ffi/composition` + TDG host adapters (`src/VelloSharp.TreeDataGrid.*` once available).
- `VelloSharp.Gauges` crates and managed bindings providing industrial instrumentation surfaces.
- Integrated SCADA/DCS visualization hosts under `src/VelloSharp.Scada.*` composed from charts, gauges, and TDG.
- Unified visual editor runtime (`ffi/editor-*`, `src/VelloSharp.Editor.*`) authoring dashboards and controls against the shared scene graph.
- Future composition-based controls (overlays, editors, partner extensions) that rely on Vello scene generation through VelloSharp.

## Shared Primitives
- **Layout & Constraints**
  - Rust types: `ScalarConstraint`, `LayoutConstraints`, `LayoutSize`, `LinearLayoutItem`, `LinearLayoutSlot`.
  - Public FFI: `vello_composition_compute_plot_area`, `vello_composition_solve_linear_layout`.
  - Responsibilities: deterministic constraint solving, safe handling of NaN/∞, spacing + margin semantics, plot-area heuristics for axis chrome.
- **Typography & Label Metrics**
  - Rust types: `TextShaper`, `LabelLayout`; managed `CompositionInterop.MeasureLabel`, `LabelMetrics`.
  - Backed by shared Roboto assets and shaping caches; consumers must avoid long-lived locks by copying results instead of retaining internal references.
- **Scene Graph Cache**
  - Rust types: `SceneGraphCache`, `SceneNodeId`, `DirtyRegion`.
  - FFI: `vello_composition_scene_cache_create(_node)`, `..._mark_dirty`, `..._mark_dirty_bounds`, `..._take_dirty`, `..._clear`, `..._dispose_node`.
  - Managed surface: `SceneCache` SafeHandle with `MarkDirty` and `MarkDirtyBounds` helpers.
  - Guarantees: O(1) node reuse via free-list, cascading dirty accumulation, deterministic reset semantics.
- **Render Materials & Layers**
  - Rust registries: `register_shader`, `register_material`, `resolve_material_color`, `resolve_material_peniko_color`.
  - FFI: `vello_composition_shader_register/unregister`, `vello_composition_material_register/unregister`, `vello_composition_material_resolve_color`.
  - Managed surface: `CompositionShaderRegistry`, `CompositionMaterialRegistry`, and `ScenePartitioner`/`RenderLayer` helpers that provide stable scene-node allocation for overlays, chrome, and inter-control composition.
  - Guarantees: shared shader/material identity across controls, centralised opacity handling, and deterministic layering hooks that TreeDataGrid/editor surfaces can compose with chart scenes.
- **Control & Template Infrastructure**
  - Base classes: `TemplatedControl`, `Panel`, `UserControl`, `InputControl` surfaced through `src/VelloSharp.Composition.Controls` with lifecycle hooks (`OnApplyTemplate`, template inflation, recycling) backed by FFI-safe descriptors.
  - Standard controls: `Button`, `CheckBox`, `RadioButton`, `DropDown`, `TabControl`, `ItemsControl`, `ListBox`, `TreeView`, each projecting composition scenes via shared layout/text virtualization and input routing.
  - Responsibilities: ensure template parsing/binding parity across host frameworks, guarantee virtualization-friendly visual trees, and keep interaction contracts (focus, command routing, keyboard/pointer gestures) reusable between chart dashboards and TDG-derived controls.
- **Geometry & Shape Primitives**
  - Controls: `Border`, `Decorator`, `Shape`, `Rectangle`, `Ellipse`, `Path`, `GeometryPresenter` (and related geometry descriptors) mirroring `Avalonia.Controls` naming, property semantics, and styling hooks.
  - Backing types: shared geometry buffers (path figures, stroke/fill descriptors) exposed through FFI-safe spans to avoid per-frame allocations; composition scene emitters reuse material registries for brush/pen application.
  - Guarantees: deterministic layout participation, hit-testing hooks compatible with shared `InputControl`, and parity test coverage across charting and TDG hosts to ensure dashboard scenarios reuse the same shape atoms.
- **Input Pipeline**
  - Managed primitives: `Controls.InputControl`, `Input.CompositionPointerEventArgs`, `Input.CompositionKeyEventArgs`, `Input.CompositionTextInputEventArgs`, `Input.PointerEventType`, `Input.InputModifiers`.
  - Host adapters: `Input.ICompositionInputSource`/`Input.ICompositionInputSink` contracts with Avalonia bridge (`VelloSharp.Integration.Avalonia.AvaloniaCompositionInputSource`) normalising pointer, keyboard, focus, and capture semantics for charts, TDG, gauges, and editor surfaces.
  - Responsibilities: ensure deterministic pointer capture bookkeeping (`InputControl.CapturedPointers`), consistent modifier translation, and lifecycle-safe attach/detach when templates mount/unmount.
  - Diagnostics: shared logging hooks should record pointer capture transitions and focus churn to aid perf regressions and stuck pointer detection.
- **Accessibility Metadata**
  - Managed primitives: `Accessibility.AccessibilityProperties`, `Accessibility.AccessibilityRole`, `Accessibility.AccessibilityAction`, `Accessibility.AccessibilityLiveSetting` exposed via `InputControl.Accessibility` and related events (`AccessibilityChanged`, `AccessibilityAnnouncementRequested`, `AccessibilityActionInvoked`).
  - Platform bridges (`VelloSharp.Integration.Avalonia.AvaloniaCompositionInputSource`, `VelloSharp.Integration.Wpf.WpfCompositionInputSource`, `ChartRuntime.Windows.WinUICompositionInputSource`) project accessibility name/help text/automation id and wire automation peers (e.g., `ChartViewAutomationPeer`) so charts, TDG, gauges, and editor hosts remain screen-reader friendly.
  - Guidance: consumers should update accessibility roles when templated controls change states (button/toggle/tab) and raise announcements via `InputControl.AnnounceAccessibility` for critical telemetry/alarm messages.
- **Editor Integration Hooks**
  - Selection, snapping, and mutation APIs exposed via `ffi/editor-core` must operate on shared scene graph identifiers and respect dirty-region semantics defined here.
  - Serialization descriptors for dashboards (`GaugePanel`, `ChartComposition`, `TreeDataGridHost`, etc.) map to shared templated controls; any schema evolution must include compatibility notes in this contract.
  - Editor-specific adorners (selection rectangles, alignment guides) rely on the same geometry/material registries and must avoid introducing new rendering primitives without contract updates.
- **Animation Timelines**
  - Rust types: `TimelineSystem`, `TimelineGroupConfig`, `EasingTrackDescriptor`, `SpringTrackDescriptor`, `TimelineSample`.
  - FFI: `vello_composition_timeline_system_create`, `..._group_create`, `..._add_easing_track`, `..._add_spring_track`, `..._tick`.
  - Responsibilities: drive low-allocation timeline playback (easing curves, spring dynamics, grouped timelines) that can mark dirty regions and update scenes without reallocating command buffers.
- **Micro-Interaction Surfaces**
  - `TreeRowInteractionAnimator` (`src/VelloSharp.TreeDataGrid/Composition/TreeRowInteractionAnimator.cs`) exposes `TreeVirtualizationPlan.RowAnimations` and `TreeVirtualizationScheduler.NotifyRowExpansion`, enabling expand/collapse height easing, selection glow, and caret rotation without perturbing virtualization buffers.
  - `TreeRowAnimationProfile`/`TreeAnimationTimeline` allow hosts to configure durations, easing, and reduced-motion behaviour via `TreeVirtualizationScheduler.ConfigureRowAnimations`, keeping TDG motion aligned with chart surfaces.
  - `ChartAnimationController` (`src/VelloSharp.ChartEngine/ChartAnimationController.cs`) now orchestrates cursor trails and annotation emphasis through the shared runtime, projecting overlay snapshots via `ChartFrameMetadata.SetCursorOverlay/SetAnnotationOverlays`.
  - Streaming motion presets feed `ChartFrameMetadata.StreamingOverlays`, exposing per-series fade, slide, and rolling-window emphasis derived from the shared timeline to keep dashboard motion consistent.
- **Telemetry & Command Services**
  - Managed services: `Telemetry.TelemetryHub`, `Telemetry.TelemetrySample`, `Telemetry.TelemetryQuality`, `Telemetry.ITelemetryObserver`, and command interfaces (`Telemetry.CommandBroker`, `Telemetry.CommandRequest`, `Telemetry.CommandResult`, `Telemetry.CommandStatus`, `Telemetry.ICommandHandler`).
  - Responsibilities: fan-out live telemetry to multiple consumers with cancellation-aware async pathways; enforce single-writer semantics per command target with deterministic deregistration when handlers dispose.
  - Convenience connectors: `Telemetry.GaugeTelemetryConnector` and `Telemetry.ScadaTelemetryRouter` bridge hub/broker pipelines into gauge dashboards and SCADA runtimes, persisting last-known samples and coordinating command acknowledgement flows.
  - Documentation: shared schema and behavioural guarantees captured in `docs/specs/telemetry-contract.md`; consumers must integrate quality flags and command acknowledgements per this contract.
- **Diagnostics Hooks**
  - Frame stats emitted via existing chart diagnostics; TDG must publish compatible payloads (`FrameStats`, `InputLatencyStats`) for joint dashboards.
  - Shared telemetry pipeline expected under `docs/metrics/performance-baselines.md`.

## FFI Surface (Rust ➜ C ABI)
- All entry points are `extern "C"` with defensive checks around null pointers and span lengths.
- Any new export must be accompanied by:
  1. Corresponding managed `LibraryImport` signature (`NativeMethods`).
  2. Unit/integration coverage (Rust tests under `ffi/composition`, managed tests under `tests/VelloSharp.Charting.Tests/Layout` or TDG equivalents).
  3. Documentation update in this contract.
- Current exports:
  - `vello_composition_compute_plot_area(double width, double height, CompositionPlotArea* out_area)`
  - `vello_composition_measure_label(const uint8_t* text, size_t len, float font_size, CompositionLabelMetrics* out_metrics)`
  - `vello_composition_solve_linear_layout(const CompositionLinearLayoutItem* items, size_t item_count, double available, double spacing, CompositionLinearLayoutSlot* out_slots, size_t out_len)`
  - `vello_composition_scene_cache_create/destroy`
  - `vello_composition_scene_cache_create_node/dispose_node`
  - `vello_composition_scene_cache_mark_dirty(double x, double y)` and `..._mark_dirty_bounds(double min_x, double max_x, double min_y, double max_y)`
  - `vello_composition_scene_cache_take_dirty(SceneNodeId node, CompositionDirtyRegion* out_region)`
  - `vello_composition_scene_cache_clear(SceneNodeId node)`
  - `vello_composition_shader_register/unregister`, `vello_composition_material_register/unregister`, `vello_composition_material_resolve_color`
  - `vello_composition_timeline_system_create/destroy`
  - `vello_composition_timeline_group_create/destroy`, `..._group_play`, `..._group_pause`, `..._group_set_speed`
  - `vello_composition_timeline_add_easing_track`, `vello_composition_timeline_add_spring_track`, `vello_composition_timeline_track_reset`, `..._track_remove`, `..._track_set_spring_target`
  - `vello_composition_timeline_tick(double delta_seconds, SceneGraphCache* cache, CompositionTimelineSample* out_samples, size_t out_len)`

## Managed Surface (C#)
- `VelloSharp.Composition.CompositionInterop`
  - `PlotArea ComputePlotArea(double width, double height)`
  - `LabelMetrics MeasureLabel(string/ReadOnlySpan<char> text, float fontSize = 14f)`
  - `int SolveLinearLayout(ReadOnlySpan<LinearLayoutChild>, double available, double spacing, Span<LinearLayoutResult>)`
- `VelloSharp.Composition.TimelineSystem`
  - SafeHandle wrapper for native timeline state.
  - `uint CreateGroup(TimelineGroupConfig config)`, `PlayGroup`, `PauseGroup`, `SetGroupSpeed`
  - `uint AddEasingTrack(...)`, `uint AddSpringTrack(...)`, `RemoveTrack(uint trackId)`, `ResetTrack(uint trackId)`, `SetSpringTarget(uint trackId, float target)`
  - `int Tick(TimeSpan delta, SceneCache? cache, Span<TimelineSample> samples)` returns produced sample count while optionally marking dirty regions via the provided `SceneCache`.
- `VelloSharp.Composition.SceneCache`
  - SafeHandle wrapper for native cache lifecycle.
  - `CreateNode(uint? parentId = null)`, `DisposeNode(uint nodeId)`
  - `MarkDirty(uint nodeId, double x, double y)` and `MarkDirtyBounds(uint nodeId, double minX, double maxX, double minY, double maxY)`
  - `bool TakeDirty(uint nodeId, out DirtyRegion region)`, `Clear(uint nodeId)`
- Consumers must respect:
  - Stackalloc thresholds (`LinearLayoutStackThreshold = 8`) to minimise heap allocations in hot paths.
  - Explicit disposal of `SceneCache` instances to avoid native leaks.

## Contractual Responsibilities
- **Rendering Architecture WG**
  - Owns API review and versioning (semantic version tags on `ffi/composition` once crates are published).
  - Maintains regression test coverage (Rust + .NET) for shared primitives.
  - Curates roadmap items in `docs/realtime-charts-library-plan.md` and `docs/realtime-tree-datagrid-plan.md`.
- **Chart Engine Team**
  - Uses shared APIs instead of private copies.
  - Provides reference implementations/tests demonstrating layout, label metrics, and dirty region integration.
  - Exposes sample telemetry and benchmark harness updates when APIs evolve.
- **TreeDataGrid Team**
  - Adopts shared layout/text/scene cache flows for virtualization and rendering.
  - Supplies TDG-specific regression benchmarks that report back through shared metrics dashboards.
  - Raises API change requests through ADR amendments when new capabilities are required (e.g., column layout modes).

## Performance & Testing Expectations
- Frame budgets: shared primitives must keep combined CPU work for layout/text within 3 ms of the 8 ms target (99th percentile) when used by both chart and TDG scenarios.
- Minimum test coverage:
  - Rust unit tests for each exported function (already covering linear layout, scene cache).
  - Managed smoke tests (`CompositionInteropTests`) verifying interop wiring.
  - Scenario benchmarks stored in `docs/metrics/performance-baselines.md` for both chart and TDG workloads.
  - Timeline interpolation golden tests (`ffi/composition/src/animation.rs`, `TimelineSystemInteropTests`) ensure easing/spring outputs remain analytically correct.
  - Shared animation microbenchmarks (`chart_benchmarks -- timeline`, `VelloSharp.Composition.Benchmarks`) exercise 10k-property runs and record CPU overhead in the metrics baseline.
- Any addition that impacts deterministic output (e.g., layout heuristics) requires golden image or structured snapshot tests before release.

## Versioning & Release Cadence
- `ffi/composition` crate and `VelloSharp.Composition` assembly advance together; breaking changes require coordinated releases and plan updates.
- Consumers must pin to compatible versions via workspace dependencies; cross-repo consumers should receive NuGet/native package releases with matching semantic versions.
- Deprecations follow: add new API → migrate consumers → mark old API `[Obsolete]`/`#[deprecated]` → remove in next minor (with ADR addendum).

## Resource & Telemetry Sharing
- GPU/CPU resource pooling: shared scene cache deliberately avoids device references; higher layers must coordinate actual renderer caches.
- Telemetry fields (`FrameStats`, `InputLatencyStats`) must remain schema-compatible; new fields are additive and gated through shared feature flags.
- Logging/tracing should use shared spans (`composition.layout`, `composition.text`, `composition.scene_cache`) to ease diagnostics.

## Adoption Roadmap & Open Tasks
- Charts: continue migrating pane/grid layout code to consume `LinearLayoutItem`/`LayoutConstraints` directly.
- TDG: integrate shared scene cache into virtualization scheduler; author golden/baseline tests (tracked in plan Phase 1).
- Editors/Other Controls: draft onboarding checklist once TDG prototype confirms the contract (future `docs/guides/composition-reuse.md` deliverable).
