# Unified Visual Editor Personas and Workflows

## Personas
- **Control Room Designer**  
  - Goals: Configure multi-screen dashboards prior to deployment, validate alarm visibility, align layouts with corporate standards.  
  - Needs: Palette of vetted controls, layout guides, preview of live telemetry with safe playback, export to staging environment.
- **Operations Engineer**  
  - Goals: Adjust running dashboards (add trends, tweak alarm thresholds) during maintenance windows.  
  - Needs: Rapid binding editor, audit trail with approval workflow, inline validation to prevent breaking SCADA policies.
- **Process Operator (Power User)**  
  - Goals: Build ad-hoc situational awareness views using pre-approved templates.  
  - Needs: Drag/drop simplicity, template locking, limited scripting, quick publishing to their operator station.
- **Systems Administrator**  
  - Goals: Manage access, deployment bundles, versioning, and integration with identity providers.  
  - Needs: RBAC configuration, diff/rollback tooling, package signing, observability dashboards for editor health.

## Key Workflows
1. **Dashboard Authoring (Designer)**  
   - Start from template → drag charts/gauges/TDG panels → adjust layout using snap lines → bind telemetry channels via `TelemetryHub` preview → configure alarm palettes → export to staging package.
2. **Live Dashboard Adjustment (Operations Engineer)**  
   - Open production dashboard draft → duplicate to sandbox → modify bindings/setpoints → run historian playback to validate → submit for approval with change notes → deploy after dual authorization.
3. **Ad-Hoc View Creation (Process Operator)**  
   - Launch simplified mode → drag a set of approved widgets → bind to predefined signals → save as personal view with limited distribution.
4. **Governance and Audit (Systems Administrator)**  
   - Review change log (who/what/when) → manage role assignments → validate TLS and identity provider settings → schedule downtime windows for deployment.

## Workflow Requirements
- Deterministic undo/redo stack with multi-hour session durability.
- Palette groupings aligned with chart/gauge/TDG taxonomies; items are searchable and taggable.
- Telemetry preview sandboxed from production connectors; historian playback toggled per widget.
- Deployment pipeline integrates with SCADA packaging (`docs/realtime-dcs-scada-visualization-plan.md`), enforcing compliance gates (ISA/IEC color audits, dual approval).
