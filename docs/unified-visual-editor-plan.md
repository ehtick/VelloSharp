# VelloSharp Unified Industrial Visual Editor Implementation Plan

## Vision and Guiding Principles
- Deliver an operator-friendly, industrial-grade visual editor that composes charts, gauges, TreeDataGrid views, and custom widgets into production-ready dashboards without writing code.
- Reuse the shared composition stack (layouts, templated controls, geometry/shape primitives, InputControl, animation timelines) to ensure parity across charting, gauges, TDG, and SCADA experiences.
- Provide deterministic, low-latency authoring with live telemetry preview, collaborative editing, and robust undo/redo pipelines that survive long-running sessions.
- Emphasize safety, accessibility, and governance—guarding configuration changes, offering audit trails, and supporting role-based workflows.

## Implementation Conventions
- [ ] Core editing engine implemented in Rust (`ffi/editor-*` crates) integrating with shared composition and scene graph primitives.
- [ ] Managed editor host (`src/VelloSharp.Editor.*`) exposing Avalonia/WinUI/Avalonia Web surfaces with extensibility hooks.
- [ ] Editor features share assets with existing products (control templates, materials, iconography). New primitives must update `docs/specs/shared-composition-contract.md`.
- [ ] Samples/tests live under `samples/VelloSharp.Editor.*` with scenario coverage for chart, gauge, TDG, and SCADA dashboards.

## Architectural Pillars
- **Shared Composition & Controls**: Unified control toolkit—`TemplatedControl`, `Panel`, `UserControl`, `Border`, `Decorator`, `Path`, `Button`, `CheckBox`, `RadioButton`, `DropDown`, `TabControl`, text primitives—exposed inside the editor palette with consistent serialization.
- **Scene Graph Editing Engine**: Rust-powered diffing, undo/redo, and validation pipeline operating on the same scene graph used at runtime, ensuring WYSIWYG fidelity.
- **Data Binding & Telemetry Preview**: Shared telemetry adapters (OPC UA, MQTT, REST) and mock providers surface live and simulated data inside the editor safely.
- **Layout & Interaction Authoring**: Visual layout tooling leveraging stack/wrap/grid/dock virtualization, drag/drop, snap lines, alignment guides, and keyboard nudging.
- **Collaboration & Governance**: Document model with version history, role-based access, audit logging, and safe deployment packaging.
- **Extensibility & Scripting**: Plugin APIs for custom widgets, behaviors, and automation scripts reused by SCADA runtime.

## Phase 0 – Discovery and Alignment (2 weeks)
**Objectives**
- [ ] Inventory requirements from charting, gauges, TDG, and SCADA plans; identify shared backlog for editor support.
- [ ] Document editor user personas (designers, operators, engineers) and workflows in `docs/specs/editor-personas.md`.
- [ ] Define serialization strategy (JSON/XAML hybrid) compatible with existing dashboard descriptors.
- [ ] Prototype drag/drop of shared controls with live composition preview to validate architecture.

**Deliverables**
- [ ] Requirements/persona dossier and workflow maps.
- [ ] Serialization proposal (`docs/specs/editor-serialization.md`) aligned with SCADA dashboard descriptors.
- [ ] Prototype demonstrating palette + live preview using shared composition controls.

#### Phase 0 Progress Snapshot
- Shared TDG/chart composition stack (layout solvers, templated/text/shape controls, virtualization capture) is now available for editor prototyping (`ffi/composition/src/panels.rs`, `src/VelloSharp.Composition/LayoutPrimitives.cs`, `src/VelloSharp.Composition/Controls/*`, `samples/VelloSharp.TreeDataGrid.CompositionSample/Program.cs`), clearing the dependency for palette and preview experiments.
- Newly shipped `InputControl` base + platform adapters (Avalonia/WPF/WinUI) and telemetry/command services (`src/VelloSharp.Composition/Controls/InputControl.cs`, adapter layer projects, `src/VelloSharp.Composition/Telemetry/*`) unblock editor tooling for selection, shortcuts, and live telemetry preview.

## Phase 1 – Core Editing Engine & Composition Integration (4 weeks)
**Objectives**
- [ ] Implement Rust editing core (`ffi/editor-core`) handling scene graph mutations, undo/redo stacks, snapping, and validation.
- [ ] Expose managed bindings (`VelloSharp.Editor.Core`) with async command pipeline and shared InputControl integration.
- [ ] Integrate templated control palette referencing shared control toolkit and geometry primitives.
- [ ] Author unit/integration tests ensuring editing operations maintain deterministic scene outputs.

**Deliverables**
- [ ] `ffi/editor-core` crate with mutation APIs, undo/redo, validation rules.
- [ ] Managed core bindings with command bus, property editing, and selection services.
- [ ] Control palette metadata referencing shared controls (`docs/specs/editor-control-palette.md`).
- [ ] Test suite covering common edit operations (add/remove/move/resize/align).

## Phase 2 – Layout, Alignment, and Interaction Tooling (4 weeks)
**Objectives**
- [ ] Build layout tooling features: drag handles, snap lines, alignment tools, distribution, and responsive breakpoints using shared layout primitives.
- [ ] Implement keyboard nudging, precise property editing, and constraints (min/max, aspect ratios).
- [ ] Add multi-selection, grouping, and z-order management across charts, gauges, TDG views.
- [ ] Provide live preview toggles for states (runtime vs. design mode, reduced motion).

**Deliverables**
- [ ] Layout tooling subsystem with shared virtualization panels.
- [ ] Interaction tests ensuring deterministic layout adjustments with composition reflows <8 ms.
- [ ] UI for alignment guides, grouping, and layering integrated into editor shell.
- [ ] Documentation (`docs/guides/editor-layout-tooling.md`) covering workflows.

## Phase 3 – Data Binding, Telemetry, and Historian Integration (5 weeks)
**Objectives**
- [ ] Integrate shared telemetry connectors and historian playback for live preview and design-time mock data.
- [ ] Build binding editor supporting chart series, gauge inputs, TDG columns, and SCADA command surfaces.
- [ ] Add validation rules for binding latency, quality indicators, and fallback strategies.
- [ ] Provide sample data generators and recorded sessions for offline authoring.

**Deliverables**
- [ ] Binding designer UI with expression support and shared validation.
- [ ] Telemetry preview services leveraging `VelloSharp.Telemetry` connectors in sandboxed mode.
- [ ] Historian playback integration for time-travel design/testing.
- [ ] Mock data toolkit and documentation.

## Phase 4 – Workflow, Collaboration, and Governance (4 weeks)
**Objectives**
- [ ] Implement document versioning, branching, and diffing using shared serialization format.
- [ ] Add role-based access control, audit trail, and approval workflows for publishing dashboards.
- [ ] Provide collaborative editing features (presence, comment threads, change suggestions).
- [ ] Integrate deployment packaging for SCADA runtime (export bundles, signed manifests).

**Deliverables**
- [ ] Versioning service with rollback/compare UI.
- [ ] RBAC model and audit logging integrated with deployment pipeline.
- [ ] Collaboration features with real-time sync and commenting.
- [ ] Deployment packaging tooling and guides.

## Phase 5 – Extensibility, Scripting, and Marketplace (3 weeks)
**Objectives**
- [ ] Expose plugin API for custom controls, behaviors, analytics overlays shared with runtime.
- [ ] Integrate scripting (Lua/C#/Python) into editor for automation, validation, and batch edits.
- [ ] Provide marketplace tooling for distributing templates, plugins, and controls.

**Deliverables**
- [ ] Plugin SDK documentation and sample extensions.
- [ ] Script console with sandboxing and API bindings to shared controls.
- [ ] Marketplace packaging format and publishing workflow.

## Phase 6 – Hardening, Accessibility, and Release (4 weeks)
**Objectives**
- [ ] Conduct performance profiling under large dashboards, ensuring editing operations remain <16 ms interactive budget.
- [ ] Validate accessibility (keyboard-only, screen reader, high contrast) and localization.
- [ ] Stress-test collaborative workflows, telemetry preview, and undo/redo durability.
- [ ] Finalize documentation, tutorials, and support playbooks.

**Deliverables**
- [ ] Performance and reliability reports with mitigation backlog.
- [ ] Accessibility conformance results and localization artifacts.
- [ ] Comprehensive documentation set (`docs/guides/editor-*`) and sample projects.
- [ ] Release packaging (installers, NuGet, container images) with signing and update channels.

## Cross-Cutting Concerns
- [ ] **Shared Control Toolkit Alignment**: Maintain parity with charting, gauges, TDG, and SCADA plans when controls/layouts evolve.
- [ ] **Performance Budgeting**: Update `docs/metrics/performance-baselines.md` with editor scenarios (large dashboard editing, telemetry preview).
- [ ] **Security & Safety**: Sandbox telemetry preview, scripting, and deployment packaging; integrate with SCADA security requirements.
- [ ] **Observability**: Instrument editor operations (undo stack health, diff timings) for diagnostics.
- [ ] **Developer Experience**: Provide SDK templates, CLI tooling, and CI workflows for editor extensions.

## Milestones
- [ ] **E0**: Requirements and architecture sign-off with working prototype.
- [ ] **E1**: Core editing engine and composition integration complete.
- [ ] **E2**: Layout tooling and interaction suite delivered.
- [ ] **E3**: Data binding, telemetry, and historian features ready for preview.
- [ ] **E4**: Collaboration and governance workflows released.
- [ ] **E5**: Extensibility and scripting support launched.
- [ ] **E6**: Accessibility, performance hardening, and release candidate shipped.

