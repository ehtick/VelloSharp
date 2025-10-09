# Editor Serialization Strategy

## Goals
- Provide a deterministic, human-readable format for dashboards created in the unified editor.
- Ensure compatibility with SCADA runtime packaging and existing composition descriptors.
- Support partial loading (per control tree), diff/merge workflows, and schema evolution.

## Format Overview
- **Container**: JSON document with explicit schema version (`"schemaVersion": "1.0.0"`).  
- **Control Tree**: Nested array of nodes representing shared composition controls. Each node includes `type`, `id`, `properties`, `bindings`, and `children`.
- **Templates & Styles**: Optional embedded XAML fragment (string) for complex templates; referenced by `templateRef` to allow reuse and caching.
- **Bindings**: Objects describing telemetry sources, command targets, historian playback options. Format aligns with `TelemetryHub`/`CommandBroker` contracts (signal ids, quality thresholds, operator metadata requirements).
- **Assets**: Palette references stored as URIs resolved relative to deployment bundle (icons, bitmaps, scripts).
- **Metadata**: Document-level metadata includes versioning info, author, timestamps, RBAC tags, compliance flags (ISA/IEC audits).

## Example Skeleton
```json
{
  "schemaVersion": "1.0.0",
  "metadata": {
    "documentId": "dash-001",
    "title": "Tank Farm Overview",
    "createdBy": "designer@plant",
    "createdUtc": "2025-10-09T10:00:00Z",
    "rbacRoles": ["Operator", "Engineer"],
    "compliance": {
      "isa101Palette": true,
      "dualApprovalRequired": true
    }
  },
  "controls": [
    {
      "type": "StackPanel",
      "id": "root",
      "properties": { "orientation": "Horizontal", "gap": 8 },
      "children": [
        {
          "type": "ChartPanel",
          "id": "trend-1",
          "bindings": {
            "series": [
              { "signalId": "tank1.level", "unit": "ft", "qualityThreshold": "Good" }
            ]
          },
          "properties": { "title": "Tank 1 Level" }
        },
        {
          "type": "AnalogGauge",
          "id": "gauge-1",
          "properties": { "min": 0, "max": 120 },
          "bindings": {
            "value": { "signalId": "tank1.pressure", "unit": "psi" },
            "acknowledge": { "targetId": "tank1.alarm", "command": "ack" }
          }
        }
      ]
    }
  ],
  "resources": {
    "templates": [
      {
        "id": "alarmBadge",
        "xaml": "<Border Background='{DynamicResource AlarmBackground}' ... />"
      }
    ],
    "scripts": [
      { "id": "customAlarm", "language": "Lua", "path": "scripts/customAlarm.lua" }
    ]
  }
}
```

## Compatibility Considerations
- JSON keys map directly to managed DTOs in `VelloSharp.Editor.Serialization`; round-tripping must preserve order for diff friendliness.
- Control types correspond to shared runtime identifiers used in gauges, charts, TDG, and SCADA packages.
- Template fragments use Avalonia XAML subset; runtime loads them via shared template loader.
- Schema evolution managed with additive changes; deprecations flagged via `metadata.deprecatedFields`.

## Next Steps
- Define JSON schema (`.json` schema file) in Phase 1 with validation tests.
- Align deployment bundler (SCADA plan) to consume the same format.
- Provide CLI tooling to lint and pretty-print editor documents.
