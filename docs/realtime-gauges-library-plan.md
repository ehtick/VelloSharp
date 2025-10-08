# VelloSharp Real-Time Gauges Library Implementation Plan

## Vision and Guiding Principles
- Deliver an industrial-grade gauges and instrumentation library that drives SCADA/DCS visualizations with deterministic, sub-8 ms updates using the Vello/VelloSharp rendering stack.
- Reuse the shared composition, layout, input, and templated control infrastructure established for charting and TreeDataGrid so gauges share a single visual language and performance profile.
- Provide pre-built gauge primitives (analog meters, bargraphs, process indicators, annunciators) and composable dashboards that integrate seamlessly with charts, data grids, and future editors.
- Emphasize resilience under bursty telemetry, deterministic alarm visualization, accessibility, and theming parity across desktop, web, and embedded hosts.

## Implementation Conventions
- [ ] Rust production code lives under `ffi/gauges-*` crates aligned with the existing composition/animation modules; managed bindings reside under `src/VelloSharp.Gauges.*`.
- [ ] Samples and diagnostics ship from `samples/VelloSharp.Gauges.*`, demonstrating real telemetry integration (MQTT, OPC UA simulators).
- [ ] Shared assets (layouts, templated controls, text primitives, animation profiles) remain under existing shared crates to avoid duplication; any new primitives must update `docs/specs/shared-composition-contract.md`.
- [ ] Each phase produces shippable artifacts with accompanying tests and docs before marking deliverables complete.

## Technical Architecture Overview
- **Shared Composition Stack**: Extends the common layout, templated controls, geometry/shape atoms (`Border`, `Decorator`, `Path`, `Rectangle`, `Ellipse`, `GeometryPresenter`), text primitives, and `InputControl` adapters used by charting and TDG.
- **Gauge Rendering Core**: Rust scene builders emitting Vello command buffers for circular/linear gauges, annunciators, numeric indicators, and composite panels, reusing material registries and animation timelines.
- **Data & State Model**: Lock-free bindings for telemetry channels, alarm state machines, and setpoint commands with snapshot diffing compatible with chart ingestion pipelines.
- **Interaction Layer**: Shared input adapters enabling tactile adjustments (knobs/sliders), acknowledgements, and keyboard navigation, aligned with charting/TDG interaction contracts.
- **Extensibility**: Plugin surfaces for custom gauge skins, alarm widgets, and integration with OPC UA/Historian connectors.

## Phase 0 – Discovery, Alignment, and Requirements (2 weeks)
**Objectives**
- [ ] Catalogue reusable assets from charting and TDG (layouts, templated controls, geometry shapes, animation profiles, input adapters) and gap-analyze for gauge needs.
- [ ] Capture industrial gauge requirements (accuracy, anti-aliasing, alarm lamination, color standards) in `docs/specs/gauges-requirements.md`.
- [ ] Define telemetry and command contracts aligning with shared data pipelines (`docs/specs/telemetry-contract.md` addendum).
- [ ] Prototype two gauge scenarios (analog dial + vertical bargraph) under `ffi/experimental/gauges_prototypes` validating <8 ms frame budgets.

**Deliverables**
- [ ] Requirements and compliance checklist (`docs/specs/gauges-requirements.md`).
- [ ] Prototype gauges with performance captures located under `docs/metrics/gauges-baselines.md`.
- [ ] Updated shared composition contract referencing gauge-specific geometry/templating needs.

## Phase 1 – Shared Foundations and Infrastructure (3–4 weeks)
**Objectives**
- [ ] Extend shared templated control lifecycle with gauge-specific adorners (tick marks, indicators) and expose reusable `GaugeControlBase`, `GaugePanel`, and `GaugeDecorator`.
- [ ] Implement telemetry binding layer (`ffi/gauges-core`) reusing charting ring buffers and TDG scheduler hooks for throttling.
- [ ] Integrate alarm state machine primitives and expose shared diagnostics overlays (`FrameStats`, `AlarmStats`).
- [ ] Author golden tests ensuring layout/text/animation parity across gauge, chart, and TDG controls.

**Deliverables**
- [ ] `ffi/gauges-core` crate with telemetry adapters, alarm state machines, and scene graph hooks.
- [ ] Managed `VelloSharp.Gauges` base control set (`GaugeControlBase`, `AnalogGauge`, `BarGauge`, `GaugePanel`, `GaugeAnnotation`).
- [ ] Shared templated control extensions documented in `docs/specs/shared-composition-contract.md`.
- [ ] Regression suite covering layout/animation parity and alarm timeline budgets.

## Phase 2 – Gauge Primitive Suite (4–5 weeks)
**Objectives**
- [ ] Implement analog gauge family (circular, semi-circular, multi-scale) with dynamic tick generation and animation easing.
- [ ] Deliver linear gauges (horizontal/vertical bargraphs, thermometers), numeric indicators, and annunciator tiles.
- [ ] Add composite shapes for piping/process diagrams leveraging shared geometry primitives.
- [ ] Provide skinning/theming via shared material registries and templated control themes.

**Deliverables**
- [ ] Analog gauge primitives with customizable ticks, needles, ranges, and alarm zones.
- [ ] Linear gauge and numeric indicator controls with smooth transitions and reduced-motion options.
- [ ] Annunciator grid control supporting alarm strobing, acknowledgement, and shared input callbacks.
- [ ] Sample dashboards demonstrating mixed analog/linear gauges, charts, and TDG tables.

## Phase 3 – Composition, Layouts, and Dashboard Authoring (4 weeks)
**Objectives**
- [ ] Build dashboard composition API combining gauges, charts, and TDG views using shared stack/wrap/grid/dock panels.
- [ ] Implement template-driven layouts mirroring `Avalonia.Controls` naming (`GaugePanel`, `GaugeGrid`, `GaugeDecorator`).
- [ ] Enable drag/drop and adaptive layout behaviours (responsive rescaling, tiled view).
- [ ] Extend shared `InputControl` base to handle knob/slider gestures and alarm acknowledgements.

**Deliverables**
- [ ] Dashboard composition builder (`src/VelloSharp.Gauges.Dashboard`) with panel primitives.
- [ ] Responsive layout samples with chart, gauge, TDG mashups.
- [ ] Interaction tests covering drag/drop, acknowledgements, keyboard navigation.
- [ ] Documentation (`docs/guides/gauge-dashboard-authoring.md`) with design guidelines.

## Phase 4 – Data Connectivity, Historian, and Scripting (3 weeks)
**Objectives**
- [ ] Provide connectors for OPC UA, MQTT, and REST telemetry aligning with shared data providers.
- [ ] Implement historian playback integration sharing charting recording infrastructure.
- [ ] Expose scripting/hooks (Lua/C#) for alarm logic and gauge behaviour overrides.

**Deliverables**
- [ ] Connector adapters with throttling/backpressure support.
- [ ] Historian playback sample integrating charts and gauges.
- [ ] Scripting API documentation and sandboxed execution tests.

## Phase 5 – Industrial Hardening and Certification (4 weeks)
**Objectives**
- [ ] Validate rendering under harsh environments (temperature, offline buffers) using fault-injection harnesses.
- [ ] Conduct accessibility and safety audits (color blindness palettes, high-contrast, keyboard-only operation).
- [ ] Prepare compliance documentation for industrial deployments (ISA, IEC color tables).

**Deliverables**
- [ ] Fault-injection reports and mitigation backlog.
- [ ] Accessibility test suite results and updated themes.
- [ ] Compliance packet including design rationales, certification checklists, and operational guidelines.

## Cross-Cutting Concerns
- [ ] **Shared Control Toolkit**: Keep templated controls, text primitives, shapes, and input adapters in sync with charting/TDG deliverables.
- [ ] **Performance Budgeting**: Maintain <8 ms frame budgets; update `docs/metrics/performance-baselines.md` with gauge workloads.
- [ ] **Telemetry & Diagnostics**: Align logging with charting/TDG; extend dashboards with gauge-specific metrics.
- [ ] **Security & Sandbox**: Harden connectors and scripting against untrusted input; document best practices.
- [ ] **Documentation & Samples**: Provide cookbook scenarios (process loop, tank farm, electrical panel) with source and walkthroughs.

## Milestones
- [ ] **G0**: Requirements sign-off and prototypes validated.
- [ ] **G1**: Shared gauge foundations complete with base controls.
- [ ] **G2**: Gauge primitive suite shipped with samples.
- [ ] **G3**: Dashboard composition tooling ready, connectors integrated.
- [ ] **G4**: Industrial hardening complete, compliance packet ready.

