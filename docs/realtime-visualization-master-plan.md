# VelloSharp Real-Time Visualization Master Plan

## Purpose
- Coordinate delivery across the TreeDataGrid, Charts, Gauges, Unified Visual Editor, and DCS/SCADA visualization initiatives.
- Ensure shared composition, templating, input, and telemetry stacks evolve coherently and stay consumable by all downstream surfaces.
- Provide a single roadmap view of cross-cutting milestones, dependencies, and gating deliverables.

## Portfolio Scope
- **TreeDataGrid (TDG)** – Virtualized hierarchical data experiences over the shared composition stack.
- **Charts** – Real-time financial/industrial charting engine with cross-platform hosts.
- **Gauges** – Industrial instrumentation controls and dashboards leveraging shared primitives.
- **Unified Visual Editor** – No-code dashboard authoring environment for composing charts/gauges/TDG.
- **DCS/SCADA Visualization** – Integrated operational runtime combining all surfaces with telemetry, alarms, and governance.

## Current Shared Foundations
- [x] Extracted layout/text/scene-diff primitives into `ffi/composition` with managed bindings (`src/VelloSharp.Composition`).
- [x] Delivered stack/wrap/grid/dock solvers and virtualization planning (`ffi/composition/src/panels.rs`, `ffi/composition/src/virtualization.rs`, `src/VelloSharp.Composition/LayoutPrimitives.cs`).
- [x] Implemented templated/text/shape control set (`src/VelloSharp.Composition/Controls/*`) with lifecycle and virtualization capture tests.
- [x] Ship shared `InputControl` base and host adapters unifying pointer/keyboard pipelines (tracked by TDG Phase 4 and Charts Phase 5).
- [x] Shipped platform adapters for WPF/WinUI (input + automation) so charts reuse accessibility metadata alongside the existing Avalonia bridge.
- [x] Formalize shared telemetry/command services (`docs/specs/shared-composition-contract.md` addenda pending gauges + SCADA Phase 1/2).

## Integrated Roadmap

### Phase A – Foundation Alignment (Weeks 0–4)
- TDG Phase 1, Charts Phase 3 shared composition tasks **complete**; publish reuse guidelines (`docs/guides/composition-reuse.md` ✅).
- Gauges Phase 0: adopt shared layout/control stack; capture requirements (`docs/specs/gauges-requirements.md`).
- SCADA Phase 0: inventory reusable components, finalize compliance requirements (`docs/specs/scada-requirements.md`).
- Unified Editor Phase 0: confirm palette prototype uses composition primitives.

### Phase B – Shared Control & Telemetry Infrastructure (Weeks 5–10)
- TDG Phase 4 + Charts Phase 5: shared `InputControl` base/`ICompositionInputSource` adapters now span Avalonia + WPF/WinUI; next up are accessibility gestures and automation regression suites across hosts.
- Gauges Phase 1: extend templated lifecycle with gauge adorners; update `docs/specs/shared-composition-contract.md`.
- SCADA Phase 1: validate unified control behaviour across modules; align theming tokens.
- Telemetry connectors consolidated under `TelemetryHub`/`CommandBroker` services for reuse by Charts Phase 4, Gauges Phase 4, SCADA Phase 2, Editor Phase 3.

### Phase C – Experience Integration (Weeks 11–18)
- Charts Phase 3 follow-up: migrate legends/tooltips to composition controls and prove reuse in samples.
- TDG Phase 2/3: finalize column/row virtualization scenarios shared with SCADA dashboards.
- Gauges Phase 2/3: deliver gauge primitives and dashboard composition API using shared panels.
- Editor Phase 1/2: deliver editing engine + layout tooling backed by shared primitives; introduce serialization compatible with SCADA.
- SCADA Phase 3: authoring/runtime surfaces leveraging editor outputs and shared dashboards; ensure cross-component linking.

### Phase D – Operationalization & Governance (Weeks 19–26)
- SCADA Phase 4/5: alarm management, scripting, automation layers consuming shared telemetry + controls.
- Editor Phase 3/4: telemetry preview, historian playback, collaboration workflows tied to SCADA governance.
- Gauges Phase 4: connectors + historian modules align with SCADA command/security requirements.
- Charts Phase 6 + TDG Phase 5: host adapters and data providers validated with SCADA runtime.

### Phase E – Hardening & Release (Weeks 27–32)
- Unified performance + accessibility validation (charts Phase 7, TDG Phase 7, Gauges Phase 5, Editor Phase 6, SCADA Phase 6).
- Cross-product compliance packet (industrial standards, security) consolidated under SCADA governance.
- Publish unified release artifacts (NuGet, installers, containers) with signed native assets.

## Cross-Initiative Dependencies
- **Composition Contract** – Updates must be negotiated via `docs/specs/shared-composition-contract.md` before release; gauges, editor, and SCADA phases depend on current TDG/Charts work.
- **Input Pipeline** – TDG Phase 4 owns base implementation; charts, gauges, editor, and SCADA cannot mark interaction tasks complete until shared adapters ship.
- **Telemetry & Historian** – Charts telemetry foundation feeds gauges (Phase 4), editor (Phase 3), and SCADA (Phase 2); coordinate API design in `docs/specs/telemetry-contract.md`.
- **Dashboard Serialization** – Editor defines canonical descriptor consumed by SCADA runtime; gauges and charts must provide schema bindings to participate.
- **Alarm/Diagnostics** – SCADA alarm engine requires gauges annunciator primitives (Phase 2) and chart event overlays (Phase 4) to expose consistent UI.

## Milestone Summary
- **M0** (Complete): Shared composition layout/text/templated stack delivered (TDG Phase 1, Charts Phase 3).
- **M1** (Dependency): Shared `InputControl` + interaction pipeline ready (TDG Phase 4, Charts Phase 5).
- **M2**: Telemetry/historian services unified (`VelloSharp.Telemetry`), adopted by gauges Phase 4 and SCADA Phase 2.
- **M3**: Editor authoring pipeline outputs dashboards consumed by SCADA runtime (Editor Phase 2/3, SCADA Phase 3).
- **M4**: Alarm and scripting layers integrated (Gauges Phase 2/4, SCADA Phase 4/5, Charts overlays).
- **M5**: Cross-product hardening, compliance, and release (all initiatives Phase 5/6/7 equivalents).

## Reporting & Governance
- Maintain consolidated status in `STATUS.md` with links to plan sections above.
- Run fortnightly shared-composition sync covering contract updates, input pipeline progress, and telemetry APIs.
- Performance baselines tracked under `docs/metrics/performance-baselines.md` (`charts`, `tdg`, `gauges`, `scada`, `editor` scenarios).
- Use shared ADR log (`docs/adrs/`) for changes affecting multiple initiatives.


