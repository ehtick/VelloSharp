# SCADA Component Inventory and Gap Analysis

## Shared Components Reused
- **Layout & Composition**: `ffi/composition/src/panels.rs`, `ffi/composition/src/virtualization.rs`, `ffi/composition/src/linear_layout.rs`, with managed counterparts in `src/VelloSharp.Composition/LayoutPrimitives.cs` and `src/VelloSharp.Composition/Controls/Panel.cs` supplying stack, grid, dock, and virtualization behaviours for dashboard assemblies.
- **Templated Controls & Chrome**: `src/VelloSharp.Composition/Controls/*` (`TemplatedControl`, `Border`, `Decorator`, `Button`, `TabControl`, `VisualTreeVirtualizer`) provide shared lifecycle and styling hooks for SCADA shell, annunciators, and command tiles.
- **Rendering Primitives**: Scene cache, material registry, and animation timelines from `ffi/composition/src/scene_cache.rs`, `ffi/composition/src/materials.rs`, and `ffi/composition/src/animation.rs` ensure deterministic updates across chart, gauge, and TDG composites.
- **Telemetry & Command Fabric**: `src/VelloSharp.Composition/Telemetry/*` (`TelemetryHub`, `CommandBroker`, `GaugeTelemetryConnector`, `ScadaTelemetryRouter`) and contracts in `docs/specs/telemetry-contract.md` cover signal fan-out, command routing, and cached sample replay.
- **Charts**: Rendering core (`ffi/chart-engine`), data pipeline (`ffi/chart-data`), diagnostics (`ffi/chart-diagnostics`), and sample hosts (`samples/VelloSharp.Charting.AvaloniaSample`) supply trend panels and historian replay integration.
- **Gauges**: Phase 0 prototypes (`ffi/experimental/gauges_prototypes`) and future `ffi/gauges-*` crates deliver analog/linear instrumentation surfaces with shared alarm palettes and telemetry bindings.
- **Tree Data Grid**: Virtualization and scene builders in `ffi/tree-datagrid`, plus managed hosts under `src/VelloSharp.TreeDataGrid.*`, provide equipment lists, alarm tables, and hierarchy viewers.
- **Input Pipeline**: `InputControl` base (`src/VelloSharp.Composition/Controls/InputControl.cs`) and platform adapters (`bindings/VelloSharp.Integration/Avalonia/AvaloniaCompositionInputSource.cs`, WPF/WinUI bridges) ensure consistent pointer, keyboard, and focus semantics.
- **Diagnostics**: Shared metrics writers (`docs/metrics/performance-baselines.md`), timeline profilers, and tracing categories support SCADA performance gating.

## Dependencies and Integration Notes
- Charts, gauges, and TDG reuse common material keys; SCADA dashboards must enumerate required palette entries (normal, warning, alarm) and keep them synchronized with `docs/specs/shared-composition-contract.md`.
- `ScadaTelemetryRouter` sits atop `TelemetryHub` to provide last-value caching, stale detection, and historian bridging for dashboard widgets.
- Platform hosts (Avalonia/WPF/WinUI) can embed SCADA dashboards by composing existing samples; integration requires aligning dependency injection with chart/gauge service registrations.
- Historian playback can share charting recorders; events surfaced through `CommandBroker` need to be mirrored into alarm panels for acknowledgement loops.

## Identified Gaps / Follow-Up
- **Alarm Workflow Toolkit**: Need reusable annunciator control templates and command affordances that align with ISA-101 colour/interaction rules; pending Phase 1 tasks.
- **Historian Scrubbing**: Shared playback timeline exists for charts but not yet exposed to gauges/TDG; SCADA runtime must publish a unified scrubber service.
- **Floor Plan Layouts**: Composition stack lacks polar/absolute positioning helpers required for plant mimic diagrams; prototypes rely on manual Affine transforms.
- **Security Context**: Command broker requires operator identity propagation (badge/role) and audit logging; managed bindings must include these fields across hosts.
- **Cluster & Redundancy Hooks**: Telemetry connectors need health monitoring integration for hot standby and redundant server failover (to be defined in Phase 1 ADRs).

## Next Actions
- Draft shared alarm palette definitions and annunciator templates during Phase 1 shared foundations work.
- Extend historian APIs to broadcast playback state to gauges and TDG views.
- Capture ADR covering security/audit requirements for command handling.
- Prototype layout helpers for floor plans or import existing vector assets to drive absolute positioning scenarios.
