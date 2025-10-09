# Gauges Asset Catalog and Gap Analysis

## Shared Assets Available for Gauges
- Layout primitives and panel infrastructure from `ffi/composition/src/panels.rs`, `ffi/composition/src/linear_layout.rs`, and managed mirrors in `src/VelloSharp.Composition/LayoutPrimitives.cs`, `src/VelloSharp.Composition/Controls/Panel.cs`.
- Templated control lifecycle (`src/VelloSharp.Composition/Controls/TemplatedControl.cs`) with reusable chrome primitives such as `Border`, `Decorator`, `GeometryPresenter`, `TextBlock`, and `VisualTreeVirtualizer`.
- Scene cache, material registry, and animation timelines (`ffi/composition/src/scene_cache.rs`, `ffi/composition/src/materials.rs`, `ffi/composition/src/animation.rs`) that already power charting and TreeDataGrid surfaces.
- Input adapters (`src/VelloSharp.Composition/Controls/InputControl.cs`, `bindings/VelloSharp.Integration/Avalonia/AvaloniaCompositionInputSource.cs`) with shared pointer, keyboard, and gesture routing.
- Telemetry and command connectors (`src/VelloSharp.Composition/Telemetry/TelemetryHub.cs`, `GaugeTelemetryConnector.cs`, `ScadaTelemetryRouter.cs`) providing deterministic signal fan out and acknowledgement handling.
- Diagnostics and performance scaffolding: timeline profilers in `ffi/composition/src/animation.rs`, chart benchmarks under `ffi/benchmarks`, and documentation patterns in `docs/metrics/performance-baselines.md`.

## Gauge Adoption Notes
- Analog dials can treat ticks, labels, and needles as templated parts: base geometry shapes derive from `GeometryPresenter` and `Path`, while label placement uses layout primitives with polar transforms emitted by prototype builders.
- Linear bargraphs reuse `Panel` layout (vertical stack) with animated `Rectangle` fills and existing material registry for alarm coloration.
- Alarm banners, annunciators, and command widgets can re-skin existing `Button`, `Toggle`, and `DropDown` controls without duplicating focus or accessibility logic.
- Telemetry ingestion aligns directly with the `TelemetryHub` contract; gauges can subscribe with `GaugeTelemetryConnector` and respect quality metadata for colour/alpha variations.

## Identified Gaps
- Polar layout helpers are not yet exposed; gauge prototypes will ship a thin adapter to convert scalar values into `Affine` transforms for tick and needle placement.
- No shared tickmark generator exists for circular scales; a reusable service must be added to the shared composition layer in Phase 1 to avoid duplicating math across gauges and charts.
- Colour standards for ISA/IEC alarm states are not codified in shared material registries; Phase 0 requirements document calls for harmonised palettes and contrast thresholds.
- Existing input pipeline lacks rotary encoder and detented knob semantics; prototypes emulate them with pointer drag math and will file follow up work for dedicated input handlers.
- Performance baselines do not yet include gauge workloads; new metrics will be published under `docs/metrics/gauges-baselines.md` and wired into CI in a follow up task.

## Next Actions
- Validate the polar layout helper and tick generator design during Phase 1 shared infrastructure work.
- Extend material palettes with ISA-5.5 and IEC 60073 mappings, ensuring shared access from charts, gauges, and TDG.
- Author integration tests covering telemetry quality to visual state mapping once managed gauge controls are introduced.
