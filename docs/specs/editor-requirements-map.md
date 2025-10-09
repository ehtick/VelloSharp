# Editor Requirements Inventory and Alignment

## Source Plans Reviewed
- `docs/realtime-charts-library-plan.md`
- `docs/realtime-gauges-library-plan.md`
- `docs/realtime-tree-datagrid-plan.md`
- `docs/realtime-dcs-scada-visualization-plan.md`
- `docs/unified-visual-editor-plan.md`

## Shared Backlog Items Needed for the Editor
- **Composition & Layout**: Reuse stack/grid/dock panels (`ffi/composition/src/panels.rs`) and virtualization hooks required by charts, TDG, and SCADA dashboards. Editor must expose these as drag/drop templates with responsive sizing metadata.
- **Control Palette Parity**: Include shared controls (`Border`, `Decorator`, `Button`, `GaugeControlBase`, `AnalogGauge`, `BarGauge`, chart panels, TDG views) so authored dashboards match runtime primitives. Palette metadata must stay in sync with gauge and SCADA plans.
- **Telemetry & Command Tooling**: Surface `TelemetryHub`, `ScadaTelemetryRouter`, and `CommandBroker` bindings inside the editor for live preview. Editor-specific binding UI should respect gauge/SCADA command attribution requirements.
- **Alarm & Historian Integration**: Provide authoring affordances for alarm palettes (ISA/IEC colors) and historian scrubbers mandated in SCADA Phase 1. Editor must publish configuration schema that both gauges and SCADA runtime consume.
- **Security & Governance**: Align with SCADA role-based access, audit logging, and deployment packaging expectations; editor workflows should capture operator metadata and change approval steps.

## Cross-Plan Dependencies
- Charts supply trend visualizations and historian playback modules the editor must instantiate and parameterize.
- Gauges contribute analog/linear controls with alarm zones; editor needs configurable templates referencing gauge requirements.
- TreeDataGrid offers virtualization scenes with hierarchical data; editor must support binding column schemas and row templates.
- SCADA runtime dictates command workflows, redundancy signals, and packaging; editor export format must produce compatible bundles.

## Gaps Identified
- Need shared metadata registry for control templates (palette entries, icons, default bindings) referenced by editor and runtime packages.
- Historian scrubbing APIs from SCADA are not yet exposed as reusable services; editor requires design-time playback hooking.
- Alarm palette definitions (ISA/IEC) must be centralized to avoid drift between gauge rendering and editor color pickers.
- Shared serialization contracts across products remain ad-hoc; Phase 0 serialization proposal (below) formalizes JSON schema and embedded XAML fragments.

## Next Actions
- Establish a shared palette metadata file in Phase 1 containing control descriptors, default sizes, and asset references.
- Coordinate historian service API design with gauges/SCADA leads to ensure editor preview compatibility.
- Update `docs/specs/shared-composition-contract.md` when new layout helpers or control primitives are added.
